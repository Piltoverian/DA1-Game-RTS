# Lab 4: asymmetric terrain prototype

Open `Lab4_Height_Noise_Walkability_Balance.html` in a browser; it runs offline. Change parameters and click Gen map. Search evaluates eight deterministic layouts and returns the lowest violation score, including a rejected result if none passes. Click two walkable cells or choose two bases to test an actual Dijkstra route.

## Terrain

N angular planning domains constrain independently jittered outer base anchors. They do not mirror terrain or create hard borders. A base closer than 30% of map width to the center is rejected. Domain-warped multiscale height covers the entire map including its edges. Only the small base safety core is guaranteed constant height: adjustable radius 4–10 cells, default 6. The surrounding transition blends into the original landscape over an irregular shoulder instead of an isolated plateau ring.

Ramps follow sampled curved centerlines with varied widths, feathered side slopes and a quintic height profile matching base and landscape endpoints. Cores are flattened again after blending. The terrain layer does not paint a bright ramp rectangle; a diagnostic layer shows the edited cells and centerlines. Without ramps, the rolling terrain can still connect bases.

Height is quantized to 1/256 cell. Two-triangle gradients determine physical traversability. Fine noise can be bounded by the tightest admitted triangle; this conservative global amplitude bound can strongly suppress detail when macro slopes are already near the limit. Rocks provide occupancy across the map. Conservative radius clearance produces navigation. Prefab footprints trigger a rebuild, followed by connectivity and fairness checks. Seed, normalized parameters and candidate index reproduce the same hash.

## Resources

Controls set prefab counts, not resource amounts: main / player (default 4), secondary / player (3), advantage / player (3), contested / angular sector (4). Counts range 0–12. Contested prefabs belong to no player; their sector is a sampling quota only. Total requested count is N times the sum of the four controls: default four-player map requests 56. Each prefab has an abstract 2×2 footprint and one of two placeholder identities.

The current resource sampler follows Lab 3's annulus / radial-sector structure. Each prefab is sampled independently, not around a fixed mine center. Main resources sample an area-uniform annulus around the core, preferring the side opposite the exit. Secondary resources sample the entire annulus from protectedRadius + 10 to 22% map width around their base. Advantage resources sample a wider annulus from 25–40% map width. Contested resources sample independent points across their assigned central wedge, at radius 7.5–28% map width. Area-uniform annulus sampling uses sqrt(rMin² + u(rMax²−rMin²)); contested quotas do not imply ownership or mirrored locations.

Candidate placement checks occupancy, footprint slope at most 8 degrees, clearance-compatible gathering access, spacing, distance from protected cores and avoidance of ramp mouths. Candidate ranking encourages separation from prior prefabs rather than an exact target distance; a broad path-distance band preserves economic tiers. Contested candidates require access from every base and a minimum approach distance of 28.5% map width. Counts or constraints can exceed available space; missing prefabs reject a candidate instead of silently reducing the requested count. Resource areas are not forced into identical flat patches. The old economic-route anchors remain terrain planning hints and are no longer mine spawn centers or visible mine labels.

Final checks compare average access for each enabled private role, nearest contested access, flat-cell area, nearest enemy and a two-neighbor exponential pressure proxy. Final advantage access must be at least four cells beyond the farthest secondary prefab. Checks run after all footprints are blocked. Passing these checks is not proof of competitive balance.

## Limits and validation

This is a 2D experiment, not the Unity implementation. Height, radius and lengths use grid-cell units. Paths cost 1 / √2 without diagonal corner cutting. Gathering reach is abstract: ceil(unitRadius + 2.5). Clearance conservatively checks the whole moving cell. Route candidates use a complete-distance MST plus cycles, not Delaunay; disjoint attack routes are not certified. Flat-cell area is not building packing. Combat, visibility, formations, ORCA and dynamic occupancy are not simulated.

Run `node Docs/Multiplayer/Labs/lab4-tests.cjs`: determinism, legal routes, adjustable / zero counts, exact core height, variation on four edges, curved ramp centerlines, asymmetry and certified versus uncertified slope stress. Browser tests require bundled Playwright and installed Edge. `build_lab4.py` rebuilds inline and standalone outputs with the installed visualization wrapper.
