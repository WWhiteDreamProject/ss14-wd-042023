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
        SubscribeLocalEvent<HumanoidComponent, MobStateChangedEvent>(HandleDeath);
    }

        // An array of death gasp messages.
    private static readonly string[] DeathGaspMessages =
    {
        "death-gasp-high",
        "death-gasp-medium",
        "death-gasp-normal"
    };

        // Handle death gasp event.
    private void HandleDeath(EntityUid uid, HumanoidComponent component, MobStateChangedEvent args)
    {
        // Exit if the mob's state is not dead.
        if (args.NewMobState != MobState.Dead)
            return;

        // Select a random death gasp message.
        var message = DeathGaspMessages[_random.Next(DeathGaspMessages.Length)];

        // Localize the message
        var localizedMessage = Loc.GetString(message);

        // Send the message as an emote to the in-game chat.
        _chat.TrySendInGameICMessage(uid, localizedMessage, InGameICChatType.Emote, false, force: true);

        // Play death sound to uid.
        _audio.PlayEntity("/White/Audio/Death/Death.wav", uid, uid, AudioParams.Default);
    }

}
