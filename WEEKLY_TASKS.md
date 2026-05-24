# Weekly Tasks

- Week of 2026-05-21
  - Implement `command queue` and the related infrastructure.
  - After the infrastructure is finished, refactor the builder system and merge it into the new flow.

## Infrastructure checklist

1. Command model and queue core
   - Standardize `CommandData`, `CommandElement`, and `CommandQueueElement`.
   - Define how each command type is validated and consumed.
   - Make queue processing deterministic and safe to clear after execution.

2. UI to command entry points
   - Update `CommandButton` and `CommandMenu` so they only emit commands.
   - Remove direct gameplay side effects from presentation code.
   - Keep the payload needed for `Move`, `TargetTo`, `Progression`, and `Build`.

3. Production execution path
   - Move unit production execution to the queue-driven flow.
   - Stop calling `TrainUnitHelper` as a direct gameplay path from UI.
   - Keep resource checks and queue limits inside the command execution layer.

4. Build execution path
   - Add a real `Build` command handler.
   - Decide where placement validation and cost payment happen.
   - Route building creation through the same queue/executor pattern as production.

5. Building and construction data flow
   - Review `BuildingData`, `ConstructionData`, and `UnderConstructionTag`.
   - Make sure construction state is applied consistently when a build command runs.
   - Keep reveal/progress logic inside ECS systems, not in UI code.

6. Movement bridge only if build/move needs it
   - Reuse `MovementAgentAPI` and the flow-field bridge only when a command needs target assignment.
   - Do not mix pathing concerns into building/production logic unless required.

## Suggested order

- First: command model and queue core.
- Second: UI entry points.
- Third: production execution path.
- Fourth: build execution path.
- Fifth: construction data flow.
- Sixth: movement bridge if needed.
