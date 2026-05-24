# Graph Report - collectify  (2026-05-23)

## Corpus Check
- 152 files · ~69,879 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 967 nodes · 1403 edges · 118 communities (70 shown, 48 thin omitted)
- Extraction: 95% EXTRACTED · 5% INFERRED · 0% AMBIGUOUS · INFERRED: 67 edges (avg confidence: 0.86)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `4e4ff490`
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
- [[_COMMUNITY_Community 52|Community 52]]
- [[_COMMUNITY_Community 53|Community 53]]
- [[_COMMUNITY_Community 54|Community 54]]
- [[_COMMUNITY_Community 55|Community 55]]
- [[_COMMUNITY_Community 56|Community 56]]
- [[_COMMUNITY_Community 57|Community 57]]
- [[_COMMUNITY_Community 59|Community 59]]
- [[_COMMUNITY_Community 60|Community 60]]
- [[_COMMUNITY_Community 61|Community 61]]
- [[_COMMUNITY_Community 62|Community 62]]
- [[_COMMUNITY_Community 63|Community 63]]
- [[_COMMUNITY_Community 64|Community 64]]
- [[_COMMUNITY_Community 65|Community 65]]
- [[_COMMUNITY_Community 66|Community 66]]
- [[_COMMUNITY_Community 72|Community 72]]
- [[_COMMUNITY_Community 113|Community 113]]
- [[_COMMUNITY_Community 114|Community 114]]
- [[_COMMUNITY_Community 115|Community 115]]
- [[_COMMUNITY_Community 116|Community 116]]
- [[_COMMUNITY_Community 117|Community 117]]

## God Nodes (most connected - your core abstractions)
1. `TmdbMovieProviderTests` - 39 edges
2. `MoviesEndpointsTests` - 37 edges
3. `IgdbGameProviderTests` - 33 edges
4. `LookupEndpointsTests` - 27 edges
5. `MusicBrainzMusicProviderTests` - 25 edges
6. `GamesEndpointsTests` - 22 edges
7. `string` - 21 edges
8. `MusicEndpointsTests` - 21 edges
9. `CoversEndpointsTests` - 19 edges
10. `TmdbMovieProvider` - 17 edges

## Surprising Connections (you probably didn't know these)
- `CollectifyDbContextModelSnapshot` --references--> `CollectifyDbContext`  [INFERRED]
  src/server/Collectify.Infrastructure/Data/Migrations/CollectifyDbContextModelSnapshot.cs → server/tests/Collectify.Tests/Infrastructure/CollectifyApiFactory.cs
- `Dashboard page` --calls--> `useList()`  [EXTRACTED]
  src/client/pages/Dashboard.tsx → client/api/collection.ts
- `Collectify.Domain.csproj` --implements--> `Collectify.Domain (Core layer, BCL only)`  [INFERRED]
  src/server/Collectify.Domain/obj/Debug/net10.0/Collectify.Domain.csproj.FileListAbsolute.txt → docs/architecture.md
- `CollectifyDbContext` --references--> `LookupCacheEntry entity`  [EXTRACTED]
  server/tests/Collectify.Tests/Infrastructure/CollectifyApiFactory.cs → src/server/Collectify.Domain/Entities/LookupCacheEntry.cs
- `InitialCreate migration` --implements--> `CollectifyDbContext`  [INFERRED]
  src/server/Collectify.Infrastructure/Data/Migrations/20260505174247_InitialCreate.cs → server/tests/Collectify.Tests/Infrastructure/CollectifyApiFactory.cs

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

## Communities (118 total, 48 thin omitted)

### Community 0 - "Client Data Hooks & DTOs"
Cohesion: 0.05
Nodes (14): DateTimeOffset, DbContextOptions, FakeTimeProvider, IDisposable, IgdbAuth, CollectifyApiFactory, CoverImageGarbageCollectorTests, CoverImageStoreTests (+6 more)

### Community 1 - "Project Docs & Build Files"
Cohesion: 0.05
Nodes (57): docs/architecture.md, Collectify.Api.csproj, Collectify.Domain.csproj, Collectify.Infrastructure.csproj, Collectify.Tests.csproj, Collectify.Api (Presentation layer / composition root), AppUser : IdentityUser, ASP.NET Core Identity + cookie auth (+49 more)

### Community 2 - "Domain Model & Enums"
Cohesion: 0.06
Nodes (34): useTags(), DIGITAL_STORES list, enrichFromMb(), fetchByMbid(), importLookup(), patch(), runLookup(), setQuery() (+26 more)

### Community 3 - "Client Auth & API Client"
Cohesion: 0.05
Nodes (26): byte, HealthEndpoints, HttpMessageHandler, HttpStatusCode, AuthOptions, IHttpClientFactory, IIgdbAuth, CoverImageStore (+18 more)

### Community 4 - "Server Endpoints & Persistence"
Cohesion: 0.07
Nodes (11): HttpClient, IgdbGameProvider, ILogger, ILookupCache, CoverImageGarbageCollector, FakeUpcClient, IUpcLookupClient, MetadataLookupOptions (+3 more)

### Community 5 - "Client App Shell & Pages"
Cohesion: 0.05
Nodes (9): IGameMetadataProvider, IMovieMetadataProvider, IMusicMetadataProvider, ScriptedGameProvider, ScriptedMovieProvider, ScriptedMusicProvider, StubGameProvider, StubMovieProvider (+1 more)

