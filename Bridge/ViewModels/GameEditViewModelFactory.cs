using Bridge.Core.Contracts;
using Bridge.Core.Entities;
using Bridge.Metadata;
using Bridge.Services;

namespace Bridge.ViewModels;

public sealed class GameEditViewModelFactory(
    IGameRepository gameRepository,
    IRepository<Genre> genreRepository,
    IRepository<Company> companyRepository,
    IRepository<Platform> platformRepository,
    IRepository<GameSource> sourceRepository,
    SteamGridDbSettings steamGridDbSettings)
{
    public GameEditViewModel Create(Game game, bool isNew = false) =>
        new(game, gameRepository, genreRepository, companyRepository, platformRepository, sourceRepository, steamGridDbSettings, isNew);
}
