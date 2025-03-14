﻿using Content.Shared.CCVar;
using Robust.Shared.Configuration;

namespace Content.Shared.White;

[CVarDefs]
public sealed class WhiteVars
{
    public static readonly CVarDef<float> MaxJukeboxSongSizeInMB = CVarDef.Create("white.max_jukebox_song_size",
        3.5f, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    public static readonly CVarDef<float> MaxJukeboxSoundRange = CVarDef.Create("white.max_jukebox_sound_range", 20f,
        CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    public static readonly CVarDef<float> JukeboxVolume =
        CVarDef.Create("white.jukebox_volume", 0f, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<string> ChatGptApi =
        CVarDef.Create("white.gpt_api_link", "", CVar.SERVERONLY | CVar.ARCHIVE | CVar.CONFIDENTIAL);
}
