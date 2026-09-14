# Worker — Blender asset set

Original low-poly worker: teal uniform, safety helmet/vest, backpack, gloves,
boots and a hand tool. Both animation sets share the same mesh and skeleton.
820 vertices, 898 polygons, 16 bones. Rigid weights suit the segmented style.

Source: `Artifacts/Worker/Worker.blend` (outside Assets to avoid automatic
Blender import). Rebuild script and manifest are alongside the source.

## Animation files

Each FBX contains the mesh, rig and one baked animation at 30 fps.
`Normal_` and `Tired_` each include Idle, Walk, Gather, Carry, Build and Rest.
Tired clips use a bent spine, lowered head, shorter strides and longer cycles.
All clips are in place. Carry suggests a loaded backpack; no detachable cargo
object is included. Gather and Build share a tool-swing gesture at different rates.

## Unity setup

Import with Rig > Animation Type = Generic, Avatar Definition = Create From
This Model, root = WorkerRig. Enable Import Animation and Loop Time for the
included looping clips. Use Normal_Idle as the visual model; clips from the
other files target the identical hierarchy. Apply Root Motion should be off.
Extract materials and use URP/Lit with the embedded material colors if Unity
imports the FBX materials with a shader incompatible with this URP project.

## Worker system review and state mapping

The current Worker prefab is a variant of Sodier and overrides its static mesh
with `Assets/Meshes/Zombie_Walk_0.asset`. Neither prefab has an Animator or
SkinnedMeshRenderer. The project uses Entities 1.4.5 and Entities Graphics.
These FBX files are assets, not an installed DOTS animation solution.

| Simulation condition | Suggested animation |
| --- | --- |
| No active job and velocity near zero | Idle |
| MovementAgentComponent.velocity nonzero, empty hands | Walk |
| Enabled WorkerGatherData, State = Gathering | Gather |
| Moving with CarryAmount > 0 | Carry |
| Enabled BuilderComponent, State = Building | Build |
| Explicit resting state | Rest |

WorkerGatherData and BuilderComponent are enableable components. A disabled
component must not keep driving an old job animation. Movement should use
actual velocity rather than only GoingToNode/ReturningDepot/GoingToSite states,
which can remain set while a unit is blocked. There is currently no fatigue
component or rest state in the reviewed Worker code; the Tired set needs an
explicit visual fatigue parameter or a later gameplay system.

No changes were made to Worker simulation, gathering rates, movement speed or
the existing Worker prefab. Runtime integration needs a presentation bridge
for ECS entities and animated visual instances, or an ECS animation renderer.
Attaching a conventional Animator alone does not establish that bridge.

## Verification

All 12 exported FBX files were reimported into Blender: 16 bones, nonempty
animation frame ranges and measurable skinned vertex motion passed for each.
Detailed results: `Artifacts/Worker/validation.json`. Normal_Idle and Tired_Rest
were rendered and visually inspected. Unity import and in-game playback have
not been verified. These are authored starter animations, not motion capture.
