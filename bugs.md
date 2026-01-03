# Known Bugs - Romanov's Vengeance

> **Master Documentation:** [AGENTS.md](AGENTS.md)

---

## Bug Summary

| # | Bug | Priority | Status |
|---|-----|----------|--------|
| 1 | MCV deploy/undeploy crash | High | FIXED |
| 2 | Harrier doesn't return to base after expending ammo | Medium | Fix ready |
| 3 | Units temporarily disappear on menu background | Low | FIXED (voxel fix) |
| 4 | Aircraft crash on Land - AddInfluence exception | High | FIXED |
| 5 | Voxel units randomly blink/disappear | High | FIXED |
| 6 | Game crash on quit/defeat - OnOwnerChanged | High | FIXED |
| 7 | Missing progressbar-thumb-empty asset | Low | FIXED |
| 8 | Micro lag / periodic stuttering (GC pauses) | High | Root cause found |
| 9 | Vehicles rendering under bridge | Medium | Needs investigation |
| 10 | Lightning Storm shows ready but can't cast | Medium | Needs investigation |
| 11 | B-2 Bomber production queue stuck | Medium | Needs investigation |
| 12 | Chrono Bomb not placed on crowded land | Low | Needs investigation |
| 13 | Allied Fog Tower visual blink | Low | Monitor |

---

## Bug: MCV deploy/undeploy crash (FIXED)

**Status:** FIXED in engine commit `42d7bddbf8fe8bb2fa187df2d19729db33b71ece`

**Reported Scenario:**
1. Deploy MCV to Construction Yard
2. Undeploy Construction Yard back to MCV
3. Game crashes with `NullReferenceException`

**Root Cause:**
The `AllBuildables` cache in `ProductionPaletteWidget.cs` was only invalidated by `WorldTick`, not when `CurrentQueue` changed. When MCV deploy/undeploy happened within the same tick, the cache returned stale data from the previous queue, causing a null reference in `RefreshIcons()`.

**Fix Applied:**
Added `cachedBuildablesQueue` tracking to invalidate cache when queue changes:
```csharp
if (cachedBuildablesTick != currentTick || cachedBuildablesQueue != CurrentQueue)
{
    // Rebuild cache
    cachedBuildablesQueue = CurrentQueue;
}
```

**File Modified:**
- `OpenRA.Mods.Common/Widgets/ProductionPaletteWidget.cs`

---

## Bug: Harrier doesn't return to base after expending ammo

**Status:** Documented, fix pending (not priority - focusing on performance)

**Reported Scenario:**
1. Harrier shoots all 3 missiles at a target
2. Target didn't die from the last missile (still alive)
3. Aircraft becomes idle with no ammo and doesn't return to base

**Root Cause Analysis:**

The bug is in the engine code flow between `FlyAttack.cs` and `Aircraft.cs`:

1. In `FlyAttack.cs` lines 142-144, when ammo is depleted:
   ```csharp
   QueueChild(new ReturnToBase(self));
   returnToBase = true;
   return attackAircraft.Info.AbortOnResupply;  // Returns true by default
   ```

2. `ReturnToBase` is queued as a **child** activity, but when `AbortOnResupply = true` (default), `FlyAttack` returns `true` immediately.

3. With `ChildHasPriority = false`, when parent returns `true`, `Activity.TickOuter` returns `NextActivity` (not `ChildActivity`), orphaning the `ReturnToBase` child.

4. `OnBecomingIdle` is called in `Aircraft.cs`, but since `IdleBehavior = None` (default), it queues `FlyIdle` instead of `ReturnToBase`.

**Affected Aircraft:**
- Aircraft with `Rearmable` trait (Harrier, Black Eagle, B2, etc.)
- Aircraft that have `AmmoPool` with limited ammo

**NOT Affected:**
- Kirovs - No `Rearmable` trait, unlimited bombs
- Helicopters without ammo limits
- Self-reloading aircraft

**Proposed Fix:**

