namespace JobNexus.Core.Search;

/// <summary>
/// A search query object for performing job listing searches.
/// </summary>
public class SearchQuery
{
    /// <summary>
    /// List of keywords to search in job listings.
    /// </summary>
    public List<string> Keywords { get; set; } = [];
    
    /// <summary>
    /// Maximum distance to search for jobs.
    /// </summary>
    public int? DistanceInMiles { get; set; }
    
    /// <summary>
    /// Earliest posting date to include jobs.
    /// </summary>
    public DateTime? DatePostedAfter { get; set; }
}
