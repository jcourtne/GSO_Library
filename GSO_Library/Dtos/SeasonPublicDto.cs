namespace GSO_Library.Dtos;

public class ShareConfigRequest
{
    public bool IncludePdf { get; set; } = true;
    public bool IncludeNotation { get; set; }
    public bool IncludePlayback { get; set; }
    public string? Password { get; set; }
    public bool ClearPassword { get; set; }
}

public class SeasonPublicDto
{
    public string Name { get; set; } = "";
    public string? EnsembleName { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public bool RequiresPassword { get; set; }
    public List<ArrangementSummaryDto> Arrangements { get; set; } = [];
    public List<DownloadSectionDto> DownloadSections { get; set; } = [];
}

public class ArrangementSummaryDto
{
    public string Name { get; set; } = "";
    public List<string> Composers { get; set; } = [];
    public List<string> Arrangers { get; set; } = [];
}

public class DownloadSectionDto
{
    public string Label { get; set; } = "";
    public string? ScorePartType { get; set; }
    public int? InstrumentId { get; set; }
    public int? FamilyId { get; set; }
    public string? FamilyName { get; set; }
    public DateTime? LastUpdated { get; set; }
    public int FileCount { get; set; }
}
