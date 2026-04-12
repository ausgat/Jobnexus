using JobNexus.Core.Models;

namespace JobNexus.Core.Search;

/// <summary>
/// A search query object for performing job listing searches.
/// </summary>
public class SearchQuery
{
    /// <summary>
    /// List of keywords to search in job listings.
    /// </summary>
    public List<string> Keywords { get; init; } = [""];

    /// <summary>
    /// Earliest posting date to include jobs.
    /// </summary>
    public DateTime? DatePostedAfter { get; set; }
    
    public string? Company { get; set; }
}
