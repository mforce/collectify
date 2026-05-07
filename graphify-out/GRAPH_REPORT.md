# Graph Report - collectify  (2026-05-06)

## Corpus Check
- 91 files · ~30,152 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 559 nodes · 778 edges · 83 communities (49 shown, 34 thin omitted)
- Extraction: 93% EXTRACTED · 7% INFERRED · 0% AMBIGUOUS · INFERRED: 54 edges (avg confidence: 0.88)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `7e85c2ec`
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
- [[_COMMUNITY_API AssemblyInfo (gen)|API AssemblyInfo (gen)]]
- [[_COMMUNITY_Domain GlobalUsings (gen)|Domain GlobalUsings (gen)]]
- [[_COMMUNITY_Community 78|Community 78]]
- [[_COMMUNITY_Community 79|Community 79]]
- [[_COMMUNITY_Community 80|Community 80]]
- [[_COMMUNITY_Community 81|Community 81]]
- [[_COMMUNITY_Community 82|Community 82]]

## God Nodes (most connected - your core abstractions)
1. `MoviesEndpointsTests` - 32 edges
2. `GamesEndpointsTests` - 20 edges
3. `MusicEndpointsTests` - 20 edges
4. `TmdbMovieProviderTests` - 17 edges
5. `CoverImageStoreTests` - 14 edges
6. `CollectifyDbContext` - 13 edges
7. `LookupCacheTests` - 13 edges
8. `Field()` - 13 edges
9. `docs/architecture.md` - 12 edges
10. `docs/data-model.md` - 12 edges

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

## Communities (83 total, 34 thin omitted)

### Community 0 - "Client Data Hooks & DTOs"
Cohesion: 0.05
Nodes (57): docs/architecture.md, Collectify.Api.csproj, Collectify.Domain.csproj, Collectify.Infrastructure.csproj, Collectify.Tests.csproj, Collectify.Api (Presentation layer / composition root), AppUser : IdentityUser, ASP.NET Core Identity + cookie auth (+49 more)

### Community 1 - "Project Docs & Build Files"
Cohesion: 0.07
Nodes (11): DbContextOptions, FakeTimeProvider, ICoverImageStore, IDisposable, CollectifyApiFactory, FakeCoverImageStore, CoverImageStoreTests, LookupCacheTests (+3 more)

### Community 2 - "Domain Model & Enums"
Cohesion: 0.08
Nodes (29): AuthState interface, useAuth(), useLogin(), useLogout(), useSetup(), api(), ApiError, useTags() (+21 more)

### Community 3 - "Client Auth & API Client"
Cohesion: 0.09
Nodes (23): api() fetch helper, useCreate(), useDelete(), useItem(), useList(), useUpdate(), Game interface, MediaType union (movies|music|games) (+15 more)

### Community 4 - "Server Endpoints & Persistence"
Cohesion: 0.07
Nodes (19): byte, HttpClient, HttpMessageHandler, HttpStatusCode, IHttpClientFactory, ILogger, ILookupCache, CoverImageStore (+11 more)

### Community 6 - "Security & Deployment"
Cohesion: 0.13
Nodes (19): Album interface, DIGITAL_STORES list, MOVIE_FORMAT_FLAGS bitfield map, MUSIC_FORMATS list, AlbumForm, GameForm, MovieForm, Button() (+11 more)

### Community 9 - "Movies Endpoints"
Cohesion: 0.27
Nodes (20): AlbumDto record, AppUser (IdentityUser), AuthEndpoints, CollectifyDbContext, CollectifyDbContextModelSnapshot, DigitalStore enum, Game entity, GameDto record (+12 more)

### Community 10 - "Music Endpoints"
Cohesion: 0.12
Nodes (7): Migration, Collectify.Infrastructure.Data.Migrations, InitialCreate, AddPersonalAcquisitionAndTagFields, Collectify.Infrastructure.Data.Migrations, AddCoverImages, Collectify.Infrastructure.Data.Migrations

### Community 11 - "EF Model Snapshot"
Cohesion: 0.13
Nodes (5): TestExtensions, JsonSerializerOptions, ILookupCache, LookupCache, TimeProvider

### Community 13 - "Migration Designer"
Cohesion: 0.2
Nodes (6): IGameMetadataProvider, IMovieMetadataProvider, IMusicMetadataProvider, StubGameProvider, StubMovieProvider, StubMusicProvider

### Community 15 - "AppUser Identity"
Cohesion: 0.29
Nodes (3): IGameMetadataProvider, IMovieMetadataProvider, IMusicMetadataProvider

### Community 21 - "MusicAlbum Entity"
Cohesion: 0.4
Nodes (3): Collectify.Infrastructure.Data.Migrations, CollectifyDbContextModelSnapshot, ModelSnapshot

### Community 22 - "Frontend Build Tooling"
Cohesion: 0.4
Nodes (3): HealthEndpointTests, CollectifyApiFactory, IClassFixture

## Knowledge Gaps
- **50 isolated node(s):** `Game`, `LookupCacheEntry`, `Movie`, `MusicAlbum`, `Tag` (+45 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **34 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `string` connect `Server Endpoints & Persistence` to `EF Model Snapshot`?**
  _High betweenness centrality (0.029) - this node is a cross-community bridge._
- **Why does `CoverImageStore` connect `Server Endpoints & Persistence` to `Movies Endpoints`?**
  _High betweenness centrality (0.020) - this node is a cross-community bridge._
- **Why does `CollectifyDbContext` connect `Movies Endpoints` to `EF Model Snapshot`, `Server Endpoints & Persistence`?**
  _High betweenness centrality (0.017) - this node is a cross-community bridge._
- **What connects `Game`, `LookupCacheEntry`, `Movie` to the rest of the system?**
  _50 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Client Data Hooks & DTOs` be split into smaller, more focused modules?**
  _Cohesion score 0.05 - nodes in this community are weakly interconnected._
- **Should `Project Docs & Build Files` be split into smaller, more focused modules?**
  _Cohesion score 0.07 - nodes in this community are weakly interconnected._
- **Should `Domain Model & Enums` be split into smaller, more focused modules?**
  _Cohesion score 0.08 - nodes in this community are weakly interconnected._