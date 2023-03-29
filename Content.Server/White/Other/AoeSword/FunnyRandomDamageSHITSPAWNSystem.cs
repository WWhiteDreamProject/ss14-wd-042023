using System.Linq;
using Content.Server.Body.Systems;
using Content.Shared.Examine;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio;
using Robust.Shared.Random;

namespace Content.Server.White.Other.AoeSword;

public sealed class FunnyRandomSHITSPAWNSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly BodySystem _bodySystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FunnyRandomDamageSHITSPAWNComponent, MeleeHitEvent>(OnHit);
        SubscribeLocalEvent<FunnyRandomDamageSHITSPAWNComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<FunnyRandomSHITSPAWNCursedComponent, MobStateChangedEvent>(CursedController);
    }


    private void OnExamined(EntityUid uid, FunnyRandomDamageSHITSPAWNComponent comp, ExaminedEvent args)
    {
        var kills = $"total killed: {comp.Kills}";
        var totalled = $"overall damage dealt: {comp.TotalDmg}";
        args.PushMarkup($"{kills}, {totalled}");
    }

    private void OnHit(EntityUid uid, FunnyRandomDamageSHITSPAWNComponent comp, MeleeHitEvent args)
    {
        if (args.Handled || !args.HitEntities.Any())
        {
            return;
        }

        CalculateDamage(ref args);

        var msg = args.BaseDamage.Total.Int() + args.BonusDamage.Total.Int();

        if (msg < 60)
        {
            _popupSystem.PopupEntity($"DAMAGE: {msg}", uid, PopupType.Large);

        }
        else
        {
            _popupSystem.PopupEntity($"CRIT! DAMAGE: {msg}", uid, PopupType.LargeCaution);
        }

        comp.TotalDmg += msg;

        foreach (var entity in args.HitEntities)
        {
            if (args.User == entity)
                continue;


            if (!TryComp<MobStateComponent>(entity, out var mobState))
                continue;


            if (HasComp<FunnyRandomSHITSPAWNCursedComponent>(entity))
                return;

            if (msg > 2000)
            {
                _bodySystem.GibBody(entity);
            }

            if (mobState.CurrentState == MobState.Dead)
            {
                EnsureComp<FunnyRandomSHITSPAWNCursedComponent>(entity);
                comp.Kills++;
                KillSteakAnnounce(uid, comp);
            }
        }
    }

    private void CursedController(EntityUid uid, FunnyRandomSHITSPAWNCursedComponent comp, MobStateChangedEvent args)
    {
        switch (args.NewMobState)
        {
            case MobState.Alive:
            case MobState.Critical:
                RemComp<FunnyRandomSHITSPAWNCursedComponent>(uid);
                break;
        }
    }

    private void KillSteakAnnounce(EntityUid uid, FunnyRandomDamageSHITSPAWNComponent comp)
    {
        switch (comp.Kills)
        {
            case 1:
                Announce(uid, "1");
                break;
            case 2:
                Announce(uid, "2");
                break;
            case 3:
                Announce(uid, "3");
                break;
            case 4:
                Announce(uid, "4");
                break;
            case 5:
                Announce(uid, "5");
                break;
            case 6:
                Announce(uid, "6");
                break;
            case 7:
                Announce(uid, "7");
                break;
            case 8:
                Announce(uid, "8");
                break;
            case 9:
                Announce(uid, "9");
                break;
            case 10:
                Announce(uid, "10");
                break;
            case >11:
                Announce(uid, "11");
                break;
        }
    }

    private void Announce(EntityUid uid, string filename)
    {
        _audio.PlayPvs($"/Audio/White/Other/Kills/{filename}.wav", uid, AudioParams.Default);
    }

    private void CalculateDamage(ref MeleeHitEvent args)
    {
        args.BonusDamage = args.BaseDamage * _random.NextDouble(0.1, 4.0);

        if (_random.Next(1000) < 2)
        {
            args.BonusDamage = args.BaseDamage * _random.NextDouble(1, 9999);
        }

        if (_random.Next(100) < 50)
        {
            args.BonusDamage -= args.BaseDamage;
        }
    }
}