Add ammo check to `Aircraft.cs` method `OnBecomingIdle` (around line 761):

```csharp
protected virtual void OnBecomingIdle(Actor self)
{
    // Check if aircraft has Rearmable trait and is out of ammo - must return to base
    var rearmable = self.TraitOrDefault<Rearmable>();
    if (rearmable != null && rearmable.RearmableAmmoPools.Length > 0 && 
        rearmable.RearmableAmmoPools.All(p => !p.HasAmmo) && GetActorBelow() == null)
    {
        self.QueueActivity(new ReturnToBase(self));
        return;
    }
    
    // Original idle behavior logic follows...
    if (Info.IdleBehavior == IdleBehaviorType.LeaveMap)
    {
        // ... existing code ...
    }
}
```

**Why this fix works:**
- `rearmable != null` - Only affects aircraft with `Rearmable` trait
- `RearmableAmmoPools.Length > 0` - Has ammo pools to check
- `All(p => !p.HasAmmo)` - Actually out of ammo
- `GetActorBelow() == null` - Not already landed on an airfield

**Files to modify:**
- `/jimicze-OpenRA/OpenRA.Mods.Common/Traits/Air/Aircraft.cs`

**Related Files (for context):**
- `/jimicze-OpenRA/OpenRA.Mods.Common/Activities/Air/FlyAttack.cs`
- `/jimicze-OpenRA/OpenRA.Mods.Common/Traits/Rearmable.cs`
- `/jimicze-OpenRA/OpenRA.Mods.Common/Traits/AmmoPool.cs`
- `/Romanovs-Vengeance/mods/rv/rules/aircraft.yaml` (orca definition at line 416)

---

## Bug: Units temporarily disappear on game menu background (FIXED)

**Status:** FIXED - Same root cause as voxel blink bug (commit `1bd361afe2`)

**Reported Scenario:**
1. Launch game and observe the background demo playing on the main menu
2. Allied units (vehicles, anti-air defense, some aircraft) randomly disappear for 1-2 seconds
3. Units reappear synchronously across the map
4. Infantry (troopers, dogs) are NOT affected
5. Only occurs on menu background demo, not during actual gameplay

**Root Cause:**
Same as the voxel blink bug - units with invisible voxel components (e.g., Mirage Tanks disguised as trees in the demo) caused empty model collections to create invalid sprites with corrupted bounds.

**Fix Applied:**
The voxel rendering fix (commit `1bd361afe2`) that returns `null` for empty model collections resolves this issue.

**Priority:** Low (cosmetic, menu-only) → RESOLVED

---

## Bug: Aircraft crash on Land activity - AddInfluence exception (FIXED)

**Status:** FIXED in engine commit `6216531c95` (included in release-20260102)

**Reported Scenario:**
1. Play a skirmish game (observed on map "Mining Helmet by MustaphaTR")
2. Aircraft attempts to land
3. Game crashes with `InvalidOperationException`

**Exception:**
```
System.InvalidOperationException: Cannot AddInfluence until previous influence is removed with RemoveInfluence
   at OpenRA.Mods.Common.Traits.Aircraft.AddInfluence(CPos landingCell) in Aircraft.cs:line 903
   at OpenRA.Mods.Common.Activities.Land.Tick(Actor self) in Land.cs:line 242
```

**Root Cause Analysis:**

The `Land` activity calls `aircraft.AddInfluence(landingCell)` at line 242 without checking if the aircraft already has influence. This can happen when:

1. An aircraft is spawned landed on the map (gets influence via `AssociateWithAirfieldActivity` at line 1287 or 1308 in `Aircraft.cs`)
2. The aircraft is later given a new `Land` activity (e.g., idle landing behavior, forced landing, return to base)
3. The Land activity's `landingInitiated` flag is `false` (new activity instance), but the aircraft already occupies a cell from previous state
4. `AddInfluence()` throws exception because `HasInfluence()` returns true

