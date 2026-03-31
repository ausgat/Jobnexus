using System;
using System.Collections.Generic;

namespace JobNexus.Core.Models;

public partial class Certification
{
    public int CertId { get; set; }

    public string? CertName { get; set; }

    public DateTime? DateGiven { get; set; }

    public string? Username { get; set; }

    public virtual Profile? UsernameNavigation { get; set; }
}
