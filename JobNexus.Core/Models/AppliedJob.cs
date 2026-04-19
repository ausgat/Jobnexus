namespace JobNexus.Core.Models
{
    public class AppliedJob
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public int JobId { get; set; }
        public DateTime ApplicationDate { get; set; }
    }
}