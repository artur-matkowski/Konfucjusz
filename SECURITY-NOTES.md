# Konfucjusz – Security Notes

This file tracks known security concerns and intended improvements for the Konfucjusz application. It is meant to inform future work (including by LLM agents) when preparing for Internet-facing or production deployments, especially on Azure.

## Context

- Application: Blazor Server/.NET app with SignalR audio streaming hub, EF Core + PostgreSQL.
- Current deployment target: local / unexposed server using Docker containers (app + Postgres + MailHog), behind an optional nginx reverse proxy.
- Future target: Azure (likely Azure Container Apps or App Service with a container image) and Azure Database for PostgreSQL.

Configuration/design principles to keep:

- No real secrets in source control (`appsettings.json` stays generic).
- Use environment variables (or equivalent secret providers) for:
  - `ConnectionStrings:MyConnection`
  - `SlugSecret`
  - SMTP credentials.
- Docker-compose and local `.env` are for **dev/test only**, not for Internet-facing production.

## Key Findings from Static Security Scan

The security scan reported overall risk **MEDIUM** and deployment gate **ALLOW_WITH_RISKS**. Main issues to address before any public/production deployment:

### 1. `/account/signin` endpoint (HIGH severity)

- Location: `Program.cs`, `/account/signin` route (MapGet).
- Behavior: Issues an auth cookie purely from query parameters `user_email` and `user_role`, without password, prior authentication, or token verification.
- Risk: Acts as a backdoor. Anyone who can hit `/account/signin?user_email=...&user_role=Administrator` can log in as any user with arbitrary roles.

**Desired future changes:**
- For production builds, either:
  - Remove this endpoint completely, or
  - Restrict it to a strongly validated, cryptographically protected token (e.g., one-time registration/verification token tied to a real user record) and/or authenticated admins.
- At a minimum, ensure it is not available in `Production` environment, e.g. only map it when `app.Environment.IsDevelopment()`.

### 2. SignalR AudioStreamHub token validation (HIGH severity)

- Location: `Hubs/AudioStreamHub.cs`, `JoinListener` and related methods.
- Current behavior (per comment): accepts any non-empty `token` as valid ("for now we'll accept token presence as valid").
- Risk: Streams intended to be protected become effectively public to anyone who knows/guesses event IDs/slugs and passes any non-empty token.

**Desired future changes:**
- Replace placeholder check with real validation using `ParticipantService` (e.g., `TryParseStreamToken`) or similar service method.
- Only allow listener/manager access when the token:
  - Decrypts/verifies correctly.
  - Matches an existing participant/event combination.
  - Satisfies authorization rules (e.g., user is an invited participant or organizer).
- Consider tying access to authenticated user identity (claims) instead of or in addition to tokens.

### 3. `SlugSecret` insecure default (MEDIUM severity)

- Location: `Program.cs` – `var slugSecret = builder.Configuration["SlugSecret"] ?? "default-slug-secret-change-in-production";` and backfill logic using the same default.
- Risk: If `SlugSecret` is not configured in production, event slugs and any tokenization relying on it will be derived from a known, weak value, making identifiers predictable.

**Desired future changes:**
- In non-development environments, require that `SlugSecret` be configured and **fail fast on startup** if it is missing or equal to the default placeholder.
- Store `SlugSecret` in environment variables or secret stores (Azure Key Vault, user-secrets for local dev) and rotate carefully if needed.

### 4. Postgres password in docker-compose (MEDIUM severity)

- Location: `docker-compose.yml` – `POSTGRES_PASSWORD` for the `db` service.
- Current behavior: Defaults to `konfucjuszpass` (or value from `DB_PASSWORD` env var) and is intended for dev/test.

**Desired future changes:**
- For any non-trivial or Internet-facing deployment:
  - Use a strong, random database password provided via environment variables or secret management, not a weak or guessable value.
  - Avoid reusing dev/test passwords in production.
