using JobNexus.Core.Models;

namespace JobNexus.Core.Search;

/// <summary>
/// Search results and metadata returned after making a query to SearchService.
/// </summary>
/// <param name="jobs">Jobs returned from the search.</param>
/// <param name="resultsPerPage">Number of results per page for the search.</param>
public class SearchResults(List<Job> jobs, int resultsPerPage, int totalCount)
{
    /// <summary>
    /// List of jobs matching search criteria.
    /// </summary>
    public readonly List<Job> Jobs = jobs;
    
    /// <summary>
    /// Number of results for the given page.
    /// </summary>
    public int Count => Jobs.Count;
    
    /// <summary>
    /// Total number of results matching search.
    /// </summary>
    public readonly int TotalCount = totalCount;
    
    /// <summary>
    /// Total number of pages containing search results.
    /// </summary>
    public int PageCount => (int)Math.Ceiling((double)Jobs.Count / resultsPerPage);
}