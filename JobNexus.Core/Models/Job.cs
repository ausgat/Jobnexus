using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace JobNexus.Core.Models;

public partial class Job
{
    public int JobId { get; set; }

    public string? Title { get; set; }

    public string? Description { get; set; }

    public string? ApplyUrl { get; set; }

    public int? Pay { get; set; }

    public DateTime? DatePosted { get; set; }

    public int? SourceId { get; set; }

    public int? CompanyId { get; set; }

    public virtual Company? Company { get; set; }

    public virtual JobSource? Source { get; set; }

    public virtual ICollection<Skill> Skills { get; set; } = new List<Skill>();

    public virtual ICollection<Profile> Usernames { get; set; } = new List<Profile>();
}
