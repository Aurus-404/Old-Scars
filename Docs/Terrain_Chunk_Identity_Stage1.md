# Old Scars — IMPL-0063 Stage 1: stable terrain chunk identity

State: **VALIDATED — STAGE 1 ONLY** (2026-10-02). IMPL-0063 as a whole is not DONE. Scope: bounded integration audit, world-addressable identity within the current active-region projection, baseline compatibility/evidence and combined automated validation. IMPL-0066 remains IMPLEMENTED / AUTOMATED PASS / MANUAL CORE PASS / TUNING ACCEPTANCE PENDING.

## Identity and authority

`DeformableTerrainChunkId(X,Y,Z)` remains the local runtime volume partition index. `TerrainChunkKey` adds WorldId, SectorId as region context, the integer logical TerrainMaterializationWindow (MinX/MinY/MaxXInclusive/MaxYInclusive), local technical X/Y/Z and explicit baseline/layout evidence. Canonical identity is `terrain_chunk_` followed by SHA-256, via the existing WorldCanonicalEncoding. Default keys are invalid; volumes reject out-of-range chunk indices.

World Sector is not a technical chunk. This address identifies a chunk under one compatible bounded materialization window; it is not a global square grid, a streaming lifecycle or persistent mutation coordinates. Different overlapping windows intentionally represent distinct addresses. Physical projection tuning participates only in compatibility and does not establish a permanent macro-unit-to-metre conversion.

## Baseline compatibility

Contracts: `terrain_chunk_key_v1`, `terrain_chunk_baseline_v1`, `terrain_chunk_layout_v1`, `terrain_chunk_samples_v1`. Layout is captured once when the volume is created, before mutations. Its canonical SHA-256 includes:

- Committed MacroGeography hash and integer logical window.
- Actual projected heightmap resolution, physical projection width/length and vertical relief.
- Technical chunk counts/cell counts X/Y/Z, vertical cells, horizontal spacing, underground depth, air headroom, surface and soil layer depths.

These are the current density/material sampling inputs. Layout excludes mesher, vertex order, Transform/GameObject, spawn, NavMesh, mutation state, water mask, alphamap, diagnostic road width and Water/Climate/Environment/Human Geography hashes because they do not affect the current volume scalar/material baseline. Logical extents are represented by the resolved window rather than duplicated requested tuning.

Serialization reuses length-prefixed UTF-8 and big-endian Int64 from WorldCanonicalEncoding; float fields use exact IEEE-754 binary32 bits in big-endian order. Collection GetHashCode is not canonical evidence. Changes to projection interpolation, density/material generation or partition semantics must bump the baseline contract. Future tuning may change without redefining WorldId, SectorId or MacroGeography; incompatible layouts produce different keys.

## Per-chunk evidence

`DeformableTerrainVolume.ComputeChunkBaselineEvidence` hashes layout evidence, local address, closed sample dimensions and baseline density/material pairs in Z/Y/X order, X fastest. Each axis includes both endpoint samples: faces, edges and corners are shared references into the existing single lattice. No density duplication or mutable buffer exposure was introduced. The current material field is immutable; future material mutations must preserve that baseline separation under a revised contract.

`BaselineDensityAtChunkSample` and `BaselineMaterialAtChunkSample` expose validated scalar reads for chunk-local closed sample bounds. Sphere/capsule mutations continue through DeformableTerrainMutationService and change the current density only. Existing whole-volume ComputeDensityEvidence remains the mutable-state evidence, unchanged. Identical fields in distinct WorldIds may have identical baseline evidence while their stable keys remain distinct.

## Audit and automated gate

The existing path remains intact: committed WorldSession → TerrainMaterializationPlanner → plan → shared-lattice volume → technical chunks → either mesher → MeshCollider/local NavMesh → shared Player in WorldRuntime → localized mutation → affected mesh/collider rebuild and existing navigation contribution. Neither mesher, the controller, application shell, mutation service nor persistence schemas were replaced.

`TerrainChunkIdentityDiagnostics.Run()` groups the gate into one warm-Editor Play session. TEST-20261002-005 records PASS; TEST-20261002-007 repeats the same mapping/assertions after the concrete TEST-20261002-006 diagnostic interruption/readiness fixes. Two combined executions total, no broad suites. Coverage: fixed WorldId with committed schema-7 truth saved/loaded via existing semantic preflight, duplicate committed reconstruction, forward/reverse/reordered key→baseline mapping, exact shared baseline density/material boundaries (X 1700, Y 2500, Z 1700 samples), different WorldIds, material-layout incompatibility, rendering-only independence, both meshers, GameObject recreation/name/order/Transform independence, sphere/capsule current-state changes with unchanged baseline mapping, exact affected rebuild counts, single local NavMesh complete path, unchanged committed world payload and real Player traversal on the volumetric collider. Saved MainMenu setup restored without saving scenes.

Validated fixture layout hash: `ff0ab7b4ba21ecd3da62e4d742b2e861c52bac5a25cff25bdd5812e9f8cb60d8`. The current fixture remains provisional: eight 2×2×2 chunks, 24×16×24 cells, horizontal spacing 2. The gate reconstructs committed snapshots in one Unity process; it does not claim fresh-process or production terrain persistence validation.

## Deferred work

No streaming/load-unload, terrain mutation persistence/journal/coordinate migration, compaction, geology/caves/resources/mining, roads/sites realization, LOD/Transvoxel, Jobs/Burst/GPU/pooling/async queues or final navigation architecture. `world_session_v1` and `deformable_terrain_spike_v1` schemas and release/default UnityTerrain path remain unchanged. Further phases require separate authorization after reviewing this Stage-1 checkpoint.
