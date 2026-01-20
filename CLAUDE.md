# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Konfucjusz is a .NET 8 Blazor Server application for event management with real-time audio streaming. It uses PostgreSQL, SignalR for live audio, and MailKit for email verification workflows.

## Build & Run Commands

```bash
# Build
dotnet build

# Run (development)
dotnet run

# Database migrations
dotnet ef migrations add MigrationName    # Create migration
dotnet ef database update                 # Apply migrations (also auto-applied at startup)

# Docker (PostgreSQL + MailHog)
docker compose up -d                      # Start services
docker compose down -v                    # Stop and reset database
```

## Architecture

**Layered Structure:**
- `Components/Pages/` — Blazor Razor pages organized by feature (Authentication/, Events/, Admin/, Organizing/, Participating/)
- `Components/Layout/` — MainLayout, NavMenu, sidebar
- `Services/` — Business logic (UserService, EventService, ParticipantService, EmailRequest)
- `Schema/` — EF Core models and ApplicationDbContext
- `Hubs/AudioStreamHub.cs` — SignalR hub for real-time audio streaming with recording

**Key Entry Points:**
- `Program.cs` — App bootstrap, DI registration, auth configuration, routes
- `Schema/ApplicationDbContext.cs` — DbContext with entity configuration

## Database & Naming Conventions

**Critical:** C# properties use camelCase but map to snake_case DB columns via `[Column(...)]` attributes:
```csharp
[Column("user_email")]
public string userEmail { get; set; }
```

When adding/modifying models, update both the property and `[Column(...)]` attribute.

**Core Tables:** `user_account`, `events`, `event_organizers`, `event_participants`, `event_recordings`

## Authentication

- Cookie-based auth with scheme name `KonfucjuszUser`
- Login path: `/login`, Access denied: `/accessDenied`
- Roles: "Administrator", "Organizer", "User"
- Dev sign-in endpoint: `/account/signin?user_email=...&user_role=...` (issues cookie directly)

## Services & DI

All services are registered as scoped in `Program.cs`:
- `ApplicationDbContext` — PostgreSQL via Npgsql
- `UserService` — User CRUD (synchronous EF calls)
- `EventService` — Event management, slug generation (requires SlugSecret config)
- `ParticipantService` — Participation/waitlist logic, token generation
- `EmailRequest` — SMTP wrapper using IOptions<SmtpOptions>

## Configuration

Required secrets (via `dotnet user-secrets` or environment variables):
- `ConnectionStrings:MyConnection` — PostgreSQL connection
- `SlugSecret` — HMAC secret for event slugs
- `Smtp:Host`, `Smtp:Port`, `Smtp:Username`, `Smtp:Password`, `Smtp:From` — Email config

For Docker, set via environment variables with double underscores: `Smtp__Host`, `ConnectionStrings__MyConnection`

## Audio Streaming

- SignalR hub: `Hubs/AudioStreamHub.cs` with methods JoinListener, JoinManager, SendAudioChunk, StartRecording, StopRecording
- Client-side: `wwwroot/js/audioStream.js` (WebAudio API, adaptive buffering)
- SignalR loaded locally from `wwwroot/lib/signalr/signalr.min.js`
- Recordings stored in `/app/recordings` directory

## CSS & Styling

- Global theme: `wwwroot/app.css` with CSS variables (primary, secondary, accent colors)
- Layout styles: `wwwroot/css/mainlayout.css`, `wwwroot/css/navmenu.css`
- Do NOT use `.razor.css` scoped styles for layouts — use the CSS files in wwwroot/css/ for dynamic reload

## Email & Token Handling

- Email verification tokens: `Schema/UserAccount.cs` using DataProtectionProvider with protector name `Konfucjusz.EmailVerification.v1`
- Token payload format: `id|userName|userPassword|userEmail|userRole`
- SMTP implementation: `Services/EmailRequest.cs` using MailKit

## Where to Find Examples

- App boot & auth: `Program.cs`
- DB model & token logic: `Schema/UserAccount.cs`
- User management UI: `Components/Pages/Admin/UserManagment.razor`
- Event management: `Components/Pages/Events/`
- Audio streaming: `Components/Pages/Events/StreamBroadcast.razor`, `Hubs/AudioStreamHub.cs`
- Email sending: `Services/EmailRequest.cs`

## Additional Documentation

- `README.md` — Setup instructions, Gmail SMTP setup, troubleshooting
- `SECURITY-NOTES.md` — Security findings and roadmap for production hardening
- `THEME-GUIDE.md` — Theme system documentation
- `LLM-docs/` — Feature index, architecture, session notes
