using System.Linq;
using Content.Server.Borgs;
using Content.Server.Flash.Components;
using Content.Server.Light.EntitySystems;
using Content.Server.Stunnable;
using Content.Shared.Examine;
using Content.Shared.Eye.Blinding;
using Content.Shared.Flash;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Traits.Assorted;
using Content.Shared.Weapons.Melee.Events;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using InventoryComponent = Content.Shared.Inventory.InventoryComponent;

namespace Content.Server.Flash
{
    internal sealed class FlashSystem : SharedFlashSystem
    {
        [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
        [Dependency] private readonly IGameTiming _gameTiming = default!;
        [Dependency] private readonly StunSystem _stunSystem = default!;
        [Dependency] private readonly InventorySystem _inventorySystem = default!;
        [Dependency] private readonly MetaDataSystem _metaSystem = default!;
        [Dependency] private readonly SharedInteractionSystem _interactionSystem = default!;
        [Dependency] private readonly TagSystem _tagSystem = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<FlashComponent, MeleeHitEvent>(OnFlashMeleeHit);
            SubscribeLocalEvent<FlashComponent, UseInHandEvent>(OnFlashUseInHand, before: new []{ typeof(HandheldLightSystem) });
            SubscribeLocalEvent<FlashComponent, ExaminedEvent>(OnFlashExamined);

            SubscribeLocalEvent<InventoryComponent, FlashAttemptEvent>(OnInventoryFlashAttempt);

            SubscribeLocalEvent<FlashImmunityComponent, FlashAttemptEvent>(OnFlashImmunityFlashAttempt);
            SubscribeLocalEvent<PermanentBlindnessComponent, FlashAttemptEvent>(OnPermanentBlindnessFlashAttempt);
            SubscribeLocalEvent<TemporaryBlindnessComponent, FlashAttemptEvent>(OnTemporaryBlindnessFlashAttempt);
        }

        private void OnFlashMeleeHit(EntityUid uid, FlashComponent comp, MeleeHitEvent args)
        {
            if (!args.IsHit ||
                !args.HitEntities.Any() ||
                !UseFlash(comp, args.User))
            {
                return;
            }

            args.Handled = true;
            foreach (var e in args.HitEntities)
            {
                Flash(e, args.User, uid, comp.FlashDuration, comp.SlowTo);
            }
        }

        private void OnFlashUseInHand(EntityUid uid, FlashComponent comp, UseInHandEvent args)
        {
            if (args.Handled || !UseFlash(comp, args.User))
                return;

            args.Handled = true;
            FlashArea(uid, args.User, comp.Range, comp.AoeFlashDuration, comp.SlowTo, true);
        }

        private bool UseFlash(FlashComponent comp, EntityUid user)
        {
            if (comp.HasUses)
            {
                // TODO flash visualizer
                if (!EntityManager.TryGetComponent<SpriteComponent?>(comp.Owner, out var sprite))
                    return false;

                if (--comp.Uses == 0 && !comp.AutoRecharge)
                {
                    sprite.LayerSetState(0, "burnt");

                    _tagSystem.AddTag(comp.Owner, "Trash");
                    comp.Owner.PopupMessage(user, Loc.GetString("flash-component-becomes-empty"));
                }
                else if (!comp.Flashing)
                {
                    int animLayer = sprite.AddLayerWithState("flashing");
                    comp.Flashing = true;

                    comp.Owner.SpawnTimer(400, () =>
                    {
                        sprite.RemoveLayer(animLayer);
                        comp.Flashing = false;
                    });
                }

                SoundSystem.Play(comp.Sound.GetSound(), Filter.Pvs(comp.Owner), comp.Owner, AudioParams.Default);

                return true;
            }

            return false;
        }

        public void Flash(EntityUid target, EntityUid? user, EntityUid? used, float flashDuration, float slowTo, bool displayPopup = true, FlashableComponent? flashable = null, bool massFlash = false)
        {
            if (!Resolve(target, ref flashable, false)) return;

            var attempt = new FlashAttemptEvent(target, user, used, massFlash);
            RaiseLocalEvent(target, attempt, true);

            if (attempt.Cancelled)
                return;

            if (HasComp<BorgComponent>(target))
            {
                if (target == user)
                {
                    return;
                }
                _stunSystem.TryParalyze(target, TimeSpan.FromSeconds(3.5f), true);
            }

            flashable.LastFlash = _gameTiming.CurTime;
            flashable.Duration = flashDuration / 1000f; // TODO: Make this sane...
            Dirty(flashable);

            _stunSystem.TrySlowdown(target, TimeSpan.FromSeconds(flashDuration/1000f), true,
                slowTo, slowTo);

            if (displayPopup && user != null && target != user && EntityManager.EntityExists(user.Value))
            {
                user.Value.PopupMessage(target, Loc.GetString("flash-component-user-blinds-you",
                    ("user", Identity.Entity(user.Value, EntityManager))));
            }
        }

