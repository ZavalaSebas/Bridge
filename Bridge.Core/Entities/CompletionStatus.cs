using Bridge.Core.Enums;

namespace Bridge.Core.Entities;

/// <summary>
/// Matches Playnite's CompletionStatus (just Id+Name) but folds in the role that
/// Playnite tracks separately in a CompletionStatusSettings singleton row
/// (PROJECT_FOUNDATION.md §28.12) — Kind marks which status is the "new game"
/// default and which one auto-applies the first time a game is launched
/// (§28.10's UpdateGameState logic). At most one status should have each Kind.
/// </summary>
public class CompletionStatus : DatabaseObject
{
    public CompletionStatusKind Kind { get; set; } = CompletionStatusKind.None;
}
