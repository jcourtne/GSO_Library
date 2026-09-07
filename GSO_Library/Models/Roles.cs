namespace GSO_Library.Models;

/// <summary>
/// Identity role names and the common <c>[Authorize(Roles = ...)]</c> combinations.
/// Kept as <c>const</c> so they can be used in attributes and stay in sync with role
/// seeding (<see cref="All"/>) and runtime <c>User.IsInRole(...)</c> checks.
/// </summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string Librarian = "Librarian";
    public const string Submitter = "Submitter";
    public const string Downloader = "Downloader";
    public const string User = "User";
    public const string EnsembleDownloader = "Ensemble Downloader";
    public const string EnsembleLibrarian = "Ensemble Librarian";

    /// <summary>Every role name — used to seed the role table.</summary>
    public static readonly string[] All =
    [
        Admin, Librarian, Submitter, Downloader, User, EnsembleDownloader, EnsembleLibrarian
    ];

    /// <summary>Admin + Librarian: full write access to a resource.</summary>
    public const string Editors = $"{Admin},{Librarian}";

    /// <summary>
    /// Editors plus Ensemble Librarians. Used for shared reference data (games, series,
    /// instruments) and for ensemble-scoped resources (seasons, performances) where the
    /// controller additionally checks the Ensemble Librarian's ensemble membership.
    /// </summary>
    public const string EditorsAndEnsembleLibrarian = $"{Admin},{Librarian},{EnsembleLibrarian}";

    /// <summary>Everyone who can create or edit an arrangement.</summary>
    public const string ArrangementEditors = $"{Admin},{Librarian},{Submitter},{EnsembleLibrarian}";
}
