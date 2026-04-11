# JobNexus

## Structure

The JobNexus web app is structured as follows:

- 🌰 `JobNexus.Core`: Shared interfaces, models, and resources
- 💾 `JobNexus.Data`: Database functionality
- 🚚 `JobNexus.Services`: Backend, business logic, and services
- 🧪 `JobNexus.Tests`: Unit and integration tests
- 🕸️ `JobNexus.Web`: Blazor frontend that pulls everything together

## Running Locally

1. Download Docker Desktop: https://www.docker.com
2. Open a command line in this folder and run `docker compose up -d`
3. Make sure you have .NET 10
4. Install the EF Core command line tools: `dotnet tool install --global dotnet-ef`
5. If you're running the database for the first time, or the schema has changed, run
   `dotnet ef database update --project JobNexus.Data --startup-project JobNexus.Web`
6. Open the `JobNexus.Web` project, and run it

Note: You can run a MySQL server with other methods as well, allowing you to skip steps 1 and 2. Make sure your
connection string in `JobNexus.Web/appsettings.Development.json` is correct, but don't commit any changes!
