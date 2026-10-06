# Xianxia RTS character modeling references

Generated with the built-in image_gen tool and imagegen skill. These PNG files are modeling concepts, not 3D models. Image-to-3D results have not been generated or validated.

- Sword cultivator: ivory/jade clothing, slim silhouette.
- Alchemist: jade clothing, compact hair bun, terracotta pill jar.
- Worker disciple: tan clothing, stocky silhouette, short sleeves.

## Intended modeling workflow

Use the front view alone for a single-image generator. Do not feed the entire three-character turnaround to a single-image generator: it could reconstruct multiple bodies. For a tool with explicit multiview inputs, supply front, side and back as separate images in the corresponding slots.

Keep weapons and work tools separate from the character mesh. Model robe panels as simple closed surfaces; paint fine folds and trim rather than sculpting them. Simplify hair into a few solid masses. Preserve shoulder, elbow, hip and knee loops for animation.

Suggested starting budget for this project (design target, not measured output): 2,000–4,000 triangles per unit at LOD0, 800–1,500 at LOD1, one 512px or 1024px atlas per character. Adjust after testing actual RTS camera distance and unit count.

Check any generated mesh for fused arms, fingers, gaps between legs, doubled geometry, garment thickness, rear silhouette, UVs and rig deformation. The side view is an A-pose projection and may visually overlap limbs. Treat small inconsistencies in references as concept ambiguity to resolve during modeling.

No mesh, topology, texture atlas, rig or runtime performance is implied by the faceted look of the images.