        public void FlashArea(EntityUid source, EntityUid? user, float range, float duration, float slowTo = 0.8f, bool displayPopup = false, SoundSpecifier? sound = null)
        {
            var transform = EntityManager.GetComponent<TransformComponent>(source);
            var mapPosition = transform.MapPosition;
            var flashableEntities = new List<EntityUid>();
            var flashableQuery = GetEntityQuery<FlashableComponent>();

            foreach (var entity in _entityLookup.GetEntitiesInRange(transform.Coordinates, range))
            {
                if (!flashableQuery.HasComponent(entity))
                    continue;

                flashableEntities.Add(entity);
            }

            foreach (var entity in flashableEntities)
            {
                // Check for unobstructed entities while ignoring the mobs with flashable components.
                if (!_interactionSystem.InRangeUnobstructed(entity, mapPosition, range, CollisionGroup.Opaque, (e) => flashableEntities.Contains(e) || e == source))
                    continue;

                // They shouldn't have flash removed in between right?
                Flash(entity, user, source, duration, slowTo, displayPopup, flashableQuery.GetComponent(entity), true);
            }
            if (sound != null)
            {
                SoundSystem.Play(sound.GetSound(), Filter.Pvs(transform), source);
            }
        }

        private void OnFlashExamined(EntityUid uid, FlashComponent comp, ExaminedEvent args)
        {
            if (comp.AutoRecharge)
            {
                if (comp.Uses == comp.MaxCharges)
                {
                    args.PushMarkup(Loc.GetString("emag-max-charges"));
                    return;
                }
                var timeRemaining = Math.Round((comp.NextChargeTime - _gameTiming.CurTime).TotalSeconds);
                args.PushMarkup(Loc.GetString("emag-recharging", ("seconds", timeRemaining)));
            }

            if (comp is { HasUses: false, AutoRecharge: false })
            {
                args.PushText(Loc.GetString("flash-component-examine-empty"));
                return;
            }

            if (args.IsInDetailsRange)
            {
                args.PushMarkup(
                    Loc.GetString(
                        "flash-component-examine-detail-count",
                        ("count", comp.Uses),
                        ("markupCountColor", "green")
                    )
                );
            }
        }

        private void OnInventoryFlashAttempt(EntityUid uid, InventoryComponent component, FlashAttemptEvent args)
        {
            foreach (var slot in new[] { "head", "eyes", "mask" })
            {
                if (args.Cancelled)
                    break;
                if (_inventorySystem.TryGetSlotEntity(uid, slot, out var item, component))
                    RaiseLocalEvent(item.Value, args, true);
            }
        }

        private void OnFlashImmunityFlashAttempt(EntityUid uid, FlashImmunityComponent component, FlashAttemptEvent args)
        {
            if(component.Enabled)
                args.Cancel();
        }

        private void OnPermanentBlindnessFlashAttempt(EntityUid uid, PermanentBlindnessComponent component, FlashAttemptEvent args)
        {
            args.Cancel();
        }

        private void OnTemporaryBlindnessFlashAttempt(EntityUid uid, TemporaryBlindnessComponent component, FlashAttemptEvent args)
        {
            args.Cancel();
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            foreach (var flash in EntityQuery<FlashComponent>())
            {
                if (!flash.AutoRecharge)
                    continue;

                if (flash.Uses == flash.MaxCharges)
                    continue;

                if (_gameTiming.CurTime < flash.NextChargeTime)
                    continue;

                ChangeFlashCharge(flash.Owner, 1, true, flash);
            }
        }

        private bool ChangeFlashCharge(EntityUid uid, int change, bool resetTimer, FlashComponent? component = null)
        {
            if (!Resolve(uid, ref component))
                return false;

            if (component.Uses + change < 0 || component.Uses + change > component.MaxCharges)
                return false;

            if (resetTimer || component.Uses == component.MaxCharges)
                component.NextChargeTime = _gameTiming.CurTime + component.RechargeDuration;

            component.Uses += change;
            Dirty(component);
            return true;
        }

    }

    public sealed class FlashAttemptEvent : CancellableEntityEventArgs
    {
        public readonly EntityUid Target;
        public readonly EntityUid? User;
        public readonly EntityUid? Used;
        public readonly bool MassFlash;

        public FlashAttemptEvent(EntityUid target, EntityUid? user, EntityUid? used, bool massFlash)
        {
            Target = target;
            User = user;
            Used = used;
            MassFlash = massFlash;
        }
    }
}
