using JobNexus.Core.Models;
using JobNexus.Web.Components;
using JobNexus.Data;
using Microsoft.EntityFrameworkCore;
using JobNexus.Services; //new thing
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMudServices();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Configure database connection
var configString = builder.Configuration.GetConnectionString("JobNexusDatabase")
    ?? throw new InvalidOperationException("ConnectionString \"JobNexusDatabase\" does not exist.");
builder.Services.AddDbContextFactory<JobNexusContext>(options =>
    options.UseMySql(configString, serverVersion: ServerVersion.AutoDetect(configString)));

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
    app.UseMigrationsEndPoint();
}

app.UseHttpsRedirection();
app.UseStatusCodePagesWithReExecute("/notfound", createScopeForStatusCodePages: true);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
