# Graph Report - collectify  (2026-05-07)

## Corpus Check
- 117 files · ~43,735 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 724 nodes · 1044 edges · 98 communities (57 shown, 41 thin omitted)
- Extraction: 95% EXTRACTED · 5% INFERRED · 0% AMBIGUOUS · INFERRED: 54 edges (avg confidence: 0.88)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `f3f78d71`
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
- [[_COMMUNITY_Infrastructure GlobalUsings|Infrastructure GlobalUsings]]
- [[_COMMUNITY_Infrastructure AssemblyInfo|Infrastructure AssemblyInfo]]
- [[_COMMUNITY_Tests GlobalUsings|Tests GlobalUsings]]
- [[_COMMUNITY_Tests AssemblyInfo|Tests AssemblyInfo]]
- [[_COMMUNITY_Test Placeholder|Test Placeholder]]
- [[_COMMUNITY_API GlobalUsings (gen)|API GlobalUsings (gen)]]
- [[_COMMUNITY_API AssemblyInfo (gen)|API AssemblyInfo (gen)]]
- [[_COMMUNITY_Domain GlobalUsings (gen)|Domain GlobalUsings (gen)]]
- [[_COMMUNITY_Domain AssemblyInfo (gen)|Domain AssemblyInfo (gen)]]
- [[_COMMUNITY_Community 44|Community 44]]
- [[_COMMUNITY_Community 45|Community 45]]
- [[_COMMUNITY_Community 46|Community 46]]
- [[_COMMUNITY_Community 47|Community 47]]
- [[_COMMUNITY_Community 48|Community 48]]
- [[_COMMUNITY_Community 49|Community 49]]
- [[_COMMUNITY_Community 50|Community 50]]
- [[_COMMUNITY_Community 51|Community 51]]
- [[_COMMUNITY_Community 55|Community 55]]
- [[_COMMUNITY_Community 93|Community 93]]
- [[_COMMUNITY_Community 94|Community 94]]
- [[_COMMUNITY_Community 95|Community 95]]
- [[_COMMUNITY_Community 96|Community 96]]
- [[_COMMUNITY_Community 97|Community 97]]

## God Nodes (most connected - your core abstractions)
1. `TmdbMovieProviderTests` - 34 edges
2. `MoviesEndpointsTests` - 32 edges
3. `IgdbGameProviderTests` - 26 edges
4. `LookupEndpointsTests` - 22 edges
5. `GamesEndpointsTests` - 20 edges
6. `MusicEndpointsTests` - 20 edges
7. `MusicBrainzMusicProviderTests` - 20 edges
8. `string` - 15 edges
9. `TmdbMovieProvider` - 15 edges
10. `IgdbGameProvider` - 15 edges

## Surprising Connections (you probably didn't know these)
- `CollectifyDbContextModelSnapshot` --references--> `CollectifyDbContext`  [INFERRED]
  src/server/Collectify.Infrastructure/Data/Migrations/CollectifyDbContextModelSnapshot.cs → server/Collectify.Infrastructure/Lookup/Images/CoverImageStore.cs
- `Dashboard page` --calls--> `useList()`  [EXTRACTED]
  src/client/pages/Dashboard.tsx → client/api/collection.ts
- `Collectify.Domain.csproj` --implements--> `Collectify.Domain (Core layer, BCL only)`  [INFERRED]
  src/server/Collectify.Domain/obj/Debug/net10.0/Collectify.Domain.csproj.FileListAbsolute.txt → docs/architecture.md
- `CollectifyDbContext` --references--> `LookupCacheEntry entity`  [EXTRACTED]
  server/Collectify.Infrastructure/Lookup/Images/CoverImageStore.cs → src/server/Collectify.Domain/Entities/LookupCacheEntry.cs
- `InitialCreate migration` --implements--> `CollectifyDbContext`  [INFERRED]
  src/server/Collectify.Infrastructure/Data/Migrations/20260505174247_InitialCreate.cs → server/Collectify.Infrastructure/Lookup/Images/CoverImageStore.cs

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

## Communities (98 total, 41 thin omitted)

### Community 0 - "Client Data Hooks & DTOs"
Cohesion: 0.05
Nodes (57): docs/architecture.md, Collectify.Api.csproj, Collectify.Domain.csproj, Collectify.Infrastructure.csproj, Collectify.Tests.csproj, Collectify.Api (Presentation layer / composition root), AppUser : IdentityUser, ASP.NET Core Identity + cookie auth (+49 more)

### Community 1 - "Project Docs & Build Files"
Cohesion: 0.07
Nodes (10): DbContextOptions, FakeTimeProvider, ICoverImageStore, CollectifyApiFactory, FakeCoverImageStore, CoverImageStoreTests, LookupCacheTests, MusicBrainzMusicProviderTests (+2 more)

