# Resource avoidance and grid scale

Tested 2026-10-10 with Unity 6000.3.13f1 in a separate headless project at `Artifacts/MovementScaleRepro`. The main scene, map configuration and actuator were not changed for this test.

## Reproduced defect

CalculateGridGradient measured distance to blocked **cell centres**. Resource cost can correctly be 255 while avoidance reports no nearby wall. A 3.90625-unit cell has its centre 1.953125 units from its edge; that exceeds a radius-1 agent's 1.8-unit avoidance search distance, even when the agent touches the edge. The smaller 1.953125-unit cells do not have this blind zone at the edge for that agent radius.

The native Unity test copied the actual UnitMovementMath source. It used a blocked column, a radius-1 agent, speed 10 and a 1/60 timestep. The simplified no-neighbour ORCA branch and the original actuator's position additions were applied for 120 steps. This isolates wall avoidance; it is not a full Main-scene resource/flow/command reproduction. Burst compilation was disabled in the test project to avoid the previously observed compiler crashes; the helper ran managed in Unity with real NativeArray storage.

| Cell size | Before: gradient 0.15 m from wall | Before: blocked frames / crossed | After: blocked frames / crossed |
|---|---|---|---|
| 1.953125 | approximately (-1, 0) | 0 / no | 0 / no |
| 3.90625 | (0, 0) | 23 / yes | 0 / no |

## Change

UnitMovementMath.CalculateGridGradient now measures distance to the closest point of the blocked cell's XZ rectangle. The current blocked cell is also considered, with an outward normal toward the closest edge if the sample lies inside. Invalid geometry/input returns zero.

No swept collision guard was reintroduced. MovementAgentActuatorSystem and flow-field algorithms retain their reverted behavior. Wall avoidance is still a soft steering mechanism, not a proof that all possible commands, unit separation forces, unit radii and step sizes can never cross a blocker.

## Verification

- Unity baseline process exited with code 0.
- Unity candidate process exited with code 0; both simulated scales had zero blocked frames and did not cross.
- Additional Unity checks: 28 passed across cell sizes 1, 1.953125, 3.90625 and 7.8125, covering four wall sides, corners, recovery direction inside a blocked cell, no influence beyond search distance, and a nonzero grid origin.
- Main runtime and Editor C# assemblies built successfully (five pre-existing unrelated warnings).

The temporary isolated project and raw logs were removed during repository cleanup. The measured results and test limitations are retained above.

The supplied live audit remains compatible with this defect: resource footprints are blocked, but avoidance previously failed to respond to their boundaries. The four ramp height seams and unsafe flow directions reported by that audit are separate remaining findings.
