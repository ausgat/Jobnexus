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
3. If you're running the database for the first time, or the schema has changed, run
   `dotnet ef database update --project JobNexus.Data --startup-project JobNexus.Web`
4. Open the `JobNexus.Web` project, and run it

Note: You can run a MySQL server with other methods as well, allowing you to skip steps 1 and 2. Make sure your
connection string in `JobNexus.Web/appsettings.Development.json` is correct, but don't commit any changes!
