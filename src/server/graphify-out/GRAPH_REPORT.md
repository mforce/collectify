# Graph Report - server  (2026-05-06)

## Corpus Check
- 70 files · ~18,663 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 349 nodes · 381 edges · 63 communities (34 shown, 29 thin omitted)
- Extraction: 100% EXTRACTED · 0% INFERRED · 0% AMBIGUOUS
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `7e85c2ec`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- [[_COMMUNITY_Community 0|Community 0]]
- [[_COMMUNITY_Community 1|Community 1]]
- [[_COMMUNITY_Community 2|Community 2]]
- [[_COMMUNITY_Community 3|Community 3]]
- [[_COMMUNITY_Community 4|Community 4]]
- [[_COMMUNITY_Community 5|Community 5]]
- [[_COMMUNITY_Community 6|Community 6]]
- [[_COMMUNITY_Community 7|Community 7]]
- [[_COMMUNITY_Community 8|Community 8]]
- [[_COMMUNITY_Community 9|Community 9]]
- [[_COMMUNITY_Community 10|Community 10]]
- [[_COMMUNITY_Community 11|Community 11]]
- [[_COMMUNITY_Community 12|Community 12]]
- [[_COMMUNITY_Community 13|Community 13]]
- [[_COMMUNITY_Community 14|Community 14]]
- [[_COMMUNITY_Community 15|Community 15]]
- [[_COMMUNITY_Community 16|Community 16]]
- [[_COMMUNITY_Community 17|Community 17]]
- [[_COMMUNITY_Community 18|Community 18]]
- [[_COMMUNITY_Community 19|Community 19]]
- [[_COMMUNITY_Community 20|Community 20]]
- [[_COMMUNITY_Community 21|Community 21]]
- [[_COMMUNITY_Community 22|Community 22]]
- [[_COMMUNITY_Community 23|Community 23]]
- [[_COMMUNITY_Community 24|Community 24]]
- [[_COMMUNITY_Community 25|Community 25]]
- [[_COMMUNITY_Community 26|Community 26]]
- [[_COMMUNITY_Community 27|Community 27]]
- [[_COMMUNITY_Community 28|Community 28]]
- [[_COMMUNITY_Community 29|Community 29]]
- [[_COMMUNITY_Community 30|Community 30]]
- [[_COMMUNITY_Community 31|Community 31]]
- [[_COMMUNITY_Community 32|Community 32]]
- [[_COMMUNITY_Community 33|Community 33]]
- [[_COMMUNITY_Community 34|Community 34]]
- [[_COMMUNITY_Community 35|Community 35]]
- [[_COMMUNITY_Community 36|Community 36]]
- [[_COMMUNITY_Community 37|Community 37]]

## God Nodes (most connected - your core abstractions)
1. `MoviesEndpointsTests` - 32 edges
2. `GamesEndpointsTests` - 20 edges
3. `MusicEndpointsTests` - 20 edges
4. `TmdbMovieProviderTests` - 17 edges
5. `CoverImageStoreTests` - 14 edges
6. `LookupCacheTests` - 13 edges
7. `TmdbMovieProvider` - 11 edges
8. `CoverImageStore` - 10 edges
9. `AuthEndpointsTests` - 10 edges
10. `TagEndpointsTests` - 8 edges

## Surprising Connections (you probably didn't know these)
- `LookupCache` --references--> `JsonSerializerOptions`  [EXTRACTED]
  Collectify.Infrastructure/Lookup/ILookupCache.cs → tests/Collectify.Tests/Infrastructure/TestExtensions.cs
- `MetadataLookupOptions` --references--> `string`  [EXTRACTED]
  Collectify.Infrastructure/Lookup/MetadataLookupOptions.cs → tests/Collectify.Tests/Infrastructure/CoverImageStoreTests.cs
