using System;
using System.Collections.Generic;

namespace JobNexus.Core.Models;

public partial class ProfileForm
{
    public string Username { get; set; } = null!;

    public string? Password { get; set; }

    public string? Email { get; set; }

    public string? Name { get; set; }

    public string? Bio { get; set; }

    public string? Location { get; set; }
}
