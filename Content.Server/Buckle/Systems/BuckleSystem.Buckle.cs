﻿using System.Diagnostics.CodeAnalysis;
using Content.Server.Administration.Logs;
using Content.Shared.Alert;
using Content.Shared.Bed.Sleep;
using Content.Shared.Buckle.Components;
using Content.Shared.Cuffs.Components;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Hands.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Pulling.Components;
using Content.Shared.Storage.Components;
using Content.Shared.Stunnable;
using Content.Shared.Vehicle.Components;
using Content.Shared.Verbs;
using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Server.Buckle.Systems;

public sealed partial class BuckleSystem
{
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;

    private void InitializeBuckle()
    {
        SubscribeLocalEvent<BuckleComponent, ComponentStartup>(OnBuckleStartup);
        SubscribeLocalEvent<BuckleComponent, ComponentShutdown>(OnBuckleShutdown);
        SubscribeLocalEvent<BuckleComponent, ComponentGetState>(OnBuckleGetState);
        SubscribeLocalEvent<BuckleComponent, MoveEvent>(MoveEvent);
        SubscribeLocalEvent<BuckleComponent, InteractHandEvent>(HandleInteractHand);
        SubscribeLocalEvent<BuckleComponent, GetVerbsEvent<InteractionVerb>>(AddUnbuckleVerb);
        SubscribeLocalEvent<BuckleComponent, InsertIntoEntityStorageAttemptEvent>(OnEntityStorageInsertAttempt);
        SubscribeLocalEvent<BuckleComponent, CanDropDraggedEvent>(OnBuckleCanDrop);
        SubscribeLocalEvent<BuckleComponent, DragDropDraggedEvent>(OnBuckleDragDrop);

        SubscribeLocalEvent<BuckleComponent, DoAfterEvent<UnBuckleDoAfter>>(OnUnBuckleDoAfter);
    }

    private void OnUnBuckleDoAfter(EntityUid uid, BuckleComponent component, DoAfterEvent<UnBuckleDoAfter> args)
    {
        if (args.Handled)
            return;
        args.Handled = true;

        if (!args.Cancelled && component.Unbuckling)
        {
            Unbuckle(uid, uid, component);

            var uidMeta = MetaData(uid);
            _popups.PopupEntity(Loc.GetString("buckle-component-cuffed-unbuckling-success", ("user", uidMeta.EntityName)), uid);
        }
    }

    private void AddUnbuckleVerb(EntityUid uid, BuckleComponent component, GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !component.Buckled)
            return;

        InteractionVerb verb = new()
        {
            Act = () => TryUnbuckle(uid, args.User, buckle: component),
            Text = Loc.GetString("verb-categories-unbuckle"),
            Icon = new SpriteSpecifier.Texture(new ResourcePath("/Textures/Interface/VerbIcons/unbuckle.svg.192dpi.png"))
        };

        if (args.Target == args.User && args.Using == null)
        {
            // A user is left clicking themselves with an empty hand, while buckled.
            // It is very likely they are trying to unbuckle themselves.
            verb.Priority = 1;
        }

