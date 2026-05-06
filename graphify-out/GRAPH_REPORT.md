# Graph Report - /home/cesar/dev/collectify  (2026-05-05)

## Corpus Check
- Corpus is ~13,817 words - fits in a single context window. You may not need a graph.

## Summary
- 236 nodes · 373 edges · 44 communities (25 shown, 19 thin omitted)
- Extraction: 87% EXTRACTED · 13% INFERRED · 0% AMBIGUOUS · INFERRED: 49 edges (avg confidence: 0.89)
- Token cost: 142,553 input · 35,637 output

## Community Hubs (Navigation)
- [[_COMMUNITY_Client Data Hooks & DTOs|Client Data Hooks & DTOs]]
- [[_COMMUNITY_Project Docs & Build Files|Project Docs & Build Files]]
- [[_COMMUNITY_Domain Model & Enums|Domain Model & Enums]]
- [[_COMMUNITY_Client Auth & API Client|Client Auth & API Client]]
- [[_COMMUNITY_Server Endpoints & Persistence|Server Endpoints & Persistence]]
- [[_COMMUNITY_Client App Shell & Pages|Client App Shell & Pages]]
- [[_COMMUNITY_Security & Deployment|Security & Deployment]]
- [[_COMMUNITY_Initial EF Migration|Initial EF Migration]]
- [[_COMMUNITY_Games Endpoints|Games Endpoints]]
- [[_COMMUNITY_Movies Endpoints|Movies Endpoints]]
- [[_COMMUNITY_Music Endpoints|Music Endpoints]]
- [[_COMMUNITY_EF Model Snapshot|EF Model Snapshot]]
- [[_COMMUNITY_DbContext Configuration|DbContext Configuration]]
- [[_COMMUNITY_Migration Designer|Migration Designer]]
- [[_COMMUNITY_Auth Endpoints|Auth Endpoints]]
- [[_COMMUNITY_AppUser Identity|AppUser Identity]]
- [[_COMMUNITY_Unit Test Stub|Unit Test Stub]]
- [[_COMMUNITY_API Entry Point|API Entry Point]]
- [[_COMMUNITY_Game Entity|Game Entity]]
- [[_COMMUNITY_Lookup Cache|Lookup Cache]]
- [[_COMMUNITY_Movie Entity|Movie Entity]]
- [[_COMMUNITY_MusicAlbum Entity|MusicAlbum Entity]]
- [[_COMMUNITY_Frontend Build Tooling|Frontend Build Tooling]]
- [[_COMMUNITY_Test Placeholder|Test Placeholder]]
- [[_COMMUNITY_API GlobalUsings (gen)|API GlobalUsings (gen)]]
- [[_COMMUNITY_API AssemblyInfo (gen)|API AssemblyInfo (gen)]]
- [[_COMMUNITY_Domain GlobalUsings (gen)|Domain GlobalUsings (gen)]]
- [[_COMMUNITY_Domain AssemblyInfo (gen)|Domain AssemblyInfo (gen)]]

## God Nodes (most connected - your core abstractions)
1. `docs/architecture.md` - 12 edges
2. `docs/data-model.md` - 12 edges
3. `App (root router component)` - 11 edges
4. `CollectifyDbContext` - 11 edges
5. `useAuth()` - 10 edges
6. `useList()` - 10 edges
7. `Field()` - 10 edges
8. `api() fetch helper` - 10 edges
9. `docs/security.md` - 9 edges
10. `Movie entity (DVD/Blu-ray/UHD)` - 9 edges

## Surprising Connections (you probably didn't know these)
- `Dashboard page` --calls--> `useList()`  [EXTRACTED]
  src/client/pages/Dashboard.tsx → client/api/collection.ts
- `Collectify.Domain.csproj` --implements--> `Collectify.Domain (Core layer, BCL only)`  [INFERRED]
  src/server/Collectify.Domain/obj/Debug/net10.0/Collectify.Domain.csproj.FileListAbsolute.txt → docs/architecture.md
- `App (root router component)` --calls--> `useAuth()`  [EXTRACTED]
  src/client/App.tsx → client/api/auth.ts
- `useAuth()` --implements--> `AuthState interface`  [EXTRACTED]
  client/api/auth.ts → src/client/api/auth.ts
- `Setup page (single-user provisioning)` --calls--> `useSetup()`  [EXTRACTED]
  src/client/pages/Setup.tsx → client/api/auth.ts

