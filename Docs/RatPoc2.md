# Rat PoC 2

## Play

Open `Assets/00.Scenes/CodexRatPoc2Scene.unity` and enter Play Mode.
The original `PoC_SiegeCore` scene and shared player/cannon prefabs are preserved.

| Input | Action |
| --- | --- |
| WASD | Move through the rooms and single ground lane |
| Left mouse | Pick up the nearest eligible rat or cannon |
| Right mouse | Throw the top carried object toward the pointer |
| Hold E | Merge sequentially inside the cyan-side safe room |
| F | Knock back and stun enemies in the facing direction |
| Escape | Pause/resume |

Basic rats spawn at B SUPPLY. Throw a rat into the cannon intake to fire it,
or into the green GROUND GATE to deploy it. The entrance markers are separate
from Siege HP. Once an entrance is destroyed, its room becomes traversable.
FACILITY destruction only logs sabotage; only Siege destruction ends the match.
Restart Play Mode to restart the match.

## Tuning

- `Assets/07.Data/RatPoc2/Rat_*.asset`: sprites, ground HP/damage/range/speed,
  attack interval and projectile damage. Form determines stored Basic count.
- `Rat PoC 2 / RatEncounter`: supply cadence/cap, enemy unit and projectile
  sequences and cadence. Enemy generation does not consume player resources.
- `RatStacking`: stage duration (default 0.6 seconds).
- Rat prefab `RatAgent`: groggy duration (default 3 seconds).
- `RatStructure`: entrance/facility HP. `SiegeHealth`: match HP (300 initially).

`Rat_Basic`, `Rat_BB`, `Rat_BBB` live in `Assets/02.Prefabs/RatPoc2`.
Ground and carry presentation share one Basic prefab. Projectiles use a separate
pooled representation carrying the definition, original faction and factory.

## Circle UI integration

The player has a `RatStacking` component exposing:

```csharp
float Progress;             // 0..1 for the current stage
bool IsStacking;
event Action<float> ProgressChanged;
event Action<RatForm> StageCompleted;
event Action StageCancelled;
```

Subscribe when the UI is enabled, immediately read `Progress` and `IsStacking`,
and unsubscribe when disabled. Drive the user-authored Circle renderer's fill
with `ProgressChanged`. Progress returns to zero after a completed/cancelled
stage; the next held stage starts on the next frame. Circle assets/layout are
intentionally left for the user to configure.

## Rules worth checking in play

- B+B+B merges the top pair into BB, then B+BB into BBB during continued hold.
  Releasing E only cancels the unfinished step. Leaving the safe room does the same.
- Carry slots count objects, not original rats. BBB+B is carryable but cannot merge.
- An enemy remains an enemy after capture. It escapes from the carry stack when
  groggy expires; cannon loading restrains it until firing.
- Captured enemy BBB attacks as the firing cannon's faction, but releases enemy B.
- BBB bursts once on cancellation, Siege impact, flight expiry or ground death.
  Flight expiry is the fallback for missed shots. All releases land on the external
  lane. Scene cleanup/pool disposal is not a burst event.
- Ground units navigate the painted floor; closed enemy entrance cells block paths.
  The player can pass the friendly entrance and a destroyed enemy entrance.

## Validation and scene authoring

`SiegeCore/Validate PoC 2` runs the real scene and pools in Play Mode. It checks
sequential merges, cancellation, over-capacity combinations, all nine projectile
pairings, burst ownership/count, capture/escape/restraint, pool reset, entrance
blocking, AI sabotage and Siege victory. A successful run logs
`RAT_POC2_VALIDATION_OK`. Run from a saved scene; validation opens the PoC scene.

Batch entry point: `-batchmode -nographics -executeMethod RatPocValidation.Run`
(without `-quit`, because the validator exits after Play Mode checks).

`SiegeCore/Create PoC 2 Scene` creates the assets and copies the original scene
only when the dedicated scene does not already exist. It does not overwrite an
authored PoC 2 scene. Generated layout is a prototype: single-cell exterior lane,
two rectangular rooms, one gate/entrance/facility per side and one player cannon.

Deferred: Heavy, general recipes, repairs, player HP, direct projectile infiltration,
facility-specific effects, enemy resource management and final combat balance.

## Verified result — 2026-09-16

Unity 6000.3.9f1 compiled the runtime/editor code and completed the Play Mode
validator with `RAT_POC2_VALIDATION_OK`. The run also verified scene startup,
movement, pause/resume, safety-zone cancellation, actual Siege impact, missed-shot
release, corpse pooling, friendly Gate deployment and firing a captured enemy.
The rendered overview was inspected and the new object sorting layers corrected.

Local evidence: `Logs/RatPoc2Validation.log` and `Logs/RatPoc2Overview.png`.
These generated logs/images are ignored by Git. The isolated validation Editor
also emitted an unrelated `UnityEditor.Search.SearchDatabase` indexing exception;
the validator explicitly excludes that Editor-only exception from gameplay failures.
The new scene uses SceneContext to initialize the pool and enter Playing, then
Siege destruction transitions to GameOver through GameManager.
