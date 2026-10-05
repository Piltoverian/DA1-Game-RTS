# Cliff path and height-side contract

Each cliff part stores two independent descriptions:

- `connections` / `connectionMask`: actual neighbouring contour cells and cardinal joining directions. Bits N/E/S/W = 1/2/4/8.
- `surfaceSides`: high and low occupied quadrants, high/low levels, cardinal downhill normals and concave downhill diagonal. Quadrant bits NW/NE/SE/SW = 1/2/4/8. These are terrain sides, not screen top/bottom.

`heightNeighbours` records the source cell and level of each cardinal neighbour. `surfaceSides` is derived from the height boundary, before choosing an image. `connectionRotation` describes joining edges; `heightRotation` describes occupancy. A connection bend must never overwrite height occupancy or determine the grass side on its own.

Example: both NW convex and NW concave parts join N/W. Convex has HIGH NW and LOW NE/SE/SW; concave has LOW NW and HIGH NE/SE/SW. Matching N/W ports alone cannot choose the image.

`assetRequirement` carries shape, connecting edges, high quadrants and low quadrants. `matchCliffAsset` requires all four to match. Unknown or reversed grass-side assets must not be treated as validated. Current V8 artwork has not been validated against this contract; its legacy preview renderer remains a diagnostic preview, not an approved rendering path.

Decorative `cliffJoinMap` entries carry the same height-side contract, retain walkability and add no collision. A ramp retains its cardinal global uphill direction under that overlay.

Ramp eligibility now requires exactly one lower cardinal neighbour on its high-side face. Corner faces and compound faces cannot become ramps. Bake closes illegal low-side portals; connectivity repair forbids reopening them; the visual compiler reports and rejects illegal corner crossings. Existing spacing and single-island requirements still apply. Some configurations currently exhaust the 16-candidate connectivity limit under this stricter rule; this is a known unresolved generator limitation, not permission to reopen corner ramps.

Update: after eight unsuccessful candidates, generation regularises height contours using deterministic block-majority runs of `max(6, rampWidthMin+3)` cells before cliff baking. This creates straight portal sites rather than reopening corners. Base plateaus and solid mountain occupancy remain protected. Output records `contourRunCells`; zero means the fallback was not needed. The 24-configuration spacing/navigation suite and four-seed visual suite pass with this fallback. This is bounded evidence for the tested configurations, not a guarantee for every possible seed.