**History:**
- Commit `972ea66dc9` originally removed the exception throwing from `AddInfluence()`
- Commit `3173ae5475` reverted that change (re-added exception throwing)
- Commit `3760b14235` added a partial fix in `Land.cs` for one specific case (when `targetPosition` matches `pos`), but doesn't cover all scenarios

**Proposed Fix:**

In `Land.cs` line 242, add a guard check before calling `AddInfluence()`:

```csharp
// Before:
aircraft.AddInfluence(landingCell);

// After:
if (!aircraft.HasInfluence())
    aircraft.AddInfluence(landingCell);
```

**Files Modified:**
- `/jimicze-OpenRA/OpenRA.Mods.Common/Activities/Air/Land.cs`

**Priority:** High (causes game crash) → RESOLVED

---

## Bug: Voxel units randomly blink/disappear during gameplay (FIXED)

**Status:** FIXED in engine commit `1bd361afe2` on branch `feature/ai-water-building-placement`

**Reported Scenario:**
1. Play a skirmish with many voxel units (tanks, vehicles with turrets)
2. Voxel units randomly disappear for 1-2 frames
3. ALL voxels blink at the same time (synchronized)
4. More frequent with many units on screen
5. Affects: Tank turrets, building turrets (Gatling Tower, Patriot), repair/support drones
6. Does NOT affect: Static buildings, infantry (sprite-based)

---

### Root Cause (FOUND)

The bug occurred when **all voxel components of an actor were legitimately invisible** (e.g., Mirage Tank disguised as a tree, Patriot turret during construction phase). In this case:

1. `RenderVoxels.Render()` calls `ModelRenderer.RenderAsync()` with an **empty model collection** (all components filtered out by `DisableFunc`)

2. In `ModelRenderer.RenderAsync()`, the bounds calculation loop never executes:
   ```csharp
   var tl = new float2(float.MaxValue, float.MaxValue);
   var br = new float2(float.MinValue, float.MinValue);
   foreach (var m in models)  // Empty collection - loop never runs!
   {
       // tl and br never get updated
   }
   ```

3. When these extreme float values are converted to sprite bounds:
   ```csharp
   var spriteRect = Rectangle.FromLTRB((int)tl.X, (int)tl.Y, (int)br.X, (int)br.Y);
   ```
   The conversion overflows to `int.MinValue` (-2147483648)

4. A `Sprite` is created with corrupted bounds: `Size=(-2147483648, -2147483648)`

5. When this invalid sprite reaches the renderer, it causes visual artifacts (the "blink")

**Diagnostic Evidence:**
```
[VOXEL-INVALID-SPRITE] Invalid sprite at draw time! Size=(-2147483648.00,-2147483648.00) Bounds=(1139,1,-2147483648,-2147483648)
```

---

### Fix Applied

**File:** `OpenRA.Mods.Cnc/Traits/World/ModelRenderer.cs`

```csharp
public IFinalizedRenderable RenderAsync(WorldRenderer wr, IEnumerable<ModelAnimation> models, ...)
{
    // Early exit if no models to render (all components invisible)
    if (!models.Any())
        return null;
    
    // ... rest of method unchanged
}
```

**File:** `OpenRA.Mods.Cnc/Graphics/ModelRenderable.cs`

```csharp
public void Render(WorldRenderer wr)
{
    // Skip rendering if RenderAsync returned null (no visible components)
    if (renderProxy == null)
        return;
    
    // ... rest of method unchanged
}

public void RenderDebugGeometry(WorldRenderer wr)
{
    if (renderProxy == null)
        return;
    
    // ... rest of method unchanged
}
```

---

### Investigation History

**Phase 1: Sheet Overflow (RULED OUT)**
- Hypothesis: Sheet overflow invalidating voxel textures
- Result: Added multi-sheet tracking, but blinking persisted

**Phase 2: IsTraitDisabled State Changes (RULED OUT)**
- Hypothesis: Condition system toggling traits rapidly
- Result: Logged state changes, but traits weren't being disabled

