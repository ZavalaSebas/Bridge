namespace Bridge.Core.Entities;

// Trivial normalized reference entities — Id + Name, nothing else, matching
// Playnite's real shape for each (PROJECT_FOUNDATION.md §28.1). Grouped in one
// file because none of them carries any behavior or extra fields; splitting them
// into six near-empty files would be pure ceremony.

public class Genre : DatabaseObject
{
}

public class Category : DatabaseObject
{
}

public class Tag : DatabaseObject
{
}

public class Series : DatabaseObject
{
}

public class AgeRating : DatabaseObject
{
}

public class GameFeature : DatabaseObject
{
}
