# Graph Report - server  (2026-05-05)

## Corpus Check
- 26 files · ~4,937 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 64 nodes · 44 edges · 26 communities (13 shown, 13 thin omitted)
- Extraction: 100% EXTRACTED · 0% INFERRED · 0% AMBIGUOUS
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `bc718292`
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

## God Nodes (most connected - your core abstractions)
1. `GamesEndpoints` - 4 edges
2. `MoviesEndpoints` - 4 edges
3. `MusicEndpoints` - 4 edges
4. `InitialCreate` - 4 edges
5. `CollectifyDbContext` - 3 edges
6. `CollectifyDbContextModelSnapshot` - 3 edges
7. `AuthEndpoints` - 2 edges
8. `InitialCreate` - 2 edges
9. `AppUser` - 2 edges
10. `UnitTest1` - 2 edges

## Surprising Connections (you probably didn't know these)
- None detected - all connections are within the same source files.

## Communities (26 total, 13 thin omitted)

### Community 0 - "Community 0"
Cohesion: 0.33
Nodes (3): Migration, Collectify.Infrastructure.Data.Migrations, InitialCreate

### Community 4 - "Community 4"
Cohesion: 0.4
Nodes (3): Collectify.Infrastructure.Data.Migrations, CollectifyDbContextModelSnapshot, ModelSnapshot

## Knowledge Gaps
- **8 isolated node(s):** `Program`, `Game`, `LookupCacheEntry`, `Movie`, `MusicAlbum` (+3 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **13 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **What connects `Program`, `Game`, `LookupCacheEntry` to the rest of the system?**
  _8 weakly-connected nodes found - possible documentation gaps or missing edges._