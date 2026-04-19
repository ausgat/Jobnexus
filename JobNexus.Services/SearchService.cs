using JobNexus.Core.Models;
using JobNexus.Core.Search;
using JobNexus.Data;
using LinqKit;
using Microsoft.EntityFrameworkCore;

namespace JobNexus.Services;

/// <summary>
/// Search service to inject where needed. Provides search by keyword and some other filtering methods relevant to the
/// Job model.
/// </summary>
/// <param name="contextFactory">DbContextFactory for accessing the database</param>
public class SearchService(IDbContextFactory<JobNexusContext> contextFactory)
{
    /// <summary>
    /// Perform an asynchronous search.
    /// </summary>
    /// <param name="searchQuery">SearchQuery object for keywords and filtering</param>
    /// <returns>A list of Job objects retrieved from the database</returns>
    public async Task<SearchResults> SearchAsync(SearchQuery searchQuery)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var keywordPredicate = PredicateBuilder.New<Job>();
        foreach (var k in searchQuery.Keywords)
        {
            keywordPredicate = keywordPredicate.Or(j =>
                j.Title != null && j.Title.ToLower().Contains(k.ToLower()));
            keywordPredicate = keywordPredicate.Or(j =>
                j.Description != null && j.Description.ToLower().Contains(k.ToLower()));
        }
        
        // Just add keywords for employment type since we don't have a specific property in the database
        List<string> employmentKeywords = [""];
        switch (searchQuery.EmploymentType)
        {
            case EmploymentType.FullTime:
                employmentKeywords = ["fulltime", "full-time", "full time"];
                break;
            case EmploymentType.PartTime:
                employmentKeywords = ["parttime", "part-time", "part time"];
                break;
            case EmploymentType.Internship:
                employmentKeywords = ["internship"];
                break;
            case EmploymentType.Temporary:
                employmentKeywords = ["temporary"];
                break;
        }
        var employmentPredicate = PredicateBuilder.New<Job>();
        foreach (var k in employmentKeywords)
        {
            employmentPredicate = employmentPredicate.Or(j =>
                j.Title != null && j.Title.ToLower().Contains(k.ToLower()));
            employmentPredicate = employmentPredicate.Or(j =>
                j.Description != null && j.Description.ToLower().Contains(k.ToLower()));
        }

        var baseQuery = context.Jobs.Include(j => j.Company).AsExpandable()
            .Where(keywordPredicate)
            .Where(employmentPredicate) // Require employment keywords to be found (AND)
            .Where(j => searchQuery.Company == null
                        || (j.Company != null && j.Company.CompanyName!.ToLower()
                            .Contains(searchQuery.Company.ToLower())))
            .Where(j => searchQuery.DatePostedAfter == null || j.DatePosted >= searchQuery.DatePostedAfter)
            .Where(j => searchQuery.MinPay == null || j.Pay >= searchQuery.MinPay)
            .Where(j => searchQuery.MaxPay == null || j.Pay <= searchQuery.MaxPay);
        var countQuery = await baseQuery.CountAsync();
        var paginatedQuery = await baseQuery
            .OrderByDescending(j => j.DatePosted)
            .Skip(Math.Max((searchQuery.PageNumber - 1) * searchQuery.ResultsPerPage, 0))
            .Take(searchQuery.ResultsPerPage)
            .ToListAsync();        

        var searchResults = new SearchResults(paginatedQuery, searchQuery.ResultsPerPage, countQuery);

        return searchResults;
    }
}