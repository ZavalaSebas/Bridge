using Bridge.Core.Import;

namespace Bridge.Core.Contracts;

public interface IGameMetadataProvider
{
    string Name { get; }
    Task<GameMetadata?> SearchAsync(string gameName, CancellationToken cancellationToken = default);
}