- Document clearly that the committed `POSTGRES_PASSWORD` default is **dev-only** and must be overridden.

### 5. Cookie/auth configuration (MEDIUM/LOW severity)

- Location: `Program.cs` – cookie auth setup.
- Issue: Some cookie options like `SecurePolicy`, `HttpOnly`, `SameSite` are not explicitly set.

**Desired future changes:**
- Set explicit, secure defaults, e.g.:
  - `opt.Cookie.SecurePolicy = CookieSecurePolicy.Always;`
  - `opt.Cookie.HttpOnly = true;`
  - `opt.Cookie.SameSite = SameSiteMode.Lax` or `Strict` (depending on SSO/CSRF considerations).
- Consider adjusting session lifetime/idle timeout according to production needs.

### 6. `AllowedHosts` wildcard (MEDIUM severity)

- Location: `appsettings.json` – `"AllowedHosts": "*"`.
- Risk: In production, allowing any host header can enable host header attacks when combined with certain reverse proxy or link-generation behaviors.

**Desired future changes:**
- For production, restrict `AllowedHosts` to explicit domains, e.g.:
  - `"example.com;www.example.com"`.

### 7. Role parsing via substring (LOW severity)

- Location: `Services/EventService.cs` – method `CanUserManageEventAsync`.
- Current behavior: Tests admin status using `userRole.Contains("Administrator")`.
- Risk: Partial matches (e.g. `"NotAdministrator"`) could be misinterpreted as admin.

**Desired future changes:**
- Use structured role claims and `User.IsInRole("Administrator")` where possible.
- If working with strings, use exact/whitelist matches on delimited values instead of `Contains`.

### 8. SignalR manager authorization (LOW severity)

- Location: `Hubs/AudioStreamHub.cs`, `JoinManager` and related manager operations.
- Issue: Comment indicates manager access is "checked in UI elsewhere"; server-side method does not enforce authorization with `[Authorize]` or explicit claim checks.

**Desired future changes:**
- Enforce authorization server-side using:
  - `[Authorize(Roles = "Administrator,Organizer")]` on the hub or methods, and/or
  - Manual inspection of claims to ensure the caller is a proper organizer/admin for the event.

### 9. Logging of tokens/identifiers (LOW severity)

- Location: `Hubs/AudioStreamHub.cs` and possibly elsewhere.
- Issue: Some log statements may include tokens or sensitive identifiers.

**Desired future changes:**
- Avoid logging full tokens or secrets.
- Mask or omit sensitive parts in production logs.

## Current Deployment Assumptions (Safe Scope)

- Deployments described so far are for:
  - Local development, and
  - Unexposed local servers (no public Internet access).
- Environment is typically set to `Development` in Docker (`ASPNETCORE_ENVIRONMENT=Development`).
- Mail is routed to MailHog (SMTP on `mailhog:1025`) for testing, **not** to real external mail servers.

Given these assumptions, the insecure patterns above are tolerated temporarily but **must be addressed** before any Internet-facing or production deployment.

## Planned Security Work for Future Sessions

When preparing for Azure or any public-facing environment, future work should:

1. **Harden authentication and authorization**
   - Remove or secure `/account/signin`.
   - Implement robust token validation and proper authorization checks in `AudioStreamHub`.
   - Ensure role checks use proper claim-based mechanisms.

2. **Enforce strong secrets and configuration**
   - Require non-default `SlugSecret` in non-development environments.
   - Use strong passwords and secret stores for database and SMTP credentials.
   - Tighten `AllowedHosts` and cookie options.

3. **Review logging and telemetry**
   - Remove or mask sensitive tokens from logs.
   - Ensure exception handling in production does not leak details.

4. **Align configuration with Azure**
   - Keep using environment variables for all secrets and connection strings.
   - Plan integration with Azure Key Vault or Azure App Configuration if needed.

This document should be updated as issues are fixed or new concerns are identified.
