namespace GSO_Library.Models;

public class SeasonAnalytics
{
    public int SeasonId { get; set; }
    public int PageAccessCount { get; set; }
    public DateTime? PageLastAccessedAt { get; set; }
    public int FileDownloadCount { get; set; }
    public DateTime? FileLastDownloadedAt { get; set; }
}
