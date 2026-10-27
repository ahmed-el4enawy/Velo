namespace Velo.Domain;

/// <summary>
/// Download category types according to PRD v2.0 Section 3 FR-16.
/// </summary>
public enum CategoryType
{
    All = 0,
    Compressed = 1,
    Video = 2,
    Music = 3,
    Documents = 4,
    Programs = 5,
    Others = 6
}
