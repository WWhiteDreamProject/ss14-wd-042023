using System.Linq;
using Content.Server.Actions;
using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking.Rules;
using Content.Server.Mind.Components;
using Content.Server.Store.Components;
using Content.Server.Store.Systems;
using Content.Shared.Actions.ActionTypes;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared.Nuke;
using Content.Shared.White.MeatyOre;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Players;

namespace Content.Server.White;

public sealed class MeatyOreStoreSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly StoreSystem _storeSystem = default!;
    [Dependency] private readonly IAdminManager _adminManager = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly TraitorRuleSystem _traitorRuleSystem = default!;
    [Dependency] private readonly IConfigurationManager _configurationManager = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;






    private static readonly string StorePresetPrototype = "StorePresetMeatyOre";
    private static readonly string MeatyOreCurrensyPrototype = "MeatyOreCoin";
    private static int DefaultMeatyOreCoinBalance = 0;
    private static bool MeatyOrePanelEnabled;


    private readonly Dictionary<IPlayerSession, StoreComponent> _meatyOreStores = new();
    public override void Initialize()
    {
        base.Initialize();

        _configurationManager.OnValueChanged(CCVars.MeatyOreDefaultBalance, balance => DefaultMeatyOreCoinBalance = balance, true);
        _configurationManager.OnValueChanged(CCVars.MeatyOrePanelEnabled, OnPanelEnableChanged, true);

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnPostRoundCleanup);
        SubscribeNetworkEvent<MeatyOreShopRequestEvent>(OnShopRequested);
        SubscribeLocalEvent<MindComponent, MeatyTraitorRequestActionEvent>(OnAntagPurchase);
    }

    private void OnPanelEnableChanged(bool newValue)
    {
        if (newValue != true)
        {
            foreach (var meatyOreStoreData in _meatyOreStores)
            {
                var playerEntity = meatyOreStoreData.Key.AttachedEntity;
                if(!playerEntity.HasValue) continue;

                _storeSystem.CloseUi(playerEntity.Value, meatyOreStoreData.Value);
            }
        }

        MeatyOrePanelEnabled = newValue;
    }


    private void OnAntagPurchase(EntityUid uid, MindComponent component, MeatyTraitorRequestActionEvent args)
    {
        if(component.Mind == null) return;
        if(component.Mind.Session == null) return;

        _traitorRuleSystem.MakeTraitor(component.Mind?.Session!);
    }

    //Зло есть зло. Меньшее, большее, какая разница?
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        foreach (var storesData in _meatyOreStores)
        {
            if(storesData.Key.AttachedEntity == null) continue;
            Transform(storesData.Value.Owner).Coordinates = Transform(storesData.Key.AttachedEntity.Value).Coordinates;
        }
    }

    private void OnShopRequested(MeatyOreShopRequestEvent msg, EntitySessionEventArgs args)
    {

        var playerSession = args.SenderSession as IPlayerSession;

        if (!MeatyOrePanelEnabled)
        {
            _chatManager.DispatchServerMessage(playerSession!, "Мясная панель отключена на данном сервере! Приятной игры!");
            return;
        }

        var playerEntity = args.SenderSession.AttachedEntity;

        if(!playerEntity.HasValue) return;
        if(!HasComp<HumanoidAppearanceComponent>(playerEntity.Value)) return;
        if(!TryGetStore(playerSession!, out var storeComponent)) return;

        _storeSystem.ToggleUi(playerEntity.Value, storeComponent.Owner, storeComponent);
    }

    private bool TryGetStore(IPlayerSession session, out StoreComponent store)
    {
        store = null!;

        var adminData = _adminManager.GetAdminData(session, true);
        if(adminData == null) return false;

        if (!adminData.HasFlag(AdminFlags.MeatyOre)) return false;

        if (_meatyOreStores.TryGetValue(session, out store!))
        {
            return true;
        }

        store = CreateStore(session);
        return true;
    }

    private void OnPostRoundCleanup(RoundRestartCleanupEvent ev)
    {
        foreach (var store in _meatyOreStores.Values)
        {
            Del(store.Owner);
        }

        _meatyOreStores.Clear();
    }

    private StoreComponent CreateStore(IPlayerSession session)
    {
        var shopEntity = _entityManager.SpawnEntity("StoreMeatyOreEntity", Transform(session.AttachedEntity!.Value).Coordinates);
        var storeComponent = Comp<StoreComponent>(shopEntity);

        _storeSystem.InitializeFromPreset("StorePresetMeatyOre", shopEntity, storeComponent);
        storeComponent.Balance.Clear();

        _storeSystem.TryAddCurrency(new Dictionary<string, FixedPoint2>() { { MeatyOreCurrensyPrototype, DefaultMeatyOreCoinBalance } }, storeComponent.Owner, storeComponent);
        _meatyOreStores[session] = storeComponent;

        return storeComponent;
    }
}
