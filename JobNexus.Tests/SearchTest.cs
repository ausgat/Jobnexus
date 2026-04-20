using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JobNexus.Core.Models;
using JobNexus.Core.Search;
using JobNexus.Data;
using JobNexus.Services;
using JobNexus.Tests.Base;
using Xunit;

namespace JobNexus.Tests;

public class SearchTest : DbTestBase
{
    [Fact]
    public async Task Search_Keywords_ReturnsCorrectResults()
    {
        // 1. Create context using the Factory to ensure the SearchService looks at the exact same data
        await using var db = Factory.CreateDbContext();
        var search = new SearchService(Factory);

        // 2. Create example jobs
        var softwareEngineerJob = new Job
        {
            Title = "Software Engineer",
            Description = "Designs software and writes code",
            DatePosted = DateTime.UtcNow,
            Pay = 100000
        };
        var janitorJob = new Job
        {
            Title = "Janitor",
            Description = "Cleans up messes",
            DatePosted = DateTime.UtcNow,
            Pay = 40000
        };
        var doctorJob = new Job
        {
            Title = "Doctor",
            Description = "Helps you feel better",
            DatePosted = DateTime.UtcNow,
            Pay = 250000
        };
        
        // 3. Save the example jobs in the database
        db.Jobs.AddRange(softwareEngineerJob, janitorJob, doctorJob);
        await db.SaveChangesAsync(); // CRITICAL: Commit to DB before searching
        
        // 4. Search for all possible jobs (Empty Query)
        var results = await search.SearchAsync(new SearchQuery());
        Assert.Equal(3, results.Jobs.Count);

        // 5. Search for the "Software" keyword
        var keywordResults = await search.SearchAsync(new SearchQuery { Keywords = ["Software"] });
        
        // 6. Assert using IDs to avoid Entity Framework reference tracking mismatches
        Assert.Single(keywordResults.Jobs);
        Assert.Contains(keywordResults.Jobs, j => j.JobId == softwareEngineerJob.JobId);
        Assert.DoesNotContain(keywordResults.Jobs, j => j.JobId == janitorJob.JobId);
        Assert.DoesNotContain(keywordResults.Jobs, j => j.JobId == doctorJob.JobId);
    }
    
    [Fact]
    public async Task Search_Company_ReturnsCorrectResults()
    {
        // 1. Connect to the database and inject the search service using the same factory
        await using var db = Factory.CreateDbContext();
        var search = new SearchService(Factory);

        // 2. Create example jobs with data so they aren't null
        var job1 = new Job { Title = "iOS Developer", Description = "Builds iPhone apps" };
        var job2 = new Job { Title = "Windows System Admin", Description = "Manages servers" };

        // 3. Create the example companies with the example jobs listed
        var fruitOfTheLoomCompany = new Company
        {
            CompanyName = "Apple",
            Jobs = new List<Job> { job1 }
        };
        var mickyMouseCompany = new Company
        {
            CompanyName = "Microsoft",
            Jobs = new List<Job> { job2 }
        };
        
        db.Companies.AddRange(fruitOfTheLoomCompany, mickyMouseCompany);
        await db.SaveChangesAsync(); // CRITICAL: Commit to DB before searching

        // 4. Search for jobs belonging to Apple
        var appleResults = await search.SearchAsync(new SearchQuery { Company = "Apple" });
        
        // 5. Verify we got the right job back by ID
        Assert.NotEmpty(appleResults.Jobs);
        Assert.Contains(appleResults.Jobs, j => j.JobId == job1.JobId);
        Assert.DoesNotContain(appleResults.Jobs, j => j.JobId == job2.JobId);
        
        // 6. Search for jobs belonging to Microsoft
        var msResults = await search.SearchAsync(new SearchQuery { Company = "Microsoft" });
        
        // 7. Verify we got the right job back by ID
        Assert.NotEmpty(msResults.Jobs);
        Assert.Contains(msResults.Jobs, j => j.JobId == job2.JobId);
        Assert.DoesNotContain(msResults.Jobs, j => j.JobId == job1.JobId);
    }
}