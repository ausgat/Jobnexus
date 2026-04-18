using System.Text.Json.Serialization;
 
namespace JobNexus.Core.Models;
 
// -----------------------------------------------------------------------
// This file defines C# classes that mirror the Adzuna API JSON response.
// These are NOT database models — they are just used to deserialize the
// raw API response before it gets normalized into our actual DB models.
//
// Reference: https://developer.adzuna.com/docs/search
// Example Adzuna response fields used here:
//   id, title, description, redirect_url, created,
//   salary_min, salary_max, company.display_name, category.label
// -----------------------------------------------------------------------
 
/// <summary>
/// Represents the top-level response object from the Adzuna search endpoint.
/// The "results" array contains the individual job listings.
/// </summary>
public class AdzunaResponse
{
    // Total number of results available (not just this page)
    [JsonPropertyName("count")]
    public int Count { get; set; }
 
    // The array of job listings returned for this page
    [JsonPropertyName("results")]
    public List<AdzunaJob> Results { get; set; } = new();
}
 
/// <summary>
/// Represents a single job listing from the Adzuna API response.
/// Field names match Adzuna's JSON property names exactly via JsonPropertyName.
/// </summary>
public class AdzunaJob
{
    // Adzuna's internal unique ID for this job listing
    [JsonPropertyName("id")]
    public string? Id { get; set; }
 
    // Job title e.g. "Software Engineer"
    [JsonPropertyName("title")]
    public string? Title { get; set; }
 
    // Snippet of the job description (Adzuna only returns a partial description)
    [JsonPropertyName("description")]
    public string? Description { get; set; }
 
    // URL to redirect the user to the full job listing on Adzuna
    [JsonPropertyName("redirect_url")]
    public string? RedirectUrl { get; set; }
 
    // Date/time the job was posted in ISO 8601 format e.g. "2013-11-08T18:07:39Z"
    [JsonPropertyName("created")]
    public DateTime? Created { get; set; }
 
    // Minimum salary for the role (may be predicted if salary_is_predicted == 1)
    [JsonPropertyName("salary_min")]
    public double? SalaryMin { get; set; }
 
    // Maximum salary for the role
    [JsonPropertyName("salary_max")]
    public double? SalaryMax { get; set; }
 
    // Whether the salary was predicted by Adzuna (1) or explicitly listed (0)
    // TODO: Consider filtering out predicted salaries if accuracy is important
    [JsonPropertyName("salary_is_predicted")]
    public int? SalaryIsPredicted { get; set; }
 
    // Nested company object — only display_name is provided by Adzuna
    [JsonPropertyName("company")]
    public AdzunaCompany? Company { get; set; }
 
    // Nested category object — maps to the job's industry/category
    [JsonPropertyName("category")]
    public AdzunaCategory? Category { get; set; }
 
    // Nested location object — contains the display name and area breakdown
    [JsonPropertyName("location")]
    public AdzunaLocation? Location { get; set; }
 
    // Full time or part time e.g. "full_time", "part_time"
    [JsonPropertyName("contract_time")]
    public string? ContractTime { get; set; }
 
    // Permanent or contract e.g. "permanent", "contract"
    [JsonPropertyName("contract_type")]
    public string? ContractType { get; set; }
}
 
/// <summary>
/// Nested company object from Adzuna.
/// Note: Adzuna only provides the company name, not a website or industry.
/// Those fields will need to be left null or filled in separately.
/// </summary>
public class AdzunaCompany
{
    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }
}
 
/// <summary>
/// Nested category object from Adzuna.
/// The label maps to our Company.Industry field.
/// </summary>
public class AdzunaCategory
{
    // Human readable label e.g. "IT Jobs", "Accounting & Finance Jobs"
    [JsonPropertyName("label")]
    public string? Label { get; set; }
 
    // URL-friendly tag e.g. "it-jobs"
    [JsonPropertyName("tag")]
    public string? Tag { get; set; }
}
 
/// <summary>
/// Nested location object from Adzuna.
/// </summary>
public class AdzunaLocation
{
    // Human readable location e.g. "London, Greater London"
    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }
 
    // Hierarchical area breakdown e.g. ["UK", "London", "Central London"]
    [JsonPropertyName("area")]
    public List<string> Area { get; set; } = new();
}