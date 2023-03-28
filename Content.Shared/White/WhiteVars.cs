using Robust.Shared.Configuration;

namespace Content.Shared.White;

[CVarDefs]
public sealed class WhiteVars
{
    public static readonly CVarDef<float> MaxJukeboxSongSizeInMB = CVarDef.Create("white.max_jukebox_song_size",
        3.5f, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    public static readonly CVarDef<float> MaxJukeboxSoundRange = CVarDef.Create("white.max_jukebox_sound_range", 20f,
        CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);
}
