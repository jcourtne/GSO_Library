namespace GSO_Library.Models;

public static class ScorePartType
{
    public const string ConductorScore = "conductor_score";
    public const string InstrumentPart = "instrument_part";
    public const string PercussionPart = "percussion_part";
    public const string UnlistedPart   = "unlisted_part";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        ConductorScore, InstrumentPart, PercussionPart, UnlistedPart
    };
}
