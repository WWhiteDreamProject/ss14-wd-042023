using System.Resources;
using Content.Shared.GameTicking;
using Content.Shared.Interaction.Events;
using Content.Shared.White;
using Content.Shared.White.Jukebox;
using Robust.Client.Audio;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Audio;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Client.White.Jukebox;

public sealed class JukeboxSystem : EntitySystem
{

    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IResourceCache _resource = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;



    private readonly Dictionary<JukeboxComponent, JukeboxAudio> _playingJukeboxes = new();

    private float _maxAudioRange = default!;

    public override void Initialize()
    {
        base.Initialize();

        _cfg.OnValueChanged(WhiteVars.MaxJukeboxSoundRange, range => _maxAudioRange = range, true);

        SubscribeLocalEvent<JukeboxComponent, ComponentHandleState>(OnStateChanged);
        SubscribeLocalEvent<JukeboxComponent, ComponentRemove>(OnComponentRemoved);
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeNetworkEvent<JukeboxStopPlaying>(OnStopPlaying);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        foreach (var playingJukebox in _playingJukeboxes.Values)
        {
            playingJukebox.PlayingStream.Stop();
        }

        _playingJukeboxes.Clear();
    }

    private void OnComponentRemoved(EntityUid uid, JukeboxComponent component, ComponentRemove args)
    {
        if (!_playingJukeboxes.TryGetValue(component, out var playingData)) return;
        playingData.PlayingStream.Stop();
        _playingJukeboxes.Remove(component);
    }

    private void OnStopPlaying(JukeboxStopPlaying ev)
    {
        if (!ev.JukeboxUid.HasValue) return;
        if(!TryComp<JukeboxComponent>(ev.JukeboxUid, out var jukeboxComponent)) return;

        if(!_playingJukeboxes.TryGetValue(jukeboxComponent, out var jukeboxAudio)) return;

        jukeboxAudio.PlayingStream.Stop();
        _playingJukeboxes.Remove(jukeboxComponent);
    }

    public void RequestSongToPlay(JukeboxComponent component, JukeboxSong jukeboxSong)
    {
        if (!_resource.TryGetResource<AudioResource>(jukeboxSong.SongPath!, out var songResource))
        {
            return;
        }

        RaiseNetworkEvent(new JukeboxRequestSongPlay()
        {
            Jukebox = component.Owner,
            SongName = jukeboxSong.SongName,
            SongPath = jukeboxSong.SongPath,
            SongDuration = (float)songResource.AudioStream.Length.TotalSeconds
        });

    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        ProcessJukeboxes();
    }

    private void OnStateChanged(EntityUid uid, JukeboxComponent component, ref ComponentHandleState args)
    {
        if (args.Current is JukeboxComponentState state)
        {
            component.Repeating = state.Playing;
            component.Volume = state.Volume;
            component.PlayingSongData = state.SongData;
        }
    }

    private void ProcessJukeboxes()
    {
        var jukeboxes = EntityQuery<JukeboxComponent>();

        foreach (var jukeboxComponent in jukeboxes)
        {
            if (_playingJukeboxes.TryGetValue(jukeboxComponent, out var jukeboxAudio))
            {
                if (jukeboxAudio.PlayingStream.Done)
                {
                    HandleDoneStream(jukeboxAudio, jukeboxComponent);
                    return;
                }

                if (jukeboxAudio.SongData.SongPath != jukeboxComponent.PlayingSongData?.SongPath)
                {
                    HandleSongChanged(jukeboxAudio, jukeboxComponent);
                    return;
                }
            }
            else
            {
                if (jukeboxComponent.PlayingSongData == null)
                {
                    SetBarsLayerVisible(jukeboxComponent, false);
                    continue;
                }

                var stream = TryCreateStream(jukeboxComponent);

                if (stream == null)
                {
                    return;
                }

                _playingJukeboxes.Add(jukeboxComponent, stream);
                SetBarsLayerVisible(jukeboxComponent, true);
            }
        }
    }

    private void HandleSongChanged(JukeboxAudio jukeboxAudio, JukeboxComponent jukeboxComponent)
    {
        jukeboxAudio.PlayingStream.Stop();

        if (jukeboxComponent.PlayingSongData != null && jukeboxComponent.PlayingSongData.SongPath == jukeboxAudio.SongData.SongPath)
        {
            var newStream = TryCreateStream(jukeboxComponent);
            if(newStream == null) return;

            _playingJukeboxes[jukeboxComponent] = newStream;
            SetBarsLayerVisible(jukeboxComponent, true);
        }
        else
        {
            _playingJukeboxes.Remove(jukeboxComponent);
            SetBarsLayerVisible(jukeboxComponent, false);
        }
    }

    private void HandleDoneStream(JukeboxAudio jukeboxAudio, JukeboxComponent jukeboxComponent)
    {
        if (!jukeboxComponent.Repeating)
        {
            jukeboxAudio.PlayingStream.Stop();
            _playingJukeboxes.Remove(jukeboxComponent);
            SetBarsLayerVisible(jukeboxComponent, false);
            return;
        }

        var newStream = TryCreateStream(jukeboxComponent);

        if (newStream == null)
        {
            _playingJukeboxes.Remove(jukeboxComponent);
            SetBarsLayerVisible(jukeboxComponent, false);
        }
        else
        {

            _playingJukeboxes[jukeboxComponent] = newStream;
            SetBarsLayerVisible(jukeboxComponent, true);
        }
    }

    private JukeboxAudio? TryCreateStream(JukeboxComponent jukeboxComponent)
    {
        //TODO: Валидация?!?!?!
        var fileName = jukeboxComponent.PlayingSongData!.SongPath!;
        var localSession = _playerManager.LocalPlayer!.Session;

        if(!_resource.TryGetResource<AudioResource>(fileName, out var audio)) return null!;

        if (audio!.AudioStream.Length.TotalSeconds < jukeboxComponent.PlayingSongData!.PlaybackPosition)
        {
            return null!;
        }

        var audioParams = new AudioParams
        {
            PlayOffsetSeconds = jukeboxComponent.PlayingSongData.PlaybackPosition,
            Volume = jukeboxComponent.Volume,
            MaxDistance = _maxAudioRange
        };

        var playingStream = _audioSystem.PlayEntity(fileName.ToString(), localSession, jukeboxComponent.Owner, audioParams) as AudioSystem.PlayingStream;
        if (playingStream == null) return null!;


        return new JukeboxAudio(playingStream, audio!, jukeboxComponent.PlayingSongData);
    }

    private class JukeboxAudio
    {
        public PlayingSongData SongData { get; }
        public AudioSystem.PlayingStream PlayingStream { get; }
        public AudioResource AudioStream { get; }

        public JukeboxAudio(AudioSystem.PlayingStream playingStream, AudioResource audioStream, PlayingSongData songData)
        {
            PlayingStream = playingStream;
            AudioStream = audioStream;
            SongData = songData;
        }
    }

    private void SetBarsLayerVisible(JukeboxComponent jukeboxComponent, bool visible)
    {
        var spriteComponent = Comp<SpriteComponent>(jukeboxComponent.Owner);
        spriteComponent.LayerMapTryGet("bars", out var layer);
        spriteComponent.LayerSetVisible(layer, visible);
    }
}
