# Movement / terrain audit

Source inspection, 2026-10-10. No live reproduction or profiler capture was available.

## Confirmed behavior

- Grid indexing uses int and the configured row width. No hardcoded 128 stride or ushort node index was found in movement. 256x256 is not inherently an index overflow.
- GridInitSystem blocks non-walkable baked terrain with cost 255. CostChangeSystem preserves these static blocks when dynamic blockers are removed.
- GridIslandSystem and IntegrationFieldSystem reject diagonals when either adjacent cardinal cell is blocked.
- FlowDirectionSystem and UnitMovementMath.GetRawDirection choose lower-cost neighbors without that diagonal check. Unit target steering recalculates direction with GetRawDirection rather than reading FieldNode.direction.
- CalculateFlowVelocity blends four neighbor directions without checking whether the samples are separated by blocked cells. MovementAgentTargetSystem blends a direct direction near the destination without checking line of sight, and does not check FlowFieldStatus before reading the field.
- MovementAgentORCASystem adds a grid gradient to avoidance. MovementAgentActuatorSystem applies separation correction and velocity directly without validating the swept segment against the grid. Avoidance therefore is not a hard terrain collision constraint.
- TerrainChunkRenderer reads the baked grid into a separate presentation map. TerrainVisualOffsetSystem modifies LocalToWorld for rendering, while movement reads LocalTransform on the flat XZ plane. Height sampling changes abruptly across non-ramp tier edges, so a logical crossing can appear as a vertical jump.
- The supplied exception comes from terrain's cached entity restoration. Restoration now enumerates live LocalToWorld entities and prunes stale cache entries. Runtime reproduction of the exception is still required.

These are observed source behaviors, not proof of which behavior caused the reported cliff crossing. Existing movement algorithms were not changed during this audit.

## Live diagnosis

Main's GridDebug now defaults to Walkability. Enter Play Mode and enable Gizmos in Scene view (or Game view). Align To Visible Terrain is enabled by default: each corner is lifted using that same cell's surface height, so the overlay aligns with the rendered top without changing movement coordinates or costs. Disable this option to view the logical Y=0.03 plane. In an isometric view, a logical flat grid and elevated terrain do not occupy the same screen position. The overlay draws through terrain in the Editor and shows a bounded area around the view rather than all 65,536 cells.

- Green: actual cost <255, unit navigation considers it open.
- Red: actual cost >=255, blocked by terrain or dynamic blockage.
- Cyan: an open cell marked with a ramp ID.
- Magenta: baked terrain says blocked but actual movement cost says open.

Select GridDebug to adjust Movement View Radius, Draw Through Terrain or Draw Step. The overlay uses wire lines; filled rectangles and labels were removed after repeated native crashes in the Editor Handles rectangle path. Cyan indicates a ramp cell, not unrestricted crossing in every direction. Walkability colors describe cells, not unit-radius clearance.

After issuing a movement command across the problematic cliff, use **RTS > Debug > Audit Movement Navigation (Play Mode)**. The Console reports dimensions, cell size, static block mismatches, illegal open tier edges, units in blocked/outside cells, unsafe raw/stored field directions and unfinished fields.

Interpretation:

- Terrain opened incorrectly >0: investigate grid initialization / cost updates.
- Illegal tier edges >0: investigate generation and ramp topology.
- Unsafe raw directions >0: the steering direction disagrees with integration's corner rule.
- Agents in blocked/outside cells >0: movement has crossed the logical grid boundary; this is not only visual elevation.
- All of these zero while the sprite jumps: compare LocalTransform and LocalToWorld, ramp height sampling and painter ordering at the exact cell.

Unfinished fields alone are expected while generating/recalculating and do not establish a bug. Direction counts cover Ready fields only. Repeat before and immediately after the crossing to distinguish an existing spawn position from a movement crossing.

Resource/ramp follow-up: the audit also reports missing resource blockers or invalid footprints, resource footprint cells that remain open, pending resource bakes, and open cardinal edges touching a ramp whose sampled heights disagree at the shared edge. Run after bootstrap and cost updates settle. A ramp seam is a topology/presentation inconsistency on an open edge, not proof of a failed flow search. Resource footprint counts are per resource and may count overlapping cells more than once.

## Validation

Compile runtime and Editor assemblies with dotnet. This validates C# compilation only; Unity Play Mode and the live audit are required to confirm the reported behavior and overlay appearance.
