using System;
using System.Collections.Generic;

namespace JobNexus.Core.Models;

public partial class Profile
{
    public string Username { get; set; } = null!;

    public string? Password { get; set; }

    public string? Email { get; set; }

    public string? Name { get; set; }

    public string? Bio { get; set; }

    public string? Location { get; set; }

    public virtual ICollection<Certification> Certifications { get; set; } = new List<Certification>();

    public virtual Resume? Resume { get; set; }

    public virtual ICollection<Job> Jobs { get; set; } = new List<Job>();

    public virtual ICollection<Skill> Skills { get; set; } = new List<Skill>();
}
