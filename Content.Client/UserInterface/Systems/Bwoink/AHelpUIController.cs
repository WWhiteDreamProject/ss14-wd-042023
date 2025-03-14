using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Client.Administration.Managers;
using Content.Client.Administration.Systems;
using Content.Client.Administration.UI.Bwoink;
using Content.Client.Gameplay;
using Content.Client.UserInterface.Controls;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Content.Shared.Input;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Audio;
using Robust.Shared.Configuration;
using Robust.Shared.Input.Binding;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.Systems.Bwoink;

[UsedImplicitly]
public sealed class AHelpUIController: UIController, IOnStateChanged<GameplayState>, IOnSystemChanged<BwoinkSystem>
{
    [Dependency] private readonly IClientAdminManager _adminManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IUserInterfaceManager _uiManager = default!;
    [Dependency] private readonly IClientAdminManager _clientAdminManager = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;


    private BwoinkSystem? _bwoinkSystem;
    private MenuButton? AhelpButton => UIManager.GetActiveUIWidgetOrNull<MenuBar.Widgets.GameTopMenuBar>()?.AHelpButton;
    public IAHelpUIHandler? UIHelper;
    private bool _discordRelayActive;
    private float defaultBwoinkVolume;
    private float adminBwoinkVolume;


    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<SharedBwoinkSystem.BwoinkDiscordRelayUpdated>(DiscordRelayUpdated);
        defaultBwoinkVolume = CCVars.BwoinkVolume.DefaultValue;
        _cfg.OnValueChanged(CCVars.BwoinkVolume, volume => adminBwoinkVolume = volume);
    }

    public void OnStateEntered(GameplayState state)
    {
        DebugTools.Assert(UIHelper == null);
        _adminManager.AdminStatusUpdated += OnAdminStatusUpdated;

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.OpenAHelp,
                InputCmdHandler.FromDelegate(_ => ToggleWindow()))
            .Register<AHelpUIController>();
    }

    public void UnloadButton()
    {
        if (AhelpButton == null)
        {
            return;
        }

        AhelpButton.OnPressed -= AHelpButtonPressed;
    }

    public void LoadButton()
    {
        if (AhelpButton == null)
        {
            return;
        }

        AhelpButton.OnPressed += AHelpButtonPressed;
    }

    private void OnAdminStatusUpdated()
    {
        if (UIHelper is not { IsOpen: true })
            return;
        EnsureUIHelper();
    }

    private void AHelpButtonPressed(BaseButton.ButtonEventArgs obj)
    {
        EnsureUIHelper();
        UIHelper!.ToggleWindow();
    }

    public void OnStateExited(GameplayState state)
    {
        SetAHelpPressed(false);
        _adminManager.AdminStatusUpdated -= OnAdminStatusUpdated;
        UIHelper?.Dispose();
        UIHelper = null;
        CommandBinds.Unregister<AHelpUIController>();
    }
    public void OnSystemLoaded(BwoinkSystem system)
    {
        _bwoinkSystem = system;
        _bwoinkSystem.OnBwoinkTextMessageRecieved += RecievedBwoink;
    }

    public void OnSystemUnloaded(BwoinkSystem system)
    {
        DebugTools.Assert(_bwoinkSystem != null);
        _bwoinkSystem!.OnBwoinkTextMessageRecieved -= RecievedBwoink;
        _bwoinkSystem = null;
    }

    private void SetAHelpPressed(bool pressed)
    {
        if (AhelpButton == null || AhelpButton.Pressed == pressed)
            return;
        AhelpButton.StyleClasses.Remove(MenuButton.StyleClassRedTopButton);
        AhelpButton.Pressed = pressed;
    }

    private void RecievedBwoink(object? sender, SharedBwoinkSystem.BwoinkTextMessage message)
    {
        Logger.InfoS("c.s.go.es.bwoink", $"@{message.UserId}: {message.Text}");
        var localPlayer = _playerManager.LocalPlayer;
        if (localPlayer == null)
        {
            return;
        }

        float bwoinkVolume = _clientAdminManager.IsActive() ? adminBwoinkVolume : defaultBwoinkVolume;

        var audioParams = new AudioParams()
        {
            Volume = bwoinkVolume
        };

        if (localPlayer.UserId != message.TrueSender)
        {
            SoundSystem.Play("/Audio/Effects/adminhelp.ogg", Filter.Local(), audioParams);
            _clyde.RequestWindowAttention();
        }

        EnsureUIHelper();
        if (!UIHelper!.IsOpen)
        {
            AhelpButton?.StyleClasses.Add(MenuButton.StyleClassRedTopButton);
        }
        UIHelper!.Receive(message);
    }

    private void DiscordRelayUpdated(SharedBwoinkSystem.BwoinkDiscordRelayUpdated args, EntitySessionEventArgs session)
    {
        _discordRelayActive = args.DiscordRelayEnabled;
        UIHelper?.DiscordRelayChanged(_discordRelayActive);
    }

    public void EnsureUIHelper()
    {
        var isAdmin = _adminManager.HasFlag(AdminFlags.Adminhelp);

        if (UIHelper != null && UIHelper.IsAdmin == isAdmin)
            return;

        UIHelper?.Dispose();
        var ownerUserId = _playerManager.LocalPlayer!.UserId;
        UIHelper = isAdmin ? new AdminAHelpUIHandler(ownerUserId) : new UserAHelpUIHandler(ownerUserId);
        UIHelper.DiscordRelayChanged(_discordRelayActive);

        UIHelper.SendMessageAction = (userId, textMessage) => _bwoinkSystem?.Send(userId, textMessage);
        UIHelper.OnClose += () => { SetAHelpPressed(false); };
        UIHelper.OnOpen +=  () => { SetAHelpPressed(true); };
        SetAHelpPressed(UIHelper.IsOpen);
    }

    public void Close()
    {
        UIHelper?.Close();
    }

    public void Open()
    {
        var localPlayer = _playerManager.LocalPlayer;
        if (localPlayer == null)
        {
            return;
        }
        EnsureUIHelper();
        if (UIHelper!.IsOpen)
            return;
        UIHelper!.Open(localPlayer.UserId, _discordRelayActive);
    }

    public void Open(NetUserId userId)
    {
        EnsureUIHelper();
        if (!UIHelper!.IsAdmin)
            return;
        UIHelper?.Open(userId, _discordRelayActive);
    }

    public void ToggleWindow()
    {
        EnsureUIHelper();
        UIHelper?.ToggleWindow();
    }

    public void PopOut()
    {
        EnsureUIHelper();
        if (UIHelper is not AdminAHelpUIHandler helper)
            return;

        if (helper.Window == null || helper.Control == null)
        {
            return;
        }

        helper.Control.Orphan();
        helper.Window.Dispose();
        helper.Window = null;
        helper.EverOpened = false;

        var monitor = _clyde.EnumerateMonitors().First();

        helper.ClydeWindow = _clyde.CreateWindow(new WindowCreateParameters
        {
            Maximized = false,
            Title = "Admin Help",
            Monitor = monitor,
            Width = 900,
            Height = 500
        });

        helper.ClydeWindow.RequestClosed += helper.OnRequestClosed;
        helper.ClydeWindow.DisposeOnClose = true;

        helper.WindowRoot = _uiManager.CreateWindowRoot(helper.ClydeWindow);
        helper.WindowRoot.AddChild(helper.Control);

        helper.Control.PopOut.Disabled = true;
        helper.Control.PopOut.Visible = false;
    }
}

