# Architecture — Clean Architecture for the Collectify backend

This document specifies the layering rules for the .NET backend. The goal is a small, maintainable Clean / Onion-style architecture that keeps domain logic free of framework concerns and makes adding the planned metadata providers (Phase 2) and barcode lookup (Phase 3) straightforward.

## Layers

```
┌────────────────────────────────────────────────────────────────┐
│  Collectify.Api  (Presentation)                                │
│  • Minimal API endpoints, DTOs, DI composition root            │
│  • ASP.NET Core, Identity, cookie auth, SPA static hosting     │
│  • Knows about Infrastructure + Domain                         │
└─────────────────────────┬──────────────────────────────────────┘
                          │ depends on
┌─────────────────────────▼──────────────────────────────────────┐
│  Collectify.Infrastructure  (Adapters)                         │
│  • EF Core DbContext, migrations, repositories (when needed)   │
│  • Identity stores, external HTTP clients (TMDB, MusicBrainz,  │
│    IGDB, UPC), cover image cache                               │
│  • Knows about Domain + the BCL + chosen libraries             │
└─────────────────────────┬──────────────────────────────────────┘
                          │ depends on
┌─────────────────────────▼──────────────────────────────────────┐
│  Collectify.Domain  (Core)                                     │
│  • Entities (Movie, MusicAlbum, Game, …)                       │
│  • Enums (MovieFormat, MusicFormat, DigitalStore)              │
│  • Domain interfaces (e.g. IMetadataProvider in Phase 2)       │
│  • NO references to ASP.NET Core, EF Core, Identity, Newtonsoft, etc. │
└────────────────────────────────────────────────────────────────┘
```

The dependency arrow points **inward only**. Domain knows nothing about Infrastructure or Api.

## Concrete rules

### Collectify.Domain
- Plain C# types: entities, value objects, enums, domain exceptions.
- Allowed dependencies: BCL only. Optionally `System.ComponentModel.DataAnnotations` if you need attributes that are *truly* domain concerns (length, required) — but prefer Fluent API in the DbContext for storage concerns.
- **Forbidden**: `Microsoft.EntityFrameworkCore.*`, `Microsoft.AspNetCore.*`, `Microsoft.AspNetCore.Identity.*`, any HTTP client, any third-party serializer.

### Collectify.Infrastructure
- `CollectifyDbContext` and EF configuration live here. Migrations under `Data/Migrations/`.
- Database provider selection via `Collectify:Database:Provider` config (default: `sqlite`; alternate: `postgres`).
  The `AddCollectifyDbContext()` extension in `Data/CollectifyDbContextExtensions.cs` handles the switch.
  SQLite is the migration author — migrations are generated against SQLite and replayed on Postgres.
- Identity user model (`AppUser : IdentityUser`) lives here, **not** in Domain — `IdentityUser` drags in framework types that would pollute Domain.
- External HTTP clients implement `IMetadataProvider<T>` (Phase 2) defined in Domain.
- Allowed dependencies: Domain, EF Core, Identity, `IHttpClientFactory`, etc.
- **Forbidden**: ASP.NET Core MVC / endpoint types, `HttpContext`, anything from `Microsoft.AspNetCore.Http`.

### Collectify.Api
- Composition root: `Program.cs` registers DI, configures auth, middleware, runs migrations on startup.
- Endpoints in `Endpoints/<Resource>Endpoints.cs` as static `MapXyzEndpoints` extension methods.
- DTOs are `record` types declared in the endpoint file.
- **Allowed**: everything.
- **Forbidden**: writing EF queries directly in endpoint handlers when the query has any meaningful logic. Keep handlers thin; push reusable query logic into Infrastructure (a query class or a small repository). For a simple `WHERE OwnerId == ... && Title LIKE ...` chain, inline LINQ is fine — see `MoviesEndpoints.cs`.

### Collectify.Tests
- Endpoint-level integration tests via `WebApplicationFactory<Program>` with a SQLite-in-memory connection.
- Pure unit tests for domain logic and providers (use `WireMock.Net` to fake external HTTP).
- Tests must not reach the real internet.

## Patterns

### Endpoint shape

