using Content.Shared.Paper;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Server.Paper
{
    [NetworkedComponent, RegisterComponent]
    public sealed class PaperComponent : SharedPaperComponent
    {
        public PaperAction Mode;
        [DataField("content")]
        public string Content { get; set; } = "";

        [DataField("contentSize")]
        public int ContentSize { get; set; } = 1000;

        [DataField("stampedBy")]
        public List<string> StampedBy { get; set; } = new();
        /// <summary>
        ///     Stamp to be displayed on the paper, state from beauracracy.rsi
        /// </summary>
        [DataField("stampState")]
        public string? StampState { get; set; }

        public readonly AudioParams DefaultParams = AudioParams.Default.WithVolume(-2f);

        [DataField("openSounds")] public SoundSpecifier? OpenSounds { get; set; } = new SoundCollectionSpecifier("BookOpen");

        [DataField("closeSounds")] public SoundSpecifier? CloseSounds { get; set; } = new SoundCollectionSpecifier("BookClose");
    }
}
