using Bridge.Core.Contracts;
using Bridge.Core.Entities;

namespace Bridge.ViewModels;

public sealed class GameEditViewModelFactory(
    IGameRepository gameRepository,
    IRepository<Genre> genreRepository,
    IRepository<Company> companyRepository,
    IRepository<Platform> platformRepository)
{
    public GameEditViewModel Create(Game game, bool isNew = false) =>
        new(game, gameRepository, genreRepository, companyRepository, platformRepository, isNew);
}
