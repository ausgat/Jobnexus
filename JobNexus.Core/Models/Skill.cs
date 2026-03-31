using System;
using System.Collections.Generic;

namespace JobNexus.Core.Models;

public partial class Skill
{
    public int SkillId { get; set; }

    public string? SkillName { get; set; }

    public string? Category { get; set; }

    public virtual ICollection<Job> Jobs { get; set; } = new List<Job>();

    public virtual ICollection<Profile> Usernames { get; set; } = new List<Profile>();
}
