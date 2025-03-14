﻿using Content.Server.Mind.Components;
using Content.Server.Traitor;
using Content.Shared.Store;

namespace Content.Server.Store.Conditions;

public sealed class BuyerBlockForAntagCondition : ListingCondition
{
    public override bool Condition(ListingConditionArgs args)
    {
        var ent = args.EntityManager;

        if (!ent.TryGetComponent<MindComponent>(args.Buyer, out var mind) || mind.Mind == null)
            return false;

        foreach (var role in mind.Mind.AllRoles)
        {
            if (role is TraitorRole traitorRole)
                return false;
        }

        return true;
    }
}
