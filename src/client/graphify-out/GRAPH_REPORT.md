# Graph Report - client  (2026-05-05)

## Corpus Check
- 29 files · ~5,804 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 69 nodes · 102 edges · 17 communities (16 shown, 1 thin omitted)
- Extraction: 95% EXTRACTED · 5% INFERRED · 0% AMBIGUOUS · INFERRED: 5 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `f851e27f`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- [[_COMMUNITY_Community 0|Community 0]]
- [[_COMMUNITY_Community 1|Community 1]]
- [[_COMMUNITY_Community 2|Community 2]]
- [[_COMMUNITY_Community 3|Community 3]]
- [[_COMMUNITY_Community 4|Community 4]]
- [[_COMMUNITY_Community 5|Community 5]]

## God Nodes (most connected - your core abstractions)
1. `Input()` - 9 edges
2. `Button()` - 8 edges
3. `Field()` - 8 edges
4. `Card()` - 7 edges
5. `useAuth()` - 5 edges
6. `Textarea()` - 5 edges
7. `Select()` - 5 edges
8. `SectionHeading()` - 5 edges
9. `api()` - 4 edges
10. `useList()` - 4 edges

## Surprising Connections (you probably didn't know these)
- `App()` --calls--> `useAuth()`  [INFERRED]
  App.tsx → api/auth.ts
- `Layout()` --calls--> `useAuth()`  [INFERRED]
  components/Layout.tsx → api/auth.ts
- `Login()` --calls--> `useLogin()`  [INFERRED]
  pages/Login.tsx → api/auth.ts
- `Layout()` --calls--> `useLogout()`  [INFERRED]
  components/Layout.tsx → api/auth.ts
- `Dashboard()` --calls--> `useList()`  [INFERRED]
  pages/Dashboard.tsx → api/collection.ts

## Communities (17 total, 1 thin omitted)

### Community 0 - "Community 0"
Cohesion: 0.32
Nodes (5): Field(), Input(), SectionHeading(), Select(), Textarea()

### Community 1 - "Community 1"
Cohesion: 0.24
Nodes (7): useCreate(), useDelete(), useItem(), useList(), useUpdate(), Card(), Dashboard()

### Community 2 - "Community 2"
Cohesion: 0.29
Nodes (5): useAuth(), useLogout(), useSetup(), App(), Layout()

### Community 3 - "Community 3"
Cohesion: 0.28
Nodes (4): Button(), commit(), onKey(), StatusPill()

### Community 4 - "Community 4"
Cohesion: 0.25
Nodes (3): api(), ApiError, useTags()

## Knowledge Gaps
- **1 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `api()` connect `Community 4` to `Community 1`, `Community 2`?**
  _High betweenness centrality (0.141) - this node is a cross-community bridge._
- **Why does `Card()` connect `Community 1` to `Community 2`, `Community 3`, `Community 5`?**
  _High betweenness centrality (0.135) - this node is a cross-community bridge._
- **Why does `Input()` connect `Community 0` to `Community 2`, `Community 3`, `Community 5`?**
  _High betweenness centrality (0.080) - this node is a cross-community bridge._
- **Are the 2 inferred relationships involving `useAuth()` (e.g. with `App()` and `Layout()`) actually correct?**
  _`useAuth()` has 2 INFERRED edges - model-reasoned connections that need verification._