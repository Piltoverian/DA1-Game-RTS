# Crash audit, 2026-10-10

Inspected Editor.log in the 12 most recent crash folders under `C:/Users/Legion/AppData/Local/Temp/Unity/Editor/Crashes`, plus the current Editor.log. Classification uses the fatal stack after the last `OUTPUTTING STACK TRACE`, not earlier warnings or import messages. Minidumps were not analyzed in a native debugger.

| Crash folder | Fatal stack group |
|---|---|
| Crash_2026-10-10_144352647 | Grid debug / Handles rectangle |
| Crash_2026-10-10_144012952 | Burst compiler / LLVM 21 |
| Crash_2026-10-10_143346632 | Grid debug / Handles rectangle |
| Crash_2026-10-10_142902681 | Burst compiler / LLVM 21 |
| Crash_2026-10-10_142759876 | Grid debug / Handles rectangle |
| Crash_2026-10-10_142422852 | Burst compiler / LLVM 21 |
| Crash_2026-10-10_141906472 | Grid debug / Handles rectangle |
| Crash_2026-10-10_141254179 | Burst compiler / LLVM 21 |
| Crash_2026-10-10_091316502 | Burst compiler / LLVM 19 |
| Crash_2026-10-10_084415803 | Burst compiler / LLVM 19 |
| Crash_2026-10-10_083554198 | Burst compiler / LLVM 19 |
| Crash_2026-10-09_091948577 | Burst compiler / LLVM 19 |

## Grid debug: repeated call path addressed

All four debug crashes include `UnityEditor.Handles.DrawSolidRectangleWithOutline -> FlowFieldDebugDrawer.DrawWalkability -> OnDrawGizmos`. The newest reproduces during URP rendering of GameView gizmos. Source lines differ because the debug implementation changed, but the call path is the same.

The debug implementation added during this chat introduced this path. Removed all Handles rendering and zTest manipulation from GridDebug.cs. Walkability now uses Gizmos wire lines, with optional depth-test-disabled Debug.DrawLine overlays. Grid coordinates, terrain alignment and cost colors remain. Filled cells and Handles labels are no longer drawn. This removes the observed crash-triggering path; a new Unity session is still required to verify runtime stability.

Unity documents Gizmos drawing in OnDrawGizmos: https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/monobehaviour/ondrawgizmos

## Burst: separate unresolved compiler crash family

Eight reports crash in native burst-llvm on a Burst compiler thread. The latest Burst report includes `WriteModuleObjectToMemory -> WriteModuleObject -> AotModule.WriteModuleToDisk -> HashAndCompileMethodGroup`, so it is a compilation/backend failure, not an executing movement job stack. Earlier reports involve LLVM 19, whereas the newest involve LLVM 21; these establish a repeated compiler crash family, not necessarily one identical internal defect.

The installed package is Burst 1.8.30, Unity 6000.3.13f1. Logs examined do not identify a specific project job responsible for the compilation. No package downgrade, cache deletion, or global Burst disabling was performed without an identified causal link. The Gizmos correction does not resolve these separate compiler crashes. Further isolation requires native dump analysis or a controlled compiler reproduction with compilation timing/target information.

## Live navigation evidence recovered from newest crash log

The last audit ran on **512x512**, cell size **1.953125**, not 256x256:

- Terrain cells incorrectly opened: 0.
- Illegal open tier edges: 0.
- Units in blocked/outside cells: 0 at that snapshot.
- Resource blockers missing/invalid: 0.
- Resource footprint cells still open: 0.
- Pending resource bakes: 0.
- Open ramp edges with height discontinuity: 27.
- Unsafe raw directions: 430017; unsafe stored directions: 429942, aggregated across Ready fields, not unique cells or units.

This snapshot does not support missing resource grid blockage. It supports investigating ramp side-edge height continuity and the disagreement between steering directions and integration corner rules. It does not prove what happened at the reported crossing, because no movement trajectory was captured.

## Verification

Runtime/Editor C# build performed after replacing the drawing path. No GUI or native crash reproduction was run. Existing unrelated compiler warnings remain.
