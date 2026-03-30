using System;
using System.Collections.Generic;

namespace JobNexus.Models;

public partial class Resume
{
    public string Username { get; set; } = null!;

    public string? JobExp { get; set; }

    public string? Education { get; set; }

    public string? Recommendations { get; set; }

    public string? Projects { get; set; }

    public virtual Profile UsernameNavigation { get; set; } = null!;
}
