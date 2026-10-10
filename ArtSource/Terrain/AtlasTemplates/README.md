# Atlas authoring kit

Each sheet uses four columns of 128×128 cells, top-left indexing. `*-sample.png` is production artwork packed without labels; `*-mask.png` preserves the exact silhouette; `*-guide.png` has names, grid and orange pivots and is only for inspection. See manifest.json for indices. Ground contains lush bank0–15 and sparse bank16–31. Top/mountain masks use bits00=1,10=2,11=4,01=8. Cliffs are -Z/-X with masks4,8,C,D,E. Other cliff masks are intentionally omitted.

The checked-in artwork was generated from flat material sources in ArtSource/Terrain/JadeSlate using canonical UV geometry. The offline generator has been removed. These sample/mask sheets are geometry references, not a request for AI to arrange 78 exact tiles. Mountain caps are joined shared-height faces, not independent full mountains; decoration footprint remains separate.

Use the exact production material prompt in ArtSource/Terrain/JadeSlate/prompt.md. Generate flat diffuse surfaces with calm colour and no perspective/objects. Code owns silhouettes, layout, pivots and geometric shading.

When preparing replacement artwork externally, preserve the canvas and canonical masks. The repository no longer includes a splitter or sheet generator.

`xianxia-concept-reference.png` now shows the current four-quadrant ImageGen material source; it is not an indexed runtime sheet or canonical mask.


Review replacement PNGs in a separate theme, then run lab, alpha, join, resource and performance validation before promotion. No generated sheet is admitted automatically.
