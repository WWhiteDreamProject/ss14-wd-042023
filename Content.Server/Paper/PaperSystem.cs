using Content.Server.Administration.Logs;
using Content.Server.Popups;
using Content.Server.UserInterface;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Paper;
using Content.Shared.Tag;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Utility;
using static Content.Shared.Paper.SharedPaperComponent;

namespace Content.Server.Paper
{
    public sealed class PaperSystem : EntitySystem
    {
        [Dependency] private readonly IAdminLogManager _adminLogger = default!;
        [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
        [Dependency] private readonly SharedInteractionSystem _interaction = default!;
        [Dependency] private readonly PopupSystem _popupSystem = default!;
        [Dependency] private readonly TagSystem _tagSystem = default!;
        [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
        [Dependency] private readonly SharedAudioSystem _audio = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<PaperComponent, ComponentInit>(OnInit);
            SubscribeLocalEvent<PaperComponent, BeforeActivatableUIOpenEvent>(BeforeUIOpen);
            SubscribeLocalEvent<PaperComponent, BoundUIClosedEvent>(UIClosed);
            SubscribeLocalEvent<PaperComponent, ExaminedEvent>(OnExamined);
            SubscribeLocalEvent<PaperComponent, InteractUsingEvent>(OnInteractUsing);
            SubscribeLocalEvent<PaperComponent, PaperInputTextMessage>(OnInputTextMessage);

            SubscribeLocalEvent<ActivateOnPaperOpenedComponent, PaperWriteEvent>(OnPaperWrite);

            SubscribeLocalEvent<PaperComponent, MapInitEvent>(OnMapInit);
        }

        private void OnMapInit(EntityUid uid, PaperComponent paperComp, MapInitEvent args)
        {
            if (!string.IsNullOrEmpty(paperComp.Content))
            {
                paperComp.Content = Loc.GetString(paperComp.Content);
            }
        }

        private void OnInit(EntityUid uid, PaperComponent paperComp, ComponentInit args)
        {
            paperComp.Mode = PaperAction.Read;
            UpdateUserInterface(uid, paperComp);

            if (TryComp<AppearanceComponent>(uid, out var appearance))
            {
                if (paperComp.Content != "")
                    _appearance.SetData(uid, PaperVisuals.Status, PaperStatus.Written, appearance);

                if (paperComp.StampState != null)
                    _appearance.SetData(uid, PaperVisuals.Stamp, paperComp.StampState, appearance);
            }

        }

        private void BeforeUIOpen(EntityUid uid, PaperComponent paperComp, BeforeActivatableUIOpenEvent args)
        {
            paperComp.Mode = PaperAction.Read;
            UpdateUserInterface(uid, paperComp);
            if (paperComp == null)
                return;
            _audio.PlayPvs(paperComp.OpenSounds, paperComp.Owner, paperComp.DefaultParams);
        }

        private void UIClosed(EntityUid uid, PaperComponent paperComp, BoundUIClosedEvent args)
        {
            if (paperComp == null)
                return;
            _audio.PlayPvs(paperComp.CloseSounds, paperComp.Owner, paperComp.DefaultParams);
        }

        private void OnExamined(EntityUid uid, PaperComponent paperComp, ExaminedEvent args)
        {
            if (!args.IsInDetailsRange)
                return;

            if (paperComp.Content != "")
                args.PushMarkup(
                    Loc.GetString(
                        "paper-component-examine-detail-has-words", ("paper", uid)
                    )
                );

            if (paperComp.StampedBy.Count > 0)
            {
                string commaSeparated = string.Join(", ", paperComp.StampedBy);
                args.PushMarkup(
                    Loc.GetString(
                        "paper-component-examine-detail-stamped-by", ("paper", uid), ("stamps", commaSeparated))
                );
            }
        }

        private void OnInteractUsing(EntityUid uid, PaperComponent paperComp, InteractUsingEvent args)
        {
            if (_tagSystem.HasTag(args.Used, "Write"))
            {
                var writeEvent = new PaperWriteEvent(uid, args.User);
                RaiseLocalEvent(args.Used, ref writeEvent);
                if (!TryComp<ActorComponent>(args.User, out var actor))
                    return;

                paperComp.Mode = PaperAction.Write;
                UpdateUserInterface(uid, paperComp);
                _uiSystem.GetUiOrNull(uid, PaperUiKey.Key)?.Open(actor.PlayerSession);
                return;
            }

            // If a stamp, attempt to stamp paper
            if (TryComp<StampComponent>(args.Used, out var stampComp) && TryStamp(uid, stampComp.StampedName, stampComp.StampState, paperComp))
            {
                // successfully stamped, play popup
                var stampPaperOtherMessage = Loc.GetString("paper-component-action-stamp-paper-other", ("user", Identity.Entity(args.User, EntityManager)),("target", Identity.Entity(args.Target, EntityManager)),("stamp", args.Used));
                    _popupSystem.PopupEntity(stampPaperOtherMessage, args.User, Filter.PvsExcept(args.User, entityManager: EntityManager), true);
                var stampPaperSelfMessage = Loc.GetString("paper-component-action-stamp-paper-self", ("target", Identity.Entity(args.Target, EntityManager)),("stamp", args.Used));
                    _popupSystem.PopupEntity(stampPaperSelfMessage, args.User, args.User);
            }
        }

        private void OnInputTextMessage(EntityUid uid, PaperComponent paperComp, PaperInputTextMessage args)
        {
            if (string.IsNullOrEmpty(args.Text))
                return;

            var text = FormattedMessage.EscapeText(args.Text);

            if (text.Length + paperComp.Content.Length <= paperComp.ContentSize)
                paperComp.Content += text + '\n';

            if (TryComp<AppearanceComponent>(uid, out var appearance))
                _appearance.SetData(uid, PaperVisuals.Status, PaperStatus.Written, appearance);

            if (TryComp<MetaDataComponent>(uid, out var meta))
                meta.EntityDescription = "";

            if (args.Session.AttachedEntity != null)
                _adminLogger.Add(LogType.Chat, LogImpact.Low,
                    $"{ToPrettyString(args.Session.AttachedEntity.Value):player} has written on {ToPrettyString(uid):entity} the following text: {args.Text}");

            UpdateUserInterface(uid, paperComp);
        }

        private void OnPaperWrite(EntityUid uid, ActivateOnPaperOpenedComponent comp, ref PaperWriteEvent args)
        {
            _interaction.UseInHandInteraction(args.User, uid);
        }

        /// <summary>
        ///     Accepts the name and state to be stamped onto the paper, returns true if successful.
        /// </summary>
        public bool TryStamp(EntityUid uid, string stampName, string stampState, PaperComponent? paperComp = null)
        {
            if (!Resolve(uid, ref paperComp))
                return false;

            if (!paperComp.StampedBy.Contains(Loc.GetString(stampName)))
            {
                paperComp.StampedBy.Add(Loc.GetString(stampName));
                if (paperComp.StampState == null && TryComp<AppearanceComponent>(uid, out var appearance))
                {
                    paperComp.StampState = stampState;
                    _appearance.SetData(uid, PaperVisuals.Stamp, paperComp.StampState, appearance);
                }
            }
            return true;
        }

        public void SetContent(EntityUid uid, string content, PaperComponent? paperComp = null)
        {
            if (!Resolve(uid, ref paperComp))
                return;

            paperComp.Content = content + '\n';
            UpdateUserInterface(uid, paperComp);

            if (!TryComp<AppearanceComponent>(uid, out var appearance))
                return;

            var status = string.IsNullOrWhiteSpace(content)
                ? PaperStatus.Blank
                : PaperStatus.Written;

            _appearance.SetData(uid, PaperVisuals.Status, status, appearance);
        }

        public void UpdateUserInterface(EntityUid uid, PaperComponent? paperComp = null)
        {
            if (!Resolve(uid, ref paperComp))
                return;

            _uiSystem.GetUiOrNull(uid, PaperUiKey.Key)?.SetState(new PaperBoundUserInterfaceState(paperComp.Content, paperComp.StampedBy, paperComp.Mode));
        }
    }

    /// <summary>
    /// Event fired when using a pen on paper, opening the UI.
    /// </summary>
    [ByRefEvent]
    public record struct PaperWriteEvent(EntityUid User, EntityUid Paper);
}
