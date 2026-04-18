using JobNexus.Core.Models;
using JobNexus.Core.Search;
using JobNexus.Data;
using JobNexus.Services;
using JobNexus.Tests.Base;
using Microsoft.Extensions.Logging.EventSource;
using Microsoft.Extensions.Options;

namespace JobNexus.Tests;

public class SearchTest : DbTestBase
{
    [Fact]
    public async Task Search_Keywords_ReturnsCorrectResults()
    {
        // Create the DB context for the database
        await using var db = CreateContext();
        
        // Inject the search service connected to the database with a factory
        var search = new SearchService(Factory);

        // Create example jobs
        var softwareEngineerJob = new Job
        {
            Title = "Software Engineer",
            Description = "Designs software and writes code",
            DatePosted = new DateTime(2025, 12, 1),
            Pay = 100000
        };
        var janitorJob = new Job
        {
            Title = "Janitor",
            Description = "Cleans up messes",
            DatePosted = new DateTime(2026, 1, 5),
            Pay = 40000
        };
        var doctorJob = new Job
        {
            Title = "Doctor",
            Description = "Helps you feel better",
            DatePosted = new DateTime(2026, 2, 20),
            Pay = 250000
        };
        
        // Save the example jobs in the database
        db.Jobs.AddRange([softwareEngineerJob, janitorJob, doctorJob]);
        await db.SaveChangesAsync();
        
        // Search for all possible jobs
        var results = await search.SearchAsync(new SearchQuery());

        // Get the IDs of the job listing results (comparing objects doesn't work; you must use IDs)
        var resultIds = results.Jobs.Select(j => j.JobId).ToList();

        // Make sure we're returning the right results
        Assert.Equal(3, resultIds.Count);
        Assert.Contains(softwareEngineerJob.JobId, resultIds);
        Assert.Contains(janitorJob.JobId, resultIds);
        Assert.Contains(doctorJob.JobId, resultIds);

        // Perform more tests with a search for the "software" keyword
        results = await search.SearchAsync(new SearchQuery{Keywords = ["software"]});
        resultIds = results.Jobs.Select(j => j.JobId).ToList();
        
        // Make sure we're only getting the software engineer job
        Assert.Single(resultIds);
        Assert.Contains(softwareEngineerJob.JobId, resultIds);
        Assert.DoesNotContain(janitorJob.JobId, resultIds);
        Assert.DoesNotContain(doctorJob.JobId, resultIds);
    }
    
    [Fact]
    public async Task Search_Company_ReturnsCorrectResults()
    {
        // Connect to the database and inject the search service
        await using var db = CreateContext();
        var search = new SearchService(Factory);

        // Create 2 example jobs
        var job1 = new Job();
        var job2 = new Job();

        // Create the example companies with the example jobs listed
        var fruitOfTheLoomCompany = new Company
        {
            CompanyName = "Apple",
            Jobs = [job1]
        };
        var mickyMouseCompany = new Company
        {
            CompanyName = "Microsoft",
            Jobs = [job2]
        };
        db.Companies.AddRange([fruitOfTheLoomCompany, mickyMouseCompany]);

        // Search for jobs belonging to Apple
        var results = await search.SearchAsync(new SearchQuery{Company = "Apple"});
        Assert.Contains(job1, results.Jobs);
        Assert.DoesNotContain(job2, results.Jobs);
        
        // Search for jobs belonging to Microsoft
        results = await search.SearchAsync(new SearchQuery{Company = "Microsoft"});
        Assert.Contains(job2, results.Jobs);
        Assert.DoesNotContain(job1, results.Jobs);
    }
}
