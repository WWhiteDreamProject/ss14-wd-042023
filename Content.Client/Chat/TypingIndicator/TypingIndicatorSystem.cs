using Content.Shared.CCVar;
using Content.Shared.Chat.TypingIndicator;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Client.Chat.TypingIndicator;

// Client-side typing system tracks user input in chat box
public sealed class TypingIndicatorSystem : SharedTypingIndicatorSystem
{
    [Dependency] private readonly IGameTiming _time = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private readonly TimeSpan _typingTimeout = TimeSpan.FromSeconds(2);
    private TimeSpan _lastTextChange;
    private bool _isClientTyping;
    private bool _isClientChatFocused; // CX-TypingIndicator

    public override void Initialize()
    {
        base.Initialize();
        _cfg.OnValueChanged(CCVars.ChatShowTypingIndicator, OnShowTypingChanged);
    }

    public void ClientChangedChatText()
    {
        // don't update it if player don't want to show typing indicator
        if (!_cfg.GetCVar(CCVars.ChatShowTypingIndicator))
            return;

        // client typed something - show typing indicator
        // CX-TypingIndicator-Start
        _isClientTyping = true;
        ClientUpdateTyping();
        // CX-TypingIndicator-End
        _lastTextChange = _time.CurTime;
    }

    public void ClientSubmittedChatText()
    {
        // don't update it if player don't want to show typing
        if (!_cfg.GetCVar(CCVars.ChatShowTypingIndicator))
            return;

        // client submitted text - hide typing indicator
        // CX-TypingIndicator-Start
        _isClientTyping = false;
        ClientUpdateTyping();
        // CX-TypingIndicator-End
    }

    // CX-TypingIndicator-Start
    public void ClientChangedChatFocus(bool isFocused)
    {
        // don't update it if player don't want to show typing
        if (!_cfg.GetCVar(CCVars.ChatShowTypingIndicator))
            return;

        // client submitted text - hide typing indicator
        _isClientChatFocused = isFocused;
        ClientUpdateTyping();
    }
    // CX-TypingIndicator-End

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // check if client didn't changed chat text box for a long time
        if (_isClientTyping)
        {
            var dif = _time.CurTime - _lastTextChange;
            if (dif > _typingTimeout)
            {
                // client didn't typed anything for a long time - hide indicator
                // CX-TypingIndicator-Start
                _isClientTyping = false;
                ClientUpdateTyping();
                // CX-TypingIndicator-End
            }
        }
    }

    private void ClientUpdateTyping() // CX-TypingIndicator
    {
        // CX-TypingIndicator-Start
        // if (_isClientTyping == isClientTyping)
        //     return;
        // _isClientTyping = isClientTyping;
        // CX-TypingIndicator-End

        // check if player controls any pawn
        if (_playerManager.LocalPlayer?.ControlledEntity == null)
            return;

        // CX-TypingIndicator-Start
        var state = TypingIndicatorState.None;
        if (_isClientChatFocused)
            state = _isClientTyping ? TypingIndicatorState.Typing : TypingIndicatorState.Idle;
        // CX-TypingIndicator-End

        // send a networked event to server
        RaiseNetworkEvent(new TypingChangedEvent(state)); // CX-TypingIndicator
    }

    private void OnShowTypingChanged(bool showTyping)
    {
        // hide typing indicator immediately if player don't want to show it anymore
        if (!showTyping)
        {
            // CX-TypingIndicator-Start
            _isClientTyping = false;
            ClientUpdateTyping();
            // CX-TypingIndicator-End
        }
    }
}
