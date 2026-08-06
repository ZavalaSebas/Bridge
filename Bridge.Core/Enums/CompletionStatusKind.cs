namespace Bridge.Core.Enums;

/// <summary>
/// Bridge's replacement for Playnite's CompletionStatusSettings singleton row
/// (PROJECT_FOUNDATION.md §28.12). Playnite stores which CompletionStatus id means
/// "default" and "played" as a separate settings row you can lose track of. Bridge
/// tags the CompletionStatus entity itself with its role instead — one less moving
/// part, same behavior (new games get the Default one; a game auto-flips to the
/// Played one the first time it's launched, per §28.10's UpdateGameState logic).
/// </summary>
public enum CompletionStatusKind
{
    None,
    Default,
    Played
}