**Phase 3: Empty Model Collections (ROOT CAUSE FOUND)**
- Added draw-time sprite validation
- Caught the invalid sprites with `int.MinValue` bounds
- Traced back to empty model collection causing float overflow

---

### Files Modified (Final - Clean)

| File | Change |
|------|--------|
| `OpenRA.Mods.Cnc/Traits/World/ModelRenderer.cs` | Returns `null` for empty model collections |
| `OpenRA.Mods.Cnc/Graphics/ModelRenderable.cs` | Handles `null` renderProxy gracefully |

**Diagnostic files cleaned up:**
- `RenderVoxels.cs` - Removed `VoxelBlinkDetector` class
- `FrameBuffer.cs` - Removed `glFinish()` diagnostic

**Priority:** High (visual corruption during gameplay) → RESOLVED

---

## Bug: Game crash on quit/defeat - OnOwnerChanged accessing destroyed player (FIXED)

**Status:** FIXED in engine (uncommitted)

**Reported Scenario:**
1. Play a skirmish game
2. Quit to main menu (or get defeated)
3. Game crashes during world disposal

**Exception (from exception-2026-01-01T184320Z.log):**
```
System.InvalidOperationException: Attempted to get trait from destroyed object (player 6 (not in world))
   at OpenRA.TraitDictionary.CheckDestroyed(Actor actor) in TraitDictionary.cs:line 84
   at OpenRA.Mods.Common.Traits.Health.OnOwnerChanged(Actor self, Player oldOwner, Player newOwner) in Health.cs:line 121
   at OpenRA.Actor.ChangeOwnerSync(Player newOwner) in Actor.cs:line 503
   at OpenRA.World.Dispose() in World.cs:line 616
```

**Root Cause:**
During world disposal (`World.Dispose()`), the game changes ownership of remaining actors (line 616). The `Health` trait's `INotifyOwnerChanged.OnOwnerChanged` callback tries to access `newOwner.PlayerActor.TraitsImplementing<>()`, but the new owner's `PlayerActor` has already been destroyed/disposed.

**Fix Applied:**

In `Health.cs` line 119:
```csharp
void INotifyOwnerChanged.OnOwnerChanged(Actor self, Player oldOwner, Player newOwner)
{
    // During world disposal, the new owner's PlayerActor may already be destroyed
    if (newOwner.PlayerActor.Disposed)
        return;

    notifyDamagePlayer = newOwner.PlayerActor.TraitsImplementing<INotifyDamage>().ToArray();
    damageModifiersPlayer = newOwner.PlayerActor.TraitsImplementing<IDamageModifier>().ToArray();
    notifyKilledPlayer = newOwner.PlayerActor.TraitsImplementing<INotifyKilled>().ToArray();
}
```

**File Modified:**
- `OpenRA.Mods.Common/Traits/Health.cs`

**Priority:** High (causes game crash on quit) → RESOLVED

---

## Bug: Missing progressbar-thumb-empty asset causing log spam (FIXED)

**Status:** FIXED in mod

**Reported Scenario:**
- Debug log contains 61,000+ occurrences of: `Could not find collection 'progressbar-thumb-empty'`
- Log spam makes debugging difficult

**Root Cause:**
`HealthBarWidget.cs` in OpenRA.Mods.AS references `EmptyHealthBar = "progressbar-thumb-empty"`, but this collection was not defined in the mod's `chrome.yaml`.

**Fix Applied:**
Added `progressbar-thumb-empty` definition to `mods/rv/chrome.yaml`:
```yaml
# Empty health bar (for HealthBarWidget)
progressbar-thumb-empty:
	Image: dialog.png
	PanelRegion: 512, 0, 1, 1, 126, 126, 1, 1
```

**File Modified:**
- `Romanovs-Vengeance/mods/rv/chrome.yaml`

**Priority:** Low (log spam only) → RESOLVED

