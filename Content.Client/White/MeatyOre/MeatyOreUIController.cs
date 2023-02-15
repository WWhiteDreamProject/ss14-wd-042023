﻿using Content.Client.Administration.Managers;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.MenuBar.Widgets;
using Content.Shared.Administration;
using Content.Shared.Humanoid;
using Content.Shared.White.MeatyOre;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client.White.MeatyOre;

public sealed class MeatyOreUIController : UIController
{
    [Dependency] private readonly IClientAdminManager _clientAdminManager = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IEntityNetworkManager _entityNetworkManager = default!;

    private bool _buttonLoaded = false;



    private MenuButton? MeatyOreButton => UIManager.GetActiveUIWidgetOrNull<GameTopMenuBar>()?.MeatyOreButton;

    public override void Initialize()
    {
        base.Initialize();
    }

    public void LoadButton()
    {
        MeatyOreButton!.OnPressed += MeatyOreButtonPressed;
        _buttonLoaded = true;
    }

    public void UnloadButton()
    {
        MeatyOreButton!.OnPressed -= MeatyOreButtonPressed;
        _buttonLoaded = false;
    }

    private void MeatyOreButtonPressed(BaseButton.ButtonEventArgs obj)
    {
        _entityNetworkManager.SendSystemNetworkMessage(new MeatyOreShopRequestEvent());
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if(!_buttonLoaded) return;
        var shouldBeVisible = CheckButtonVisibility();
        MeatyOreButton!.Visible = shouldBeVisible;
    }


    private bool CheckButtonVisibility()
    {
        var isMeatyOre = _clientAdminManager.HasFlag(AdminFlags.MeatyOre);
        if(isMeatyOre != true) return false;

        var controlledEntity = _playerManager!.LocalPlayer!.ControlledEntity;
        if(controlledEntity == null) return false;

        if (!_entityManager.HasComponent<HumanoidAppearanceComponent>(controlledEntity)) return false;

        return true;
    }
}