### Community 10 - "Music Endpoints"
Cohesion: 0.19
Nodes (22): AlbumDto record, AppUser (IdentityUser), AuthEndpoints, CollectifyDbContext, CollectifyDbContextModelSnapshot, DigitalStore enum, Game entity, GameDto record (+14 more)

### Community 13 - "Migration Designer"
Cohesion: 0.1
Nodes (9): Migration, Collectify.Infrastructure.Data.Migrations, InitialCreate, AddPersonalAcquisitionAndTagFields, Collectify.Infrastructure.Data.Migrations, AddCoverImages, Collectify.Infrastructure.Data.Migrations, Collectify.Infrastructure.Data.Migrations (+1 more)

### Community 14 - "Auth Endpoints"
Cohesion: 0.14
Nodes (12): AuthState interface, useAuth(), useLogout(), /api/auth/* server endpoints, App(), Layout (top nav shell), Layout(), useAuth() (+4 more)

### Community 16 - "Unit Test Stub"
Cohesion: 0.16
Nodes (12): Album interface, Movie interface, MOVIE_FORMAT_FLAGS bitfield map, MUSIC_FORMATS list, AlbumForm, GameForm, MovieForm, Field() (+4 more)

### Community 18 - "Game Entity"
Cohesion: 0.18
Nodes (10): useLogin(), useSetup(), handleDetected(), useToast(), Button(), Card(), Input(), Login() (+2 more)

### Community 20 - "Movie Entity"
Cohesion: 0.13
Nodes (5): TestExtensions, JsonSerializerOptions, ILookupCache, LookupCache, TimeProvider

### Community 21 - "MusicAlbum Entity"
Cohesion: 0.14
Nodes (3): IGameMetadataProvider, IMovieMetadataProvider, IMusicMetadataProvider

### Community 22 - "Frontend Build Tooling"
Cohesion: 0.27
Nodes (8): useList(), MediaType union (movies|music|games), CollectionList(), read(), useViewPreference(), Dashboard(), useDashboard(), useList()

### Community 23 - "React Entry"
Cohesion: 0.27
Nodes (8): api(), api() fetch helper, ApiError, useCreate(), useDelete(), useItem(), useUpdate(), /api/{movies,music,games} server endpoints

### Community 24 - "PostCSS Config"
Cohesion: 0.27
Nodes (11): App (root router component), CollectionList (generic listing), index.html (root mount), main.tsx (React entry), QueryClient (TanStack Query), Dashboard page, GamesList page, Login page (+3 more)

### Community 25 - "Tailwind Config"
Cohesion: 0.22
Nodes (4): api(), ApiError, useDeleteTag(), useTags()

### Community 26 - "Vite Config"
Cohesion: 0.31
Nodes (5): EditPage(), useCreate(), useDelete(), useItem(), useUpdate()

### Community 27 - "Client Types"
Cohesion: 0.2
Nodes (3): Game interface, StatusPill(), gamePlatformLabel()

### Community 28 - "API GlobalUsings"
Cohesion: 0.29
Nodes (5): dismissImpl(), emit(), push(), _resetToasts(), Toaster()

### Community 30 - "DigitalStore Enum"
Cohesion: 0.38
Nodes (3): Dictionary, GamePlatformMapping, GamePlatform

### Community 32 - "MusicFormat Enum"
Cohesion: 0.38
Nodes (3): CoversEndpoints, HashSet, long

### Community 33 - "Domain GlobalUsings"
Cohesion: 0.33
Nodes (3): HealthEndpointTests, CollectifyApiFactory, IClassFixture

### Community 37 - "Tests GlobalUsings"
Cohesion: 0.4
Nodes (3): Collectify.Infrastructure.Data.Migrations, CollectifyDbContextModelSnapshot, ModelSnapshot

## Knowledge Gaps
- **57 isolated node(s):** `LookupCacheEntry`, `Movie`, `MusicAlbum`, `Tag`, `CoverImage` (+52 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **48 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `string` connect `Client Auth & API Client` to `Client Data Hooks & DTOs`, `MusicFormat Enum`, `Server Endpoints & Persistence`, `Initial EF Migration`, `Movies Endpoints`, `AppUser Identity`, `Movie Entity`?**
  _High betweenness centrality (0.083) - this node is a cross-community bridge._
- **Why does `TmdbMovieProviderTests` connect `Initial EF Migration` to `Client Data Hooks & DTOs`, `Client Auth & API Client`?**
  _High betweenness centrality (0.028) - this node is a cross-community bridge._
- **Why does `IgdbGameProviderTests` connect `Movies Endpoints` to `Client Data Hooks & DTOs`, `Client Auth & API Client`?**
  _High betweenness centrality (0.024) - this node is a cross-community bridge._
- **What connects `LookupCacheEntry`, `Movie`, `MusicAlbum` to the rest of the system?**
  _57 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Client Data Hooks & DTOs` be split into smaller, more focused modules?**
  _Cohesion score 0.05 - nodes in this community are weakly interconnected._
- **Should `Project Docs & Build Files` be split into smaller, more focused modules?**
  _Cohesion score 0.05 - nodes in this community are weakly interconnected._
- **Should `Domain Model & Enums` be split into smaller, more focused modules?**
  _Cohesion score 0.06 - nodes in this community are weakly interconnected._