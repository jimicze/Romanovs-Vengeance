# Romanov's Vengeance Server Lag Investigation

## Summary

After hosting Romanov's Vengeance (RV) and Shattered Paradise (SP) dedicated servers on a DigitalOcean droplet, we observed significant input lag (1-2 seconds) in RV, while SP runs smoothly. This document summarizes our investigation findings.

## Environment

| Component | Details |
|-----------|---------|
| Server | DigitalOcean Droplet (1GB RAM, 1 vCPU) |
| OS | Debian (Docker containers) |
| RV Version | playtest-20241215 |
| SP Version | (latest AppImage) |
| Ports | RV: 1234/tcp, SP: 1235/tcp |

## Symptoms

- **1-2 second delay** after clicking on buildings, units, or issuing commands
- Delay occurs even with **1 player** in the game
- Delay **gets worse** as unit count increases
- **Shattered Paradise runs smoothly** on the same server
- Issue occurs regardless of network path (direct, OpenVPN, Tailscale)

## Resource Analysis During Gameplay

| Container | CPU | RAM | Notes |
|-----------|-----|-----|-------|
| rv-server | ~3% | 230 MB | Game in progress |
| sp-server | ~3% | 208 MB | Idle |
| Total RAM | - | 791 MB / 961 MB (82%) | No swap configured |

**Observation**: CPU and RAM usage appear normal, suggesting the issue is not resource exhaustion.

## Server-Side Optimizations Attempted

### 1. Removed unused OpenVPN container
- Freed ~2MB RAM
- No impact on lag

### 2. Added performance flags to RV launch script
```bash
Server.EnableSyncReports=False   # Disables sync report generation
Server.EnableLintChecks=False    # Disables map validation checks
```
- Minimal/no impact on lag

## Root Cause Analysis

### Finding 1: OpenRA Engine is Single-Threaded

