# Security — OWASP Top 10 mitigations & frontend hardening

This document is the security spec for Collectify. It is intentionally checklist-shaped so it can be scanned during reviews.

> Threat model: a self-hosted single-user (Phase 1–3) or small-multi-user (Phase 4+) app, typically reached over LAN or via the user's own reverse proxy / VPN. We assume the operator is non-hostile but the network and any embedded webview / browser are partially trusted at best.

## Backend — OWASP Top 10 (2021) checklist

### A01:2021 — Broken Access Control
- Every `/api/*` endpoint except `/api/auth/{me,setup,login}` calls `.RequireAuthorization()`.
- Every read/write of a collection row filters by `OwnerId == users.GetUserId(ctx.User)`. Never trust an `OwnerId` supplied in a request body — derive it server-side from the cookie.
- `PUT /api/{type}/{id}` and `DELETE /api/{type}/{id}` re-check `OwnerId` before mutation, returning `404 Not Found` (never `403`) when the row exists but belongs to another owner — avoids leaking existence.
- The first-run `/api/auth/setup` is one-shot: it 400s if any user already exists.
- Role checks (Phase 4): use `RequireAuthorization(policy => policy.RequireRole("Admin"))` for any registration / deletion endpoints. Don't roll a custom check.

### A02:2021 — Cryptographic Failures
- Passwords are stored via ASP.NET Core Identity's default PBKDF2 hasher. Don't switch to a custom hasher.
- Cookie auth uses ASP.NET Core Data Protection. In Docker, the keys live in `/data/keys` (configure once we add `services.AddDataProtection().PersistKeysToFileSystem(...)`); without persistence, cookies invalidate on container rebuild.
- Outbound HTTPS: never `ServerCertificateCustomValidationCallback` shortcuts. Use the system trust store.
- Secrets (`TMDB_API_KEY`, `IGDB_*`, `DISCOGS_TOKEN`) come from environment variables, never committed. `.env` is gitignored.

### A03:2021 — Injection
- All persistence is through EF Core LINQ → parameterized SQL. Never concatenate user input into raw SQL. If you ever need raw SQL, use `FromSqlInterpolated`.
- Never pass user input into `EF.Functions.Like` patterns without first wrapping it: `var like = $"%{userQuery}%";` is *parameterized* by EF Core, but always confirm by reading the generated SQL when reviewing.
- For the Phase 2 metadata clients, build URLs with `QueryHelpers.AddQueryString` or `UriBuilder` — never string interpolation into URLs.
- Identity username & password validation: enforce `RequiredLength`, no whitespace-only, normalized comparisons via `UserManager` (already does this).

### A04:2021 — Insecure Design
- Single-user-by-default is a deliberate design choice; multi-user requires opt-in via admin-controlled registration (Phase 4).
- All multi-tenant scoping is on the row (`OwnerId`), not on a connection string or schema, so cross-tenant leaks reduce to "did I forget the `WHERE OwnerId == …`" — easy to grep for.
- Camera barcode scanning happens **client-side only**; the server never receives an image, only a UPC string. This avoids storing user-controlled binary blobs.

### A05:2021 — Security Misconfiguration
- `ASPNETCORE_ENVIRONMENT=Production` in the runtime image. Never ship Swagger / developer exception pages in production builds.
- `app.UseHsts()` and `app.UseHttpsRedirection()` should be added once the operator runs behind TLS — flagged by config flag, not always-on, since some self-hosted setups terminate TLS at the proxy. Document this in the README.
- The container runs as UID 1000, not root. The data volume is owned by 1000.
- `Cookie.SameSite = Lax`, `HttpOnly = true`, `Secure = true` (auto when `RequireHttpsMetadata` is on). Set `Secure` explicitly once we know the operator's terminating proxy contract.
- Disable directory browsing (default) — `UseStaticFiles` only serves the built React bundle.

