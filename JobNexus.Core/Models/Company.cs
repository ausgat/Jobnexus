using System;
using System.Collections.Generic;

namespace JobNexus.Core.Models;

public partial class Company
{
    public int CompanyId { get; set; }

    public string? CompanyName { get; set; }

    public string? WebsiteUrl { get; set; }

    public string? Industry { get; set; }

    public virtual ICollection<Job> Jobs { get; set; } = new List<Job>();
}