## Hyperedges (group relationships)
- **Auth lifecycle (setup/login/logout/state)** — api_auth_useauth, api_auth_usesetup, api_auth_uselogin, api_auth_uselogout, backend_api_auth_endpoints [EXTRACTED 1.00]
- **Generic CRUD pattern across MediaType** — api_collection_uselist, api_collection_useitem, api_collection_usecreate, api_collection_useupdate, api_collection_usedelete, api_types_mediatype [EXTRACTED 1.00]
- **MediaType-driven form dispatch in Add/Edit pages** — pages_addpage, pages_editpage, components_movieform, components_albumform, components_gameform, api_types_mediatype [INFERRED 0.85]
- **Media-type CRUD endpoints (per-owner authorized minimal API pattern)** — movies_endpoints, music_endpoints, games_endpoints [INFERRED 0.95]
- **Per-owner media collection entities (Id, OwnerId, Title, Barcode, AddedAt/UpdatedAt)** — movie_entity, musicalbum_entity, game_entity [INFERRED 0.95]
- **Media format/store enumerations attached to entities** — movieformat_enum, musicformat_enum, digitalstore_enum [INFERRED 0.85]
- **Clean architecture layered dependency chain** — concept_domain_layer, concept_infrastructure_layer, concept_api_layer, concept_tests_layer [EXTRACTED 1.00]
- **Three-collection-types data model with shared owner scoping** — concept_movie_entity, concept_musicalbum_entity, concept_game_entity, concept_tag_entity, concept_ownership_scoping [EXTRACTED 1.00]
- **Phase 2 metadata-lookup flow (providers + cache + abstraction)** — concept_metadata_provider, concept_lookup_cache, concept_tmdb_provider, concept_musicbrainz_provider, concept_igdb_provider [EXTRACTED 1.00]

## Communities (44 total, 19 thin omitted)

### Community 0 - "Client Data Hooks & DTOs"
Cohesion: 0.1
Nodes (27): useCreate(), useDelete(), useItem(), useList(), useUpdate(), Album interface, DIGITAL_STORES list, Game interface (+19 more)

### Community 1 - "Project Docs & Build Files"
Cohesion: 0.1
Nodes (26): docs/architecture.md, Collectify.Api.csproj, Collectify.Domain.csproj, Collectify.Infrastructure.csproj, Collectify.Tests.csproj, Collectify.Api (Presentation layer / composition root), AppUser : IdentityUser, ASP.NET Core Identity + cookie auth (+18 more)

### Community 2 - "Domain Model & Enums"
Cohesion: 0.17
Nodes (21): CollectifyDbContext, CollectionStatus enum, CompletionStatus enum, Condition enum, DigitalStore enum (Steam/Gog/Epic/Xbox/Psn/Nintendo/Other), Game entity (physical/digital), IGDB game provider, LookupCache (provider response cache) (+13 more)

### Community 3 - "Client Auth & API Client"
Cohesion: 0.16
Nodes (14): AuthState interface, useAuth(), useLogin(), useLogout(), useSetup(), api(), api() fetch helper, ApiError (+6 more)

### Community 4 - "Server Endpoints & Persistence"
Cohesion: 0.27
Nodes (20): AlbumDto record, AppUser (IdentityUser), AuthEndpoints, CollectifyDbContext, CollectifyDbContextModelSnapshot, DigitalStore enum, Game entity, GameDto record (+12 more)

### Community 5 - "Client App Shell & Pages"
Cohesion: 0.27
Nodes (11): App (root router component), CollectionList (generic listing), index.html (root mount), main.tsx (React entry), QueryClient (TanStack Query), Dashboard page, GamesList page, Login page (+3 more)

### Community 6 - "Security & Deployment"
Cohesion: 0.2
Nodes (10): Content-Security-Policy and security headers, CSRF mitigation (SameSite=Lax cookie + same-origin), Data Protection keys persisted to /data/keys, collectify-data Docker named volume (/data), Multi-stage Docker (single image, port 8080), ASP.NET Identity PBKDF2 password hashing, /api/auth/setup one-shot first-run, SSRF mitigation (fixed base URLs only) (+2 more)

### Community 7 - "Initial EF Migration"
Cohesion: 0.33
Nodes (3): Migration, Collectify.Infrastructure.Data.Migrations, InitialCreate

### Community 11 - "EF Model Snapshot"
Cohesion: 0.4
Nodes (3): Collectify.Infrastructure.Data.Migrations, CollectifyDbContextModelSnapshot, ModelSnapshot

## Knowledge Gaps
- **35 isolated node(s):** `Program`, `Game`, `LookupCacheEntry`, `Movie`, `MusicAlbum` (+30 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **19 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `docs/architecture.md` connect `Project Docs & Build Files` to `Domain Model & Enums`?**
  _High betweenness centrality (0.024) - this node is a cross-community bridge._
- **Why does `App (root router component)` connect `Client App Shell & Pages` to `Client Data Hooks & DTOs`, `Client Auth & API Client`?**
  _High betweenness centrality (0.023) - this node is a cross-community bridge._
- **Why does `api() fetch helper` connect `Client Auth & API Client` to `Client Data Hooks & DTOs`?**
  _High betweenness centrality (0.018) - this node is a cross-community bridge._
- **Are the 2 inferred relationships involving `CollectifyDbContext` (e.g. with `InitialCreate migration` and `CollectifyDbContextModelSnapshot`) actually correct?**
  _`CollectifyDbContext` has 2 INFERRED edges - model-reasoned connections that need verification._
- **Are the 2 inferred relationships involving `useAuth()` (e.g. with `App()` and `Layout()`) actually correct?**
  _`useAuth()` has 2 INFERRED edges - model-reasoned connections that need verification._
- **What connects `Program`, `Game`, `LookupCacheEntry` to the rest of the system?**
  _35 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Client Data Hooks & DTOs` be split into smaller, more focused modules?**
  _Cohesion score 0.1 - nodes in this community are weakly interconnected._