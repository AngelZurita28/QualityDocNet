# Repository Guidelines

## Project Structure & Module Organization
This repository contains a single ASP.NET Core web app in `QualityDoc/` and root-level Docker setup files. `QualityDoc.slnx` is the solution entry point. Application startup is in `QualityDoc/Program.cs`, EF Core configuration is in `QualityDoc/Data/`, controllers live in `QualityDoc/Controllers/`, reusable utilities in `QualityDoc/Helpers/`, and Razor Pages in `QualityDoc/Pages/`. Page models and domain models currently sit under `QualityDoc/Pages/Models/`. Static assets are in `QualityDoc/wwwroot/`, with uploaded files mounted to `QualityDoc/wwwroot/uploads/` through `uploads_compartidos/`. SQL setup scripts are in `QualityDoc/Database/`.

## Build, Test, and Development Commands
- `./setup.sh` or `.\setup.ps1`: first-time environment setup; prompts for local database credentials and starts Docker.
- `docker compose up -d`: runs SQL Server and the web app at `http://localhost:5000` with `dotnet watch`.
- `docker compose down`: stops containers while preserving the SQL volume.
- `dotnet build QualityDoc.slnx`: builds the .NET solution locally when the .NET 10 SDK is installed.
- `cd QualityDoc && npm install`: installs Tailwind/PostCSS dependencies used by the app.

The current `npm test` script is a placeholder and exits with an error.

## Coding Style & Naming Conventions
Use C# nullable reference types and implicit usings as configured in `QualityDoc.csproj`. Follow standard ASP.NET Core naming: PascalCase for classes, Razor Page models, controllers, and public members; camelCase for locals and parameters. Keep Razor page pairs together, for example `Pages/Login.cshtml` and `Pages/Login.cshtml.cs`. Use 4-space indentation in C# and Razor files. Prefer existing helper and data-access patterns before adding new abstractions.

## Testing Guidelines
No automated test project is currently present. For backend changes, add focused tests in a future `QualityDoc.Tests/` project using a conventional `*Tests.cs` naming pattern. Until then, verify with `dotnet build QualityDoc.slnx` and a Docker run of the affected workflow. For UI or authentication changes, include manual verification steps in the PR.

## Commit & Pull Request Guidelines
Recent commits use short, lowercase, imperative summaries such as `db update` and `linux script`. Keep commit messages concise and focused on one change. Pull requests should include a brief description, testing performed, database or configuration changes, linked issues when applicable, and screenshots for visible UI changes.

## Security & Configuration Tips
Do not commit real credentials, connection strings, or Google OAuth secrets. Use environment variables and the setup scripts for local database passwords. Treat `appsettings.json` as non-secret configuration only.
