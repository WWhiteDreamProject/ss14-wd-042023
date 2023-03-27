using Content.Server.Power.Components;
using Content.Server.Power.Events;
using Content.Server.Stunnable.Components;
using Content.Shared.Audio;
using Content.Shared.Damage.Events;
using Content.Shared.Examine;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Toggleable;
using Content.Shared.Weapons.Melee.Events;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Player;

namespace Content.Server.Weapons.Melee.Chainsword
{
    public sealed class ChainswordSystem : EntitySystem
    {
        [Dependency] private readonly SharedItemSystem _item = default!;
        [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<ChainswordComponent, UseInHandEvent>(OnUseInHand);
            SubscribeLocalEvent<ChainswordComponent, ExaminedEvent>(OnExamined);
            SubscribeLocalEvent<ChainswordComponent, MeleeHitEvent>(OnMeleeHit);
        }

        private void OnMeleeHit(EntityUid uid, ChainswordComponent comp, MeleeHitEvent args)
        {
            if (!comp.Activated)
                return;

            // Apply more damage if it's  activated.
            args.BonusDamage -= args.BaseDamage;
            args.BonusDamage += comp.ActiveDamageBonus;
            SoundSystem.Play(comp.HitSound.GetSound(), Filter.Pvs(comp.Owner), comp.Owner, AudioHelpers.WithVariation(0.25f));
        }

        private void OnUseInHand(EntityUid uid, ChainswordComponent comp, UseInHandEvent args)
        {
            if (comp.Activated)
            {
                TurnOff(uid, comp);
            }
            else
            {
                TurnOn(uid, comp, args.User);
            }
        }

        private void OnExamined(EntityUid uid, ChainswordComponent comp, ExaminedEvent args)
        {
            var msg = comp.Activated
                ? "The sword is currently on"
                : "The sword is currently off";
            args.PushMarkup(msg);
        }

        private void TurnOff(EntityUid uid, ChainswordComponent comp)
        {
            if (!comp.Activated)
                return;

            if (TryComp<AppearanceComponent>(comp.Owner, out var appearance) &&
                TryComp<ItemComponent>(comp.Owner, out var item))
            {
                _item.SetHeldPrefix(comp.Owner, "off", item);
                _appearance.SetData(uid, ToggleVisuals.Toggled, false, appearance);
            }

            SoundSystem.Play(comp.TurnOffSound.GetSound(), Filter.Pvs(comp.Owner), comp.Owner, AudioHelpers.WithVariation(0.25f));

            comp.Activated = false;
        }

        private void TurnOn(EntityUid uid, ChainswordComponent comp, EntityUid user)
        {
            if (comp.Activated)
                return;

            var playerFilter = Filter.Pvs(comp.Owner, entityManager: EntityManager);

            if (EntityManager.TryGetComponent<AppearanceComponent>(comp.Owner, out var appearance) &&
                EntityManager.TryGetComponent<ItemComponent>(comp.Owner, out var item))
            {
                _item.SetHeldPrefix(comp.Owner, "on", item);
                _appearance.SetData(uid, ToggleVisuals.Toggled, true, appearance);
            }

            SoundSystem.Play(comp.TurnOnSound.GetSound(), playerFilter, comp.Owner, AudioHelpers.WithVariation(0.25f));
            comp.Activated = true;
        }
    }
}
