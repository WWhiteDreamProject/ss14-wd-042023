
using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Server.Ghost.Components;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.GameTicking;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Timing;


namespace Content.Server.Chat.Systems
{
    public sealed class CoolDownChatSystem : EntitySystem
    {
        [Dependency] private readonly IAdminManager _adminManager = default!;
        [Dependency] private readonly IGameTiming _gameTiming = default!;
        [Dependency] private readonly IChatManager _chatManager = default!;
        [Dependency] private readonly IConfigurationManager _configurationManager = default!;

        public Dictionary<NetUserId, TimeSpan> LastTimeUserMessage = new Dictionary<NetUserId, TimeSpan>();
        public Dictionary<NetUserId, TimeSpan> LastTimeUserLOOCMessage = new Dictionary<NetUserId, TimeSpan>();

        private TimeSpan _cooldownAllMessage = new TimeSpan(0);
        private TimeSpan _cooldownLOOCMessage = new TimeSpan(0);

        public override void Initialize()
        {
            base.Initialize();

            _configurationManager.OnValueChanged(CCVars.CooldownAllMessage, (value) => _cooldownAllMessage = TimeSpan.FromSeconds(value), true);
            _configurationManager.OnValueChanged(CCVars.CooldownLOOCMessage, (value) => _cooldownLOOCMessage = TimeSpan.FromSeconds(value), true);

            SubscribeLocalEvent<RoundRestartCleanupEvent>(OnClear);
        }

        public override void Shutdown()
        {
            base.Shutdown();

            LastTimeUserLOOCMessage.Clear();
            LastTimeUserMessage.Clear();
        }

        public void OnClear(RoundRestartCleanupEvent ev)
        {
            LastTimeUserMessage.Clear();
            LastTimeUserLOOCMessage.Clear();
        }

        public bool CheckLOOC(EntityUid source, IPlayerSession? player, bool hideChat)
        {
            if (player == null) return false;

            if (CheckMessageCoolDown(player, LastTimeUserLOOCMessage, _cooldownLOOCMessage, out int remainingTime))
            {
                var mes = Loc.GetString("chat-manager-cooldown-warn-message_channel", ("inChat", "в LOOC"), ("remainingTime", remainingTime));
                _chatManager.ChatMessageToOne(ChatChannel.LOOC, mes, mes, source, hideChat, player.ConnectedClient, colorOverride: Color.White);
                return true;
            }

            return false;
        }

        public bool Check(EntityUid source, IPlayerSession? player, bool hideChat)
        {
            if (player == null) return false;

            if (CheckMessageCoolDown(player, LastTimeUserMessage, _cooldownAllMessage, out int remainingTime))
            {
                var mes = Loc.GetString("chat-manager-cooldown-warn-message", ("remainingTime", remainingTime ));
                _chatManager.ChatMessageToOne(ChatChannel.LOOC, mes, mes, source, hideChat, player.ConnectedClient, colorOverride: Color.White);
                return true;
            }

            return false;
        }

        public bool CheckDeadOrLooc(EntityUid source, IPlayerSession? player, bool hideChat)
        {
            if (HasComp<GhostComponent>(source))
                return Check(source, player, hideChat);
            else
                return CheckLOOC(source, player, hideChat);
        }

        public bool CheckMessageCoolDown(IPlayerSession player, Dictionary<NetUserId, TimeSpan> lastSendMessageStorage, TimeSpan coolDown, out int remainingTime)
        {
            if (_adminManager.HasAdminFlag(player, AdminFlags.Admin))
            {
                remainingTime = -1;
                return false;
            }

            if (lastSendMessageStorage.ContainsKey(player.UserId))
            {
                TimeSpan delta = _gameTiming.CurTime.Subtract(lastSendMessageStorage[player.UserId]);

                if (delta >= coolDown)
                    lastSendMessageStorage[player.UserId] = _gameTiming.CurTime;
                else
                {
                    remainingTime = (int) Math.Ceiling(coolDown.Subtract(delta).TotalSeconds);
                    return true;
                }
            }
            else
                lastSendMessageStorage.Add(player.UserId, _gameTiming.CurTime);

            remainingTime = -1;
            return false;
        }
    }
}
