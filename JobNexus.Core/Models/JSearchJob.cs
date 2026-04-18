using System.Text.Json.Serialization;

namespace JobNexus.Core.Models;

// -----------------------------------------------------------------------
// This file defines C# classes that mirror the JSearch API JSON response.
// Like AdzunaJob.cs, these are NOT database models — they are only used
// to deserialize the raw API response before normalization.
//
// JSearch is accessed via RapidAPI and has a HARD LIMIT of 200 requests
// per month on the free tier. The rate limiter in JSearchNormalizer
// tracks usage to avoid exceeding this cap.
//
// Reference: https://rapidapi.com/letscrape-6bRBa3QguO5/api/jsearch
// -----------------------------------------------------------------------

/// <summary>
/// Top-level response object from the JSearch API.
/// The "data" array contains the individual job listings.
/// </summary>
public class JSearchResponse
{
    // "OK" on success, error message otherwise
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    // Unique ID for this request — useful for debugging
    [JsonPropertyName("request_id")]
    public string? RequestId { get; set; }

    // The array of job listings returned
    [JsonPropertyName("data")]
    public List<JSearchJob> Data { get; set; } = new();
}

/// <summary>
/// Represents a single job listing from the JSearch API response.
/// JSearch provides 30+ data points per job — we map the ones that
/// correspond to our existing Job, Company, and JobSource models.
/// </summary>
public class JSearchJob
{
    // JSearch's unique identifier for this job listing — used for deduplication
    [JsonPropertyName("job_id")]
    public string? JobId { get; set; }

    // Job title e.g. "Software Engineer"
    [JsonPropertyName("job_title")]
    public string? JobTitle { get; set; }

    // Full job description (JSearch provides the complete description unlike Adzuna)
    [JsonPropertyName("job_description")]
    public string? JobDescription { get; set; }

    // Direct link to apply for the job
    [JsonPropertyName("job_apply_link")]
    public string? JobApplyLink { get; set; }

    // ISO 8601 datetime string e.g. "2024-07-05T12:36:32.000Z"
    [JsonPropertyName("job_posted_at_datetime_utc")]
    public DateTime? JobPostedAtDatetimeUtc { get; set; }

    // Minimum salary — may be null if not listed
    [JsonPropertyName("job_min_salary")]
    public double? JobMinSalary { get; set; }

    // Maximum salary — may be null if not listed
    [JsonPropertyName("job_max_salary")]
    public double? JobMaxSalary { get; set; }

    // Salary period e.g. "YEAR", "MONTH", "HOUR"
    // TODO: Consider normalizing hourly/monthly rates to annual for consistent Pay values
    [JsonPropertyName("job_salary_period")]
    public string? JobSalaryPeriod { get; set; }

    // Name of the employer/company
    [JsonPropertyName("employer_name")]
    public string? EmployerName { get; set; }

    // Company website URL — JSearch provides this, Adzuna does not
    [JsonPropertyName("employer_website")]
    public string? EmployerWebsite { get; set; }

    // City where the job is located
    [JsonPropertyName("job_city")]
    public string? JobCity { get; set; }

    // State/province
    [JsonPropertyName("job_state")]
    public string? JobState { get; set; }

    // Country code e.g. "US"
    [JsonPropertyName("job_country")]
    public string? JobCountry { get; set; }

    // Whether the job is remote
    [JsonPropertyName("job_is_remote")]
    public bool? JobIsRemote { get; set; }

    // The source site this job came from e.g. "linkedin.com", "indeed.com"
    // NOTE: This is the original job board, not JSearch itself
    [JsonPropertyName("job_publisher")]
    public string? JobPublisher { get; set; }
}   