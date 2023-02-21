using System.Linq;
using Content.Server.GameTicking.Rules.Components;
using Content.Shared.EntityHealthBar;
using Content.Shared.Humanoid;
using Content.Shared.White.Mindshield;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Mindshield;

public sealed class MindshieldSystem : EntitySystem
{
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    private ResourcePath _hudPath = new("/Textures/White/Overlays/MindShield/hud.rsi");
    private string _state = "hui";

    private List<SpriteComponent> _huds = new();

    private ShaderInstance _shader = default!;

    private bool _overlayEnabled;

    public bool OverlayEnabled
    {
        get => _overlayEnabled;
        set
        {
            _overlayEnabled = value;
            DisableHud();
        }
    }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ShowMindShieldHudComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<ShowMindShieldHudComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<ShowMindShieldHudComponent, PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<ShowMindShieldHudComponent, PlayerDetachedEvent>(OnPlayerDetached);
    }

    private void OnInit(EntityUid uid, ShowMindShieldHudComponent component, ComponentInit args)
    {
        if (_playerManager.LocalPlayer?.ControlledEntity != uid) return;
        OverlayEnabled = true;
    }

    private void OnRemove(EntityUid uid, ShowMindShieldHudComponent component, ComponentRemove args)
    {
        if (_playerManager.LocalPlayer?.ControlledEntity != uid) return;
        OverlayEnabled = false;
    }

    private void OnPlayerAttached(EntityUid uid, ShowMindShieldHudComponent component, PlayerAttachedEvent args)
    {
        OverlayEnabled = true;
    }

    private void OnPlayerDetached(EntityUid uid, ShowMindShieldHudComponent component, PlayerDetachedEvent args)
    {
        OverlayEnabled = false;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        if(!_overlayEnabled) return;

        var entities = EntityManager.EntityQuery<HumanoidAppearanceComponent, MindShieldComponent>();
        var existingHuds = new List<SpriteComponent>();
        foreach (var (_, mindShieldComponent) in entities)
        {
            var entity = mindShieldComponent.Owner;
            var spriteComponent = Comp<SpriteComponent>(entity);
            existingHuds.Add(spriteComponent);
            if(_huds.Contains(spriteComponent)) continue;

            AddHud(mindShieldComponent, spriteComponent);
        }

        var removedHuds = _huds.Except(existingHuds);

        foreach (var removedHud in removedHuds.ToList())
        {
            RemoveHud(removedHud);
        }
    }

    private void AddHud(MindShieldComponent mindShieldComponent, SpriteComponent spriteComponent)
    {
        var layerExists = spriteComponent.LayerMapTryGet(MindShieldComponent.LayerName, out var layer);
        if (!layerExists)
            layer = spriteComponent.LayerMapReserveBlank(MindShieldComponent.LayerName);

        spriteComponent.LayerSetRSI(layer, _hudPath);
        spriteComponent.LayerSetState(layer, _state);
        spriteComponent.LayerSetShader(layer, _shader);

        _huds.Add(spriteComponent);
    }

    private void RemoveHud(SpriteComponent spriteComponent)
    {
        if(!_huds.Contains(spriteComponent)) return;
        var layerExists = spriteComponent.LayerMapTryGet(MindShieldComponent.LayerName, out var layer);
        if(!layerExists) return;
        
        if (HasComp<TransformComponent>(spriteComponent.Owner))
        {
            spriteComponent.RemoveLayer(layer);
        }

        _huds.Remove(spriteComponent);
    }

    private void DisableHud()
    {
        foreach (var hud in _huds)
        {
            var layerExists = hud.LayerMapTryGet(MindShieldComponent.LayerName, out var layer);
            if(!layerExists) continue;

            hud.RemoveLayer(layer);
        }

        _huds.Clear();
    }
}