---

## Bug: Micro lag / periodic stuttering (ROOT CAUSE FOUND)

**Status:** Root cause identified - .NET Garbage Collection pauses

**Reported Scenario:**
1. Play a skirmish game
2. Periodic micro-lag/stuttering (0.5-1 second freezes)
3. More frequent with many units and longer games

**Diagnostic Evidence (from lag-diag-v1 build):**

Biggest lag spikes detected:
```
[LAG-LOGIC] 5942ms (delayed=5914ms, orderMgr=0ms) Mem=852.7MB (delta=-219.6MB) GC! (Gen0=15254, Gen1=2080, Gen2=128)
[LAG-LOGIC] 4446ms (delayed=0ms, orderMgr=4446ms) Mem=545.3MB (delta=+164.9MB) GC! (Gen0=1677, Gen1=227, Gen2=65)
```

**Key Observations:**
- Memory grows from ~226MB to 1.1GB during gameplay
- Gen2 (full) garbage collections cause 100-300ms pauses normally
- Occasional massive GC pauses of 4-6 seconds
- 14,558 GC events logged during one session
- Gen2 collection count reached 129 (indicating high allocation pressure)

**Root Cause:**

.NET's garbage collector is running in **Workstation GC mode** (default) which:
1. Uses a single GC thread
2. Suspends all managed threads during collection ("stop-the-world")
3. Is optimized for responsiveness on single-core, not throughput

With the game allocating heavily (memory growing to 1.1GB), Gen2 collections become increasingly expensive.

**The 5942ms spike breakdown:**
- `delayed=5914ms` - The ActionQueue's `PerformActions()` triggered a massive GC
- `delta=-219.6MB` - GC reclaimed 219MB of memory
- Gen2 jumped from 116 to 128 in one pause

**Proposed Fixes:**

### Option 1: Enable Server GC Mode (Recommended)

Add to `OpenRA.Launcher.csproj`:
```xml
<PropertyGroup>
    <ServerGarbageCollection>true</ServerGarbageCollection>
</PropertyGroup>
```

Or add `runtimeconfig.template.json`:
```json
{
    "configProperties": {
        "System.GC.Server": true,
        "System.GC.Concurrent": true
    }
}
```

Server GC uses:
- Multiple GC threads (one per core)
- Concurrent collection (less stop-the-world time)
- Higher memory usage but lower pause times

### Option 2: Reduce Allocation Pressure

Profile memory allocations to find:
- Object pools that could be reused
- Unnecessary temporary allocations
- Large object heap (LOH) allocations

### Option 3: Manual GC Tuning

```csharp
// At game start
GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

// Periodically during loading screens
GC.Collect(2, GCCollectionMode.Optimized, blocking: false);
```

**Files to Modify:**
- `OpenRA.Launcher/OpenRA.Launcher.csproj` - Add ServerGarbageCollection
- Or create `OpenRA.Launcher/runtimeconfig.template.json`

**Priority:** High (major gameplay impact)

---

## Bug: Vehicles rendering under bridge instead of on top (INVESTIGATING)

**Status:** Needs investigation

**Reported Scenario:**
1. Move vehicles onto a bridge
2. Vehicles appear to render BELOW the bridge instead of on top
3. Z-order/rendering layer issue

**Notes:**
- Likely unrelated to our voxel rendering changes
- May be pre-existing issue with bridge/unit render ordering

**Priority:** Medium

---

## Bug: Lightning Storm shows ready but cannot be cast after Chrono Bomb (INVESTIGATING)

**Status:** Needs investigation

**Reported:** 2026-01-03

**Reported Scenario:**
1. Have Weather Control building with Lightning Storm ready to cast
2. Cast Chrono Bomb (different support power)
3. Lightning Storm icon on building still shows "ready"
4. Cannot actually cast Lightning Storm - timer resets to 5 minutes
5. Visual display shows ready, but actual state is on cooldown

