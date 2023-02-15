﻿using Content.Client.Chat.Managers;
using Content.Client.Gameplay;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.Emotions.Windows;
using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Input;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input.Binding;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.Systems.Emotions;

public sealed class EmotionsUIController : UIController, IOnStateChanged<GameplayState>
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;

    private EmotionsWindow? _window;
    private MenuButton? EmotionsButton => UIManager.GetActiveUIWidgetOrNull<MenuBar.Widgets.GameTopMenuBar>()?.EmotionsButton;

    public void OnStateEntered(GameplayState state)
    {
        DebugTools.Assert(_window == null);

        _window = UIManager.CreateWindow<EmotionsWindow>();

        _window.OnOpen += OnWindowOpened;
        _window.OnClose += OnWindowClosed;

        var emotions = _prototypeManager.EnumeratePrototypes<EmotePrototype>();

        foreach (var emote in emotions)
        {
            var control = new Button();
            control.OnPressed += _ => _chatManager.SendMessage(_random.Pick(emote.ChatMessages).ToCharArray(), ChatSelectChannel.Emotes);
            control.Text = emote.ButtonText;
            control.HorizontalExpand = true;
            control.VerticalExpand = true;
            control.MaxWidth = 250;
            control.MaxHeight = 50;
            _window.EmotionsContainer.AddChild(control);
        }

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.OpenEmotionsMenu,
                InputCmdHandler.FromDelegate(_ => ToggleWindow()))
            .Register<EmotionsUIController>();
    }

    public void UnloadButton()
    {
        if (EmotionsButton == null)
        {
            return;
        }

        EmotionsButton.OnPressed -= EmotionsButtonPressed;
    }

    public void LoadButton()
    {
        if (EmotionsButton == null)
        {
            return;
        }

        EmotionsButton.OnPressed += EmotionsButtonPressed;
    }

    private void OnWindowOpened()
    {
        if (EmotionsButton != null)
            EmotionsButton.Pressed = true;
    }

    private void OnWindowClosed()
    {
        if (EmotionsButton != null)
            EmotionsButton.Pressed = false;
    }

    public void OnStateExited(GameplayState state)
    {
        if (_window != null)
        {
            _window.OnOpen -= OnWindowOpened;
            _window.OnClose -= OnWindowClosed;

            _window.Dispose();
            _window = null;
        }

        CommandBinds.Unregister<EmotionsUIController>();
    }

    private void EmotionsButtonPressed(BaseButton.ButtonEventArgs args)
    {
        ToggleWindow();
    }

    private void ToggleWindow()
    {
        if (_window == null)
            return;

        if (_window.IsOpen)
        {
            _window.Close();
            return;
        }

        _window.Open();
    }
}
