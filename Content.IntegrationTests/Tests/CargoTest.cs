using System.Threading.Tasks;
using System.Linq;
using Content.Server.Cargo.Components;
using Content.Server.Cargo.Systems;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.Stacks;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests;

[TestFixture]
public sealed class CargoTest
{
    [Test]
    public async Task NoCargoOrderArbitrage()
    {
        await using var pairTracker = await PoolManager.GetServerClient(new PoolSettings() {NoClient = true});
        var server = pairTracker.Pair.Server;

        var testMap = await PoolManager.CreateTestMap(pairTracker);

        var entManager = server.ResolveDependency<IEntityManager>();
        var mapManager = server.ResolveDependency<IMapManager>();
        var protoManager = server.ResolveDependency<IPrototypeManager>();
        var pricing = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<PricingSystem>();

        await server.WaitAssertion(() =>
        {
            var mapId = testMap.MapId;

            Assert.Multiple(() =>
            {
                foreach (var proto in protoManager.EnumeratePrototypes<CargoProductPrototype>())
                {
                    var ent = entManager.SpawnEntity(proto.Product, new MapCoordinates(Vector2.Zero, mapId));
                    var price = pricing.GetPrice(ent);

                    Assert.That(price, Is.AtMost(proto.PointCost), $"Found arbitrage on {proto.ID} cargo product! Cost is {proto.PointCost} but sell is {price}!");
                    entManager.DeleteEntity(ent);
                }
            });

            mapManager.DeleteMap(mapId);
        });

        await pairTracker.CleanReturnAsync();
    }

    [Test]
    public async Task NoStaticPriceAndStackPrice()
    {
        await using var pairTracker = await PoolManager.GetServerClient(new PoolSettings{NoClient = true});
        var server = pairTracker.Pair.Server;

        var testMap = await PoolManager.CreateTestMap(pairTracker);

        var entManager = server.ResolveDependency<IEntityManager>();
        var mapManager = server.ResolveDependency<IMapManager>();
        var protoManager = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var mapId = testMap.MapId;
            var grid = mapManager.CreateGrid(mapId);
            var coord = new EntityCoordinates(grid.Owner, 0, 0);

            var protoIds = protoManager.EnumeratePrototypes<EntityPrototype>()
                .Where(p=>!p.Abstract)
                .Where(p => !p.Components.ContainsKey("MapGrid")) // Generation Shit
                .Select(p => p.ID)
                .ToList();

            foreach (var proto in protoIds)
            {
                var ent = entManager.SpawnEntity(proto, coord);

                if (entManager.TryGetComponent<StackPriceComponent>(ent, out var stackpricecomp)
                    && stackpricecomp.Price > 0)
                {
                    if (entManager.TryGetComponent<StaticPriceComponent>(ent, out var staticpricecomp))
                    {
                        Assert.That(staticpricecomp.Price, Is.EqualTo(0),
                            $"The prototype {proto} has a StackPriceComponent and StaticPriceComponent whose values are not compatible with each other.");
                    }
                }

                if (entManager.HasComponent<StackComponent>(ent))
                {
                    if (entManager.TryGetComponent<StaticPriceComponent>(ent, out var staticpricecomp))
                    {
                        Assert.That(staticpricecomp.Price, Is.EqualTo(0),
                            $"The prototype {proto} has a StackComponent and StaticPriceComponent whose values are not compatible with each other.");
                    }
                }

                entManager.DeleteEntity(ent);
            }
            mapManager.DeleteMap(mapId);
        });
        await pairTracker.CleanReturnAsync();
    }

    [Test]
    public async Task StackPrice()
    {
        const string StackProto = @"
- type: entity
  id: A

- type: stack
  id: StackProto
  spawn: A

- type: entity
  id: StackEnt
  components:
  - type: StackPrice
    price: 20
  - type: Stack
    stackType: StackProto
    count: 5
";

        await using var pairTracker = await PoolManager.GetServerClient(new PoolSettings{NoClient = true, ExtraPrototypes = StackProto});
        var server = pairTracker.Pair.Server;

        var entManager = server.ResolveDependency<IEntityManager>();
        var priceSystem = entManager.System<PricingSystem>();

        var ent = entManager.SpawnEntity("StackEnt", MapCoordinates.Nullspace);
        var price = priceSystem.GetPrice(ent);
        Assert.That(price, Is.EqualTo(100.0));

        await pairTracker.CleanReturnAsync();
    }
}
