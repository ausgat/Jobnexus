namespace JobNexus.Web.ViewModels;

public partial class ProfileModel
{
    public string Username { get; set; } = null!;

    public string? Email { get; set; }

    public string? Name { get; set; }

    public string? Bio { get; set; }

    public string? Location { get; set; }
}
