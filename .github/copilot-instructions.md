# Copilot / AI agent instructions — Konfucjusz

Quick, actionable notes for an AI editing or navigating this codebase. Focus on concrete, discoverable patterns and where to look for examples.

- Project type: .NET 8 Blazor Server app using interactive Razor Components (see `Program.cs` — `AddRazorComponents()` / `MapRazorComponents<App>()`).
- Main entry: `Program.cs` (app bootstrap, DI, auth, routes).

Run & debug
- Build: `dotnet build` in repo root.
- Run (development): `dotnet run --environment Development` or just `dotnet run` (this repository runs as a simple Blazor server app).
- The app uses `Properties/launchSettings.json` for IDE launch profiles.

Database
- EF Core `ApplicationDbContext` is registered in `Program.cs` with Npgsql using connection string name `MyConnection` (see `appsettings.json`).
- Current code expects an existing DB schema. There are no EF Migrations tracked in-repo — the table mapping is in `Schema/UserAccount.cs` (table `user_account`, columns mapped with `[Column(...)]`).
- Important: C# model property names use camelCase (e.g. `userEmail`) while DB uses snake_case (e.g. `user_email`) and attributes define the mapping. If you add/rename columns update both the model attributes and the DB schema.

Authentication & sign-in flow
- Cookie authentication is configured in `Program.cs`: cookie name `KonfucjuszUser`, login path `/login`, access denied `/accessDenied`, MaxAge 30 minutes.
- There is a lightweight sign-in endpoint at `/account/signin` which accepts query parameters `user_email` and `user_role` and issues the auth cookie (see `Program.cs`). Use that for quick manual sign-ins during testing.

Services & DI
- `ApplicationDbContext` is registered with `AddDbContext` (scoped). `UserService` is registered as scoped (`AddScoped<UserService>`). Prefer using these registrations when adding features.
- `UserService` (see `Services/UserService.cs`) provides basic CRUD against `users` DbSet. It uses synchronous EF calls (no async methods currently).

Email and tokens
- Email sending is implemented with MailKit in `Services/EmailRequest.cs`. SMTP settings come from config `Smtp:*` keys.
- For Gmail SMTP setup, see README.md#gmail-smtp-setup (requires 2FA + app password).
- Email verification token generation/consumption is implemented in `Schema/UserAccount.cs` using `DataProtectionProvider.Create("Konfucjusz")` and protector name `Konfucjusz.EmailVerification.v1`. Look here for the token payload format (id|userName|userPassword|userEmail|userRole).

Conventions & gotchas (project-specific)
- Naming: this project uses camelCase property names in POCOs (e.g. `userName`, `userPassword`) and explicit `[Column(...)]` attributes to map to snake_case DB columns. Do not assume default EF naming conventions.
- Sync vs Async: `UserService` uses synchronous EF core calls (e.g., `ToList()`, `SaveChanges()`). If introducing async variants, be consistent across the service layer and callers.
- Secrets in repo: `appsettings.json` currently contains the DB connection string and `EmailRequest.cs` contains SMTP credentials. Be cautious when editing or committing changes that could leak secrets.

Where to look for examples
- App boot & auth rules: `Program.cs` (auth, DI, endpoints, static files, antiforgery).
- DB model & token logic: `Schema/UserAccount.cs`.
- EF context: `Schema/ApplicationDbContext.cs`.
- User management: `Services/UserService.cs` and `Components/Pages/Admin/UserManagment.razor` (UI for users).
- Email sending: `Services/EmailRequest.cs`.

Editing guidance
- Small change example (add a user field):
  1. Update `Schema/UserAccount.cs` with property + `[Column(...)]` mapping.
  2. Update UI pages under `Components/Pages` and any code that constructs `UserAccount`.
  3. Apply DB migration externally or run SQL to alter `user_account` — repo contains no automatic migrations.

If something is missing or unclear
- Tell me which file or flow you want clarified (e.g., registration, email verification flow, or database setup) and I'll expand this file with a short how-to that includes exact file edits and verification steps.

— End
