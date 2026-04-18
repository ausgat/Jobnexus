using JobNexus.Core.Models;
using JobNexus.Web.Components;
using JobNexus.Data;
using Microsoft.EntityFrameworkCore;
using JobNexus.Services;
using Microsoft.AspNetCore.Identity;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add Razor Pages (required for Identity)
builder.Services.AddRazorPages();

// Add MudBlazor UI components
builder.Services.AddMudServices();

// Add Razor components with interactive server rendering
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Configure MySQL database connection
var configString = builder.Configuration.GetConnectionString("JobNexusDatabase")
    ?? throw new InvalidOperationException("ConnectionString \"JobNexusDatabase\" does not exist.");
builder.Services.AddDbContextFactory<JobNexusContext>(options =>
    options.UseMySql(configString, serverVersion: ServerVersion.AutoDetect(configString)));

// ASP.NET Core Identity — uses Profile as the user model
builder.Services.AddDefaultIdentity<Profile>(options =>
    options.SignIn.RequireConfirmedAccount = false)
    .AddEntityFrameworkStores<JobNexusContext>();

// Configure Identity cookie paths
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
    options.LogoutPath = "/logout";
    options.AccessDeniedPath = "/accessdenied";
});

// Application services
builder.Services.AddScoped<CurrentUserService>();   // Gets the currently logged-in user
builder.Services.AddScoped<SearchService>();         // Handles job search queries via LinqKit

builder.Services.AddQuickGridEntityFrameworkAdapter();
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Background sync service — fetches jobs from Adzuna and JSearch on a schedule
builder.Services.AddHostedService<JobSyncService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseMigrationsEndPoint();
app.UseHttpsRedirection();
app.UseStaticFiles();

// Auth middleware — must be in this order
app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorPages();   // Required for Identity Razor Pages
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