// please kill all this indirection
public interface IAHelpUIHandler : IDisposable
{
    public bool IsAdmin { get; }
    public bool IsOpen { get; }
    public void Receive(SharedBwoinkSystem.BwoinkTextMessage message);
    public void Close();
    public void Open(NetUserId netUserId, bool relayActive);
    public void ToggleWindow();
    public void DiscordRelayChanged(bool active);
    public event Action OnClose;
    public event Action OnOpen;
    public Action<NetUserId, string>? SendMessageAction { get; set; }
}
public sealed class AdminAHelpUIHandler : IAHelpUIHandler
{
    private readonly NetUserId _ownerId;
    public AdminAHelpUIHandler(NetUserId owner)
    {
        _ownerId = owner;
    }
    private readonly Dictionary<NetUserId, BwoinkPanel> _activePanelMap = new();
    public bool IsAdmin => true;
    public bool IsOpen => Window is { Disposed: false, IsOpen: true } || ClydeWindow is { IsDisposed: false };
    public bool EverOpened;

    public BwoinkWindow? Window;
    public WindowRoot? WindowRoot;
    public IClydeWindow? ClydeWindow;
    public BwoinkControl? Control;

    public void Receive(SharedBwoinkSystem.BwoinkTextMessage message)
    {
        var panel = EnsurePanel(message.UserId);
        panel.ReceiveLine(message);
        Control?.OnBwoink(message.UserId);
    }

    private void OpenWindow()
    {
        if (Window == null)
            return;

        if (EverOpened)
            Window.Open();
        else
            Window.OpenCentered();
    }

    public void Close()
    {
        Window?.Close();

        // popped-out window is being closed
        if (ClydeWindow != null)
        {
            ClydeWindow.RequestClosed -= OnRequestClosed;
            ClydeWindow.Dispose();
            // need to dispose control cause we cant reattach it directly back to the window
            // but orphan panels first so -they- can get readded when the window is opened again
            if (Control != null)
            {
                foreach (var (_, panel) in _activePanelMap)
                {
                    panel.Orphan();
                }
                Control?.Dispose();
            }
            // window wont be closed here so we will invoke ourselves
            OnClose?.Invoke();
        }
    }

