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
    /// <param name="query">SearchQuery object for keywords and filtering</param>
    /// <returns>A list of Job objects retrieved from the database</returns>
    public async Task<List<Job>> SearchAsync(SearchQuery query)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var predicate = PredicateBuilder.New<Job>();
        foreach (var k in query.Keywords)
        {
            predicate = predicate.Or(j =>
                j.Title != null && j.Title.ToLower().Contains(k.ToLower()));
            predicate = predicate.Or(j =>
                j.Description != null && j.Description.ToLower().Contains(k.ToLower()));
        }
        
        var results = context.Jobs.AsExpandable()
            .Where(predicate)
            .Where(j => query.DatePostedAfter == null || j.DatePosted >= query.DatePostedAfter)
            .OrderByDescending(j => j.DatePosted)
            .ToList();
        return results;
    }
}