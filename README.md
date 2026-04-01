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
3. Open the `JobNexus.Web` project, and run it

Note: You can run a MySQL server with other methods as well, allowing
you to skip steps 1 and 2. Just make sure you initialize the database
with `setup.sql` first.