### Community 2 - "Domain Model & Enums"
Cohesion: 0.07
Nodes (13): DateTimeOffset, HttpClient, IDisposable, IgdbAuth, IgdbGameProvider, ILogger, ILookupCache, CoverImageStore (+5 more)

### Community 3 - "Client Auth & API Client"
Cohesion: 0.06
Nodes (9): IGameMetadataProvider, IMovieMetadataProvider, IMusicMetadataProvider, ScriptedGameProvider, ScriptedMovieProvider, ScriptedMusicProvider, StubGameProvider, StubMovieProvider (+1 more)

### Community 4 - "Server Endpoints & Persistence"
Cohesion: 0.08
Nodes (20): byte, HttpMessageHandler, HttpStatusCode, IHttpClientFactory, IIgdbAuth, SingleClientFactory, StubHandler, FakeAuth (+12 more)

### Community 6 - "Security & Deployment"
Cohesion: 0.13
Nodes (24): api() fetch helper, useCreate(), useDelete(), useItem(), useList(), useUpdate(), Album interface, DIGITAL_STORES list (+16 more)

### Community 8 - "Games Endpoints"
Cohesion: 0.15
Nodes (16): Button(), commit(), Field(), Input(), Label(), onKey(), SectionHeading(), Select() (+8 more)

### Community 12 - "DbContext Configuration"
Cohesion: 0.17
Nodes (15): AuthState interface, useAuth(), useLogin(), useLogout(), useSetup(), /api/auth/* server endpoints, App(), Layout (top nav shell) (+7 more)

### Community 14 - "Auth Endpoints"
Cohesion: 0.27
Nodes (20): AlbumDto record, AppUser (IdentityUser), AuthEndpoints, CollectifyDbContext, CollectifyDbContextModelSnapshot, DigitalStore enum, Game entity, GameDto record (+12 more)

### Community 15 - "AppUser Identity"
Cohesion: 0.15
Nodes (13): MOVIE_FORMAT_FLAGS bitfield map, MUSIC_FORMATS list, App (root router component), CollectionList (generic listing), index.html (root mount), main.tsx (React entry), QueryClient (TanStack Query), Dashboard page (+5 more)

### Community 16 - "Unit Test Stub"
Cohesion: 0.12
Nodes (7): Migration, Collectify.Infrastructure.Data.Migrations, InitialCreate, AddPersonalAcquisitionAndTagFields, Collectify.Infrastructure.Data.Migrations, AddCoverImages, Collectify.Infrastructure.Data.Migrations

### Community 17 - "API Entry Point"
Cohesion: 0.13
Nodes (5): TestExtensions, JsonSerializerOptions, ILookupCache, LookupCache, TimeProvider

### Community 18 - "Game Entity"
Cohesion: 0.18
Nodes (3): IGameMetadataProvider, IMovieMetadataProvider, IMusicMetadataProvider

### Community 20 - "Movie Entity"
Cohesion: 0.2
Nodes (5): Card(), api(), ApiError, useDeleteTag(), useTags()

### Community 22 - "Frontend Build Tooling"
Cohesion: 0.25
Nodes (3): api(), ApiError, useTags()

### Community 27 - "Client Types"
Cohesion: 0.4
Nodes (3): Collectify.Infrastructure.Data.Migrations, CollectifyDbContextModelSnapshot, ModelSnapshot

### Community 28 - "API GlobalUsings"
Cohesion: 0.4
Nodes (3): HealthEndpointTests, CollectifyApiFactory, IClassFixture

## Knowledge Gaps
- **50 isolated node(s):** `Game`, `LookupCacheEntry`, `Movie`, `MusicAlbum`, `Tag` (+45 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **41 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `string` connect `Server Endpoints & Persistence` to `Project Docs & Build Files`, `Domain Model & Enums`, `Initial EF Migration`, `Migration Designer`, `API Entry Point`?**
  _High betweenness centrality (0.068) - this node is a cross-community bridge._
- **Why does `TmdbMovieProviderTests` connect `Initial EF Migration` to `Project Docs & Build Files`, `Domain Model & Enums`, `Server Endpoints & Persistence`?**
  _High betweenness centrality (0.030) - this node is a cross-community bridge._
- **Why does `CoverImageStore` connect `Domain Model & Enums` to `Server Endpoints & Persistence`, `Auth Endpoints`?**
  _High betweenness centrality (0.024) - this node is a cross-community bridge._
- **What connects `Game`, `LookupCacheEntry`, `Movie` to the rest of the system?**
  _50 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Client Data Hooks & DTOs` be split into smaller, more focused modules?**
  _Cohesion score 0.05 - nodes in this community are weakly interconnected._
- **Should `Project Docs & Build Files` be split into smaller, more focused modules?**
  _Cohesion score 0.07 - nodes in this community are weakly interconnected._
- **Should `Domain Model & Enums` be split into smaller, more focused modules?**
  _Cohesion score 0.07 - nodes in this community are weakly interconnected._