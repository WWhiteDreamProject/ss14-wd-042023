using Robust.Shared.ContentPack;
using Robust.Shared.Network;
using Robust.Shared.Utility;

namespace Content.Shared.White.Jukebox;

public abstract class JukeboxSongsSyncManager
{
    [Dependency] private readonly INetManager _netManager = default!;
    [Dependency] protected readonly IResourceManager ResourceManager = default!;

    public static readonly ResourcePath Prefix = ResourcePath.Root / "Jukebox";

    protected readonly MemoryContentRoot ContentRoot = new();

    public virtual void Initialize()
    {
        ResourceManager.AddRoot(Prefix, ContentRoot);

        _netManager.RegisterNetMessage<JukeboxSongUploadNetMessage>(OnSongUploaded);
    }

    public abstract void OnSongUploaded(JukeboxSongUploadNetMessage message);
}
