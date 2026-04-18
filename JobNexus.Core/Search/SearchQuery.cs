using JobNexus.Core.Models;

namespace JobNexus.Core.Search;

public enum EmploymentType
{
    FullTime,
    PartTime,
    Internship,
    Temporary
}

/// <summary>
/// A search query object for performing job listing searches.
/// </summary>
public class SearchQuery
{
    /// <summary>
    /// Number of results to retrieve per page. Defaults to 10.
    /// </summary>
    public int ResultsPerPage { get; set; } = 10;

    /// <summary>
    /// Which page of results to retrieve, starting at 1.
    /// </summary>
    public int PageNumber { get; set; } = 1;

    /// <summary>
    /// List of keywords to search in job listings.
    /// </summary>
    public List<string> Keywords { get; set; } = [];

    /// <summary>
    /// Earliest posting date to include jobs.
    /// </summary>
    public DateTime? DatePostedAfter { get; set; }

    /// <summary>
    /// Minimum amount of pay.
    /// </summary>
    public int? MinPay;
    
    /// <summary>
    /// Maximum amount of pay.
    /// </summary>
    public int? MaxPay;
    
    /// <summary>
    /// Name of company that posted the listing.
    /// </summary>
    public string? Company { get; set; }
    
    /// <summary>
    /// Type of employment (e.g. full-time, internship, etc.)
    /// </summary>
    public EmploymentType? EmploymentType { get; set; }
}