### A06:2021 — Vulnerable & Outdated Components
- `dotnet list package --vulnerable --include-transitive` should be clean before each release. Currently `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.0.0 pulls a transitive `System.Security.Cryptography.Xml` 9.0.0 with a `NU1903` warning — track upstream and pin once a patched version ships.
- `npm audit --omit=dev` should be clean. Dev dependencies (Vite, TS) are excluded because they don't ship to users.
- Dependabot or `npm-check-updates` runs in a future task to keep these moving.

### A07:2021 — Identification and Authentication Failures
- Use `SignInManager.PasswordSignInAsync(..., lockoutOnFailure: false)` for now; **enable lockout (`true`) and configure `IdentityOptions.Lockout`** before any deployment is exposed beyond a private network.
- No password reset email flow yet — single-user deployments reset by clearing the DB volume; document this in the README. Revisit for Phase 4 multi-user.
- Cookie expiry: 30 days sliding. Logout properly calls `SignOutAsync`.
- Don't expose username enumeration: `/api/auth/login` returns identical `401` whether the user exists or not.

### A08:2021 — Software and Data Integrity Failures
- All packages restored from `nuget.org` and `registry.npmjs.org` — no custom feeds.
- `package-lock.json` and EF Core migrations are committed. CI must `npm ci` (not `npm install`) and `dotnet restore --locked-mode` to guarantee reproducibility (add when CI is set up).
- Built React bundle is served with default `Content-Type` and a strong `ETag`; no client-side script loaded from third-party CDNs.

### A09:2021 — Security Logging and Monitoring Failures
- ASP.NET default logging is on (`Information` for app, `Warning` for framework). Log:
  - Failed logins (level: Warning)
  - Setup attempts after first user exists (Warning)
  - Provider configuration errors (Error)
- Don't log secrets, full request bodies, or full external API responses. Log the cache key + status only.
- Logs go to stdout (Docker captures them). No log files inside the container.

### A10:2021 — Server-Side Request Forgery (SSRF)
- Phase 2/3 metadata clients only call **fixed, hard-coded base URLs** (TMDB / MusicBrainz / IGDB / UPCitemdb). Never accept a URL from a user-supplied field and dispatch a server request to it.
- If we ever fetch a cover image by URL provided by an external API, validate it points to that provider's known image host, set a timeout (≤ 5 s), cap the response size (≤ 5 MB), and disallow HTTP redirects to private IP ranges.

## HTTP / transport hardening

- Add a basic security-headers middleware (Phase 2 or earlier) emitting:
  - `Content-Security-Policy: default-src 'self'; img-src 'self' data: https:; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'`
  - `X-Content-Type-Options: nosniff`
  - `Referrer-Policy: strict-origin-when-cross-origin`
  - `Permissions-Policy: camera=(self), microphone=()` (camera needed for barcode scan)
- Same-origin only: don't enable CORS.

## Frontend — OWASP & general hardening

### XSS
- React escapes by default. **Never** call `dangerouslySetInnerHTML` on user-supplied strings. The codebase contains zero usages today; keep it that way.
- Don't render markdown/HTML coming from external metadata providers — display as plain text only. If we ever want rich text, add `DOMPurify` and a strict allow-list.
- The CSP above forbids inline scripts and remote scripts.

### CSRF
- Cookie auth means we are CSRF-relevant. Mitigations:
  - `SameSite=Lax` on the auth cookie (already set).
  - Same-origin only — no CORS.
  - All state-changing methods (`POST`, `PUT`, `DELETE`) require auth, so a forged GET cannot mutate state.
  - For extra safety, add an `X-Requested-With: XMLHttpRequest` requirement on state-changing endpoints (Phase 2 task) — `fetch` from another origin can't set custom headers without preflight.

### Sensitive data exposure
- Never store passwords in `localStorage` / `sessionStorage` / IndexedDB. Auth state is the cookie, period.
- Don't put API keys in the React bundle. All keys live server-side in `appsettings`/`.env`.
- Avoid logging full server responses to the browser console in production builds.

### Dependency hygiene
- `npm audit --omit=dev` clean.
- Pin `@zxing/browser` (Phase 3) to a known-good version; review release notes before upgrading.

### Camera & permissions (Phase 3)
- `getUserMedia` requires HTTPS. README documents how to set up TLS via reverse proxy (Caddy / Traefik / mkcert).
- Stop the camera stream as soon as the scanner unmounts. Always check `track.stop()` in `useEffect` cleanup.
- Never upload the camera frame to the server — only the decoded UPC string.

### Mobile / PWA notes
- `viewport-fit=cover` is set; that's only layout, not security.
- Don't enable an SPA service-worker until the cache strategy is intentionally designed; a misconfigured SW can serve stale code.

## Review checklist (use during PR review)

- [ ] New endpoint: has `.RequireAuthorization()` (or explicit comment why not)
- [ ] New endpoint: filters by `OwnerId` for any DB read/write
- [ ] No raw SQL string interpolation
- [ ] No new third-party JS / CSS loaded from CDN
- [ ] No new `dangerouslySetInnerHTML`
- [ ] No new package without locking and reviewing audit output
- [ ] No new env var read directly via `Environment.GetEnvironmentVariable`; use `IConfiguration`
- [ ] If a feature flag controls security behavior (HTTPS redirect, lockout), default is the safer value
- [ ] Tests cover the auth-failure and ownership-mismatch cases
