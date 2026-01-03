# Engine Diagnostics - Romanov's Vengeance

This document describes the diagnostic logging in the engine fork (`jimicze-OpenRA`) and documents future optimization opportunities.

> **Note:** Diagnostics are currently DISABLED (commented out) for release builds.

---

## Quick Reference

- **Engine Version:** `b74bd2094b21862478941ff218dee6950188db98`
- **Latest Release:** `release-20260102`
- **Master Documentation:** [AGENTS.md](AGENTS.md)

---

## Diagnostic Status: DISABLED

All diagnostic logging has been commented out for the production build. To re-enable for debugging, uncomment the relevant sections in the files listed below.

## Available Diagnostics (Currently Disabled)

### 1. Frame Lag Detection

**Files:**
- `OpenRA.Game/Game.cs` - `[LAG-LOGIC]`, `[LAG-RENDER]`

**Log Tags:**
| Tag | Description |
|-----|-------------|
| `[LAG-LOGIC]` | LogicTick exceeded threshold or GC occurred |
| `[LAG-RENDER]` | RenderTick exceeded threshold or GC occurred |

### 2. Tick Timing Breakdown

**Files:**
- `OpenRA.Game/Game.cs` - `[LAG-TICK]`
- `OpenRA.Game/World.cs` - `[LAG-WORLD]`

**Log Tags:**
| Tag | Description |
|-----|-------------|
| `[LAG-TICK]` | Detailed tick timing (sound, tickImmediate, tryTick, worldTick, tickRender) |
| `[LAG-WORLD]` | World tick breakdown (actorTick, traitTick, effectsTick) |

### 3. Bot Module Timing

**Files:**
- `OpenRA.Mods.Common/Traits/Player/ModularBot.cs` - `[LAG-BOT]`

**Log Tags:**
| Tag | Description |
|-----|-------------|
| `[LAG-BOT]` | Per-module timing for bot AI modules exceeding 10ms |

---

## Performance Fixes Applied

### Completed Optimizations

| Commit | Description | Impact |
|--------|-------------|--------|
| `26db99d723` | Throttle SupportPowerBotASModule (17 tick interval) | 200-338ms → 10-50ms |
| `820c2b8688` | Cache RangedGpsDotEffect provider lookups | 44ms → 5-6ms |
| `6f1b0ff491` | Convert pathfinder exceptions to warnings | Prevents crashes |
| `652c245688` | Use HashSet in ControlGroups | Minor improvement |
| `327f01f289` | Optimize box selection priority lookup | Minor improvement |
| `3e84c7601c` | Optimize ProductionQueueFromSelection | Minor improvement |
| `02e44157ea` | Eliminate LINQ in Move.PopPath | Minor improvement |

### Bug Fixes Applied

| Commit | Description | Status |
|--------|-------------|--------|
| `6216531c95` | Prevent Aircraft AddInfluence crash in Land activity | In release |
| `1bd361afe2` | Skip voxel rendering when all components invisible (blink fix) | In release |
| `88f6e6422b` | Health.cs crash on quit | In release |
| `53d480fb9a` | AI now respects building terrain placement (water buildings) | In release |
| `cea2e24e99` | Prevent crash when PlayerActor disposed during cleanup | In release |

---

## Future Optimization Opportunities

These are identified performance bottlenecks that could be addressed in future updates.

### High Priority

#### 1. Other Bot Modules (ModularBot)
**Current:** ModularBot still occasionally spikes to 50-200ms
**Files:** `OpenRA.Mods.Common/Traits/Player/ModularBot.cs` and modules in `OpenRA.Mods.AS/Traits/BotModules/`
**Proposed Fix:** Apply similar throttling to other expensive bot modules:
- `BaseBuilderBotModule` - 206ms spike observed
- `SquadManagerBotModule` - occasional spikes
- Other AS bot modules

#### 2. AttackAircraft Trait
**Current:** 206ms spike observed
**Files:** `OpenRA.Mods.Common/Traits/Attack/AttackAircraft.cs`
**Analysis Needed:** Profile to identify hotspots, likely target scanning or pathfinding

#### 3. Cloak Trait
**Current:** 192ms spike observed
**Files:** `OpenRA.Mods.Common/Traits/Cloak.cs`
**Analysis Needed:** May be iterating over many actors for visibility checks

### Medium Priority

#### 4. AttackFollowFrontal
**Current:** 94ms spike
**Files:** `OpenRA.Mods.AS/Traits/Attack/AttackFollowFrontal.cs`
**Proposed Fix:** Cache target lookups, reduce frequency of calculations

#### 5. PeriodicExplosion
**Current:** 65ms spike
**Files:** Likely in AS or Common mods
**Proposed Fix:** Batch explosion processing, reduce per-tick overhead

#### 6. ActorMap
**Current:** 57ms spike
**Files:** `OpenRA.Game/ActorMap.cs`
**Proposed Fix:** Optimize spatial queries, consider better data structures

### Low Priority (Battle-Induced)

#### 7. Effects Processing
**Current:** 193ms spike with 1186 effects during large battles
**Files:** `OpenRA.Game/World.cs` effects tick
**Note:** This is expected during intense battles. Could consider:
- Effect pooling
- LOD for distant effects
- Batch processing

---

## Log File Location

Logs are written to:
- **macOS:** `~/Library/Application Support/OpenRA/Logs/debug.log`
- **Windows:** `%APPDATA%\OpenRA\Logs\debug.log`
- **Linux:** `~/.config/openra/Logs/debug.log`

## Analysis Commands

```bash
# Check for lag spikes
grep "\[LAG-" ~/Library/Application\ Support/OpenRA/Logs/debug.log

# Find slowest ticks
grep "\[LAG-TICK\]" ~/Library/Application\ Support/OpenRA/Logs/debug.log | tail -20

# Check bot module timing
grep "\[LAG-BOT\]" ~/Library/Application\ Support/OpenRA/Logs/debug.log | sort | uniq -c | sort -rn

# Check world tick breakdown
grep "\[LAG-WORLD\]" ~/Library/Application\ Support/OpenRA/Logs/debug.log | tail -20

# Check perf.log for slow traits
grep "Trait:" ~/Library/Application\ Support/OpenRA/Logs/perf.log | sort -t'|' -k1 -rn | head -30
```

## How to Re-Enable Diagnostics

1. In `OpenRA.Game/Game.cs`, uncomment `[LAG-LOGIC]`, `[LAG-RENDER]`, `[LAG-TICK]` logging
2. In `OpenRA.Game/World.cs`, uncomment `[LAG-WORLD]` logging
3. In `OpenRA.Mods.Common/Traits/Player/ModularBot.cs`, uncomment `[LAG-BOT]` logging
4. Rebuild and test

## Related Documentation

- [bugs.md](bugs.md) - Known bugs with root cause analysis
- [ROMANOVS-LAG-INVESTIGATION.md](Romanovs-Vengeance/ROMANOVS-LAG-INVESTIGATION.md) - Original lag investigation notes