**Symptoms:**
- Support power visual state desync after casting another power
- Building icon shows ready, but power is unusable
- Timer shows 5 minute countdown after attempting to use

**Possible Causes:**
- Support power state synchronization issue between multiple ready powers
- UI not updating correctly when another power is cast
- `SupportPowerManager` state getting corrupted

**Files to Investigate:**
- `OpenRA.Mods.Common/Traits/SupportPowers/SupportPowerManager.cs`
- `OpenRA.Mods.Common/Widgets/SupportPowersWidget.cs`
- Chrono Bomb specific support power implementation

**Priority:** Medium (gameplay affecting but workaround: wait for cooldown)

---

## Bug: B-2 Bomber production queue stuck at "ready" (INVESTIGATING)

**Status:** Needs investigation

**Reported:** 2026-01-03

**Reported Scenario:**
1. Have 4 Air Force Command HQs (airbases)
2. Build 2-3 B-2 bombers successfully
3. Queue 7 more B-2 bombers
4. Queue shows planes as "ready" in the building production tab (air tab)
5. No damage to buildings, no low power
6. Planes are not being produced despite free airbase slots
7. Re-queuing (cancel and re-add) fixes the issue

**Symptoms:**
- Production queue state shows "ready" but nothing happens
- Airbase slots are available but not being used
- Cancel + re-queue workaround works

**Possible Causes:**
- `ParallelProductionQueue` logic issue with aircraft
- Airbase slot availability detection not updating
- Queue state not refreshing after production completes
- Race condition between production completion and next item start

**Files to Investigate:**
- `OpenRA.Mods.Common/Traits/Production/ParallelProductionQueue.cs`
- `OpenRA.Mods.Common/Traits/Production/ProductionAirdrop.cs`
- Aircraft-specific production logic
- Airbase/`Reservable` trait interaction

**Priority:** Medium (gameplay affecting, has workaround)

---

## Bug: Chrono Bomb not placed on land with object collision (INVESTIGATING)

**Status:** Needs investigation

**Reported:** 2026-01-03

**Reported Scenario:**
1. Have Chronosphere ready
2. Cast Chrono Bomb targeting a small land area between buildings and units
3. Chrono effect animation plays (visual confirmation of cast)
4. No bomb is actually placed on the ground
5. Support power goes on cooldown (new countdown starts)
6. Ability is consumed but has no effect

**Symptoms:**
- Chrono effect plays (so cast was registered)
- Bomb doesn't appear at target location
- Power goes on cooldown (resources consumed)
- Happens when targeting tight spaces with nearby objects

**Possible Causes:**
- Bomb placement validation rejecting the target cell due to nearby actors
- Collision detection too strict for the bomb actor
- Target cell occupation check failing
- Bomb actor spawning but immediately being destroyed

**Files to Investigate:**
- Chrono Bomb support power trait (likely in `OpenRA.Mods.RA2/` or mod YAML)
- `SpawnActorPower` or similar support power base class
- Actor spawning collision logic
- `IOccupySpace` validation for spawned actors

**Priority:** Low (rare edge case)

---

## Bug: Allied Fog Tower visual blink (LOW PRIORITY)

**Status:** Documented - monitor for recurrence

**Reported:** 2026-01-03

**Reported Scenario:**
1. Playing as Allies with Fog Tower (Gap Generator?)
2. Brief visual blink/flicker observed once
3. Lasted approximately 1 second
4. Only occurred once during session
5. May have been collision with spy plane or other effect

**Symptoms:**
- Single brief visual artifact
- Not reproducible (one-time occurrence)

**Possible Causes:**
- Same voxel blink issue (thought to be fixed)
- Spy plane reveal interaction
- Shroud/fog rendering edge case
- Graphics driver issue

**Notes:**
- May be related to the voxel blink fix (commit `1bd361afe2`)
- Monitor for recurrence before investigating further
- Could be unrelated graphics glitch

**Priority:** Low (one-time occurrence, not reproducible)

