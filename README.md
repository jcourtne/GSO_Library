# GSO Library

A web application for managing musical arrangements from video games. It provides a catalog of arrangements along with their associated games, series, instruments, performances, ensembles, and file uploads.

## Architecture

- **Backend:** ASP.NET Core 10.0 Web API with JWT authentication and role-based authorization (Admin, Editor, User)
- **Frontend:** React 19 + TypeScript, built with Vite, using React Bootstrap for UI
- **Database:** PostgreSQL -- application tables managed with raw SQL + Dapper, ASP.NET Identity tables managed with Entity Framework Core

### API Resources

| Resource | Description |
|---|---|
| Arrangements | Musical arrangements with metadata (composer, arranger, key, duration, year) and file attachments |
| Games | Video games, each belonging to a series |
| Series | Game franchises/series that group games |
| Instruments | Instruments used in arrangements |
| Performances | Performance events with links, dates, and optional ensemble association |
| Ensembles | Musical groups that perform arrangements |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [PostgreSQL](https://www.postgresql.org/download/) (14+)
- [Node.js](https://nodejs.org/) (20+) and npm

## Getting Started

### 1. Set Up the Database

Connect to PostgreSQL as a superuser and run the setup script to create the database and application role:

```sql
-- GSO_Library/Sql/setup_database.sql
CREATE ROLE gso_app WITH LOGIN PASSWORD 'gso_app_password';
CREATE DATABASE gso_library OWNER gso_app;
```

### 2. Run EF Core Migrations (Identity Tables)

From the repository root, apply the Entity Framework migrations to create the ASP.NET Identity tables:

```bash
dotnet ef database update --project GSO_Library
```

### 3. Run Application SQL Scripts

Connect to the `gso_library` database and run the SQL scripts in order to create the application tables:

```
GSO_Library/Sql/001_create_application_tables.sql
GSO_Library/Sql/002_add_audit_fields.sql
GSO_Library/Sql/003_create_audit_events.sql
GSO_Library/Sql/004_create_ensembles.sql
```

### 4. Configure Seed Users (Optional)

The API can seed initial users on startup from a `seed_users.json` file. This file is gitignored since it contains passwords. Create it at `GSO_Library/seed_users.json`:

```json
[
  {
    "username": "admin",
    "email": "admin@gsolibrary.com",
    "password": "Admin123!",
    "firstName": "Admin",
    "lastName": "User",
    "roles": ["Admin"]
  }
]
```

Valid roles are `Admin`, `Editor`, and `User`.

### 5. Start the API

```bash
dotnet run --project GSO_Library
```

The API will be available at `https://localhost:5001` (or the port configured by your launch profile). The OpenAPI document is served at `/openapi/v1.json` in development mode.

### 6. Start the Frontend

```bash
cd GSO_Library.Web
npm install
npm run dev
```

The frontend dev server starts at `http://localhost:5173` by default.

## Configuration

Key settings in `GSO_Library/appsettings.json`:

| Setting | Description |
|---|---|
| `ConnectionStrings:DefaultConnection` | PostgreSQL connection string |
| `Jwt:SecretKey` | Signing key for JWT tokens (min 32 characters) |
| `Jwt:Issuer` / `Jwt:Audience` | JWT token issuer and audience |
| `FileStorage:BasePath` | Directory for uploaded arrangement files (default: `./uploads`) |
| `FileUpload:AllowedExtensions` | Permitted file types (.pdf, .xml, .mxl, .mid, .midi, .mp3, .wav, .flac, .ogg, .mscz, .dorico, .sib) |
| `FileUpload:MaxFileSizeBytes` | Max upload size (default: 50 MB) |
| `SeedUsersFile` | Path to the seed users JSON file |

## Running Tests

The test project uses xUnit with an in-memory SQLite database, so no external database is needed:

```bash
dotnet test GSO_Library.Tests
```
