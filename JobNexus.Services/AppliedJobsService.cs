using System.Text.Json;
using JobNexus.Core.Models;
using Microsoft.JSInterop;

namespace JobNexus.Services
{
    public class AppliedJobsService
    {
        private readonly IJSRuntime _jsRuntime;
        private const string StorageKey = "appliedJobs";

        public AppliedJobsService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        public async Task<List<AppliedJobRecord>> GetAppliedJobsAsync()
        {
            var json = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", StorageKey);

            if (string.IsNullOrWhiteSpace(json))
                return new List<AppliedJobRecord>();

            return JsonSerializer.Deserialize<List<AppliedJobRecord>>(json) ?? new List<AppliedJobRecord>();
        }

        public async Task SaveAppliedJobsAsync(List<AppliedJobRecord> jobs)
        {
            var json = JsonSerializer.Serialize(jobs);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
        }

        public async Task AddAppliedJobAsync(AppliedJobRecord job)
        {
            var jobs = await GetAppliedJobsAsync();

            // optional: avoid duplicate entries for same job
            var alreadyExists = jobs.Any(x => x.JobId == job.JobId);
            if (!alreadyExists)
            {
                jobs.Insert(0, job);
                await SaveAppliedJobsAsync(jobs);
            }
        }
    }
}