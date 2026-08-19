namespace Bridge.Emulation;

public enum CheatFetchOutcome
{
    Success,
    PlatformNotSupported,
    NotFound,
    FetchFailed,
    Corrupted
}

public sealed class Cheat
{
    public int Index { get; init; }
    public string Description { get; init; } = string.Empty;
    public bool Enabled { get; init; }
}

public sealed class CheatsResult
{
    public CheatFetchOutcome Outcome { get; set; }
    public IReadOnlyList<Cheat> Cheats { get; set; } = [];
    public string? ErrorMessage { get; set; }
    public string? SourceFileUrl { get; set; }
}

public sealed record CheatParseResult(bool IsValid, IReadOnlyList<Cheat> Cheats);
