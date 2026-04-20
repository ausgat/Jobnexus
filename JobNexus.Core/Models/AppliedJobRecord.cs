using System;

namespace JobNexus.Core.Models
{
    public class AppliedJobRecord
    {
        public int JobId { get; set; }
        public string Title { get; set; } = "";
        public string CompanyName { get; set; } = "";
        public DateTime DateApplied { get; set; }
    }
}