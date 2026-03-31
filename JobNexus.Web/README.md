# JobNexus.Web

This is the frontend portion of JobNexus.

## Development and Testing

To run the web app, open a command line in the `JobNexus.Web/` folder and run
`dotnet watch`. This should build the project and run it. Open a browser and go
to http://localhost:5103.

## Project Structure

Project files in JobNexus.Web:

- 📄 `Program.cs`: Entry point that sets up the ASP.NET Core web application
host and contains the app's startup logic
- ⚙️ `appsettings.json`: App settings file for production
- ️️⚙️ `appsettings.Development.json`: App settings file for development
- 📁 `Components/`: Modular Razor components for frontend
  - 📁 `Layout/`: Layout components and stylesheets
    - 📄 `MainLayout.razor`: App's layout component
    - 📄 `NavMenu.razor`: Navigation component
  - 📁 `Pages/`: Routable Razor components (basically the app's pages)
- 📁 `Properties/launchSettings.json`: Development environment configuration
- 📁 `wwwroot/`: Public static assets
- 📄 `_Imports.razor`: Common Razor directives to include in the app's
components
- 📄 `App.razor`: Root component that sets up client-side routing using the
Router component
- 📄 `Routes.razor`: Sets up routing using the Router component