- `TmdbMovieProvider` --references--> `string`  [EXTRACTED]
  Collectify.Infrastructure/Lookup/Tmdb/TmdbMovieProvider.cs → tests/Collectify.Tests/Infrastructure/CoverImageStoreTests.cs
- `CoverImageStore` --references--> `string`  [EXTRACTED]
  Collectify.Infrastructure/Lookup/Images/CoverImageStore.cs → tests/Collectify.Tests/Infrastructure/CoverImageStoreTests.cs
- `LookupCache` --references--> `CollectifyDbContext`  [EXTRACTED]
  Collectify.Infrastructure/Lookup/ILookupCache.cs → Collectify.Infrastructure/Lookup/Images/CoverImageStore.cs

## Communities (63 total, 29 thin omitted)

### Community 1 - "Community 1"
Cohesion: 0.13
Nodes (5): DbContextOptions, FakeTimeProvider, IDisposable, LookupCacheTests, TmdbMovieProviderTests

### Community 2 - "Community 2"
Cohesion: 0.1
Nodes (14): byte, HttpMessageHandler, HttpStatusCode, IHttpClientFactory, CoverImageStore, ICoverImageStore, SingleClientFactory, StubHandler (+6 more)

### Community 5 - "Community 5"
Cohesion: 0.11
Nodes (11): HttpClient, IGameMetadataProvider, ILogger, ILookupCache, IMovieMetadataProvider, IMusicMetadataProvider, MetadataLookupOptions, StubGameProvider (+3 more)

### Community 6 - "Community 6"
Cohesion: 0.14
Nodes (6): ICoverImageStore, CollectifyApiFactory, FakeCoverImageStore, CoverImageStoreTests, SqliteConnection, WebApplicationFactory

### Community 7 - "Community 7"
Cohesion: 0.12
Nodes (6): CollectifyDbContext, TestExtensions, JsonSerializerOptions, ILookupCache, LookupCache, TimeProvider

### Community 8 - "Community 8"
Cohesion: 0.12
Nodes (7): Migration, Collectify.Infrastructure.Data.Migrations, InitialCreate, AddPersonalAcquisitionAndTagFields, Collectify.Infrastructure.Data.Migrations, AddCoverImages, Collectify.Infrastructure.Data.Migrations

### Community 11 - "Community 11"
Cohesion: 0.29
Nodes (3): IGameMetadataProvider, IMovieMetadataProvider, IMusicMetadataProvider

### Community 17 - "Community 17"
Cohesion: 0.4
Nodes (3): Collectify.Infrastructure.Data.Migrations, CollectifyDbContextModelSnapshot, ModelSnapshot

### Community 18 - "Community 18"
Cohesion: 0.4
Nodes (3): HealthEndpointTests, CollectifyApiFactory, IClassFixture

## Knowledge Gaps
- **23 isolated node(s):** `Program`, `Game`, `LookupCacheEntry`, `Movie`, `MusicAlbum` (+18 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **29 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `string` connect `Community 2` to `Community 5`, `Community 7`?**
  _High betweenness centrality (0.055) - this node is a cross-community bridge._
- **Why does `TmdbMovieProviderTests` connect `Community 1` to `Community 2`, `Community 6`?**
  _High betweenness centrality (0.031) - this node is a cross-community bridge._
- **Why does `CoverImageStoreTests` connect `Community 6` to `Community 1`, `Community 2`?**
  _High betweenness centrality (0.030) - this node is a cross-community bridge._
- **What connects `Program`, `Game`, `LookupCacheEntry` to the rest of the system?**
  _23 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Community 0` be split into smaller, more focused modules?**
  _Cohesion score 0.1 - nodes in this community are weakly interconnected._
- **Should `Community 1` be split into smaller, more focused modules?**
  _Cohesion score 0.13 - nodes in this community are weakly interconnected._
- **Should `Community 2` be split into smaller, more focused modules?**
  _Cohesion score 0.1 - nodes in this community are weakly interconnected._