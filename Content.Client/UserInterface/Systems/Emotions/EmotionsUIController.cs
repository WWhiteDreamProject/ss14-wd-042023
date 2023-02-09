using Content.Client.Gameplay;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.Emotions.Windows;
using Content.Shared.Input;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input.Binding;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.Systems.Emotions;

public sealed class EmotionsUIController : UIController, IOnStateChanged<GameplayState>
{
    private EmotionsWindow? _window;
    private MenuButton? EmotionsButton => UIManager.GetActiveUIWidgetOrNull<MenuBar.Widgets.GameTopMenuBar>()?.EmotionsButton;

    public void OnStateEntered(GameplayState state)
    {
        DebugTools.Assert(_window == null);

        _window = UIManager.CreateWindow<EmotionsWindow>();

        _window.OnOpen += OnWindowOpened;
        _window.OnClose += OnWindowClosed;

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
