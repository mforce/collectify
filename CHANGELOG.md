# Changelog

## [0.0.9](https://github.com/mforce/collectify/compare/v0.0.8...v0.0.9) (2026-09-02)


### Features

* **database:** add verified SQLite migration snapshots ([4accc41](https://github.com/mforce/collectify/commit/4accc4141e93dbc7b66fde31886b0d4e5b13f819))
* **database:** back up SQLite before startup migrations ([23ecf51](https://github.com/mforce/collectify/commit/23ecf51143d6f03e01d682cbfb4d321806e90c7f))
* **database:** back up SQLite before startup migrations ([53cf83b](https://github.com/mforce/collectify/commit/53cf83b5b742c2125dfba0652626948e9cf27ddf))


### Bug fixes

* **database:** preserve the current migration snapshot ([16d6dd8](https://github.com/mforce/collectify/commit/16d6dd8bd2dde14b8b694b3c97cdc96ab498b43c))


### Documentation

* **database:** document migration backup recovery ([c8ee805](https://github.com/mforce/collectify/commit/c8ee805b43166f255203d2f21da6a8f2e87d9a35))

## [0.0.8](https://github.com/mforce/collectify/compare/v0.0.7...v0.0.8) (2026-08-27)


### Features

* add enum parity CI check and model Steam Deck as Pc ([38b7539](https://github.com/mforce/collectify/commit/38b75391d0a1eb5d37e62259d94c918c62479443))
* add enum parity CI check and model Steam Deck as Pc ([9a1982e](https://github.com/mforce/collectify/commit/9a1982e8a51d92b9414bb5e37a319548447d2a11))
* **api:** bulk genres replace-set like tags, message-pinned 400 ([#169](https://github.com/mforce/collectify/issues/169)) ([7865843](https://github.com/mforce/collectify/commit/786584398a59f96265ba87d1591363403ecc2e06))
* **api:** generic bulk PATCH endpoint ([#89](https://github.com/mforce/collectify/issues/89)) ([1178740](https://github.com/mforce/collectify/commit/117874062c8aaf7c18fc0a345c18857125c3258f))
* **api:** per-type bulk field whitelists ([#89](https://github.com/mforce/collectify/issues/89)) ([fedf71e](https://github.com/mforce/collectify/commit/fedf71e0a56fdb35fff1d8a7b02c4a04d2fd8b3d))
* **api:** relational genres like tags; Genre entity, resolver, bulk-ready seam, exact filter, schema-only migration ([#169](https://github.com/mforce/collectify/issues/169)) ([6161e5a](https://github.com/mforce/collectify/commit/6161e5a67867cef5e26cdea5a8e00b8a346adc13))
* auto-backfill IGDB metadata for games after Steam import ([#132](https://github.com/mforce/collectify/issues/132)) ([434b80d](https://github.com/mforce/collectify/commit/434b80dd71ca41fb2a0ea21162b10f505b4cb408))
* bulk update of movies, music and games ([#89](https://github.com/mforce/collectify/issues/89)) ([e80f3d8](https://github.com/mforce/collectify/commit/e80f3d82e2f47012cf3b0379680b756c4343c451))
* **cache:** add DistributedCacheAdapter with fault-tolerant distributed cache access ([6d7568a](https://github.com/mforce/collectify/commit/6d7568ab99ea5b02a0c22c18c49c55672cee5f1f))
* **cache:** drop SQLite LookupCache persistence via EF migration ([e87f3af](https://github.com/mforce/collectify/commit/e87f3af88b020003740b0c6bfeef888f377d4661))
* **cache:** migrate lookup contract to write-time TTL across five callers ([a0da318](https://github.com/mforce/collectify/commit/a0da318ebb38507b647e0cde41f9bad81da8a0e8))
* **cache:** replace SQLite lookup cache with distributed cache ([b2579b4](https://github.com/mforce/collectify/commit/b2579b407162cebead3a2925d041c39daec6cc15))
* **cache:** verify DI cache selection, singleton lifetime, and TTL validation ([ca358a3](https://github.com/mforce/collectify/commit/ca358a343b5e1c2da14db110386a69a6093d02b2))
* capture Steam cover + last-played on import and add Steam logo to the page ([b17f3c9](https://github.com/mforce/collectify/commit/b17f3c9108190dc0349a20893e91d10eeb930dd3))
* capture Steam rich metadata (developer/publisher/year/description) on import ([cbaa649](https://github.com/mforce/collectify/commit/cbaa649b3bdff5c16b3055e946c2b1a1c826d5b7))
* **client:** bulk update selection, action bar and modal ([#89](https://github.com/mforce/collectify/issues/89)) ([25c229f](https://github.com/mforce/collectify/commit/25c229fd3b8480b07af362595ef6702e5ea5184a))
* **client:** navigate to /games after a successful Steam import ([f0455a0](https://github.com/mforce/collectify/commit/f0455a09075b9be9b33ee9b7194955f40adf65a4))
* **client:** relational genre chips, exact filter, bulk genres ([#169](https://github.com/mforce/collectify/issues/169)) ([9468c0d](https://github.com/mforce/collectify/commit/9468c0dfdfad8a4aa79660f231f89a6231b9987d))
* **client:** shared lookup protocol, candidate list, and camera hook ([#99](https://github.com/mforce/collectify/issues/99)) ([f13b252](https://github.com/mforce/collectify/commit/f13b2529590dfc851bfd43fe20050142b89bba93))
* **client:** single media registry + shared lookup-and-prefill protocol ([#98](https://github.com/mforce/collectify/issues/98), [#99](https://github.com/mforce/collectify/issues/99)) ([74187ea](https://github.com/mforce/collectify/commit/74187ea0a6a44ae68ac323f3c3e70af3a3a74adc))
* **client:** single media registry keyed by MediaType ([#98](https://github.com/mforce/collectify/issues/98)) ([dbbd940](https://github.com/mforce/collectify/commit/dbbd940eb90f39a153b8015afe66e46a0d8d0a4a))
* **client:** type-specific detail views + rich fields (issue [#88](https://github.com/mforce/collectify/issues/88)) ([2d45128](https://github.com/mforce/collectify/commit/2d45128a235d5ddb88f5e450a7bf4f7f2bd8a467))
* **client:** useBulkUpdate hook and BulkUpdates types ([#89](https://github.com/mforce/collectify/issues/89)) ([320eb12](https://github.com/mforce/collectify/commit/320eb12871e783472ee2deaba91fc641a00511bd))
* **collections:** add collection list sorting ([0712907](https://github.com/mforce/collectify/commit/0712907e18ec1a26b9f43a88701fe5acb0483492))
* **collections:** add per-type server sort fields ([4a17d74](https://github.com/mforce/collectify/commit/4a17d74e90b99c5583cd5d5d0f76f375dc321cac))
* **collections:** add shared responsive sort controls to collection lists ([1ae35d5](https://github.com/mforce/collectify/commit/1ae35d531bb7c13d35863c1b416906cedf6e4a97))
* **collections:** add shared server sort contract for collection lists ([2de8a2b](https://github.com/mforce/collectify/commit/2de8a2b9a5248ea896eb9a8755b44ff800859bff))
* **collections:** add typed client sort state and request identity ([415fb03](https://github.com/mforce/collectify/commit/415fb03a74b3b6e0507930f36a91476926a9f9b9))
* **collections:** show sortable values on list items ([c954663](https://github.com/mforce/collectify/commit/c954663bee293d3d1ddc07a160f367a1c4f4d251))
* connect Steam & import owned games ([60455f7](https://github.com/mforce/collectify/commit/60455f79dbd041f1712ba4ecae289bebdfe7cc36))
* connect Steam & import owned games ([0709e34](https://github.com/mforce/collectify/commit/0709e3408460540aeb22a303ab8152ecc962fbf2))
* **database:** add provider-native PostgreSQL migration lineage ([d51c139](https://github.com/mforce/collectify/commit/d51c1392d1d78e3efa23e0c112e67faeef3f3696))
* **database:** migrate PostgreSQL through EF Core migrations ([0cd506a](https://github.com/mforce/collectify/commit/0cd506a3fd6f9fe99af847a3b79e00a847bd1bba)), closes [#100](https://github.com/mforce/collectify/issues/100)
* **database:** unify SQLite and PostgreSQL migrations ([e0f92ef](https://github.com/mforce/collectify/commit/e0f92efdd2a8f50cd3957f86f7e54643396949d1))
* **games:** allow multiple digital stores per game ([#91](https://github.com/mforce/collectify/issues/91)) ([5881b72](https://github.com/mforce/collectify/commit/5881b72a08e3f5a080720bf05c168d29347faf60))
* **games:** allow multiple digital stores per game ([#91](https://github.com/mforce/collectify/issues/91)) ([7fe4a15](https://github.com/mforce/collectify/commit/7fe4a1551a77a57ff12fbcedd191755c166d1d26))
* normalize genres to relational 1-to-many like tags ([#169](https://github.com/mforce/collectify/issues/169)) ([66a78a1](https://github.com/mforce/collectify/commit/66a78a13defdeac1d2076c994f07ef8946fb1378))
* platform-scoped IGDB matching + edit-page prefill priority ([2c40335](https://github.com/mforce/collectify/commit/2c4033584cbac67310ffad7554e8097a23044c9a))
* **server:** add rich-detail fields for movies, games, music (issue [#88](https://github.com/mforce/collectify/issues/88)) ([c805e7c](https://github.com/mforce/collectify/commit/c805e7c8b597941aa7da441e274cc9f6d3e8b0dc))
* **steam:** add filtered page size controls ([a20a530](https://github.com/mforce/collectify/commit/a20a530312d6fb9aa9573942007de50cf05e9f82))
* **steam:** add hide-imported toggle on import page ([#180](https://github.com/mforce/collectify/issues/180)) ([68d1385](https://github.com/mforce/collectify/commit/68d138510c2f0c6b2d6deba7f2e9523e71540312))
* **steam:** paginate owned-games preview beyond 500-cap ([#181](https://github.com/mforce/collectify/issues/181)) ([243ff48](https://github.com/mforce/collectify/commit/243ff48e597653d149d80752776de7fb6b497f49))
* type-specific detail views with rich provider fields ([#88](https://github.com/mforce/collectify/issues/88)) ([86500eb](https://github.com/mforce/collectify/commit/86500ebd2648793e4be1a80c028bcbffcc6b51ce))
* **ui:** add format/platform style icons ([#90](https://github.com/mforce/collectify/issues/90)) ([9d1a846](https://github.com/mforce/collectify/commit/9d1a8465821e923c3bc4182b0d2b937949aadde6))
* **ui:** add format/platform style icons ([#90](https://github.com/mforce/collectify/issues/90)) ([1016a7a](https://github.com/mforce/collectify/commit/1016a7a8c2b2cd84bcff37e6f0c76cf75907b8e3))


### Bug fixes

* **#169:** fold genre schema into first Postgres migration, sync snapshot, regenerate PG manifests ([bbf4b96](https://github.com/mforce/collectify/commit/bbf4b96b2e0c64c5f94b9027484bcb393a43b4a6))
* address Codex bot round 3 (planner + atomic concurrent guard + rotation) ([597aef4](https://github.com/mforce/collectify/commit/597aef4c3f26e7efddc42003fdd59664ffd520c5))
* address Codex review round 2 (runner [#137](https://github.com/mforce/collectify/issues/137)) ([7bef753](https://github.com/mforce/collectify/commit/7bef753367d248ec0ead6335a4024bc57029296e))
* address Codex/Claude review findings on Steam import ([e0e024c](https://github.com/mforce/collectify/commit/e0e024c1bd281c09b7efc74088c03aa18df0fcfe))
* address Steam import review findings + add DLC parent schema hook ([4a43bdd](https://github.com/mforce/collectify/commit/4a43bdd8bd53c1b09c52cb7141147ee3490b77e3))
* **api,client:** drop stale bulk selections on result change; enum overflow -&gt; 400 ([#89](https://github.com/mforce/collectify/issues/89)) ([d6eecc8](https://github.com/mforce/collectify/commit/d6eecc8c84125f8ec35a38929c107e29f7e800e5))
* **api,client:** review round 1 — bulk tags, enum/number/currency parity, UI guards ([#89](https://github.com/mforce/collectify/issues/89)) ([2815719](https://github.com/mforce/collectify/commit/2815719b1205bfa76971348d7f39361af35eedd2))
* **api,test:** malformed bulk tags 400 + tags/unknown mixed-key and nullable-enum case tests ([#89](https://github.com/mforce/collectify/issues/89)) ([53c2d08](https://github.com/mforce/collectify/commit/53c2d08459136bd27479ecfaeac7b90b8ad5e739))
* **api:** BulkFieldBuilder.Enum must accept JSON string enum values ([#89](https://github.com/mforce/collectify/issues/89)) ([ffe96a8](https://github.com/mforce/collectify/commit/ffe96a8c4cc6f14c21b81c0d262e11044c6e09d4))
* **api:** reject empty members in movie format filter ([#97](https://github.com/mforce/collectify/issues/97)) ([226d12e](https://github.com/mforce/collectify/commit/226d12e0f5b2b86ff64898512304f170721c8f73))
* **api:** reject negative bulk hoursPlayed; pin NullableEnum overflow ([#89](https://github.com/mforce/collectify/issues/89)) ([d6f404d](https://github.com/mforce/collectify/commit/d6f404db3155c336363c9c43359eec1cd22256a6))
* **api:** reject repeated query values on collection filters ([#97](https://github.com/mforce/collectify/issues/97)) ([013360d](https://github.com/mforce/collectify/commit/013360d4cc6b278ae99925cafc93b8c390f1bd39))
* **api:** reject undefined enum values on the write boundary ([#115](https://github.com/mforce/collectify/issues/115)) ([15f46b2](https://github.com/mforce/collectify/commit/15f46b2f8e03350366524f2b9189a3068cc244fc))
* **api:** reject undefined enum values on the write boundary ([#115](https://github.com/mforce/collectify/issues/115)) ([e0fc37d](https://github.com/mforce/collectify/commit/e0fc37d141c44b1fbc498dd98b3ae306e226dcd4))
* **api:** restore list-filter wire contract in generic collection module ([#97](https://github.com/mforce/collectify/issues/97)) ([24363d2](https://github.com/mforce/collectify/commit/24363d27660bc2a3ff053c013866ca274fc85edf))
* **cache:** address review round 1 findings ([04e9a64](https://github.com/mforce/collectify/commit/04e9a64afeb331678e8b4bcba416e00ee370e963))
* **cache:** sanitize cache exceptions and validation guidance ([091f632](https://github.com/mforce/collectify/commit/091f6325c139a0987f502ee6480707342a2f8c30))
* **client:** add vite-env.d.ts for TS7 CSS type declarations ([2e8ad5f](https://github.com/mforce/collectify/commit/2e8ad5f434ac90944f00e9cb0e14b556b48ca884))
* **client:** align enum display labels ([cd35f9f](https://github.com/mforce/collectify/commit/cd35f9f5934e66b6eb540daae32aa6a259ca9975))
* **client:** align enum labels and filter controls ([9467ea4](https://github.com/mforce/collectify/commit/9467ea47e1e6a8584398fd51ce7737c82c4faa1c))
* **client:** drop removed BrowserRouter future prop for react-router v7 ([e1d515c](https://github.com/mforce/collectify/commit/e1d515cdd58687f507bef07a654e3b955b204f16))
* **client:** drop removed BrowserRouter future prop for react-router v7 ([2e69a15](https://github.com/mforce/collectify/commit/2e69a15d5abd11191ad5fb3118225c83cb3efbd1))
* **client:** expose tags and align filter counts ([d3208d5](https://github.com/mforce/collectify/commit/d3208d58cf5dcfaa517f66df6e00dd8cc277b0ef))
* **client:** keep react/@types/react in lockstep with react-dom 19 ([81dd844](https://github.com/mforce/collectify/commit/81dd8446a5f35372079d6669ec0088c590a9de08))
* **client:** label digital-store filter chips ([9cb812b](https://github.com/mforce/collectify/commit/9cb812b12050e39db68f5c35bb0a4daddb698405))
* **client:** migrate to Tailwind v4 PostCSS plugin ([71441b7](https://github.com/mforce/collectify/commit/71441b7a9cc5c65217b2dbe672128a8aa326a791))
* **client:** parse offsetless added dates as UTC ([e32fe48](https://github.com/mforce/collectify/commit/e32fe483884336473a27b684379b0bc248450bdb))
* **client:** show only selected sort value ([15e81dc](https://github.com/mforce/collectify/commit/15e81dca2fbe625370c1eb988865c3c0e07cd230))
* **database:** capture PostgreSQL catalog manifests ([ac5b3bc](https://github.com/mforce/collectify/commit/ac5b3bc7dd759d1327fc99456717b107f638913b))
* **database:** compare semantic PostgreSQL catalog shape ([718b01f](https://github.com/mforce/collectify/commit/718b01fee598b3c4ff55d164b967ccc96819395b))
* **database:** match Program.cs provider parsing to AddCollectifyDbContext ([1eb49f2](https://github.com/mforce/collectify/commit/1eb49f2f08ba76567dc5ef58fb4e8f41e0a8d15a))
* **database:** use valid deterministic pg_dump restrict key ([be9f5ab](https://github.com/mforce/collectify/commit/be9f5abf6484ca10f6fcdb3e27b574c7f58f7cd4))
* edit-page game search filters at source when platform is known ([aa6528d](https://github.com/mforce/collectify/commit/aa6528de183e4d7901655598a04adca8c426df58))
* **games:** address [#91](https://github.com/mforce/collectify/issues/91) review findings (ledger remap, Down, filter, docs) ([6283cb8](https://github.com/mforce/collectify/commit/6283cb89f0ba7cf6db55a4f184c65e25699d9ef9))
* **games:** backfill IGDB metadata immediately after Steam import ([be84500](https://github.com/mforce/collectify/commit/be84500de0a966e6c11e8112ae6d8ddb681ea1ad))
* **games:** canonicalize store filter names case-insensitively ([#91](https://github.com/mforce/collectify/issues/91)) ([e9a7a22](https://github.com/mforce/collectify/commit/e9a7a22bd043c1560655119bf213a5240598ab70))
* **games:** canonicalize store labels in active-filter chip case-insensitively ([#91](https://github.com/mforce/collectify/issues/91)) ([d34df19](https://github.com/mforce/collectify/commit/d34df19d12efe4c52df3c95feac1a738a706eac4))
* **games:** expose store button pressed state for screen readers; refresh graph ([#91](https://github.com/mforce/collectify/issues/91)) ([d0f0ab5](https://github.com/mforce/collectify/commit/d0f0ab502302901f6e408c453635c336e56ffe59))
* **games:** fill-only IGDB import + immediate backfill on startup ([9e549dc](https://github.com/mforce/collectify/commit/9e549dc9d4a3f03dee6fba332ee39b12b92c6a3f))
* **games:** hide digital-store chip when the filter mask has no defined bits ([#91](https://github.com/mforce/collectify/issues/91)) ([d53e51d](https://github.com/mforce/collectify/commit/d53e51d4010fd7f0b1d5ec217eb49a8ae7ffbb63))
* **games:** resolve store labels in active-filter chip from the canonical mask ([#91](https://github.com/mforce/collectify/issues/91)) ([9ccb61e](https://github.com/mforce/collectify/commit/9ccb61ead68f51fd505fa824325254406242c77c))
* harden Steam import against concurrent races + reviewer findings ([b9b340a](https://github.com/mforce/collectify/commit/b9b340a9cb82cbd85d2a6a832f316581843143ac))
* heal stale/missing Steam covers on re-import ([025b2c3](https://github.com/mforce/collectify/commit/025b2c3cc71d5c2fa3e5dd10718c64e4a55eab1c))
* IGDB platform-scoped search filters at the source so the PC SKU is found ([a6c1554](https://github.com/mforce/collectify/commit/a6c155413ab2c54a23f0946c764edfd2db9b534b))
* **igdb:** include Linux id in Pc source-filter search ([#102](https://github.com/mforce/collectify/issues/102)) ([5627fe9](https://github.com/mforce/collectify/commit/5627fe977b75b4c9fdb4e94de3549bc9af95ab35))
* **issue #88:** address review findings (backfill persist, IDs, cache version) ([694f783](https://github.com/mforce/collectify/commit/694f783b7e158f9cec7cb3a3fac5d8d790118a29))
* match candidates against full IGDB platform set (Codex P2) ([113ce66](https://github.com/mforce/collectify/commit/113ce662fda150ca0255eec4c572d6e2c467cdca))
* parse KnownProxies from scalar comma value or indexed keys ([a51555e](https://github.com/mforce/collectify/commit/a51555e2fe2500927c1fffc7037f46b7a53f9106))
* partition steams callback limiter by client IP + server-side preview search ([af16553](https://github.com/mforce/collectify/commit/af1655319a6cd5a7bff5df08734b1090551a0b62))
* **platform:** accept legacy Linux on game writes via JSON converter ([#102](https://github.com/mforce/collectify/issues/102)) ([3ae42d4](https://github.com/mforce/collectify/commit/3ae42d4edfbf66a56150ba507e43634577d547c3))
* **platform:** address Claude+Codex review findings ([#102](https://github.com/mforce/collectify/issues/102)) ([834fe1b](https://github.com/mforce/collectify/commit/834fe1be64e736af2671b630d2860b98fd6b6c51))
* **platform:** fold Linux into Pc, keep Mac separate ([#102](https://github.com/mforce/collectify/issues/102)) ([bb488a2](https://github.com/mforce/collectify/commit/bb488a29910276cb8164e0eb1335890f53280c4b))
* **platform:** fold Linux into Pc, keep Mac separate ([#102](https://github.com/mforce/collectify/issues/102)) ([1626605](https://github.com/mforce/collectify/commit/16266050c871f3b6579b8b7d77b5ea5638a4fb52))
* **platform:** reject retired/undefined numeric platform query values ([#102](https://github.com/mforce/collectify/issues/102)) ([feda326](https://github.com/mforce/collectify/commit/feda326b4c8759fd06d59e762f94af026346fa15))
* **platform:** restore ?platform=Other filter; harden lookup + docs ([#102](https://github.com/mforce/collectify/issues/102)) ([5d99e76](https://github.com/mforce/collectify/commit/5d99e763b0c3e51a04c5f312cf718723151f0629))
* reject spoofed forwarded IPs, detach only conflicting cover, keep search filter mounted ([00d55e5](https://github.com/mforce/collectify/commit/00d55e55d36529069a1c3673fa2d34ab6ab12f16))
* resolve Steam cover from storefront assets, fixes 404 for hash-pathed apps ([c74d2cd](https://github.com/mforce/collectify/commit/c74d2cdc357b82e4f8bc9e35cb622c936e661a1b))
* revalidate match inputs after outbound work (Codex round 4) ([abd8229](https://github.com/mforce/collectify/commit/abd8229c3774c9f402eb01b35077f57f7d27506e))
* rotate backfill sweep window to avoid head-of-queue starvation ([ee76540](https://github.com/mforce/collectify/commit/ee765401f54dde1c7000d1f163ab9ee8e27886a5))
* **steam:** base page range on fetched page, not the hidden subset (PR [#182](https://github.com/mforce/collectify/issues/182) review) ([7965bd8](https://github.com/mforce/collectify/commit/7965bd8e2137c2ba0af6e23a855a43576c72d0a7))
* **steam:** batch large imports server-side ([e9387c9](https://github.com/mforce/collectify/commit/e9387c9d24b0ccbb1c94ffb783d16486888a3bc6))
* **steam:** bound page-scoped cover repair batches ([be311be](https://github.com/mforce/collectify/commit/be311be616a1c854c4034124a9e5bbc7f68025b4))
* **steam:** cap cross-page selections at 500 ([fbde6cf](https://github.com/mforce/collectify/commit/fbde6cfcd43c802c08a97437e8ef0a2996b4794b))
* **steam:** filter imported titles before pagination ([9bf1a4a](https://github.com/mforce/collectify/commit/9bf1a4ad2bd284e9cae55725ed8fa5001a240a47))
* **steam:** improve filtered empty and mobile states ([6aea821](https://github.com/mforce/collectify/commit/6aea821cd31b7680427755f2bef31897ad6c1c36))
* **steam:** merge, not replace, cross-page select-all (PR [#182](https://github.com/mforce/collectify/issues/182) review) ([744f89a](https://github.com/mforce/collectify/commit/744f89a52ff52cf08879931a6e651687d9a86175))
* **steam:** persist ReleaseDate from SteamReleaseDate on import ([9138ced](https://github.com/mforce/collectify/commit/9138ced129a7f9532d64838b3e59833afd8e18a4)), closes [#156](https://github.com/mforce/collectify/issues/156)
* **steam:** persist ReleaseDate from SteamReleaseDate on import ([#156](https://github.com/mforce/collectify/issues/156)) ([83ddb53](https://github.com/mforce/collectify/commit/83ddb53ca946de1a20453ab150178e9fee852c54))
* **steam:** preserve selection when repairing covers ([47f0979](https://github.com/mforce/collectify/commit/47f09794f53900f03eb34284a9d2833ad4cd6e0f))
* **steam:** prevent concurrent import actions ([05cc006](https://github.com/mforce/collectify/commit/05cc0068bf5d7589a1f1e909f43e8864845bf2d0))
* **steam:** repair covers across library ([1deef25](https://github.com/mforce/collectify/commit/1deef2573bd457dd8cb036f2f07907f36e552037))
* **steam:** reset page offset immediately when search term clears (PR [#182](https://github.com/mforce/collectify/issues/182) review) ([b19d837](https://github.com/mforce/collectify/commit/b19d837f653f1fb73120d539df538c21e674f3e8))
* **steam:** share configured import cap with client ([63344aa](https://github.com/mforce/collectify/commit/63344aadf37a73c2fe910da1b4f7509d833df5c1))
* **ui:** dedupe game platform label in detail view ([fcc79a8](https://github.com/mforce/collectify/commit/fcc79a8096fbd9f76e9eaadbf067841e54b5fed9))
* **ui:** keep platform icon with platform label in game list row ([afd8515](https://github.com/mforce/collectify/commit/afd8515f4d28b5d6d294b97781c314033298adab))
* **ui:** omit blank secondary row for unclassified games ([aeb0b93](https://github.com/mforce/collectify/commit/aeb0b9342e8955df08e55388be392b4bb878643d))
* use Steam's 600x900 library cover instead of the tiny logo/icon ([f0d2f85](https://github.com/mforce/collectify/commit/f0d2f857e266a3e00594959741ae26d712ffa5c9))
* verify cover winner exists before swallowing save error + document trusted-proxy setting ([e37950a](https://github.com/mforce/collectify/commit/e37950ac4a27c8e1be865a8a169613d613429e16))
* version IGDB cache keys so stale DTO-shape rows are never served ([eb46f2f](https://github.com/mforce/collectify/commit/eb46f2f97e4b88460e696178348886eaf86c93ee))


### Refactoring

* **api:** collapse collection endpoints into one generic module ([#97](https://github.com/mforce/collectify/issues/97)) ([1d65ec5](https://github.com/mforce/collectify/commit/1d65ec5e0f45f232336bc7f0055e32e137014535))
* **api:** hoist MovieFormat bitmask to a static readonly field ([2589952](https://github.com/mforce/collectify/commit/2589952afe0e16913f1f6d64d0c432c1b2b7d3a8))
* **cache:** extract shared JSON options for lookup cache ([e3d3fca](https://github.com/mforce/collectify/commit/e3d3fcac170a996c1d2e0994db7b423de23935f8))
* **client:** derive all per-type tables from media registry ([#98](https://github.com/mforce/collectify/issues/98)) ([9bcd2a9](https://github.com/mforce/collectify/commit/9bcd2a93cf35890c6938b6225ccd9e23d95700e9))
* **client:** drop dead null! type-token fields from registry ([#149](https://github.com/mforce/collectify/issues/149)) ([44fe55d](https://github.com/mforce/collectify/commit/44fe55d4340f9147b0b8226b870804ba2eca04b7))
* **client:** structured byId noun/hint, drop label-sniffing ([#149](https://github.com/mforce/collectify/issues/149)) ([63efb07](https://github.com/mforce/collectify/commit/63efb0763b420c7e79f1f885b0f1b861b8c173fd))
* **client:** type-safe scan-to-form prefill channel ([#99](https://github.com/mforce/collectify/issues/99)) ([d47c6bd](https://github.com/mforce/collectify/commit/d47c6bd43592aa3f42ffe016152c576c488a72b1))
* **lookup:** unify metadata provider interfaces into generic IMetadataProvider&lt;T&gt; ([#94](https://github.com/mforce/collectify/issues/94)) ([47135a2](https://github.com/mforce/collectify/commit/47135a2ca41f4c18ab03bd5f71f9581f618c40eb))
* **lookup:** unify metadata provider interfaces into generic IMetadataProvider&lt;T&gt; ([#94](https://github.com/mforce/collectify/issues/94)) ([a1b3530](https://github.com/mforce/collectify/commit/a1b35308983fab6f548980aecdbd072b120b6e14))
* **steam:** drop "what" comments in toggle-all per AGENTS.md (PR [#182](https://github.com/mforce/collectify/issues/182) review) ([ce10b20](https://github.com/mforce/collectify/commit/ce10b20b55d8aba59ee1b159ff3a9b9c43bf0c99))
* **steam:** drop unused IConfiguration from /games handler (PR [#182](https://github.com/mforce/collectify/issues/182) review) ([28c0cb8](https://github.com/mforce/collectify/commit/28c0cb85a80a657c6fd21a334e7e29909950c5e3))
* **steam:** remove obsolete preview cap ([b104c45](https://github.com/mforce/collectify/commit/b104c45469788e64bafe7ee95e2ce1b6f388459f))


### Documentation

* **#91:** fix truncated-500 bold marker and stale delivery-dimension comment ([61aab67](https://github.com/mforce/collectify/commit/61aab6723dd2a3eff2af31ee080b3ccb417db93c))
* add CI/CD invariants, decision records, and gate-aware ci-local ([#107](https://github.com/mforce/collectify/issues/107)) ([fcd0b8f](https://github.com/mforce/collectify/commit/fcd0b8f5300110c7bb06d23e7edd32e3809b9f3e))
* add CONTRIBUTING.md and .githooks/README.md, dedupe commit rules ([0762e3a](https://github.com/mforce/collectify/commit/0762e3a85772d30003432bbcbfe4eaa7fdca65d2))
* **cache:** document distributed cache backing, Redis opt-in, and SQLite migration ([40c562d](https://github.com/mforce/collectify/commit/40c562d14e3db3b9d2f732a08f49f2fc91a79517))
* **data-model:** genres are relational like tags; legacy CSV dropped ([#169](https://github.com/mforce/collectify/issues/169)) ([c5f8d3a](https://github.com/mforce/collectify/commit/c5f8d3a9be48169c381bece12703d4f159cfa97d))
* **database:** correct stale EnsureCreated PostgreSQL guidance ([57f22fd](https://github.com/mforce/collectify/commit/57f22fd7e5cae0959f4b222c259b8fa7670334c7))
* **database:** remove remaining PostgreSQL reset claims ([4707c79](https://github.com/mforce/collectify/commit/4707c795acc14a3b1312f4e42d73c413d07f6e4b))
* document Steam setup (API key + PublicBaseUrl) in .env.example and README ([a002720](https://github.com/mforce/collectify/commit/a00272035f4c24472aa47175a8de9de6013bca5b))
* move commit-message conventions into README ([00ce676](https://github.com/mforce/collectify/commit/00ce67659ea08920886c10648f22cf295d26ad23))
* **platform-import:** drop stale Postgres reset advice from rollout ([c5fb126](https://github.com/mforce/collectify/commit/c5fb126c3eb8eaf0142f1c0669f3a23b038ab08a))

## Changelog

Releases from v0.0.8 onward are generated by
[release-please](https://github.com/googleapis/release-please) from
[Conventional Commits](https://www.conventionalcommits.org/). Earlier releases
(v0.0.1–v0.0.7) predate this automation and are recorded only as
[GitHub releases and tags](https://github.com/mforce/collectify/releases).
