# Graph Report - collectify  (2026-05-06)

## Corpus Check
- 79 files · ~26,495 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 397 nodes · 561 edges · 71 communities (43 shown, 28 thin omitted)
- Extraction: 91% EXTRACTED · 9% INFERRED · 0% AMBIGUOUS · INFERRED: 49 edges (avg confidence: 0.89)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `f851e27f`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

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
- [[_COMMUNITY_React Entry|React Entry]]
- [[_COMMUNITY_PostCSS Config|PostCSS Config]]
- [[_COMMUNITY_Tailwind Config|Tailwind Config]]
- [[_COMMUNITY_Vite Config|Vite Config]]
- [[_COMMUNITY_Client Types|Client Types]]
- [[_COMMUNITY_API GlobalUsings|API GlobalUsings]]
- [[_COMMUNITY_API AssemblyInfo|API AssemblyInfo]]
- [[_COMMUNITY_DigitalStore Enum|DigitalStore Enum]]
- [[_COMMUNITY_MovieFormat Enum|MovieFormat Enum]]
- [[_COMMUNITY_MusicFormat Enum|MusicFormat Enum]]
- [[_COMMUNITY_Domain GlobalUsings|Domain GlobalUsings]]
- [[_COMMUNITY_Domain AssemblyInfo|Domain AssemblyInfo]]
- [[_COMMUNITY_Community 66|Community 66]]
- [[_COMMUNITY_Community 67|Community 67]]
- [[_COMMUNITY_Community 68|Community 68]]
- [[_COMMUNITY_Community 69|Community 69]]
- [[_COMMUNITY_Community 70|Community 70]]

## God Nodes (most connected - your core abstractions)
1. `MoviesEndpointsTests` - 30 edges
2. `GamesEndpointsTests` - 20 edges
3. `MusicEndpointsTests` - 20 edges
4. `Field()` - 12 edges
5. `docs/architecture.md` - 12 edges
6. `docs/data-model.md` - 12 edges
7. `App (root router component)` - 11 edges
8. `CollectifyDbContext` - 11 edges
9. `AuthEndpointsTests` - 10 edges
10. `useAuth()` - 10 edges

## Surprising Connections (you probably didn't know these)
- `Collectify.Domain.csproj` --implements--> `Collectify.Domain (Core layer, BCL only)`  [INFERRED]
  src/server/Collectify.Domain/obj/Debug/net10.0/Collectify.Domain.csproj.FileListAbsolute.txt → docs/architecture.md
- `App (root router component)` --calls--> `useAuth()`  [EXTRACTED]
  src/client/App.tsx → client/api/auth.ts
- `useAuth()` --calls--> `api() fetch helper`  [EXTRACTED]
  client/api/auth.ts → src/client/api/client.ts
- `useAuth()` --implements--> `AuthState interface`  [EXTRACTED]
  client/api/auth.ts → src/client/api/auth.ts
- `useSetup()` --calls--> `api() fetch helper`  [EXTRACTED]
  client/api/auth.ts → src/client/api/client.ts

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

## Communities (71 total, 28 thin omitted)

### Community 0 - "Client Data Hooks & DTOs"
Cohesion: 0.1
Nodes (31): api() fetch helper, useCreate(), useDelete(), useItem(), useList(), useUpdate(), Album interface, DIGITAL_STORES list (+23 more)

### Community 2 - "Domain Model & Enums"
Cohesion: 0.12
Nodes (31): docs/architecture.md, Collectify.Api.csproj, Collectify.Domain.csproj, Collectify.Infrastructure.csproj, Collectify.Tests.csproj, Collectify.Api (Presentation layer / composition root), Clean / Onion Architecture (layered backend), CollectifyDbContext (+23 more)

### Community 3 - "Client Auth & API Client"
Cohesion: 0.08
Nodes (26): AppUser : IdentityUser, ASP.NET Core Identity + cookie auth, Collectify (self-hostable media tracker), Content-Security-Policy and security headers, CSRF mitigation (SameSite=Lax cookie + same-origin), Data Protection keys persisted to /data/keys, collectify-data Docker named volume (/data), Multi-stage Docker (single image, port 8080) (+18 more)

### Community 4 - "Server Endpoints & Persistence"
Cohesion: 0.19
Nodes (10): Button(), commit(), Field(), Input(), Label(), onKey(), SectionHeading(), Select() (+2 more)

### Community 7 - "Initial EF Migration"
Cohesion: 0.27
Nodes (20): AlbumDto record, AppUser (IdentityUser), AuthEndpoints, CollectifyDbContext, CollectifyDbContextModelSnapshot, DigitalStore enum, Game entity, GameDto record (+12 more)

### Community 8 - "Games Endpoints"
Cohesion: 0.18
Nodes (13): AuthState interface, useAuth(), useLogin(), useLogout(), useSetup(), /api/auth/* server endpoints, App(), Layout (top nav shell) (+5 more)

### Community 9 - "Movies Endpoints"
Cohesion: 0.18
Nodes (5): Migration, Collectify.Infrastructure.Data.Migrations, InitialCreate, AddPersonalAcquisitionAndTagFields, Collectify.Infrastructure.Data.Migrations

### Community 12 - "DbContext Configuration"
Cohesion: 0.22
Nodes (3): TestExtensions, JsonSerializerOptions, string

### Community 13 - "Migration Designer"
Cohesion: 0.25
Nodes (3): api(), ApiError, useTags()

### Community 17 - "API Entry Point"
Cohesion: 0.33
Nodes (3): CollectifyApiFactory, SqliteConnection, WebApplicationFactory

### Community 18 - "Game Entity"
Cohesion: 0.4
Nodes (3): Collectify.Infrastructure.Data.Migrations, CollectifyDbContextModelSnapshot, ModelSnapshot

### Community 19 - "Lookup Cache"
Cohesion: 0.4
Nodes (3): HealthEndpointTests, CollectifyApiFactory, IClassFixture

## Knowledge Gaps
- **42 isolated node(s):** `Program`, `Game`, `LookupCacheEntry`, `Movie`, `MusicAlbum` (+37 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **28 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `App (root router component)` connect `Client Data Hooks & DTOs` to `Games Endpoints`?**
  _High betweenness centrality (0.009) - this node is a cross-community bridge._
- **Why does `docs/architecture.md` connect `Domain Model & Enums` to `Client Auth & API Client`?**
  _High betweenness centrality (0.008) - this node is a cross-community bridge._
- **Why does `useAuth()` connect `Games Endpoints` to `Client Data Hooks & DTOs`?**
  _High betweenness centrality (0.007) - this node is a cross-community bridge._
- **What connects `Program`, `Game`, `LookupCacheEntry` to the rest of the system?**
  _42 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Client Data Hooks & DTOs` be split into smaller, more focused modules?**
  _Cohesion score 0.1 - nodes in this community are weakly interconnected._
- **Should `Project Docs & Build Files` be split into smaller, more focused modules?**
  _Cohesion score 0.11 - nodes in this community are weakly interconnected._
- **Should `Domain Model & Enums` be split into smaller, more focused modules?**
  _Cohesion score 0.12 - nodes in this community are weakly interconnected._