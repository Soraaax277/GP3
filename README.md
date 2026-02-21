# README CHANGES

> Made for huge changes and updates in the game's system

> This document is intended for the development team as a handoff and continuation guide.

> Put what a header of when and who updated who above here:

--------------------------------------------------------------------------------------------
## 02/21/2026 UPDATE (UPDATED BY CHARLES)
---

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Current Implementation Status](#2-current-implementation-status)
3. [Functions Written But Not Yet Called](#3-functions-written-but-not-yet-called)
4. [Units Written But Not Yet In Gameplay](#4-units-written-but-not-yet-in-gameplay)
5. [Missing Major Systems](#5-missing-major-systems)
6. [Tech Tree Lanes — Status](#6-tech-tree-lanes--status)
7. [How to Implement Lane C — Marketing](#7-how-to-implement-lane-c--marketing)
8. [How to Implement Lane D — Sabotage](#8-how-to-implement-lane-d--sabotage)
9. [Cost and Upkeep System — Current State](#9-cost-and-upkeep-system--current-state)
10. [Scripts Not Provided / Missing Dependencies](#10-scripts-not-provided--missing-dependencies)
11. [How-To Guides for Adding New Content](#11-how-to-guides-for-adding-new-content)
12. [Coordination Notes](#12-coordination-notes)
13. [Debug Flags to Disable Before Shipping](#13-debug-flags-to-disable-before-shipping)

---

Core win condition added: most accumulated influence after 100 turns (checked in `TurnManager.CheckGameEnd()`). // change niyo if ever, nilagay ko lang as placeholder

---

## 1. Architecture Overview

| Manager            | File                | Responsibility                                               |       |
| ------------------ | ------------------- | ------------------------------------------------------------ | ----- |
| `GameManager`      | GameManager.cs      | Boot, player creation, initial spawning                      |       |
| `TurnManager`      | TurnManager.cs      | Turn flow, era tracking, unit/tower/wire registration        |       |
| `GridManager`      | GridManager.cs      | Hex grid generation, pathfinding, neighbor queries           |       |
| `InfluenceManager` | InfluenceManager.cs | Per-turn influence scoring across all tiles                  | (NEW) |
| `EconomyManager`   | EconomyManager.cs   | Gold and RP income, upkeep deduction                         | (NEW) |
| `TechManager`      | TechManager.cs      | Tech research, stat multipliers/bonuses, era upgrades        | (NEW) |
| `PowerGridManager` | PowerGridManager.cs | Wire and tower power propagation (BFS from HQ sources)       |       |
| `EnemyAI`          | EnemyAI.cs          | AI turn coroutine: spawning, building, moving units          |       |
| `CameraController` | CameraController.cs | WASD/drag pan, scroll zoom, cutscene lock, focus transitions |       |
| `PlayerInput`      | PlayerInput.cs      | Mouse raycasting for unit selection and movement             |       |

**Game objects on the map:**

- `HexTile` — the map cell. Tracks occupancy, influence by player, and references to whatever is placed on it (`placedNode`, `placedTower`, `placedWire`, `placedUnit`).
- `SignalNode` — the player's HQ building. Propagates signal to connected towers each turn. Registered in `PlayerData.ownedNodes`.
- `TowerNode` — a three-phase structure: Hologram → Constructed → Powered. Decays each turn.
- `WireNode` — connects the power grid. Decays each turn.
- `Unit` (abstract base) — all field units. Subclasses: BuilderUnit, WireSpecialist, Technician, SalesMarketer, Foremen, ITPersonnel, MaintenanceCrew, RoboWorker, RoboMarshall.

**Turn flow (per player):**

```
StartTurn()
  → SignalNode.PropagateSignal() for all players
  → InfluenceManager.RecalculateGlobalInfluence()
  → EconomyManager.ProcessTurnIncome(currentPlayer)
  → Unit.OnTurnStart() for all units
  → TowerNode.ProcessTurnDecay() for all towers (owner only)
  → WireNode.DecayWire() for all wires (owner only)
  → CameraController focus/cutscene
  → EnemyAI.ExecuteTurn() if AI turn
[Player or AI acts]
EndTurn()
  → Advance player index, increment turn, UpdateEra(), SaveSystem.SaveGame()
  → Loop back to StartTurn()
```

---

## 2. Current Implementation Status

### What is fully working

- Hex grid procedural generation with continent post-processing (`GridManager`)
- Turn system with era progression
- HQ placement and signal propagation (BFS through wires)
- Three-phase tower construction: Hologram → Constructed → Powered (auto-handled by `PowerGridManager`)
- Wire placement with cost and length checks
- Influence calculation with era penalty multiplier
- Gold and RP income per turn with tech multipliers
- Upkeep deduction per turn for towers, wires, and units
- EnemyAI executing a full turn (spawn, place towers, lay wires, move units)
- TechManager infrastructure upgrades and unit stat upgrades
- Camera pan, zoom, rotation, focus transitions, and cutscene lock
- UI: GameStatusUI (live resource display), UnitActionPanel, BuildUIManager, TechButton visuals, TechLine animations, Pause menu
- CRT/glitch UI shader effect (UICRTEffect + CRTRuntimeHook)

### What is partially working

- **Lanes A and B of the tech tree** — implemented in TechManager and TechEffect, but have **not been fully tested**. The era upgrade pipeline (`UpgradeHardwareEra`, `UpgradeWorkforceEra`, era multipliers on influence and upkeep) is wired up but needs playtesting to verify numbers feel right and that edge cases (e.g. multiple era upgrades from one session) behave correctly. **Test and adjust values there as needed.**
- **SalesMarketer passive deny** — the fields (`denyRange`, `denyChance`, `denyAmount`) and the range indicator are implemented, but the actual influence suppression logic is not connected to any action or game loop.
- **Save/Load** — `TurnManager` calls `SaveSystem.SaveGame()` and `SaveSystem.LoadGame()`. `GameState.cs` has the full serializable data model. However, `SaveSystem.cs` was not provided and is presumably not yet complete.

### What is not yet started

- Terrain system
- Canteen, Service Center, BPO Center buildings // Basta any building aside sa tower and main building, wala pa
- Tech tree Lane C (Marketing)
- Tech tree Lane D (Sabotage)
- Tech tree fog reveal (`TechTreeFogController.cs` — fully commented out)

---

## 4. Functions Written But Not Yet Called

The following methods exist in the codebase and are not currently triggered by any game system, UI, or AI path. They will need to be wired up when their systems are implemented.

---

### `TowerNode.Power()`

**File:** TowerNode.cs  
**Written intent:** To be called by a Technician unit to manually advance a tower from `Constructed` → `Powered`.  
**Why it's unused:** The `Constructed → Powered` transition is currently handled **automatically** by `PowerGridManager.RefreshGrid()` → `UpdatePowerState(true)`, which checks `if (powered && state == Constructed) state = Powered`. The Technician's `RepairAdjacentStructure()` only handles `Destroyed` towers. There is no unit action, UI button, or AI call that invokes `Power()` directly.  
**To implement:** If you want the Technician to manually power a tower (rather than it being automatic on wire connection), add a new action to `Technician` that finds an adjacent `Constructed` tower and calls `tower.Power()`. Add a button for it in `UnitActionPanel`. Otherwise, the auto-grid approach should be kept and `Power()` can remain dormant.

---

### `TowerNode.SetBuilt()`

**File:** TowerNode.cs  
**Written intent:** Direct state override for the save/load system.  
**Why it's unused:** `SaveSystem.cs` is not yet complete.  
**To implement:** Call from `SaveSystem.LoadGame()` when restoring tower states from `TowerData`.

---

### `WireNode.GetPlacementCost(int baseCost)`

**File:** WireNode.cs  
**Written intent:** Static helper to calculate wire cost after tech discounts.  
**Why it's unused:** `WirePlacementManager` has its own `GetCurrentWireCost()` method that independently reads the same `TechManager.GetInfraMultiplier("WireCost")` value. These two methods are functionally identical. The WireNode static version is never called.  
**Recommendation:** Either route `WirePlacementManager.GetCurrentWireCost()` to call `WireNode.GetPlacementCost()` to reduce duplication, or document that the WirePlacementManager version is canonical and remove the WireNode static.

---

### `WireNode.GetMaxWireLength()`

**File:** WireNode.cs  
**Written intent:** Static helper for wire placement range.  
**Why it's unused:** Same duplication issue as above. `WirePlacementManager.MaxWireLength` property independently reads `TechManager.GetInfraFlatBonus("WireLength")`. The WireNode static is never called.

---

### `HexTile.influenceSuppression`

**File:** HexTile.cs  
**Written intent:** A field intended to track how much influence is being suppressed on a tile (for SalesMarketer / sabotage mechanics).  
**Why it's unused:** The SalesMarketer's deny logic has never been connected. Nothing writes to or reads from this field.  
**To implement:** When a SalesMarketer performs a deny action, write to `tile.influenceSuppression` and have `InfluenceManager.RecalculateGlobalInfluence()` subtract the suppression value when tallying player influence on that tile. See Section 8.

---

### `SalesMarketer` — Deny Action (denyRange, denyChance, denyAmount)

**File:** SalesMarketer.cs  
**Written intent:** The SalesMarketer passively or actively reduces enemy influence in a radius using `denyChance` and `denyAmount`.  
**Why it's unused:** No `PerformDeny()` or equivalent method exists on the class. The AI's `HandleSalesMarketer()` only moves the unit toward enemy-influenced tiles but never triggers any denial. `UnitActionPanel` has no button for SalesMarketer actions.  
**To implement:** See Section 8 (Lane D / Sabotage) — the SalesMarketer deny system is the entry point for sabotage mechanics.

---

### `BuilderUnit.RepairAdjacentStructure()`

**File:** BuilderUnit.cs  
**Written intent:** Allows a Builder to repair a destroyed tower once the "Versatile Builder Tool Kit" tech is unlocked.  
**Why it's unused:** `UnitActionPanel` has no repair button for BuilderUnit (only a construct button). The AI's `HandleBuilder()` also never calls this. The method is complete with cost deduction and efficiency multiplier, but has no trigger.  
**To implement:** Add a `repairButton` to `UnitActionPanel` conditioned on `unit is BuilderUnit && builder.canRepairInfrastructure`. Call `builder.RepairAdjacentStructure()` on click. For the AI, add a check in `HandleBuilder()` if `builder.canRepairInfrastructure` and there are destroyed adjacent towers.

---

### `WireSpecialist.RepairAdjacentTower()`

**File:** WireSpecialist.cs  
**Written intent:** Allows a Wire Specialist to repair destroyed towers once "Versatile Repairmen" tech is unlocked.  
**Why it's unused:** No UI button or AI path calls this. Same situation as BuilderUnit repair.  
**To implement:** Same pattern as BuilderUnit repair — add conditionally visible button in `UnitActionPanel` and an AI handling branch.

---

### `GameStatusUI.UpdateUI(bool force)`

**File:** GameStatusUI.cs  
**Written intent:** Force a full UI redraw after loading a saved game.  
**Why it's unused (partially):** Called from `Start()` with `force=true` for initialization. The "on Load Game" use case is not yet wired because `SaveSystem.LoadGame()` exists only as a call in TurnManager, not as a complete implementation.  
**To implement:** Call `GameStatusUI.Instance.UpdateUI(true)` from `SaveSystem.LoadGame()` after all game state has been restored.

---

## 5. Units Written But Not Yet In Gameplay

The following unit classes are fully written with Initialize, stat upgrades, actions, and Die logic. However, only **BuilderUnit, WireSpecialist, Technician, and SalesMarketer** are currently active in the game (available to player via `UnitPurchaseUI` and used by `EnemyAI`).

The remaining units exist in the codebase but have no spawn buttons, no prefab assignments in `EnemyAI`, and no tech unlock entries that would surface them to the player:

| Unit Class        | File               | Notes                                                                                                                                                                                                     |
| ----------------- | ------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Foremen`         | Foremen.cs         | Higher-end builder. 5 build charges, move 3. Starts with construction. Higher upkeep (20g). Unlock: "Increased Workforce Size" tech.                                                                      |
| `ITPersonnel`     | ITPersonnel.cs     | Elite repair unit. Can repair both towers AND wires from spawn. 1.5x repair efficiency, move 3. Upkeep 18g. Unlock: "Repair Specialization" tech.                                                         |
| `MaintenanceCrew` | MaintenanceCrew.cs | Combined builder/repair. 4 charges, move 2. Tower repair requires "Versatile Repairmen" tech unlock. Upkeep 15g. Unlock: "Company Service Centers" tech.                                                  |
| `RoboWorker`      | RoboWorker.cs      | Robot builder. 6 charges, move 4, build range 2. **Zero upkeep.** Unlock: "Fully Mechanical Workforce" tech.                                                                                              |
| `RoboMarshall`    | RoboMarshall.cs    | Robot repair. 5 charges, 1.5x efficiency, move 4. Can repair towers and wires. **Zero upkeep.** Has 10% full-restore chance with "UntestedStimulants" feature. Unlock: "Fully Mechanical Workforce" tech. |

### To add a new unit to the game

1. Ensure a prefab exists for the unit with the correct script component attached.
2. In the `UnitSpawner.GetRecruitmentCost()` switch statement, a case for the class name already exists for all units above. No change needed there.
3. Add a `UnitPurchaseButton` in the `UnitPurchaseUI` panel. Assign the prefab to its `unitPrefab` field in the Inspector.
4. For the AI to use the unit: add a `public GameObject [unitName]Prefab` to `EnemyAI`, assign in Inspector, and add a spawn condition block in `AITurnRoutine()` following the same pattern as the existing builder/specialist/technician blocks.
5. If the unit is unlocked via a tech node: create a `TechNode` ScriptableObject, add a `TechEffect` with `EffectType.UnlockUnit`, set `targetUnits` to the prefab. When researched, `TechManager.ResearchTech()` will add the unit name to `unlockedUnitNames`. You would then gate the `UnitPurchaseButton` visibility on `TechManager.Instance.unlockedUnitNames.Contains("RoboWorker")` or similar.
6. Tech effects (stat upgrades) will automatically be applied to newly spawned units via `TechManager.ApplyEffectsToNewUnit(unit)` called from `UnitSpawner.SpawnUnit()`.

---

## 6. Missing Major Systems

### 6.1 Terrain System

**Status:** Not started. No terrain-related code exists in any provided script.  
**What will be needed:**

- `HexTile` would need a `TerrainType` enum field (e.g. Plains, Forest, Urban, Water border).
- `GridManager.GenerateGrid()` would need to assign terrain types during or after noise generation.
- Terrain should affect movement cost (modify `movementRemaining` deduction per step in `Unit.MoveRoutine()`), influence generation multipliers (in `HexTile.GetTotalInfluence()` or `InfluenceManager.RecalculateGlobalInfluence()`), and possibly tower placement validity (`TowerPlacementManager.ValidateTile()`).
- Visual: terrain tile prefab variants or material swapping in `HexTile`.

### 6.2 Canteen

**Status:** Not started.  
**Intended role:** Likely a support building that reduces unit upkeep or increases unit action charges.  
**Implementation path:** This is a standalone building type — not directly part of Lane C (Marketing). The cleanest implementation uses a new `TechEffect` with `EffectType.UpgradeInfrastructure`, `infraStatName = "MaintenanceCost"`, `isMultiplier = true`, `infraValueMod = -0.1` (for -10% upkeep). `EconomyManager.CalculateTotalUpkeep()` already reads `TechManager.GetInfraMultiplier("MaintenanceCost")` via `TurnManager.GetUpkeepMultiplier()`, so the pipeline is there — you only need the TechNode data and the building placement to activate it. If it needs to be a placeable map object, follow the SignalNode pattern (see Section 11, Adding a New Building Type).

### 6.3 Service Center

**Status:** Not started.  
**Intended role:** A placed building that boosts gold income or enables the MaintenanceCrew unit.  
**Implementation path:** This is a standalone building type — not directly part of Lane C (Marketing). A `TechEffect` with `infraStatName = "FinalRevenueGain"` or `"TowerRevenue"` would boost income as a passive tech unlock. Alternatively, the Service Center as a physical building would need its own MonoBehaviour, HexTile placement logic, and registration with the economy system (see Section 11, Adding a New Building Type).

### 6.4 BPO Center

**Status:** Not started.  
**Intended role:** Likely generates bonus Research Points per turn.  
**Implementation path:** This is a standalone building type — not directly part of Lane C (Marketing). A `TechEffect` with `infraStatName = "ResearchGain"` or a new `infraStatName = "BPOResearch"` would feed into `EconomyManager.ProcessTurnIncome()`. Alternatively, as a physical building with its own RP output, it would require a new field in `EconomyManager` to sum contributions from placed BPO buildings each turn. The simplest approach: add `rpBonusPerTurn` to the associated `TechNode` (the field already exists on TechNode), and the TechManager will accumulate it automatically via `_totalRPBonusPerTurn`.

---

## 7. Tech Tree Lanes — Status

| Lane | Name      | Status                                                                                                                                                                                                                                                                                                                              |
| ---- | --------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| A    | Hardware  | Implemented. Needs more testing — verify era upgrade pipeline, influence penalties, and multiplier stacking under extended play.                                                                                                                                                                                                    |
| B    | Workforce | Implemented. Needs more testing — verify upkeep penalty pipeline and labor mismatch edge cases.                                                                                                                                                                                                                                     |
| C    | Marketing | **Not implemented.** TechNode ScriptableObjects and their associated TechEffect data do exist. The SalesMarketer unit exists but its upgrades are not yet driven by any tech data. The lane spans all four eras (Industrial through Future) and is entirely focused on SalesMarketer and influence suppression mechanics.           |
| D    | Sabotage  | **Not implemented.** The unlock mechanism (`_sabotageTabUnlocked`, `IsSabotageTabUnlocked()`) is ready in TechManager, but no sabotage unit actions, and no suppression logic exist yet. **Critically: Lane D does not start until the Retro era** — there are no Industrial or Eighties tier nodes for this lane. (change if ever) |

### ⚠️ DO NOT MODIFY `TechTreeWindowManager.cs`

The fog reveal system (`TechTreeFogController.cs`) is actively being implemented and was the reason this branch was pushed. `TechTreeWindowManager` is tightly coupled to the fog controller logic and tab visibility. Work on Lane C and D **can and should begin** using TechNode ScriptableObjects, TechEffect data, and unit/building code **without touching TechTreeWindowManager**. The window manager integration (registering Lane C/D buttons, connecting fog states) will be handled separately once the fog system is complete.

### Testing Lanes A and B

When testing:

- Verify that `TurnManager.GetEraInfluenceMultiplier()` correctly debuffs influence at each era gap (gap 1 = 0.75x, gap 2 = 0.50x, gap 3 = 0.25x).
- Verify that `TurnManager.GetUpkeepMultiplier()` correctly penalizes upkeep for labor mismatch (gap 1 = 1.5x, gap 2 = 2.0x).
- Confirm that `TechManager.freeResearchMode` is OFF during testing (see Section 13).
- Confirm that era upgrades from `TechEffect.EffectType.UpgradePlayerEra` correctly advance `PlayerData.hardwareEra` and `PlayerData.workforceEra` only up to `Futuristic`, not beyond.
- Test that `InfluenceManager.RecalculateGlobalInfluence()` correctly applies `GetEraInfluenceMultiplier()` per player and that the multiplier shows up correctly in the debug log.

---

## 8. How to Implement Lane C — Marketing

Lane C is the **Marketing** lane. It is entirely focused on influence suppression, promotional campaigns, and business-presence mechanics. The lane runs from the **Industrial era through to the Future era** and is the primary driver of the SalesMarketer unit's upgrades. Every node in this lane either upgrades the SalesMarketer's deny/suppress capabilities, boosts revenue and RP generation, or unlocks new marketing-related mechanics.

The existing `SalesMarketer.cs` fields (`denyRange`, `denyChance`, `denyAmount`, `ReceiveStatUpgrade()`) and the `HexTile.influenceSuppression` hook are the core infrastructure this lane targets. Critically, **the deny action itself does not yet exist** — see Step 4 below, which is the single most important code change for this lane to have any gameplay impact.

### Step 1 — Create the TechNode ScriptableObjects (DONE for structure, data still needed)

For each Lane C node visible in the tech tree image:

1. Right-click in the Project window → `Create → Tech Tree → Tech Node`.
2. Fill in `techName`, `description`, `researchCost`, `goldCost`, and `preReqs` (chain them together to match the layout in the image).
3. Under `unlockEffects`, add one or more `TechEffect` entries per node (see Step 2 for effect types to use).
4. Wire up each node's `TechButton` in the scene (assign the ScriptableObject to `TechButton.tech`).
5. Run `TechTreeGraph.GenerateConnections()` (right-click the TechTreeGraph component in the Inspector → "Regenerate Connections") to auto-draw the connector lines between nodes.

**No code changes are needed for this step** — TechManager, TechEffect, and TechNode already support everything required.

### Step 2 — Marketing Lane Effects via TechEffect

The majority of Lane C nodes will use one of these two effect patterns:

**Pattern A — SalesMarketer stat upgrades (most common for Lane C):**

Use `EffectType.UpgradeUnitStat`. These are immediately applied to all existing SalesMarketer units and to any spawned afterward, automatically, via `TechManager.ApplyEffectsToNewUnit()` called from `UnitSpawner.SpawnUnit()`. No additional code is needed — the `SalesMarketer.ReceiveStatUpgrade()` method already handles all of the following stat names.
// nilagay ko muna sa salesmarketer as the 'scout', change niyo na lang if ever

| Node type            | statToUpgrade  | amount (example) | What it does in SalesMarketer.cs                                      |
| -------------------- | -------------- | ---------------- | --------------------------------------------------------------------- |
| Deny range increase  | `"DenyRange"`  | `1`              | Expands suppression radius by 1 hex; also resizes the range indicator |
| Deny chance increase | `"DenyChance"` | `0.10`           | +10% probability of successful influence denial per tile roll         |
| Deny power increase  | `"DenyAmount"` | `2`              | Increases influence removed per successful deny by 2                  |
| Movement upgrade     | `"MoveRange"`  | `1`              | SalesMarketer moves further per turn (handled in base `Unit`)         |
| Extra action charges | `"Actions"`    | `1`              | Increases available action count (subclass handling required if used) |

**Pattern B — Infrastructure/income upgrades (for revenue/RP-boosting nodes):**

Use `EffectType.UpgradeInfrastructure`. These feed directly into `EconomyManager.ProcessTurnIncome()` and `TurnManager.GetUpkeepMultiplier()` each turn. No additional code needed — the pipelines are fully wired.

| Node type                  | infraStatName                    | isMultiplier | infraValueMod | Where it is consumed                                                          |
| -------------------------- | -------------------------------- | ------------ | ------------- | ----------------------------------------------------------------------------- |
| Revenue multiplier         | `"FinalRevenueGain"`             | ✅           | `0.10`        | `EconomyManager.ProcessTurnIncome()`                                          |
| RP income multiplier       | `"ResearchGain"`                 | ✅           | `0.15`        | `EconomyManager.ProcessTurnIncome()`                                          |
| Passive RP per turn (flat) | set `rpBonusPerTurn` on TechNode | n/a          | any int       | `TechManager.GetTotalRPBonus()` → `EconomyManager`                            |
| Influence radius expansion | `"InfluenceRadius"`              | ☐ (flat)     | `1`           | `SignalNode.CurrentInfluenceRadius`                                           |
| Upkeep reduction           | `"MaintenanceCost"`              | ✅           | `-0.1`        | `TurnManager.GetUpkeepMultiplier()` → `EconomyManager.CalculateTotalUpkeep()` |

**Files to touch for Steps 1 and 2:** Only Inspector/ScriptableObject data. No `.cs` file edits required.

### Step 3 — Unlock the SalesMarketer via Lane C

The "Freelance Brand/Service Promoter" node at the start of Lane C is the natural place to gate the SalesMarketer unit behind a tech requirement. Currently the SalesMarketer is available from the start with no unlock gate. If it should be locked behind this node:

1. Add a `TechEffect` with `EffectType.UnlockUnit` to the "Freelance Brand/Service Promoter" TechNode.
2. Set `targetUnits` to the SalesMarketer prefab.
3. When researched, `TechManager.ResearchTech()` will add `"SalesMarketer"` to `TechManager.unlockedUnitNames` automatically.
4. In `UnitPurchaseUI`, gate the SalesMarketer's `UnitPurchaseButton` visibility on `TechManager.Instance.unlockedUnitNames.Contains("SalesMarketer")`.

**Files to touch:** `UnitPurchaseUI.cs` or the individual `UnitPurchaseButton` GameObject's `OnEnable`/visibility logic. The TechNode ScriptableObject data requires no code change.

### Step 4 — Implementing the SalesMarketer Deny Action ⚠️ Required for Lane C to have any gameplay impact

The deny logic does not yet exist in the codebase. Without this step, all of Lane C's stat upgrades will accumulate on the SalesMarketer but produce no in-game result. This is the single most critical code change for the entire Marketing lane.

Add a `PerformDeny()` method to `SalesMarketer.cs`:

```
For each tile within denyRange of the SalesMarketer's currentTile:
    If tile.influenceByPlayer contains an enemy player entry with value > 0:
        Roll Random.value against denyChance.
        If the roll succeeds:
            call tile.RemoveInfluence(enemyPlayer, denyAmount)
            (tile.RemoveInfluence() already exists on HexTile and clamps to 0)
Call ConsumeAction() after the loop.
```

`tile.RemoveInfluence()` already exists on `HexTile` and clamps at zero, so no new HexTile code is needed for this approach.

**Alternative — persistent suppression approach:** If the design intent is that suppression persists across turns (accumulates and fades gradually rather than being a single-turn instant removal), use `tile.influenceSuppression` instead. Write `tile.influenceSuppression += denyAmount` during the deny loop. Then in `InfluenceManager.RecalculateGlobalInfluence()`, subtract `tile.influenceSuppression` from the tallied influence score before adding it to the player total. Add a decay step in `TurnManager.StartTurn()` that iterates all tiles and reduces `tile.influenceSuppression` by a fixed amount each turn to prevent indefinite stacking.

**Files to touch:** `SalesMarketer.cs` (add `PerformDeny()`). For persistent suppression only: also `HexTile.cs` (ensure `influenceSuppression` initializes to 0), `InfluenceManager.cs` (subtract during tally), `TurnManager.cs` (decay step).

### Step 5 — Add the Deny Button to UnitActionPanel

`UnitActionPanel.cs` currently has buttons for construct (Builder), buildWire (WireSpecialist), and repair (Technician). The SalesMarketer has no button yet.

1. Add `public Button denyButton;` (or `marketingButton`) as a serialized field in `UnitActionPanel`.
2. In `Open(Unit unit)`, add the following block alongside the existing button setup:
   ```
   bool isMarketer = unit is SalesMarketer;
   denyButton.gameObject.SetActive(isMarketer);
   if (isMarketer) denyButton.interactable = unit.CanAct;
   ```
3. Add `OnClickDeny()` that casts `currentUnit` to `SalesMarketer` and calls `marketer.PerformDeny()`, then calls `Close()`.
4. In the Unity Inspector, wire the button's `onClick` event to `UnitActionPanel.OnClickDeny()`.

**Files to touch:** `UnitActionPanel.cs`, UnitActionPanel prefab in the Unity Inspector.

### Step 6 — Wire the AI for Deny

In `EnemyAI.HandleSalesMarketer()`, currently the AI only moves toward enemy-influenced tiles. After the movement block, add:

```csharp
if (marketer.CanAct)
    marketer.PerformDeny();
```

This follows the same movement-then-act pattern already in place for BuilderUnit (`HandleBuilder`) and Technician (`HandleTechnician`) in the AI coroutine.

**Files to touch:** `EnemyAI.cs`.

### Step 7 — If Lane C introduces additional Marketing units in Future era

If the Future-era nodes of Lane C unlock a new unit type beyond the SalesMarketer:

1. Create a new class inheriting from `Unit`, following the SalesMarketer pattern.
2. Implement `Initialize()`, `ReceiveStatUpgrade()` (handle at minimum `"DenyRange"`, `"DenyChance"`, `"DenyAmount"` and `"MoveRange"` for consistency with the lane), `UnlockSkill()`, and the unit's primary action.
3. Add a cost case to `UnitSpawner.GetRecruitmentCost()` for the new class name.
4. Unit registration with `TurnManager`, spawn, and automatic tech effect application are all handled by the base `Unit.Initialize()` → `TurnManager.RegisterUnit(this)` and `UnitSpawner.SpawnUnit()` → `TechManager.ApplyEffectsToNewUnit()` pipeline. No extra wiring needed there.
5. Add a `TechEffect` with `EffectType.UnlockUnit` to the appropriate Future-era TechNode. Set `targetUnits` to the new prefab.
6. Gate the `UnitPurchaseButton` visibility on `TechManager.Instance.unlockedUnitNames.Contains("YourNewUnit")`.

**Files to touch:** New unit `.cs` file, `UnitSpawner.cs` (add cost case), `EnemyAI.cs` (add spawn condition block and action coroutine), `UnitActionPanel.cs` (add action button and handler), `UnitPurchaseUI` prefab (add purchase button in Inspector).

---

## 9. How to Implement Lane D — Sabotage

The sabotage system has significant groundwork already in place. Here is the full implementation path.

### ⚠️ Important: Lane D starts in the Retro era only

Unlike Lanes A, B, and C which all have nodes beginning in the Industrial era, **Lane D has no Industrial or Eighties tier nodes at all**. This has a few important consequences:

- The sabotage tab in the UI should not become visible or accessible until the first Retro-era Lane D node is researched.
- The first Lane D node must have `unlocksSabotageTab = true` set on its TechNode ScriptableObject. When that node is researched, `TechManager.ResearchTech()` automatically sets `_sabotageTabUnlocked = true` and calls `TechTreeWindowManager.Instance.RefreshSabotageButton()`, which enables the tab button. This entire mechanism requires **no code change** — only the TechNode data.
- Until that node is researched (i.e. for the entire Industrial and Eighties eras), the sabotage tab button remains disabled as governed by the existing `IsSabotageTabUnlocked()` check.
- Lane D shares the SalesMarketer unit and its `PerformDeny()` action with Lane C. If Lane C's deny action has already been implemented (Section 8, Step 4), Lane D can build on top of it immediately.

### Existing hooks ready to use

- `TechManager._sabotageTabUnlocked` and `IsSabotageTabUnlocked()` — already implemented and checked by `TechTreeWindowManager.RefreshSabotageButton()`. Wiring requires only a TechNode with `unlocksSabotageTab = true`.
- `HexTile.influenceSuppression` — a field that exists but is never written to or read. This is the intended persistent suppression hook for Lane D's stronger deny effects.
- `SalesMarketer.denyRange`, `denyChance`, `denyAmount` — all defined and upgradeable via `ReceiveStatUpgrade()`. No new code needed for these stats. The range indicator is already shown in-game.

### Step 1 — Create the Sabotage TechNode ScriptableObjects (DONE)

**No code change needed for the tab unlock mechanism.** Only TechNode ScriptableObject data needs to be created.

### Step 2 — Implement the SalesMarketer Deny Action

This is the same Step 4 from Section 8 (Lane C) — the `PerformDeny()` method on `SalesMarketer.cs` is shared between both lanes. Lane C nodes upgrade the stats that feed into it, and Lane D nodes further amplify those stats or unlock additional sabotage behaviors on top of the deny foundation.

If you are implementing Lane D before Lane C's deny action is complete, `PerformDeny()` still needs to be written first. See Section 8, Step 4 for the full implementation guide. Add `PerformDeny()` to `SalesMarketer.cs`, add the deny button in `UnitActionPanel`, and connect it in `EnemyAI` as described there. Everything in this step is identical whether you are coming at it from the Lane C or Lane D angle.

**Files to touch:** `SalesMarketer.cs`, `UnitActionPanel.cs`, `EnemyAI.cs`.

### Step 3 — Sabotage-Specific Effects via TechEffect

Lane D nodes will use the same `EffectType.UpgradeUnitStat` patterns as Lane C (targeting `SalesMarketer` with `"DenyRange"`, `"DenyChance"`, `"DenyAmount"`) to push the SalesMarketer to its full power level. For new mechanics unique to Lane D:

**If "Contract Smuggling" steals enemy resources:**
This would require a new method on SalesMarketer (e.g. `PerformSmuggling()`) or a dedicated sabotage unit. There is no existing hook for resource stealing — this is entirely new code. The method would deduct from the enemy `PlayerData.resources` and add to the acting player's resources. Gate it behind `TechManager.Instance.IsFeatureUnlocked("ContractSmuggling")` by adding a `TechEffect` with `EffectType.UnlockFeature`, `featureName = "ContractSmuggling"` to the relevant TechNode.

**If nodes introduce persistent influence suppression (beyond the single-turn instant deny):**
Use `HexTile.influenceSuppression`. During `PerformDeny()`, write `tile.influenceSuppression += denyAmount` to the targeted tiles instead of (or in addition to) calling `RemoveInfluence()`. Then:

- In `InfluenceManager.RecalculateGlobalInfluence()`, subtract `tile.influenceSuppression` from the tallied influence score for each tile before adding it to the player's total.
- In `TurnManager.StartTurn()`, add a decay loop that iterates all `GridManager.Instance.tiles.Values` and reduces each tile's `influenceSuppression` by a fixed amount (e.g. 2 per turn) to prevent permanent indefinite stacking.

**Files to touch for persistent suppression:** `SalesMarketer.cs` (write suppression), `HexTile.cs` (ensure `influenceSuppression` initializes to 0 — confirm this in the field declaration), `InfluenceManager.cs` (subtract during tally), `TurnManager.cs` (add decay loop at turn start).

### Step 4 — Add the Sabotage Action Button in UnitActionPanel

This is the same step as Section 8, Step 5 — the deny button added for Lane C also serves Lane D. If Lane D introduces a second distinct action (e.g. `PerformSmuggling()`), add a second button to `UnitActionPanel` conditioned on the feature unlock:

```
bool smugglingUnlocked = TechManager.Instance.IsFeatureUnlocked("ContractSmuggling");
smugglingButton.gameObject.SetActive(isMarketer && smugglingUnlocked);
if (isMarketer && smugglingUnlocked) smugglingButton.interactable = unit.CanAct;
```

**Files to touch:** `UnitActionPanel.cs`, UnitActionPanel prefab (Inspector).

### Step 5 — Wire the AI for Sabotage

In `EnemyAI.HandleSalesMarketer()`, after the movement block, add the deny/sabotage call (same as Section 8, Step 6). If additional sabotage-specific actions exist, add conditional branches:

```csharp
if (marketer.CanAct)
{
    // Prefer smuggling if unlocked, otherwise fall back to standard deny
    if (TechManager.Instance.IsFeatureUnlocked("ContractSmuggling"))
        marketer.PerformSmuggling();
    else
        marketer.PerformDeny();
}
```

**Files to touch:** `EnemyAI.cs`.

### Step 6 — Sabotage Unit Stat Upgrades via TechEffect

Existing `SalesMarketer.ReceiveStatUpgrade()` already handles `"DenyRange"`, `"DenyChance"`, `"DenyAmount"`, and `"Actions"` with no additional code needed. TechNode data drives all of these.

Example TechEffect setup for a "Subversive Tactics" node:

- `EffectType.UpgradeUnitStat`, `statToUpgrade = "DenyChance"`, `amount = 0.15`, `targetUnits = [SalesMarketerPrefab]`.

**Files to touch:** None — only TechNode ScriptableObject data in the Inspector.

### Step 7 — New Dedicated Sabotage Units (If Future-era Lane D nodes introduce them)

If Lane D's Future-era branching nodes unlock a unit type beyond the SalesMarketer:

1. Create a new class inheriting from `Unit`.
2. Implement `Initialize()`, `ReceiveStatUpgrade()`, `UnlockSkill()`, and the dedicated sabotage action (e.g. `PerformSabotage()`).
3. Add a cost case to `UnitSpawner.GetRecruitmentCost()` for the new class name.
4. Unit registration with `TurnManager`, spawn pipeline, and automatic tech effect application are all handled by the base `Unit.Initialize()` → `TurnManager.RegisterUnit(this)` and `UnitSpawner.SpawnUnit()` → `TechManager.ApplyEffectsToNewUnit()`. No extra wiring needed.
5. Add a `TechEffect` with `EffectType.UnlockUnit` to the appropriate Future-era TechNode. Set `targetUnits` to the prefab.
6. Gate the `UnitPurchaseButton` visibility on `TechManager.Instance.unlockedUnitNames.Contains("YourSabotageUnit")`.

**Files to touch:** New unit `.cs` file, `UnitSpawner.cs` (cost case), `EnemyAI.cs` (spawn condition and action coroutine), `UnitActionPanel.cs` (action button and handler), `UnitPurchaseUI` prefab (purchase button in Inspector).

---

## 10. Cost and Upkeep System — Current State

**The cost and upkeep code is written and largely functional, but some parts are not yet fully surfaced in the player-facing UI.** This is an active work area. If you are planning to work on this, flag it so we don't step on each other — the current focus ko (charles) here is the **Tech Tree UI (fog system)**.

### What is already deducting correctly

| System                | File                     | Status                                                                                                                                     |
| --------------------- | ------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------ |
| Wire placement cost   | WirePlacementManager.cs  | ✅ Deducted on placement via `PlaceWire()`                                                                                                 |
| Tower placement cost  | TowerPlacementManager.cs | ✅ Deducted on placement via `PlaceTower()`. Note: `baseTowerCost = 0` by default — towers are currently free unless changed in Inspector. |
| Unit recruitment cost | UnitSpawner.cs           | ✅ Deducted in `SpawnUnit()`. Per-type base costs are defined in `GetRecruitmentCost()`.                                                   |
| Tower upkeep per turn | EconomyManager.cs        | ✅ Tallied via `TurnManager.GetAllTowers()` and `tower.GetCurrentUpkeep()`                                                                 |
| Wire upkeep per turn  | EconomyManager.cs        | ✅ Tallied via `TurnManager.GetAllWires()`                                                                                                 |
| Unit upkeep per turn  | EconomyManager.cs        | ✅ Tallied via `TurnManager.GetAllUnits()` and `unit.goldUpkeep`                                                                           |

### What is missing

- **Pre-purchase cost display in UI:** No tooltip or label shows the recruit cost of a unit before the player clicks the purchase button in `UnitPurchaseUI`. The deduction happens silently.
- **Build cost display:** No label shows the tower or wire placement cost during drag/placement mode. The hologram color (green/red) indicates validity but not the gold amount.
- **Insufficient funds feedback:** If the player tries to recruit a unit or place a wire they can't afford, the system logs a warning and silently refuses, but no in-game message or UI feedback is shown to the player.

### What to do when implementing the cost UI

- `WirePlacementManager.GetCurrentWireCost()` — call this to display wire cost in the placement overlay.
- `TowerPlacementManager.GetCurrentTowerCost()` — call this for tower placement cost display.
- `UnitSpawner.GetRecruitmentCost(unitPrefab)` — this is `private`. You will need to make it `public` or add a new public method to read costs before attempting a purchase.
- `GameStatusUI` already updates gold in real time, so the player will see their gold drop immediately after a purchase.

---

## 11. How-To Guides for Adding New Content

### Adding a New Tech Upgrade Effect

1. Open or create a `TechNode` ScriptableObject (`Create → Tech Tree → Tech Node`).
2. Under `unlockEffects`, add a new `TechEffect` entry.
3. Choose `EffectType`:
   - `UpgradeInfrastructure` — affects towers, wires, income, signal. Set `infraStatName` (see TechManager.cs header comment for all valid names), `infraValueMod`, and `isMultiplier`. Consumed by `TechManager.GetInfraMultiplier()` or `GetInfraFlatBonus()` wherever that stat is read.
   - `UpgradeUnitStat` — affects a specific unit class. Set `targetUnits` (prefab list), `statToUpgrade`, `amount`. Applied to existing units immediately and to new units on spawn via `ApplyEffectsToNewUnit()`.
   - `UnlockUnit` — adds unit name to `TechManager.unlockedUnitNames`. Gate spawn button visibility on this.
   - `UnlockSkill` — calls `unit.UnlockSkill(skillName)` on target units. Each unit class has `override void UnlockSkill(string)` to handle specific skill names.
   - `UnlockFeature` — adds a string key to `TechManager.unlockedFeatures`. Gate mechanic availability on `TechManager.Instance.IsFeatureUnlocked("YourFeatureName")`.
   - `UpgradePlayerEra` — advances Hardware or Workforce era. Set `isHardwareEra` toggle.
4. The `TechEffectDrawer` custom Inspector will show only the relevant fields for the selected type.
5. **No `.cs` file changes are needed** for most new effects — the data drives everything.

### Adding a New Infrastructure Stat

If none of the existing `infraStatName` values cover your new effect:

1. Pick a new unique string key (e.g. `"CanteenCapacity"`).
2. Wherever the stat should be consumed (e.g. `EconomyManager.ProcessTurnIncome()`, a unit's action method, etc.), add a call to:
   - `TechManager.Instance.GetInfraMultiplier("CanteenCapacity")` for multiplier effects, or
   - `TechManager.Instance.GetInfraFlatBonus("CanteenCapacity")` for additive bonuses.
3. Document the new key in the comment block at the top of `TechManager.cs` under the appropriate category.
4. Create a `TechEffect` in a TechNode with `infraStatName = "CanteenCapacity"`.

**Files to touch:** The consuming system (e.g. `EconomyManager.cs`), and a note added to `TechManager.cs` header comment. No changes to TechManager logic itself.

### Adding a New Unit Type

1. Create `YourUnit.cs` inheriting from `Unit`.
2. Override `Initialize(HexTile, PlayerData)` — call `base.Initialize()`, then `SetMoveRange(n)`, set `goldUpkeep`.
3. Override `ReceiveStatUpgrade(string statName, float amount)` — call `base.ReceiveStatUpgrade()` first, then handle unit-specific stat names.
4. Override `UnlockSkill(string skillName)` for any skill-gated abilities.
5. Write the primary action method (e.g. `PerformAction()`). Call `ConsumeAction()` at the end. Call `Die()` if charges run out.
6. In `Die()`: null the `currentTile.placedUnit`, call `TurnManager.Instance.UnregisterUnit(this)`, then `Destroy(gameObject)`.
7. Add a cost case to `UnitSpawner.GetRecruitmentCost()`.
8. Create a prefab. Assign the script component.
9. Add a `UnitPurchaseButton` in `UnitPurchaseUI` prefab. Assign prefab to `unitPrefab`.
10. Add a button + handler in `UnitActionPanel` for the unit's action.
11. For AI: add prefab field to `EnemyAI`, handle in `AITurnRoutine()`.
12. For tech unlock: create a TechNode with `EffectType.UnlockUnit`, assign prefab to `targetUnits`.

### Adding a New Building Type

Follow the `SignalNode` pattern:

1. Create `YourBuilding.cs` as a `MonoBehaviour`.
2. Add `Initialize(HexTile tile, PlayerData owner)` — assign tile, set `tile.placedNode = this` (or a new HexTile field), add to player's owned list.
3. Register with `TurnManager` if it needs per-turn processing (add a `RegisterBuilding()` method and list).
4. In `EconomyManager.ProcessTurnIncome()`, iterate your building list and apply effects.
5. Add a placement button and call a spawner from `BuildUIManager` or a dedicated UI panel.
6. If the AI should build it, add logic to `EnemyAI.AITurnRoutine()`.

### Adding a New UI Animation

`UIAnimator` (not provided in submitted scripts) appears to be the component on panels that handles entry/exit tweens. `BuildUIManager.CloseBuildMenu()` and `UnitActionPanel.Close()` both call:

```csharp
UIAnimator animator = panel.GetComponent<UIAnimator>();
if (animator != null)
    animator.AnimateExit(() => { panel.SetActive(false); });
```

To add an animation to a new panel:

1. Add the `UIAnimator` component to the panel GameObject.
2. Assign a `UITheme` ScriptableObject (create via `Create → UI → Animation Theme`). Configure `AnimationStyle` (Scale, Shutter, Slide, PopUp), durations, and ease curves in the theme asset.
3. `UIAnimationManager` holds references to default themes (`defaultWindowTheme`, `defaultButtonTheme`, etc.) and distributes them — check if UIAnimator auto-pulls from UIAnimationManager or requires manual assignment.
4. When closing the panel in code, call `animator.AnimateExit(callback)` where callback sets the panel inactive. When opening, call `animator.AnimateEntry()` if such a method exists.

**Files to touch:** The panel prefab (Inspector — add UIAnimator, assign theme), `UITheme` ScriptableObject (create/configure), and the script that opens/closes the panel.

---

## 12. Coordination Notes

### Active Work Division

| Area                              | Owner                               | Notes                                                                                                                                                                                                                                                                                                                                  |
| --------------------------------- | ----------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Tech Tree UI / Fog System         | Primary contributor                 | **Do not touch `TechTreeWindowManager.cs`.** The fog controller is in progress. Lane C and D can be started in data (TechNode ScriptableObjects, unit code) without touching this file.                                                                                                                                                |
| Lane C (Marketing) implementation | Open                                | Can start immediately with TechNode ScriptableObject creation and TechEffect data — no `.cs` edits needed for that phase. When ready to implement the SalesMarketer deny action (Section 8, Step 4), that touches `SalesMarketer.cs` and `UnitActionPanel.cs` — flag before starting those files.                                      |
| Lane D (Sabotage) implementation  | Open                                | Should be started alongside or after Lane C, since both share the SalesMarketer deny action. The Retro-era gating via `unlocksSabotageTab` on the first Lane D TechNode requires no code change. New Lane D-specific mechanics (e.g. resource stealing, persistent suppression) will require code — flag before touching shared files. |
| Cost and Upkeep UI                | Primary contributor (working on it) | If you need to read unit costs for any reason (e.g. displaying in tooltips), you will need to make `UnitSpawner.GetRecruitmentCost()` public. Coordinate before changing this.                                                                                                                                                         |
|                                   |

### Before starting on a new area

Flag to the team which file(s) you're editing. The most likely collision points are:

- `EnemyAI.cs` — multiple features (new units, sabotage deny, new buildings) all require additions here.
- `EconomyManager.cs` — new buildings and cost display both touch this.
- `UnitActionPanel.cs` — new unit actions (SalesMarketer deny, Builder repair, WireSpecialist repair) all add buttons here.
- `BuildUIManager.cs` — new placement modes and building types extend this.
- `SalesMarketer.cs` — both Lane C and Lane D converge here for the `PerformDeny()` implementation. Only one person should write this at a time.

---

## 13. Debug Flags to Disable Before Shipping

| Flag               | File                | Field Name         | What it does                                                                                         |
| ------------------ | ------------------- | ------------------ | ---------------------------------------------------------------------------------------------------- |
| Free Research Mode | TechManager.cs      | `freeResearchMode` | All tech nodes research for free. RP and gold costs are not deducted.                                |
| Unit Testing Mode  | Unit.cs (inherited) | `testingMode`      | Bypasses `CanAct`, movement checks, and charge consumption on all units. Per-unit, set in Inspector. |