**Source**: [OpenRA Issue #21218 - Performance issues with big amount of units](https://github.com/OpenRA/OpenRA/issues/21218)

The OpenRA engine processes all game logic on a **single CPU thread**. This is a fundamental architectural limitation that cannot be fixed via server configuration.

Key points from the issue:
- 400-600 units total causes significant lag
- AI pathfinding is a major performance bottleneck
- Units attacking unreachable targets can cause game freezing
- Large maps exacerbate the problem
- Spectators contribute to sync overhead

### Finding 2: RV Mod Complexity

**Source**: [RV Issue #88 - Massive lag in network + AI game](https://github.com/MustaphaTR/Romanovs-Vengeance/issues/88)

Romanov's Vengeance is significantly more complex than Shattered Paradise:
- More unit types, effects, and animations
- RA2-based mod with additional features (Commanders, upgrades, etc.)
- Known issue: **Dreadnought missiles spam mini-map** with "character lost" notifications
- Performance improves when enemy bases are destroyed (fewer units)

### Finding 3: Lockstep Networking Model

OpenRA uses a **lockstep networking model** where all clients must synchronize every game tick before advancing. This means:
- The slowest client/connection determines game speed
- Server performance affects all clients equally
- Network latency between players compounds the issue

## Why Shattered Paradise Works Better

| Factor | Romanov's Vengeance | Shattered Paradise |
|--------|---------------------|-------------------|
| Base game | Red Alert 2 (complex) | Tiberian Sun (simpler) |
| RAM usage | ~258 MB | ~183 MB |
| Unit complexity | Higher (more effects) | Lower |
| Mod features | Commanders, upgrades, etc. | Fewer additions |

## Conclusion

**The input lag is a known engine-level limitation in OpenRA, not a server configuration issue.**

The OpenRA engine would need significant refactoring to support multi-threading, which is a massive undertaking. The RV mod's additional complexity amplifies the issue compared to simpler mods like Shattered Paradise.

## Possible Mitigations (Gameplay)

Since the issue cannot be fixed server-side, these gameplay adjustments may help:

| Mitigation | Description |
|------------|-------------|
| Use smaller maps | Reduces pathfinding calculations |
| Limit AI players | AI pathfinding is expensive |
| Cap unit counts | Avoid massing 400+ units total |
| Avoid Dreadnoughts | Known to spam mini-map notifications |
| Use "fastest" game speed | May reduce perceived lag |
| Fewer spectators | Reduces sync overhead |

## Potential Long-Term Solutions

### 1. Contribute to OpenRA Multi-Threading

The OpenRA engine would benefit from multi-threaded game logic processing. Relevant areas:
- Pathfinding calculations
- Unit AI processing
- Render/logic separation

This would require significant engine refactoring and is tracked in various OpenRA issues.

**Relevant code areas to investigate**:
- `OpenRA.Game/` - Core game loop
- `OpenRA.Mods.Common/Pathfinder/` - Pathfinding logic
- `OpenRA.Game/Server/` - Network synchronization

### 2. Mod-Specific Optimizations

Work with RV mod author (MustaphaTR) to identify and optimize performance-heavy features:
- Reduce mini-map notification spam
- Optimize unit effect calculations
- Simplify complex unit behaviors

### 3. Hardware Scaling (Limited Impact)

Upgrading server hardware may provide marginal improvement but won't solve the core single-threaded limitation:
- More RAM: Prevents swapping during large battles
- Faster single-core CPU: May slightly improve tick processing
- Note: Multi-core CPUs won't help due to single-threaded design

## Resources

- **RV Discord**: https://discord.gg/hk428Wk (recommended for mod-specific help)
- **OpenRA Wiki - Dedicated Server**: https://github.com/OpenRA/OpenRA/wiki/Dedicated-Server
- **OpenRA Wiki - Settings**: https://github.com/OpenRA/OpenRA/wiki/Settings
- **RV GitHub**: https://github.com/MustaphaTR/Romanovs-Vengeance
- **OpenRA GitHub**: https://github.com/OpenRA/OpenRA

## Related GitHub Issues

| Repository | Issue | Title | Status |
|------------|-------|-------|--------|
| OpenRA | [#21218](https://github.com/OpenRA/OpenRA/issues/21218) | Performance issues with big amount of units | Open |
| RV | [#88](https://github.com/MustaphaTR/Romanovs-Vengeance/issues/88) | Massive lag in network + AI game (~5FPS) | Closed |
| RV | [#130](https://github.com/MustaphaTR/Romanovs-Vengeance/issues/130) | How to run my own dedicated server? | Closed |

---

*Last updated: 2025-12-29*

---

## Code-Level Performance Analysis

This section provides detailed analysis of performance hotspots identified in the RV mod codebase, with specific file and line references. All hotspots are marked with `// PERF:` comments in the source code.

For engine-level multi-threading considerations, see [ENGINE-MULTITHREADING-NOTES.md](ENGINE-MULTITHREADING-NOTES.md).

### Priority 1: Dreadnought/Missile System (PRIMARY FOCUS)

The Dreadnought missile system is the **most significant performance bottleneck** identified. When multiple Dreadnoughts fire simultaneously, performance degrades rapidly due to:

1. Per-missile LINQ allocations on creation
2. Uncached speed calculations called multiple times per tick
3. Expensive spatial index updates on every position change
4. Trigonometric calculations every tick during flight

#### File: `OpenRA.Mods.RA2/Traits/BallisticMissileOld.cs`

| Line | Issue | Impact | Proposed Fix |
|------|-------|--------|--------------|
| 128 | `.ToArray().Select()` creates new collections on every missile creation | High - allocations per missile | Cache `ISpeedModifier[]` array once, compute lazily |
| 146-148 | `MovementSpeed` property recalculates modifiers on every access | High - called multiple times per tick per missile | Cache computed speed value in a field |
| 189 | `World.UpdateMaps()` called on every position update | High - expensive spatial index operation | Consider batching or reducing update frequency |

**Current Code (Line 128):**
```csharp
void INotifyCreated.Created(Actor self)
{
    speedModifiers = self.TraitsImplementing<ISpeedModifier>().ToArray().Select(sm => sm.GetSpeedModifier());
}
```

**Proposed Fix:**
```csharp
ISpeedModifier[] speedModifierTraits;
int cachedMovementSpeed;

void INotifyCreated.Created(Actor self)
{
    speedModifierTraits = self.TraitsImplementing<ISpeedModifier>().ToArray();
    cachedMovementSpeed = Util.ApplyPercentageModifiers(Info.Speed,
        speedModifierTraits.Select(sm => sm.GetSpeedModifier()));
}

public int MovementSpeed => cachedMovementSpeed;
```

#### File: `OpenRA.Mods.RA2/Traits/MissileSpawnerOldMaster.cs`

| Line | Issue | Impact | Proposed Fix |
|------|-------|--------|--------------|
| 91-93 | Iterates all slave entries on every attack | Low-Medium - O(n) where n = missile count | Minor optimization possible |
| 108 | `Trait<BallisticMissileOld>()` lookup on every attack | Medium - trait lookup overhead | Cache trait reference in slave entry |
| 163 | LINQ `.Select()` in tick respawn logic | Low - only when respawning | Evaluate modifiers only when needed |

#### File: `OpenRA.Mods.RA2/Activities/BallisticMissileFlyOld.cs`

| Line | Issue | Impact | Proposed Fix |
|------|-------|--------|--------------|
| 50 | `Tan()` trigonometric calculation every tick | Medium - trig ops are expensive | Pre-compute `LaunchAngle.Tan()` in constructor |

**Current Code (Line 50):**
```csharp
WAngle GetEffectiveFacing()
{
    var at = (float)ticks / (length - 1);
    var attitude = bm.Info.LaunchAngle.Tan() * (1 - 2 * at) / (4 * 1024);
    // ...
}
```

**Proposed Fix:**
```csharp
readonly int launchAngleTan;

public BallisticMissileFlyOld(Actor self, Target t, BallisticMissileOld bm)
{
    // ...
    launchAngleTan = bm.Info.LaunchAngle.Tan();  // Pre-compute once
}

WAngle GetEffectiveFacing()
{
    var at = (float)ticks / (length - 1);
    var attitude = launchAngleTan * (1 - 2 * at) / (4 * 1024);
    // ...
}
```

### Priority 2: Warhead Processing

#### File: `OpenRA.Mods.RA2/Warheads/LegacySpreadWarhead.cs`

| Line | Issue | Impact | Proposed Fix |
|------|-------|--------|--------------|
| 46-49 | LINQ chain per victim on every weapon impact | Medium - executed for each actor in radius | Pre-filter or cache HitShape queries |
| 61-62 | `OrderBy().Take()` for buildings | Medium - sorting on every impact | Manual min-finding for hot path |

### Priority 3: Mirage System

#### File: `OpenRA.Mods.RA2/Traits/Mirage.cs`

| Line | Issue | Impact | Proposed Fix |
|------|-------|--------|--------------|
| 50 | `.First()` linear search for player lookup | Low-Medium - called on property access | Cache player reference in constructor |
| 117 | Same `.First()` linear search | Low-Medium - same issue | Cache player reference |

**Current Code:**
```csharp
public Player Owner
{
    get { return IsMirage ? self.World.Players.First(p => p.InternalName == Info.EffectiveOwner) : null; }
}
```

**Proposed Fix:**
```csharp
Player cachedEffectiveOwner;

void INotifyCreated.Created(Actor self)
{
    cachedEffectiveOwner = self.World.Players.FirstOrDefault(p => p.InternalName == Info.EffectiveOwner);
}

public Player Owner => IsMirage ? cachedEffectiveOwner : null;
```

### Summary of Hotspots

| Priority | File | Line(s) | Category | Fix Complexity | Status |
|----------|------|---------|----------|----------------|--------|
| 1 | BallisticMissileOld.cs | 128 | LINQ allocation | Easy | **FIXED** |
| 1 | BallisticMissileOld.cs | 146-148 | Uncached property | Easy | **FIXED** |
| 1 | BallisticMissileOld.cs | 189 | Spatial index | Medium | Engine-level |
| 1 | BallisticMissileFlyOld.cs | 50 | Trigonometry | Easy | **FIXED** |
| 2 | MissileSpawnerOldMaster.cs | 163 | LINQ in tick | Easy | **FIXED** |
| 2 | LegacySpreadWarhead.cs | 46-49 | LINQ per victim | Medium | **FIXED** |
| 2 | LegacySpreadWarhead.cs | 61-62 | LINQ sorting | Easy | **FIXED** |
| 3 | Mirage.cs | 50, 117 | Linear search | Easy | **FIXED** |
| 3 | HeliGrantConditionOnDeploy.cs | 198-199 | O(n*m) search | Easy | **FIXED** |
| 3 | SpawnActorOrWeaponWarhead.cs | 112 | Player lookup | Easy | **FIXED** |
| 3 | SpawnBuildingOrWeaponWarhead.cs | 102 | Player lookup | Easy | **FIXED** |

### Completed Optimizations (Branch: perf/lag-investigation)

The following optimizations have been implemented:

#### Commit 1: Missile System Optimization
- **BallisticMissileOld.cs**: Cached `ISpeedModifier[]` array and pre-computed `MovementSpeed` value
- **BallisticMissileFlyOld.cs**: Pre-computed `LaunchAngle.Tan()` in constructor
- **MissileSpawnerOldMaster.cs**: Pre-allocated reload modifier buffer, eliminated LINQ `.Select()`

#### Commit 2: Mirage System Optimization
- **Mirage.cs**: Cached `effectiveOwner` player reference in both `Mirage` and `MirageTooltip` classes

#### Commit 3: Warhead and Deploy System Optimization
- **LegacySpreadWarhead.cs**: Replaced LINQ chain with manual loops for HitShape lookup and building cell damage
- **HeliGrantConditionOnDeploy.cs**: Used HashSet for O(1) actor ID lookup instead of O(n) Contains()
- **SpawnActorOrWeaponWarhead.cs**: Cached internal owner player reference
- **SpawnBuildingOrWeaponWarhead.cs**: Cached owner player reference

### Testing Recommendations

To measure the impact of these optimizations:

1. **Baseline Test**: Host a game with 4+ Dreadnoughts firing continuously
2. **Metrics to Capture**:
   - Frame time (ms per tick)
   - Input latency (time from click to action)
   - CPU usage during combat
3. **Comparison**: Test with each optimization applied individually to measure impact