    public void ToggleWindow()
    {
        EnsurePanel(_ownerId);

        if (IsOpen)
            Close();
        else
            OpenWindow();
    }

    public void DiscordRelayChanged(bool active)
    {
    }

    public event Action? OnClose;
    public event Action? OnOpen;
    public Action<NetUserId, string>? SendMessageAction { get; set; }

    public void Open(NetUserId channelId, bool relayActive)
    {
        SelectChannel(channelId);
        OpenWindow();
    }

    public void OnRequestClosed(WindowRequestClosedEventArgs args)
    {
        Close();
    }

    private void EnsureControl()
    {
        if (Control is { Disposed: false })
            return;

        Window = new BwoinkWindow();
        Control = Window.Bwoink;
        Window.OnClose += () => { OnClose?.Invoke(); };
        Window.OnOpen += () =>
        {
            OnOpen?.Invoke();
            EverOpened = true;
        };

        // need to readd any unattached panels..
        foreach (var (_, panel) in _activePanelMap)
        {
            if (!Control!.BwoinkArea.Children.Contains(panel))
            {
                Control!.BwoinkArea.AddChild(panel);
            }
            panel.Visible = false;
        }
    }

    public BwoinkPanel EnsurePanel(NetUserId channelId)
    {
        EnsureControl();

        if (_activePanelMap.TryGetValue(channelId, out var existingPanel))
            return existingPanel;

        _activePanelMap[channelId] = existingPanel = new BwoinkPanel(text => SendMessageAction?.Invoke(channelId, text));
        existingPanel.Visible = false;
        if (!Control!.BwoinkArea.Children.Contains(existingPanel))
            Control.BwoinkArea.AddChild(existingPanel);

        return existingPanel;
    }
    public bool TryGetChannel(NetUserId ch, [NotNullWhen(true)] out BwoinkPanel? bp) => _activePanelMap.TryGetValue(ch, out bp);

    private void SelectChannel(NetUserId uid)
    {
        EnsurePanel(uid);
        Control!.SelectChannel(uid);
    }

    public void Dispose()
    {
        Window?.Dispose();
        Window = null;
        Control = null;
        _activePanelMap.Clear();
        EverOpened = false;
    }
}

public sealed class UserAHelpUIHandler : IAHelpUIHandler
{
    private readonly NetUserId _ownerId;
    public UserAHelpUIHandler(NetUserId owner)
    {
        _ownerId = owner;
    }
    public bool IsAdmin => false;
    public bool IsOpen => _window is { Disposed: false, IsOpen: true };
    private DefaultWindow? _window;
    private BwoinkPanel? _chatPanel;
    private bool _discordRelayActive;

    public void Receive(SharedBwoinkSystem.BwoinkTextMessage message)
    {
        DebugTools.Assert(message.UserId == _ownerId);
        EnsureInit(_discordRelayActive);
        _chatPanel!.ReceiveLine(message);
        _window!.OpenCentered();
    }

    public void Close()
    {
        _window?.Close();
    }

    public void ToggleWindow()
    {
        EnsureInit(_discordRelayActive);
        if (_window!.IsOpen)
        {
            _window.Close();
        }
        else
        {
            _window.OpenCentered();
        }
    }

    // user can't pop out their window.
    public void PopOut()
    {
    }

    public void DiscordRelayChanged(bool active)
    {
        _discordRelayActive = active;

        if (_chatPanel != null)
        {
            _chatPanel.RelayedToDiscordLabel.Visible = active;
        }
    }

    public event Action? OnClose;
    public event Action? OnOpen;
    public Action<NetUserId, string>? SendMessageAction { get; set; }

    public void Open(NetUserId channelId, bool relayActive)
    {
        EnsureInit(relayActive);
        _window!.OpenCentered();
    }

    private void EnsureInit(bool relayActive)
    {
        if (_window is { Disposed: false })
            return;
        _chatPanel = new BwoinkPanel(text => SendMessageAction?.Invoke(_ownerId, text));
        _chatPanel.RelayedToDiscordLabel.Visible = relayActive;
        _window = new DefaultWindow()
        {
            TitleClass="windowTitleAlert",
            HeaderClass="windowHeaderAlert",
            Title=Loc.GetString("bwoink-user-title"),
            MinSize=(500, 200),
        };
        _window.OnClose += () => { OnClose?.Invoke(); };
        _window.OnOpen += () => { OnOpen?.Invoke(); };
        _window.Contents.AddChild(_chatPanel);
    }

    public void Dispose()
    {
        _window?.Dispose();
        _window = null;
        _chatPanel = null;
    }
}
