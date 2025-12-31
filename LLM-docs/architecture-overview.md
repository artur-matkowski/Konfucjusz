# Architecture Overview

## High-Level
- Blazor Server app (.NET 8) with SignalR for audio streaming.
- EF Core with PostgreSQL as the database.
- SMTP email sending via `EmailRequest` using MailKit and options binding (`SmtpOptions`).
- Docker-based deployment: `app` + `db` (+ `mailhog` for dev-only).

## Key Components
- **Program.cs**: configures services, authentication, DbContext, and options.
- **Schema/**: entity models and `ApplicationDbContext`.
- **Services/EmailRequest.cs**: SMTP client wrapper using `SmtpOptions` from configuration.
- **Hubs/AudioStreamHub.cs**: SignalR hub for audio streaming.

## Configuration
- Default config via `appsettings.json` / `appsettings.Development.json`.
- Overridden via environment variables in Docker (e.g. `ConnectionStrings__MyConnection`, `SlugSecret`, `Smtp__*`).

## Security Considerations
- See `SECURITY-NOTES.md` for detailed findings and desired changes prior to Internet-facing deployment.
