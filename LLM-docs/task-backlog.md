# Project Task Backlog

## Current Sprint (Active)

### High Priority
- [ ] **Replace MailHog with OVH SMTP and secure SMTP configuration** - Migrate from dev MailHog to real OVH SMTP using env-var-based secrets and `SmtpOptions`. (Est: 1–2 sessions)
  - Status: in-progress
  - Owner: tdd-developer + user
  - Dependencies: OVH SMTP credentials, docker-compose, EmailRequest configuration
  - Files: `docker-compose.yml`, `.env`, `Services/EmailRequest.cs`, `appsettings.json.example`

### Medium Priority
- [ ] **Security Hardening (Authentication and AudioStreamHub)** - Apply changes outlined in `SECURITY-NOTES.md` before any Internet-facing deployment.

### Low Priority
- [ ] **Improve Cookie and AllowedHosts configuration** - Harden cookie settings and restrict `AllowedHosts` for production.

## Backlog (Planned)

### Next Sprint Candidates
- [ ] **Add stream link pre-availability** - Make stream link available before the stream starts, not just during broadcast. (Est: 1 session)
  - Status: reported by user
  - Files: Likely `Components/Pages/Events/EventEdit.razor`, `Components/Pages/Events/StreamListen.razor`
  - Enhancement: Users should be able to share/access stream link before event begins
  
- [ ] **Add QR codes for enlist and stream links** - Generate QR codes for easy mobile access to enlistment and streaming pages. (Est: 1-2 sessions)
  - Status: requested by user
  - Files: `Components/Pages/Events/EventEdit.razor` (enlist link section), stream link display areas
  - Dependencies: QR code generation library (e.g., QRCoder, ZXing.Net)
  - Enhancement: Display QR codes alongside URLs for easy scanning

- [ ] **Audit registration/login flows** - Ensure no backdoor-like endpoints are enabled in production.

### Future Considerations
- [ ] **Azure-ready deployment** - Plan container deployment and secret management for Azure.

## Completed (Last Sprint)
- [x] **Create "My Events" page for participants** - Added dedicated page for users to view and access their enrolled events (2026-01-03)
  - Created MyParticipations.razor with event cards, status badges, and stream/recording access
  - Added navigation menu item for all authenticated users
  - Implemented cancel participation with confirmation modal
  - Added comprehensive logging using [MyParticipations] prefix
  - Files: Components/Pages/Events/MyParticipations.razor, Components/Layout/NavMenu.razor
- [x] **Fix 'Enlist Now' button functionality** - Fixed button not working for logged-in users (2026-01-03)
  - Enhanced user ID extraction to use Name claim (email stored there)
  - Fixed parameter order bug in service call (eventId and userId were swapped)
  - Added comprehensive logging throughout enlistment flow
  - Files: Components/Pages/Events/Enlist.razor
- [x] **Fix stream broadcast permission error** - Fixed "You do not have permission to manage this event" error for Organizers (2026-01-03)
  - Enhanced user ID extraction to support multiple claim types (ClaimTypes.Email, email, preferred_username, Name)
  - Added comprehensive logging to StreamBroadcast.razor for diagnosing permission issues
  - Added authorization check to AudioStreamHub.StartRecording method (defense-in-depth)
  - Files: Components/Pages/Events/StreamBroadcast.razor, Hubs/AudioStreamHub.cs
- [x] **Administrator bulk event deletion** - Added checkbox selection and simple confirmation for efficient bulk deletion (2026-01-02)
  - Implemented DeleteMultipleEventsWithCleanupAsync in EventService
  - Added checkbox column and selection toolbar to EventManagement
  - Removed individual delete buttons (forced bulk workflow)
  - Created simple confirmation dialog (no typing required)
  - Comprehensive documentation created
- [x] **Fix missing event enlist links (slug generation)** - Added slug generation to all event creation flows (2026-01-02)
  - Fixed EventList.razor to use CreateEventWithSlugAsync
  - Fixed EventEdit.razor to call EnsureSlugAsync on save
  - Added comprehensive logging to EventService
  - Silenced EF Core database query spam
- [x] **Remove unreliable captcha system from registration** - Removed math-based captcha from Register.razor (2026-01-02)
- [x] **Implement hCaptcha bot protection** - Added Texnomic.Blazor.hCaptcha integration to registration form (2026-01-02)

## Blocked Items
- [ ] **OVH SMTP integration finalization** - Blocked by: exact OVH SMTP host/port/encryption details and credentials.
  - Contact: user
  - Estimated Resolution: once OVH mailbox is configured

## Architecture Decisions Needed
- **Environment Separation for Mail** - Whether to keep a dev-only MailHog compose file and a separate OVH-based compose for real deployments.
  - Options: [single-compose-ovh, dual-compose-dev+prod]
  - Impact: medium
  - Timeline: before finalizing SMTP migration

## Technical Debt Registry
- **Area:** SMTP and Security
  - **Issue:** Temporary dev-oriented config, MailHog legacy values, not yet hardened secrets.
  - **Impact:** maintainability, security
  - **Priority:** high
  - **Effort:** medium
