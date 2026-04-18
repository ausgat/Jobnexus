using JobNexus.Core.Models;
using JobNexus.Core.Search;
using JobNexus.Data;
using LinqKit;
using Microsoft.EntityFrameworkCore;
 
namespace JobNexus.Services;
 
/// <summary>
/// Search service to inject where needed. Provides search by keyword and
/// filtering relevant to the Job model.
///
/// Uses LinqKit's PredicateBuilder to build dynamic OR queries across
/// multiple keyword terms and job fields.
/// </summary>
public class SearchService(IDbContextFactory<JobNexusContext> contextFactory)
{
    /// <summary>
    /// Perform an asynchronous search against the job listings database.
    /// </summary>
    /// <param name="query">SearchQuery object containing keywords and filters</param>
    /// <returns>A list of Job objects retrieved from the database</returns>
    public async Task<List<Job>> SearchAsync(SearchQuery query)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
 
        // Build a dynamic OR predicate across all provided keywords
        // LinqKit's PredicateBuilder lets us chain OR conditions at runtime
        var predicate = PredicateBuilder.New<Job>();
        foreach (var k in query.Keywords)
        {
            predicate = predicate.Or(j =>
                j.Title != null && j.Title.ToLower().Contains(k.ToLower()));
            predicate = predicate.Or(j =>
                j.Description != null && j.Description.ToLower().Contains(k.ToLower()));
        }
 
        var results = await context.Jobs
            .AsExpandable()                         // Required for LinqKit predicate building
            .Include(j => j.Company)                // Load Company so CompanyName is available in the grid
            .Include(j => j.Source)                 // Load Source so SourceName badge works in the grid
            .Where(predicate)
            .Where(j => query.DatePostedAfter == null || j.DatePosted >= query.DatePostedAfter)
            .Where(j => query.Company == null || (j.Company != null && j.Company.CompanyName == query.Company))
            .OrderByDescending(j => j.DatePosted)
            .ToListAsync();
 
        return results;
    }
}