```csharp
public static class MoviesEndpoints
{
    public record MovieDto( /* … */ );

    public static IEndpointRouteBuilder MapMoviesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/movies").RequireAuthorization();
        group.MapGet("/", List);
        group.MapGet("/{id:int}", Get);
        group.MapPost("/", Create);
        group.MapPut("/{id:int}", Update);
        group.MapDelete("/{id:int}", Delete);
        return app;
    }
    // … handlers as private static async methods or local funcs
}
```

### Ownership scoping

Every query against a collection table **must** filter by the current user's `Id`:

```csharp
var ownerId = users.GetUserId(ctx.User)!;
var q = db.Movies.AsNoTracking().Where(m => m.OwnerId == ownerId);
```

This is the multi-user-readiness contract. Even though we ship single-user, never write a query that returns rows across owners.

### External providers (Phase 2+)

The seam lives in `Collectify.Infrastructure/Lookup/`. One interface per media type so each result shape stays strongly typed:

```csharp
public interface IMovieMetadataProvider
{
    string Name { get; }
    bool IsConfigured { get; }
    Task<IReadOnlyList<MovieLookupResult>> SearchAsync(string query, CancellationToken ct = default);
}
// + IMusicMetadataProvider, IGameMetadataProvider with their own result records
```

- DI is set up by `services.AddMetadataLookup(config)` (called from `Program.cs`). It binds `MetadataLookupOptions` from `Collectify:Metadata`, registers `IHttpClientFactory`, and wires a `Stub*Provider` for each slot via `TryAddScoped`.
- A real provider PR (TMDB / MusicBrainz / IGDB) registers its typed `HttpClient` and its `IXxxMetadataProvider` implementation. `Replace()` (or running its registration before `AddMetadataLookup`) swaps the stub out.
- Outbound calls go through `ILookupCache` (`Provider`, `Key`), a memory/Redis distributed cache backed by `DistributedCacheAdapter`. TTL is applied **at write time** and comes from `MetadataLookupOptions.CacheTtl` (default 30 days, Steam uses its own short 5-minute TTL). The cache is ephemeral: an unconfigured install uses an in-process memory cache that resets on restart (cold-start provider burst); opt-in Redis (`Collectify:Cache:Provider=redis`) shares cached payloads across instances. Redis outages fail open — a missing/erroring cache simply re-queries the provider.
- Fail-soft: if not configured, `IsConfigured = false`. The lookup endpoint replies with `{ provider, configured: false, results: [] }` so the UI can show a clear "set TMDB__ApiKey to enable" hint instead of an error toast.

### Vision client (Phase 5)

Cover photo analysis lives behind `IVisionClient` in
`Infrastructure/Lookup/Vision/`. Provider-agnostic — the default
implementation uses Google Cloud Vision API (TEXT_DETECTION +
WEB_DETECTION), but the interface can be swapped via DI. Configured
through `MetadataLookupOptions.Vision`.

- `IVisionClient.AnalyseAsync()` returns multi-signal results: OCR text,
  web entity descriptions, and matching page URLs.
- The `POST /api/lookup/{type}/by-image` endpoint orchestrates a
  three-path matching strategy: (A) OCR text search, (B) web entity
  search, (C) known-domain URL routing → direct provider ID lookup.
- `IVisionClient` follows the same fail-soft contract: `IsConfigured`
  controls whether the endpoint attempts analysis.
- Images are processed in memory and discarded. Never persisted.
- Upload validation mirrors `CoversEndpoints`: content-type whitelist,
  magic-byte sniff, 5 MiB cap.

## Migrations

- Always generated via `dotnet ef migrations add <Name> --project Collectify.Infrastructure --startup-project Collectify.Api --output-dir Data/Migrations`.
- Applied automatically on container start (`db.Database.MigrateAsync()` in `Program.cs`).
- Never edit a migration once it's been applied to anyone's database — write a new migration instead.

## What we are NOT doing (to keep things small)

- No CQRS, MediatR, AutoMapper. The codebase is too small to justify the indirection.
- No repository pattern around EF unless a query is reused or has non-trivial logic — `DbContext` is a Unit of Work + repository already.
- No service interfaces purely for "swappability"; only abstract things we genuinely substitute (external providers, time, etc.).
- No layered DTO ↔ entity mappers; hand-written mapping in 5 lines is clearer than a 200-line profile.

If the project grows beyond Phase 3 and these constraints start to hurt, revisit — don't pre-introduce.
