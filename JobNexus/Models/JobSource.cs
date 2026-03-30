using System;
using System.Collections.Generic;

namespace JobNexus.Models;

public partial class JobSource
{
    public int SourceId { get; set; }

    public string? SourceName { get; set; }

    public virtual ICollection<Job> Jobs { get; set; } = new List<Job>();
}
