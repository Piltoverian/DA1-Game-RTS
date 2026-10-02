# Texture research follow-up

2026-10-02. Prototype assets: `Assets/Art/Terrain/RTSPrototypeTiles_v1/`.

## Microsoft: confirmed details

[US20070206023A1 — Random map generation in a strategy video game](https://patents.google.com/patent/US20070206023A1/en), description around steps 502–503 and Figures 7–8:

- Noise adds constrained elevations to tiles.
- A terrain mix file lists textures and background models with occurrence weights; their noise parameters can differ.
- Noise values select entries using weights. This is not evidence that every pixel is blended with those weights.
- Cliff outlines can include openings for ramps. Predefined cliff meshes fit tiles, can be rotated, and join through shared coordinates.
- Cliff mesh vertices can be adjusted to surrounding corner elevations; texture density can be kept consistent.

This documents a proposed strategy-game system, not verified source for a specific AoE release. Our shader blend and triplanar choices are project proposals, not instructions from the patent.

## IEEE: access and scope

[Ma et al. — Angle-Based Multi-Objective Evolutionary Algorithm Based On Pruning-Power Indicator for Game Map Generation](https://ieeexplore.ieee.org/document/9410278/), DOI 10.1109/TETCI.2021.3067104; volume 6(2), pp. 341–354, April 2022; online publication April 2021.

Only the publisher abstract/metadata were accessible. Searches for the title and DOI did not yield an accessible author full text. Exact objective equations, map encoding, algorithm parameters and detailed experimental outcomes have NOT been verified.

The abstract describes local partitions in a hyperspherical coordinate space, pruning power for dominance assessment and radius-penalized angles for search direction. These angles concern optimization objectives; they are not terrain slope or ramp angles. The application generates MegaGlest maps using fairness, playability, strategy and interestingness objectives. It is not a texture synthesis or PBR paper.

## Application proposed for this project

Separate three decisions:

1. Geometry: plateau surfaces, cliff boundaries, and ramps.
2. Appearance: a palette of repeatable surface textures and masks controlling their placement.
3. Quality: reject unplayable navigation first, then compare valid candidates by resource access distances, usable space, routes and variety.

The following metrics are our proposals, not claimed to be the paper's formulas: fairness via spread in shortest-path resource access; playability via connectivity and clearance; strategy via alternate routes and controlled bottlenecks; variety via region size/shape distributions. They need gameplay validation.

Start with four coordinated surface images: grass/soil, packed soil, rocky soil and cliff rock. Plateau height does not force rock appearance. Ramp readability combines geometry with a soil/stone mask. Appearance noise must not rewrite walkability. Retain prefab-owned resource amount; placement stores only prefab identity and location.

Next validation: repeat each draft 2x2, tune world-space material scale at the RTS camera distance, inspect lighting, then test blending on the two-level ramp fixture. Generated drafts are color images only; seamless edges and complete PBR materials are not yet validated.