        args.Verbs.Add(verb);
    }

    private void OnBuckleStartup(EntityUid uid, BuckleComponent component, ComponentStartup args)
    {
        UpdateBuckleStatus(uid, component);
    }

    private void OnBuckleShutdown(EntityUid uid, BuckleComponent component, ComponentShutdown args)
    {
        TryUnbuckle(uid, uid, true, component);

        component.BuckleTime = default;
    }

    private void OnBuckleGetState(EntityUid uid, BuckleComponent component, ref ComponentGetState args)
    {
        args.State = new BuckleComponentState(component.Buckled, component.LastEntityBuckledTo, component.DontCollide);
    }

    private void HandleInteractHand(EntityUid uid, BuckleComponent component, InteractHandEvent args)
    {
        if (TryUnbuckle(uid, args.User, buckle: component))
            args.Handled = true;
    }

    private void MoveEvent(EntityUid uid, BuckleComponent buckle, ref MoveEvent ev)
    {
        var strap = buckle.BuckledTo;

        if (strap == null)
        {
            return;
        }

        var strapPosition = Transform(strap.Owner).Coordinates;

        if (ev.NewPosition.InRange(EntityManager, strapPosition, strap.MaxBuckleDistance))
        {
            return;
        }

        Unbuckle(uid, buckle.Owner, buckle);
    }

    private void OnEntityStorageInsertAttempt(EntityUid uid, BuckleComponent comp, ref InsertIntoEntityStorageAttemptEvent args)
    {
        if (comp.Buckled)
            args.Cancelled = true;
    }

    private void OnBuckleCanDrop(EntityUid uid, BuckleComponent component, ref CanDropDraggedEvent args)
    {
        args.Handled = HasComp<StrapComponent>(args.Target);
    }

    private void OnBuckleDragDrop(EntityUid uid, BuckleComponent component, ref DragDropDraggedEvent args)
    {
        args.Handled = TryBuckle(uid, args.User, args.Target, component);
    }

    /// <summary>
    ///     Shows or hides the buckled status effect depending on if the
    ///     entity is buckled or not.
    /// </summary>
    private void UpdateBuckleStatus(EntityUid uid, BuckleComponent component)
    {
        if (component.Buckled)
        {
            var alertType = component.BuckledTo?.BuckledAlertType ?? AlertType.Buckled;
            _alerts.ShowAlert(uid, alertType);
        }
        else
        {
            _alerts.ClearAlertCategory(uid, AlertCategory.Buckled);
        }
    }

    private void SetBuckledTo(BuckleComponent buckle, StrapComponent? strap)
    {
        buckle.BuckledTo = strap;
        buckle.LastEntityBuckledTo = strap?.Owner;

        if (strap == null)
        {
            buckle.Buckled = false;
        }
        else
        {
            buckle.DontCollide = true;
            buckle.Buckled = true;
            buckle.BuckleTime = _gameTiming.CurTime;
        }

        _actionBlocker.UpdateCanMove(buckle.Owner);
        UpdateBuckleStatus(buckle.Owner, buckle);
        Dirty(buckle);
    }

    private bool CanBuckle(
        EntityUid buckleId,
        EntityUid user,
        EntityUid to,
        [NotNullWhen(true)] out StrapComponent? strap,
        BuckleComponent? buckle = null)
    {
        strap = null;

        if (user == to ||
            !Resolve(buckleId, ref buckle, false) ||
            !Resolve(to, ref strap, false))
        {
            return false;
        }

        var strapUid = strap.Owner;
        bool Ignored(EntityUid entity) => entity == buckleId || entity == user || entity == strapUid;

        if (!_interactions.InRangeUnobstructed(buckleId, strapUid, buckle.Range, predicate: Ignored, popup: true))
        {
            return false;
        }

        // If in a container
        if (_containers.TryGetContainingContainer(buckleId, out var ownerContainer))
        {
            // And not in the same container as the strap
            if (!_containers.TryGetContainingContainer(strap.Owner, out var strapContainer) ||
                ownerContainer != strapContainer)
            {
                return false;
            }
        }

        if (!HasComp<SharedHandsComponent>(user))
        {
            _popups.PopupEntity(Loc.GetString("buckle-component-no-hands-message"), user, user);
            return false;
        }

        if (buckle.Buckled)
        {
            var message = Loc.GetString(buckleId == user
                    ? "buckle-component-already-buckled-message"
                    : "buckle-component-other-already-buckled-message",
                ("owner", Identity.Entity(buckleId, EntityManager)));
            _popups.PopupEntity(message, user, user);

            return false;
        }

        var parent = Transform(to).ParentUid;
        while (parent.IsValid())
        {
            if (parent == user)
            {
                var message = Loc.GetString(buckleId == user
                    ? "buckle-component-cannot-buckle-message"
                    : "buckle-component-other-cannot-buckle-message", ("owner", Identity.Entity(buckleId, EntityManager)));
                _popups.PopupEntity(message, user, user);

                return false;
            }

            parent = Transform(parent).ParentUid;
        }

        if (!StrapHasSpace(to, buckle, strap))
        {
            var message = Loc.GetString(buckleId == user
                ? "buckle-component-cannot-fit-message"
                : "buckle-component-other-cannot-fit-message", ("owner", Identity.Entity(buckleId, EntityManager)));
            _popups.PopupEntity(message, user, user);

            return false;
        }

        return true;
    }

    public bool TryBuckle(EntityUid buckleId, EntityUid user, EntityUid to, BuckleComponent? buckle = null)
    {
        if (!Resolve(buckleId, ref buckle, false))
            return false;

        if (!CanBuckle(buckleId, user, to, out var strap, buckle))
            return false;

        _audio.PlayPvs(strap.BuckleSound, buckleId);

        if (!StrapTryAdd(to, buckle, strap: strap))
        {
            var message = Loc.GetString(buckleId == user
                ? "buckle-component-cannot-buckle-message"
                : "buckle-component-other-cannot-buckle-message", ("owner", Identity.Entity(buckleId, EntityManager)));
            _popups.PopupEntity(message, user, user);
            return false;
        }

        if (TryComp<AppearanceComponent>(buckleId, out var appearance))
            _appearance.SetData(buckleId, BuckleVisuals.Buckled, true, appearance);

        ReAttach(buckleId, strap, buckle);
        SetBuckledTo(buckle, strap);

        var ev = new BuckleChangeEvent { Buckling = true, Strap = strap.Owner, BuckledEntity = buckleId };
        RaiseLocalEvent(ev.BuckledEntity, ev);
        RaiseLocalEvent(ev.Strap, ev);

        if (TryComp(buckleId, out SharedPullableComponent? ownerPullable))
        {
            if (ownerPullable.Puller != null)
            {
                _pulling.TryStopPull(ownerPullable);
            }
        }

        if (TryComp(to, out SharedPullableComponent? toPullable))
        {
            if (toPullable.Puller == buckleId)
            {
                // can't pull it and buckle to it at the same time
                _pulling.TryStopPull(toPullable);
            }
        }

        // Logging
        if (user != buckleId)
            _adminLogger.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(user):player} buckled {ToPrettyString(buckleId)} to {ToPrettyString(to)}");
        else
            _adminLogger.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(user):player} buckled themselves to {ToPrettyString(to)}");

        return true;
    }

    /// <summary>
    ///     Tries to unbuckle the Owner of this component from its current strap.
    /// </summary>
    /// <param name="buckleId">The entity to unbuckle.</param>
    /// <param name="user">The entity doing the unbuckling.</param>
    /// <param name="force">
    ///     Whether to force the unbuckling or not. Does not guarantee true to
    ///     be returned, but guarantees the owner to be unbuckled afterwards.
    /// </param>
    /// <param name="buckle">The buckle component of the entity to unbuckle.</param>
    /// <returns>
    ///     true if the owner was unbuckled, otherwise false even if the owner
    ///     was previously already unbuckled.
    /// </returns>
    public bool TryUnbuckle(EntityUid buckleId, EntityUid user, bool force = false, BuckleComponent? buckle = null)
    {
        if (!Resolve(buckleId, ref buckle, false) ||
            buckle.BuckledTo is not { } oldBuckledTo)
        {
            return false;
        }

        if (!TryComp(buckleId, out CuffableComponent? cuffable))
            return false;

        if (buckleId == user)
        {
            if (buckle.Unbuckling)
                return false;

            if (cuffable.CuffedHandCount == 2)
            {
                TryCuffUnbuckle(user, buckle);

                var userMeta = MetaData(user);

                _popups.PopupEntity(Loc.GetString("buckle-component-cuffed-unbuckling", ("user", userMeta.EntityName)), user);
                return false;
            }
        }

        if (!force)
        {
            if (_gameTiming.CurTime < buckle.BuckleTime + buckle.UnbuckleDelay)
                return false;

            if (!_interactions.InRangeUnobstructed(user, oldBuckledTo.Owner, buckle.Range, popup: true))
                return false;

            if (HasComp<SleepingComponent>(buckleId) && buckleId == user)
                return false;

            // If the strap is a vehicle and the rider is not the person unbuckling, return.
            if (TryComp(oldBuckledTo.Owner, out VehicleComponent? vehicle) &&
                vehicle.Rider != user)
                return false;
        }

        Unbuckle(buckleId, user, buckle);
        return true;
    }

    private void Unbuckle(EntityUid buckleId, EntityUid user, BuckleComponent? buckle = null)
    {
        if (!Resolve(buckleId, ref buckle, false) ||
            buckle.BuckledTo is not { } oldBuckledTo)
        {
            return;
        }

        //We need this to cancel doAfter if someone unbuckles person that is currently cuffed and trying to unbuckle himself.
        if (buckleId != user && buckle.Unbuckling)
        {
            if (!TryComp(buckleId, out DoAfterComponent? comp))
                return;

            var index = (byte)(comp.RunningIndex - 1);
            if (comp.DoAfters.ContainsKey(index))
            {
                var doAfter = comp.DoAfters[index];
                _doAfter.Cancel(buckleId, doAfter, comp);
            }
        }

        buckle.Unbuckling = false;

        // Logging
        if (user != buckleId)
            _adminLogger.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(user):player} unbuckled {ToPrettyString(buckleId)} from {ToPrettyString(oldBuckledTo.Owner)}");
        else
            _adminLogger.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(user):player} unbuckled themselves from {ToPrettyString(oldBuckledTo.Owner)}");

        SetBuckledTo(buckle!, null);

        var xform = Transform(buckleId);
        var oldBuckledXform = Transform(oldBuckledTo.Owner);

        if (xform.ParentUid == oldBuckledXform.Owner && !Terminating(xform.ParentUid))
        {
            _containers.AttachParentToContainerOrGrid(xform);
            xform.WorldRotation = oldBuckledXform.WorldRotation;

            if (oldBuckledTo.UnbuckleOffset != Vector2.Zero)
                xform.Coordinates = oldBuckledXform.Coordinates.Offset(oldBuckledTo.UnbuckleOffset);
        }

        if (TryComp(buckleId, out AppearanceComponent? appearance))
            _appearance.SetData(buckleId, BuckleVisuals.Buckled, false, appearance);

        if ((TryComp<MobStateComponent>(buckleId, out var mobState) && _mobState.IsIncapacitated(buckleId, mobState)) ||
            HasComp<KnockedDownComponent>(buckleId))
        {
            _standing.Down(buckleId);
        }
        else
        {
            _standing.Stand(buckleId);
        }

        if (_mobState.IsIncapacitated(buckleId, mobState))
        {
            _standing.Down(buckleId);
        }
        // Sync StrapComponent data
        _appearance.SetData(oldBuckledTo.Owner, StrapVisuals.State, false);
        if (oldBuckledTo.BuckledEntities.Remove(buckleId))
        {
            oldBuckledTo.OccupiedSize -= buckle.Size;
            Dirty(oldBuckledTo);
        }

        _audio.PlayPvs(oldBuckledTo.UnbuckleSound, buckleId);

        var ev = new BuckleChangeEvent { Buckling = false, Strap = oldBuckledTo.Owner, BuckledEntity = buckleId };
        RaiseLocalEvent(buckleId, ev);
        RaiseLocalEvent(oldBuckledTo.Owner, ev);
    }

    private void TryCuffUnbuckle(EntityUid user, BuckleComponent buckle)
    {
        buckle.Unbuckling = true;
        Dirty(buckle);

        var doAfterEventArgs = new DoAfterEventArgs(user, buckle.CuffedUnbuckleTime, default, user)
        {
            RaiseOnTarget = true,
            RaiseOnUsed = false,
            RaiseOnUser = false,
            BreakOnUserMove = true,
            BreakOnTargetMove = true,
            BreakOnDamage = true,
            BreakOnStun = true,
            NeedHand = false
        };

        _doAfter.DoAfter(doAfterEventArgs, new UnBuckleDoAfter());
    }

    /// <summary>
    ///     Makes an entity toggle the buckling status of the owner to a
    ///     specific entity.
    /// </summary>
    /// <param name="buckleId">The entity to buckle/unbuckle from <see cref="to"/>.</param>
    /// <param name="user">The entity doing the buckling/unbuckling.</param>
    /// <param name="to">
    ///     The entity to toggle the buckle status of the owner to.
    /// </param>
    /// <param name="force">
    ///     Whether to force the unbuckling or not, if it happens. Does not
    ///     guarantee true to be returned, but guarantees the owner to be
    ///     unbuckled afterwards.
    /// </param>
    /// <param name="buckle">The buckle component of the entity to buckle/unbuckle from <see cref="to"/>.</param>
    /// <returns>true if the buckling status was changed, false otherwise.</returns>
    public bool ToggleBuckle(
        EntityUid buckleId,
        EntityUid user,
        EntityUid to,
        bool force = false,
        BuckleComponent? buckle = null)
    {
        if (!Resolve(buckleId, ref buckle, false))
            return false;

        if (buckle.BuckledTo?.Owner == to)
        {
            return TryUnbuckle(buckleId, user, force, buckle);
        }

        return TryBuckle(buckleId, user, to, buckle);
    }

    private struct UnBuckleDoAfter
    {
    }
}
