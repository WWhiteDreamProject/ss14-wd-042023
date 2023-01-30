using Content.Server.Chat.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Mobs;
using Robust.Shared.Audio;
using Robust.Shared.Random;

namespace Content.Server.White.Other;

public sealed class OnDeath : EntitySystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<HumanoidAppearanceComponent, MobStateChangedEvent>(HandleDeathEvent);
    }

    private IPlayingAudioStream? _playingStream;
    private static readonly string[] DeathGaspMessages =
    {
        "death-gasp-high",
        "death-gasp-medium",
        "death-gasp-normal"
    };

    private void HandleDeathEvent(EntityUid uid, HumanoidAppearanceComponent component, MobStateChangedEvent args)
    {
        switch (args.NewMobState)
        {
            case MobState.Critical:
                PlayPlayingStream(uid);
                break;
            case MobState.Dead:
                StopPlayingStream();
                var deathGaspMessage = SelectRandomDeathGaspMessage();
                var localizedMessage = LocalizeDeathGaspMessage(deathGaspMessage);
                SendDeathGaspMessage(uid, localizedMessage);
                PlayDeathSound(uid);
                break;
        }
    }


    private void PlayPlayingStream(EntityUid uid)
        => _playingStream = _audio.PlayEntity("/White/Audio/Heart/heart.ogg", uid, uid, AudioParams.Default.WithLoop(true));

    private void StopPlayingStream()
        => _playingStream?.Stop();

    private string SelectRandomDeathGaspMessage()
        => DeathGaspMessages[_random.Next(DeathGaspMessages.Length)];

    private string LocalizeDeathGaspMessage(string message)
        => Loc.GetString(message);

    private void SendDeathGaspMessage(EntityUid uid, string message)
        => _chat.TrySendInGameICMessage(uid, message, InGameICChatType.Emote, false, force: true);

    private void PlayDeathSound(EntityUid uid)
        => _audio.PlayEntity("/White/Audio/Death/death.wav", uid, uid, AudioParams.Default);

}
