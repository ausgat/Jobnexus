using JobNexus.Core.Models;
using JobNexus.Web.Components;
using JobNexus.Data;
using Microsoft.EntityFrameworkCore;
using JobNexus.Services;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add Razor pages
builder.Services.AddRazorPages();

builder.Services.AddMudServices();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Configure database connection
var configString = builder.Configuration.GetConnectionString("JobNexusDatabase")
    ?? throw new InvalidOperationException("ConnectionString \"JobNexusDatabase\" does not exist.");
builder.Services.AddDbContextFactory<JobNexusContext>(options =>
    options.UseMySql(configString, serverVersion: ServerVersion.AutoDetect(configString)));
builder.Services.AddScoped<JobNexusContext>(p => 
    p.GetRequiredService<IDbContextFactory<JobNexusContext>>().CreateDbContext());

// Needed for identity service
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services
    .AddDefaultIdentity<Profile>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddEntityFrameworkStores<JobNexusContext>();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
    options.LogoutPath = "/logout";
    options.AccessDeniedPath = "/accessdenied";
});

builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddHostedService<JobSyncService>();
builder.Services.AddScoped<SearchService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseMigrationsEndPoint();
app.UseHttpsRedirection();
app.UseStatusCodePagesWithReExecute("/notfound", createScopeForStatusCodePages: true);

app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorPages();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
