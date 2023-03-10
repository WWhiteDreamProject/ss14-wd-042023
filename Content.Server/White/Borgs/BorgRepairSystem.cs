using Content.Server.Borgs;
using Content.Server.DoAfter;

using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tools;

namespace Content.Server.White.Borgs;

public sealed class BorgRepairSystem : EntitySystem
{
    [Dependency] private readonly SharedToolSystem _toolSystem = default!;
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly DoAfterSystem _doAfterSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BorgRepairComponent, InteractUsingEvent>(Repair);
        SubscribeLocalEvent<BorgRepairComponent, DoAfterEvent>(OnDoAfter);
    }

    private void Repair(EntityUid uid, BorgRepairComponent component, InteractUsingEvent args)
    {
        var delay = 7f;

        if (!HasComp<BorgComponent>(args.Target))
        {
            return;
        }

        if (args.User == args.Target)
        {
            delay *= component.SelfRepairPenalty;
        }

        if (!EntityManager.TryGetComponent(component.Owner, out DamageableComponent? damageable) || damageable.TotalDamage == 0)
            return;

        var doAfterArgs = new DoAfterEventArgs(
            args.User,
            delay,
            default,
            args.Target)
        {
            BreakOnTargetMove = true,
            BreakOnUserMove = true,
            BreakOnStun = true,
            NeedHand = true
        };

        if (!_toolSystem.HasQuality(args.Used, component.QualityNeeded))
        {
            return;
        }

        _doAfterSystem.DoAfter(doAfterArgs);
        _toolSystem.PlayToolSound(args.Used);

        component.Owner.PopupMessage(
            args.User,
            Loc.GetString(
                "comp-repairable-repair",
                ("target", component.Owner),
                ("tool", args.Used)));
    }

    private void OnDoAfter(EntityUid uid, BorgRepairComponent component, DoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Args.Target == null)
            return;

        _damageableSystem.TryChangeDamage(
            uid,
            new DamageSpecifier(component.Damage),
            true);
    }
}
