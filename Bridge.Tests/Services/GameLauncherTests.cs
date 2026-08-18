using Bridge.Core.Contracts;
using Bridge.Core.Entities;
using Bridge.Services;

namespace Bridge.Tests.Services;

public class GameLauncherTests
{
    [Fact]
    public void Launch_without_play_action_throws()
    {
        var launcher = new GameLauncher(new EmptyEmulatorRepository());
        var game = new Game { Name = "No Action Game" };

        Assert.Throws<InvalidOperationException>(() => launcher.Launch(game));
    }

    [Fact]
    public void Launch_while_already_running_is_noop()
    {
        var launcher = new GameLauncher(new EmptyEmulatorRepository());
        var game = new Game { Name = "Running", IsRunning = true };

        var ex = Record.Exception(() => launcher.Launch(game));

        Assert.Null(ex);
    }

    [Fact]
    public void Stop_when_not_tracking_is_noop()
    {
        var launcher = new GameLauncher(new EmptyEmulatorRepository());
        var game = new Game { Name = "Idle" };

        var ex = Record.Exception(() => launcher.Stop(game));

        Assert.Null(ex);
    }

    private sealed class EmptyEmulatorRepository : IRepository<Emulator>
    {
        public Emulator? Get(Guid id) => null;
        public IReadOnlyList<Emulator> GetAll() => [];
        public void Add(Emulator entity) { }
        public void Update(Emulator entity) { }
        public bool Remove(Guid id) => false;
        public Emulator GetOrCreateByName(string name) => throw new NotSupportedException();
    }
}
