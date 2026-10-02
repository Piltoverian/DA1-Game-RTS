# RTS terrain tiles — prototype v1

Generated 2026-10-02 using the built-in image_gen tool. Exact prompts are in `prompts.json`.

| File | Intended use |
|---|---|
| grass_soil.png | Plains and walkable plateau tops |
| packed_dirt.png | Base yards and paths |
| ramp_soil_stone.png | Walkable ramp surface blended with soil |
| cliff_rock.png | Cliff faces and rock surfaces |

These are color/albedo drafts, not complete PBR materials. No normal, roughness or displacement maps are included. Images have a coordinated painterly palette; small stones and grass remain present in the dirt draft. Rock contains painted relief cues. Inspect under actual game lighting before adopting the style.

Tileable edges were requested, but exact seamless repetition has not been established. Do not describe these as production-ready seamless textures. Verify 2x2 repetition and the target camera scale before shipping. No Unity material or scene assignment has been made.

Suggested Unity import: color texture, sRGB enabled, Wrap Repeat, mipmaps enabled, bilinear/trilinear filtering. These are visible surface textures, unlike data textures. Use a shared world-space texture scale, blend masks for ground materials, and triplanar mapping for cliff rock.

Do not derive walkability from the color of these images. Terrain geometry and navigation data remain authoritative.
