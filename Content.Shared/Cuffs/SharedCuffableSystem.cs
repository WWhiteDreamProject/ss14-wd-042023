using System.Linq;
using Content.Shared.ActionBlocker;
using Content.Shared.Administration.Components;
using Content.Shared.Administration.Logs;
using Content.Shared.Alert;
using Content.Shared.Buckle.Components;
using Content.Shared.Cuffs.Components;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.Events;
using Content.Shared.Item;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Events;
using Content.Shared.Physics.Pull;
using Content.Shared.Popups;
using Content.Shared.Pulling.Components;
using Content.Shared.Pulling.Events;
using Content.Shared.Rejuvenate;
using Content.Shared.Stunnable;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Shared.Cuffs
{
    public abstract class SharedCuffableSystem : EntitySystem
    {
        [Dependency] private readonly IComponentFactory _componentFactory = default!;
        [Dependency] private readonly INetManager _net = default!;
        [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;
        [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
        [Dependency] private readonly AlertsSystem _alerts = default!;
        [Dependency] private readonly MobStateSystem _mobState = default!;
        [Dependency] private readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly SharedContainerSystem _container = default!;
        [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
        [Dependency] private readonly SharedHandsSystem _hands = default!;
        [Dependency] private readonly SharedHandVirtualItemSystem _handVirtualItem = default!;
        [Dependency] private readonly SharedInteractionSystem _interaction = default!;
        [Dependency] private readonly SharedPopupSystem _popup = default!;
        [Dependency] private readonly SharedTransformSystem _transform = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<HandCountChangedEvent>(OnHandCountChanged);
            SubscribeLocalEvent<UncuffAttemptEvent>(OnUncuffAttempt);

            SubscribeLocalEvent<CuffableComponent, EntRemovedFromContainerMessage>(OnCuffsRemovedFromContainer);
            SubscribeLocalEvent<CuffableComponent, EntInsertedIntoContainerMessage>(OnCuffsInsertedIntoContainer);
            SubscribeLocalEvent<CuffableComponent, RejuvenateEvent>(OnRejuvenate);
            SubscribeLocalEvent<CuffableComponent, ComponentInit>(OnStartup);
            SubscribeLocalEvent<CuffableComponent, StopPullingEvent>(HandleStopPull);
            SubscribeLocalEvent<CuffableComponent, UpdateCanMoveEvent>(HandleMoveAttempt);
            SubscribeLocalEvent<CuffableComponent, IsEquippingAttemptEvent>(OnEquipAttempt);
            SubscribeLocalEvent<CuffableComponent, IsUnequippingAttemptEvent>(OnUnequipAttempt);
            SubscribeLocalEvent<CuffableComponent, BeingPulledAttemptEvent>(OnBeingPulledAttempt);
            SubscribeLocalEvent<CuffableComponent, GetVerbsEvent<Verb>>(AddUncuffVerb);
            SubscribeLocalEvent<CuffableComponent, DoAfterEvent<UnCuffDoAfter>>(OnCuffableDoAfter);
            SubscribeLocalEvent<CuffableComponent, PullStartedMessage>(OnPull);
            SubscribeLocalEvent<CuffableComponent, PullStoppedMessage>(OnPull);
            SubscribeLocalEvent<CuffableComponent, DropAttemptEvent>(CheckAct);
            SubscribeLocalEvent<CuffableComponent, PickupAttemptEvent>(CheckAct);
            SubscribeLocalEvent<CuffableComponent, AttackAttemptEvent>(CheckAct);
            SubscribeLocalEvent<CuffableComponent, UseAttemptEvent>(CheckAct);
            SubscribeLocalEvent<CuffableComponent, InteractionAttemptEvent>(CheckAct);

            SubscribeLocalEvent<HandcuffComponent, AfterInteractEvent>(OnCuffAfterInteract);
            SubscribeLocalEvent<HandcuffComponent, MeleeHitEvent>(OnCuffMeleeHit);
            SubscribeLocalEvent<HandcuffComponent, DoAfterEvent<AddCuffDoAfter>>(OnAddCuffDoAfter);

        }

        private void OnUncuffAttempt(ref UncuffAttemptEvent args)
        {
            if (args.Cancelled)
            {
                return;
            }
            if (!Exists(args.User) || Deleted(args.User))
            {
                // Should this even be possible?
                args.Cancelled = true;
                return;
            }

            // If the user is the target, special logic applies.
            // This is because the CanInteract blocking of the cuffs prevents self-uncuff.
            if (args.User == args.Target)
            {
                // This UncuffAttemptEvent check should probably be In MobStateSystem, not here?
                if (_mobState.IsIncapacitated(args.User))
                {
                    args.Cancelled = true;
                }
                else
                {
                    // TODO Find a way for cuffable to check ActionBlockerSystem.CanInteract() without blocking itself
                }
            }
            else
            {
                // Check if the user can interact.
                if (!_actionBlocker.CanInteract(args.User, args.Target))
                {
                    args.Cancelled = true;
                }
            }

            if (args.Cancelled && _net.IsServer)
            {
                _popup.PopupEntity(Loc.GetString("cuffable-component-cannot-interact-message"), args.Target, args.User);
            }
        }

        private void OnStartup(EntityUid uid, CuffableComponent component, ComponentInit args)
        {
            component.Container = _container.EnsureContainer<Container>(uid, _componentFactory.GetComponentName(component.GetType()));
        }

        private void OnRejuvenate(EntityUid uid, CuffableComponent component, RejuvenateEvent args)
        {
            _container.EmptyContainer(component.Container, true);
        }

        private void OnCuffsRemovedFromContainer(EntityUid uid, CuffableComponent component, EntRemovedFromContainerMessage args)
        {
            if (args.Container.ID != component.Container.ID)
                return;

            _handVirtualItem.DeleteInHandsMatching(uid, args.Entity);
            UpdateCuffState(uid, component);
        }

        private void OnCuffsInsertedIntoContainer(EntityUid uid, CuffableComponent component, ContainerModifiedMessage args)
        {
            if (args.Container == component.Container)
                UpdateCuffState(uid, component);
        }

        public void UpdateCuffState(EntityUid uid, CuffableComponent component)
        {
            var canInteract = TryComp(uid, out SharedHandsComponent? hands) && hands.Hands.Count > component.CuffedHandCount;

            if (canInteract == component.CanStillInteract)
                return;

            component.CanStillInteract = canInteract;
            Dirty(component);
            _actionBlocker.UpdateCanMove(uid);

            if (component.CanStillInteract)
                _alerts.ClearAlert(uid, AlertType.Handcuffed);
            else
                _alerts.ShowAlert(uid, AlertType.Handcuffed);

            var ev = new CuffedStateChangeEvent();
            RaiseLocalEvent(uid, ref ev);
        }

        private void OnBeingPulledAttempt(EntityUid uid, CuffableComponent component, BeingPulledAttemptEvent args)
        {
            if (!TryComp<SharedPullableComponent>(uid, out var pullable))
                return;

            if (pullable.Puller != null && !component.CanStillInteract) // If we are being pulled already and cuffed, we can't get pulled again.
                args.Cancel();
        }

        private void OnPull(EntityUid uid, CuffableComponent component, PullMessage args)
        {
            if (!component.CanStillInteract)
                _actionBlocker.UpdateCanMove(uid);
        }

        private void HandleMoveAttempt(EntityUid uid, CuffableComponent component, UpdateCanMoveEvent args)
        {
            if (component.CanStillInteract || !EntityManager.TryGetComponent(uid, out SharedPullableComponent? pullable) || !pullable.BeingPulled)
                return;

            args.Cancel();
        }

        private void HandleStopPull(EntityUid uid, CuffableComponent component, StopPullingEvent args)
        {
            if (args.User == null || !Exists(args.User.Value))
                return;

            if (args.User.Value == uid && !component.CanStillInteract)
                args.Cancel();
        }

        private void AddUncuffVerb(EntityUid uid, CuffableComponent component, GetVerbsEvent<Verb> args)
        {
            // Can the user access the cuffs, and is there even anything to uncuff?
            if (!args.CanAccess || component.CuffedHandCount == 0 || args.Hands == null)
                return;

            // We only check can interact if the user is not uncuffing themselves. As a result, the verb will show up
            // when the user is incapacitated & trying to uncuff themselves, but TryUncuff() will still fail when
            // attempted.
            if (args.User != args.Target && !args.CanInteract)
                return;

            Verb verb = new()
            {
                Act = () => TryUncuff(uid, args.User, cuffable: component),
                DoContactInteraction = true,
                Text = Loc.GetString("uncuff-verb-get-data-text")
            };
            //TODO VERB ICON add uncuffing symbol? may re-use the alert symbol showing that you are currently cuffed?
            args.Verbs.Add(verb);
        }

        private void OnCuffableDoAfter(EntityUid uid, CuffableComponent component, DoAfterEvent<UnCuffDoAfter> args)
        {
            component.Uncuffing = false;

            if (args.Args.Target is not { } target || args.Args.Used is not { } used)
                return;
            if (args.Handled)
                return;
            args.Handled = true;

            Dirty(component);

            var user = args.Args.User;

            if (!args.Cancelled)
            {
                Uncuff(target, user, used, component);
            }
            else if (_net.IsServer)
            {
                _popup.PopupEntity(Loc.GetString("cuffable-component-remove-cuffs-fail-message"), user, user);
            }
        }

        private void OnCuffAfterInteract(EntityUid uid, HandcuffComponent component, AfterInteractEvent args)
        {
            if (args.Target is not {Valid: true} target)
                return;

            if (!args.CanReach)
            {
                if (_net.IsServer)
                    _popup.PopupEntity(Loc.GetString("handcuff-component-too-far-away-error"), args.User, args.User);
                return;
            }

            TryCuffing(args.User, target, uid, component);
            args.Handled = true;
        }

        private void OnCuffMeleeHit(EntityUid uid, HandcuffComponent component, MeleeHitEvent args)
        {
            if (!args.HitEntities.Any())
                return;

            TryCuffing(args.User, args.HitEntities.First(), uid, component);
            args.Handled = true;
        }

        private void OnAddCuffDoAfter(EntityUid uid, HandcuffComponent component, DoAfterEvent<AddCuffDoAfter> args)
        {
            var user = args.Args.User;

            if (!TryComp<CuffableComponent>(args.Args.Target, out var cuffable))
                return;

            var target = args.Args.Target.Value;

            if (args.Handled)
                return;
            args.Handled = true;
            component.Cuffing = false;

            if (!args.Cancelled && TryAddNewCuffs(target, user, uid, cuffable))
            {
                if (!_net.IsServer)
                    return;

                _audio.PlayPvs(component.EndCuffSound, uid);

                _popup.PopupEntity(Loc.GetString("handcuff-component-cuff-observer-success-message",
                        ("user", Identity.Name(user, EntityManager)), ("target", Identity.Name(target, EntityManager))),
                    target, Filter.Pvs(target, entityManager: EntityManager)
                        .RemoveWhere(e => e.AttachedEntity == target || e.AttachedEntity == user), true);

                if (target == user)
                {
                    _popup.PopupEntity(Loc.GetString("handcuff-component-cuff-self-success-message"), user, user);
                    _adminLog.Add(LogType.Action, LogImpact.Medium,
                        $"{ToPrettyString(user):player} has cuffed himself");
                }
                else
                {
                    _popup.PopupEntity(Loc.GetString("handcuff-component-cuff-other-success-message",
                        ("otherName", Identity.Name(target, EntityManager, user))), user, user);
                    _popup.PopupEntity(Loc.GetString("handcuff-component-cuff-by-other-success-message",
                        ("otherName", Identity.Name(user, EntityManager, target))), target, target);
                    _adminLog.Add(LogType.Action, LogImpact.Medium,
                        $"{ToPrettyString(user):player} has cuffed {ToPrettyString(target):player}");
                }
            }
            else
            {
                if (!_net.IsServer)
                    return;
                if (target == user)
                {
                    _popup.PopupEntity(Loc.GetString("handcuff-component-cuff-interrupt-self-message"), user, user);
                }
                else
                {
                    _popup.PopupEntity(Loc.GetString("handcuff-component-cuff-interrupt-message",
                        ("targetName", Identity.Name(target, EntityManager, user))), user, user);
                    _popup.PopupEntity(Loc.GetString("handcuff-component-cuff-interrupt-other-message",
                        ("otherName", Identity.Name(user, EntityManager, target))), target, target);
                }
            }

        }

        /// <summary>
        ///     Check the current amount of hands the owner has, and if there's less hands than active cuffs we remove some cuffs.
        /// </summary>
        private void OnHandCountChanged(HandCountChangedEvent message)
        {
            var owner = message.Sender;

            if (!TryComp(owner, out CuffableComponent? cuffable) ||
                !cuffable.Initialized)
            {
                return;
            }

            var dirty = false;
            var handCount = CompOrNull<SharedHandsComponent>(owner)?.Count ?? 0;

            while (cuffable.CuffedHandCount > handCount && cuffable.CuffedHandCount > 0)
            {
                dirty = true;

                var container = cuffable.Container;
                var entity = container.ContainedEntities[^1];

                container.Remove(entity);
                _transform.SetWorldPosition(entity, _transform.GetWorldPosition(owner));
            }

            if (dirty)
            {
                UpdateCuffState(owner, cuffable);
            }
        }

        /// <summary>
        ///     Adds virtual cuff items to the user's hands.
        /// </summary>
        private void UpdateHeldItems(EntityUid uid, EntityUid handcuff, CuffableComponent? component = null)
        {
            if (!Resolve(uid, ref component))
                return;

            // TODO we probably don't just want to use the generic virtual-item entity, and instead
            // want to add our own item, so that use-in-hand triggers an uncuff attempt and the like.

            if (!TryComp<SharedHandsComponent>(uid, out var handsComponent))
                return;

            var freeHands = 0;
            foreach (var hand in _hands.EnumerateHands(uid, handsComponent))
            {
                if (hand.HeldEntity == null)
                {
                    freeHands++;
                    continue;
                }

                // Is this entity removable? (it might be an existing handcuff blocker)
                if (HasComp<UnremoveableComponent>(hand.HeldEntity))
                    continue;

                _hands.DoDrop(uid, hand, true, handsComponent);
                freeHands++;
                if (freeHands == 2)
                    break;
            }

            if (_handVirtualItem.TrySpawnVirtualItemInHand(handcuff, uid, out var virtItem1))
                EnsureComp<UnremoveableComponent>(virtItem1.Value);

            if (_handVirtualItem.TrySpawnVirtualItemInHand(handcuff, uid, out var virtItem2))
                EnsureComp<UnremoveableComponent>(virtItem2.Value);
        }

        /// <summary>
        /// Add a set of cuffs to an existing CuffedComponent.
        /// </summary>
        public bool TryAddNewCuffs(EntityUid target, EntityUid user, EntityUid handcuff, CuffableComponent? component = null, HandcuffComponent? cuff = null)
        {
            if (!Resolve(target, ref component) || !Resolve(handcuff, ref cuff))
                return false;

            if (!_interaction.InRangeUnobstructed(handcuff, target))
                return false;

            // Success!
            _hands.TryDrop(user, handcuff);

            component.Container.Insert(handcuff);
            UpdateHeldItems(target, handcuff, component);
            return true;
        }

        public void TryCuffing(EntityUid user, EntityUid target, EntityUid handcuff, HandcuffComponent? handcuffComponent = null, CuffableComponent? cuffable = null)
        {
            if (!Resolve(handcuff, ref handcuffComponent) || !Resolve(target, ref cuffable, false))
                return;

            if (handcuffComponent.Cuffing)
                return;

            if (!TryComp<SharedHandsComponent?>(target, out var hands))
            {
                if (_net.IsServer)
                {
                    _popup.PopupEntity(Loc.GetString("handcuff-component-target-has-no-hands-error",
                        ("targetName", Identity.Name(target, EntityManager, user))), user, user);
                }
                return;
            }

            if (cuffable.CuffedHandCount >= hands.Count)
            {
                if (_net.IsServer)
                {
                    _popup.PopupEntity(Loc.GetString("handcuff-component-target-has-no-free-hands-error",
                        ("targetName", Identity.Name(target, EntityManager, user))), user, user);
                }
                return;
            }

            if (_net.IsServer)
            {
                _popup.PopupEntity(Loc.GetString("handcuff-component-start-cuffing-observer",
                    ("user", Identity.Name(user, EntityManager)), ("target", Identity.Name(target, EntityManager))),
                    target, Filter.Pvs(target, entityManager: EntityManager)
                    .RemoveWhere(e => e.AttachedEntity == target || e.AttachedEntity == user), true);

                if (target == user)
                {
                    _popup.PopupEntity(Loc.GetString("handcuff-component-target-self"), user, user);
                }
                else
                {
                    _popup.PopupEntity(Loc.GetString("handcuff-component-start-cuffing-target-message",
                        ("targetName", Identity.Name(target, EntityManager, user))), user, user);
                    _popup.PopupEntity(Loc.GetString("handcuff-component-start-cuffing-by-other-message",
                        ("otherName", Identity.Name(user, EntityManager, target))), target, target);
                }
            }

            _audio.PlayPvs(handcuffComponent.StartCuffSound, handcuff);

            var cuffTime = handcuffComponent.CuffTime;

            if (HasComp<StunnedComponent>(target))
                cuffTime = MathF.Max(0.1f, cuffTime - handcuffComponent.StunBonus);

            if (HasComp<DisarmProneComponent>(target))
                cuffTime = 0.0f; // cuff them instantly.

            var doAfterEventArgs = new DoAfterEventArgs(user, cuffTime, default, target, handcuff)
            {
                RaiseOnUser = false,
                RaiseOnTarget = false,
                RaiseOnUsed = true,
                BreakOnTargetMove = true,
                BreakOnUserMove = true,
                BreakOnDamage = true,
                BreakOnStun = true,
                NeedHand = true
            };

            handcuffComponent.Cuffing = true;
            if (_net.IsServer)
                _doAfter.DoAfter(doAfterEventArgs, new AddCuffDoAfter());
        }

        /// <summary>
        /// Attempt to uncuff a cuffed entity. Can be called by the cuffed entity, or another entity trying to help uncuff them.
        /// If the uncuffing succeeds, the cuffs will drop on the floor.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="user">The cuffed entity</param>
        /// <param name="cuffsToRemove">Optional param for the handcuff entity to remove from the cuffed entity. If null, uses the most recently added handcuff entity.</param>
        /// <param name="cuffable"></param>
        /// <param name="cuff"></param>
        public void TryUncuff(EntityUid target, EntityUid user, EntityUid? cuffsToRemove = null, CuffableComponent? cuffable = null, HandcuffComponent? cuff = null)
        {
            if (!Resolve(target, ref cuffable))
                return;

            if (cuffable.Uncuffing)
                return;

            if (!TryComp(user, out BuckleComponent? buckle))
                return;

            if (buckle.Buckled)
            {
                if (target == user)
                {
                    if (_net.IsServer)
                        _popup.PopupEntity(Loc.GetString("cuffable-component-buckled"), user, user);
                    return;
                }
            }

            var isOwner = user == target;

            if (cuffsToRemove == null)
            {
                if (cuffable.Container.ContainedEntities.Count == 0)
                {
                    return;
                }

                cuffsToRemove = cuffable.LastAddedCuffs;
            }
            else
            {
                if (!cuffable.Container.ContainedEntities.Contains(cuffsToRemove.Value))
                {
                    Logger.Warning("A user is trying to remove handcuffs that aren't in the owner's container. This should never happen!");
                }
            }

            if (!Resolve(cuffsToRemove.Value, ref cuff))
                return;

            var attempt = new UncuffAttemptEvent(user, target);
            RaiseLocalEvent(user, ref attempt, true);

            if (attempt.Cancelled)
            {
                return;
            }

            if (!isOwner && !_interaction.InRangeUnobstructed(user, target))
            {
                if (_net.IsServer)
                    _popup.PopupEntity(Loc.GetString("cuffable-component-cannot-remove-cuffs-too-far-message"), user, user);
                return;
            }

            if (_net.IsServer)
                _popup.PopupEntity(Loc.GetString("cuffable-component-start-removing-cuffs-message"), user, user);

            _audio.PlayPredicted(isOwner ? cuff.StartBreakoutSound : cuff.StartUncuffSound, target, user);

            var uncuffTime = isOwner ? cuff.BreakoutTime : cuff.UncuffTime;
            var doAfterEventArgs = new DoAfterEventArgs(user, uncuffTime, default, target, cuffsToRemove)
            {
                RaiseOnTarget = true,
                RaiseOnUsed = false,
                RaiseOnUser = false,
                BreakOnUserMove = true,
                BreakOnTargetMove = true,
                BreakOnDamage = true,
                BreakOnStun = true,
                NeedHand = true
            };

            cuffable.Uncuffing = true;
            Dirty(cuffable);
            if (_net.IsServer)
                _doAfter.DoAfter(doAfterEventArgs, new UnCuffDoAfter());
        }

        public void Uncuff(EntityUid target, EntityUid user, EntityUid cuffsToRemove, CuffableComponent? cuffable = null, HandcuffComponent? cuff = null)
        {
            if (!Resolve(target, ref cuffable) || !Resolve(cuffsToRemove, ref cuff))
                return;

            _audio.PlayPvs(cuff.EndUncuffSound, target);

            cuffable.Container.Remove(cuffsToRemove);

            if (!TryComp(target, out BuckleComponent? buckle))
                return;

            if (buckle.Unbuckling)
            {
                buckle.Unbuckling = false;
                if (!TryComp(target, out DoAfterComponent? comp))
                    return;

                var index = (byte)(comp.RunningIndex - 1);
                if (comp.DoAfters.ContainsKey(index))
                {
                    var doAfter = comp.DoAfters[index];
                    _doAfter.Cancel(target, doAfter, comp);
                }
            }

            if (cuff.BreakOnRemove)
            {
                QueueDel(cuffsToRemove);
                var trash = Spawn(cuff.BrokenPrototype, Transform(cuffsToRemove).Coordinates);
                _hands.PickupOrDrop(user, trash);
            }
            else
            {
                _hands.PickupOrDrop(user, cuffsToRemove);
            }

            // Only play popups on server because popups suck
            if (_net.IsServer)
            {
                if (cuffable.CuffedHandCount == 0)
                {
                    _popup.PopupEntity(Loc.GetString("cuffable-component-remove-cuffs-success-message"), user, user);

                    if (target != user)
                    {
                        _popup.PopupEntity(Loc.GetString("cuffable-component-remove-cuffs-by-other-success-message",
                            ("otherName", Identity.Name(user, EntityManager, user))), target, target);
                        _adminLog.Add(LogType.Action, LogImpact.Medium,
                            $"{ToPrettyString(user):player} has successfully uncuffed {ToPrettyString(target):player}");
                    }
                    else
                    {
                        _adminLog.Add(LogType.Action, LogImpact.Medium,
                            $"{ToPrettyString(user):player} has successfully uncuffed themselves");
                    }
                }
                else
                {
                    if (user != target)
                    {
                        _popup.PopupEntity(Loc.GetString("cuffable-component-remove-cuffs-partial-success-message",
                            ("cuffedHandCount", cuffable.CuffedHandCount),
                            ("otherName", Identity.Name(user, EntityManager, user))), user, user);
                        _popup.PopupEntity(Loc.GetString(
                            "cuffable-component-remove-cuffs-by-other-partial-success-message",
                            ("otherName", Identity.Name(user, EntityManager, user)),
                            ("cuffedHandCount", cuffable.CuffedHandCount)), target, target);
                    }
                    else
                    {
                        _popup.PopupEntity(Loc.GetString("cuffable-component-remove-cuffs-partial-success-message",
                            ("cuffedHandCount", cuffable.CuffedHandCount)), user, user);
                    }
                }
            }
        }

        #region ActionBlocker

        private void CheckAct(EntityUid uid, CuffableComponent component, CancellableEntityEventArgs args)
        {
            if (!component.CanStillInteract)
                args.Cancel();
        }

        private void OnEquipAttempt(EntityUid uid, CuffableComponent component, IsEquippingAttemptEvent args)
        {
            // is this a self-equip, or are they being stripped?
            if (args.Equipee == uid)
                CheckAct(uid, component, args);
        }

        private void OnUnequipAttempt(EntityUid uid, CuffableComponent component, IsUnequippingAttemptEvent args)
        {
            // is this a self-equip, or are they being stripped?
            if (args.Unequipee == uid)
                CheckAct(uid, component, args);
        }

        #endregion

        public IReadOnlyList<EntityUid> GetAllCuffs(CuffableComponent component)
        {
            return component.Container.ContainedEntities;
        }

        private struct UnCuffDoAfter
        {
        }

        private struct AddCuffDoAfter
        {
        }
    }
}
