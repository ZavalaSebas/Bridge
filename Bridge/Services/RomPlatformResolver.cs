using Bridge.Core.Contracts;
using Bridge.Core.Entities;
using Bridge.Emulation;

namespace Bridge.Services;

public static class RomPlatformResolver
{
    public static RomPlatformDefinition? Resolve(Game game, IRepository<Platform> platformRepository)
    {
        var platform = game.PlatformIds.Select(platformRepository.Get).FirstOrDefault(item => item is not null);
        return platform is null ? null : RomPlatformCatalog.FindByPlatformName(platform.Name);
    }
}
