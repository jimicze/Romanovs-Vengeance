# OpenRA Engine Multi-Threading Notes

This document outlines potential areas in the OpenRA engine that could benefit from multi-threading to improve performance. These are notes for potential upstream contribution to the OpenRA project.

**Related investigation**: See [ROMANOVS-LAG-INVESTIGATION.md](ROMANOVS-LAG-INVESTIGATION.md) for the full performance analysis.

---

## Current Architecture

The OpenRA engine is **single-threaded** for game logic. All game state updates, pathfinding, AI decisions, and network synchronization happen on a single CPU core.

### Why Single-Threaded?

1. **Determinism**: OpenRA uses a lockstep networking model where all clients must produce identical game states
2. **Simplicity**: Single-threaded code is easier to debug and maintain
3. **Historical**: The engine was designed when multi-core CPUs were less common

### The Problem

With modern games involving 400-600+ units, the single thread becomes a bottleneck:
- Pathfinding calculations are expensive
- Per-unit AI processing scales linearly with unit count
- All trait `ITick` implementations run sequentially
- Network sync waits for the slowest tick to complete

---

## Potential Multi-Threading Candidates

### 1. Pathfinding (`OpenRA.Mods.Common/Pathfinder/`)

**Current Behavior**: All pathfinding requests are processed sequentially on the main game thread.

**Multi-Threading Potential**: HIGH

**Approach**:
- Queue pathfinding requests to a worker thread pool
- Return paths asynchronously via callback or future
- Main thread continues processing; units wait for path result

**Challenges**:
- Path requests depend on current game state (unit positions, terrain)
- Results must be deterministic across all clients
- May require read-only snapshot of game state for pathfinder

**Files to Investigate**:
- `engine/OpenRA.Mods.Common/Pathfinder/PathFinder.cs`
- `engine/OpenRA.Mods.Common/Pathfinder/PathSearch.cs`
- `engine/OpenRA.Mods.Common/Traits/World/PathFinderOverlay.cs`

### 2. Unit AI Processing

**Current Behavior**: Each actor's `ITick.Tick()` runs sequentially on the main thread.

**Multi-Threading Potential**: MEDIUM

**Approach**:
- Partition actors into groups
- Process groups in parallel on worker threads
- Synchronize before network sync point

**Challenges**:
- Actors may interact with each other (attacking, following)
- Order of execution must be deterministic
- Shared state access must be synchronized

**Files to Investigate**:
- `engine/OpenRA.Game/Actor.cs`
- `engine/OpenRA.Game/World.cs` - tick loop
- `engine/OpenRA.Game/Traits/TraitsInterfaces.cs` - `ITick` interface

### 3. Spatial Index Updates (`World.UpdateMaps()`)

**Current Behavior**: Every time an actor moves, `World.UpdateMaps()` is called to update spatial indices.

**Multi-Threading Potential**: MEDIUM

**Approach**:
- Batch position updates and apply at end of tick
- Use concurrent data structures for spatial index
- Defer non-critical updates to background thread

**Challenges**:
- Spatial queries during tick depend on current state
- Must maintain consistency within a tick

**Files to Investigate**:
- `engine/OpenRA.Game/World.cs`
- `engine/OpenRA.Game/Map/ActorMap.cs`
- `engine/OpenRA.Game/Map/CellLayer.cs`

### 4. Render/Logic Separation

**Current Behavior**: Rendering and game logic are tightly coupled on the main thread.

**Multi-Threading Potential**: HIGH (but complex)

**Approach**:
- Run game logic on dedicated thread
- Render thread interpolates between game states
- Use double-buffering for game state

**Challenges**:
- Major architectural change
- Requires careful synchronization
- Input handling becomes more complex

**Files to Investigate**:
- `engine/OpenRA.Game/Game.cs` - main loop
- `engine/OpenRA.Game/Renderer/Renderer.cs`
- `engine/OpenRA.Platforms.Default/DefaultPlatform.cs`

---

## Lockstep Networking Constraints

OpenRA uses **lockstep networking** which imposes strict requirements:

1. **Determinism**: All clients must produce identical game states from identical inputs
2. **Synchronization**: Each tick must complete on all clients before advancing
3. **Order Matters**: Events must be processed in the same order everywhere

### Implications for Multi-Threading

- Any parallelization must produce **deterministic results**
- Non-deterministic operations (random numbers, floating-point rounding) must be synchronized
- Thread scheduling differences across machines could cause desyncs

### Safe Parallelization Patterns

1. **Read-Only Parallel Processing**: Multiple threads read game state, results merged deterministically
2. **Partitioned Processing**: Divide work into independent partitions, each processed by different thread
3. **Deferred Updates**: Collect updates in parallel, apply sequentially in deterministic order

---

## Effort Estimates

| Area | Impact | Complexity | Risk |
|------|--------|------------|------|
| Pathfinding | High | Medium | Medium - must maintain determinism |
| Unit AI | Medium | High | High - complex interactions |
| Spatial Index | Medium | Low | Low - mostly internal optimization |
| Render/Logic Split | High | Very High | High - major architecture change |

---

## Recommended Starting Points

For someone looking to contribute multi-threading improvements to OpenRA:

### 1. Start with Profiling

Before optimizing, profile the engine to identify actual bottlenecks:
- Use dotTrace, PerfView, or built-in profiling
- Focus on tick times during large battles
- Identify which operations consume the most time

### 2. Low-Hanging Fruit

- **Batch spatial index updates**: Collect all position changes, apply once per tick
- **Cache expensive calculations**: Many traits recalculate values unnecessarily
- **Optimize LINQ usage**: Replace hot-path LINQ with manual loops

### 3. Pathfinding Offloading

This is the most promising area:
- Pathfinding is already somewhat isolated
- Results can be cached and reused
- Async path requests are a known pattern in game engines

---

## Related OpenRA Issues

| Issue | Title | Relevance |
|-------|-------|-----------|
| [#21218](https://github.com/OpenRA/OpenRA/issues/21218) | Performance issues with big amount of units | Main performance tracking issue |
| [#12345](https://github.com/OpenRA/OpenRA/issues) | (Search for "pathfinding performance") | Pathfinding specific discussions |
| [#12345](https://github.com/OpenRA/OpenRA/issues) | (Search for "multi-thread") | Threading discussions |

---

## References

- [OpenRA GitHub](https://github.com/OpenRA/OpenRA)
- [OpenRA Wiki - Dedicated Server](https://github.com/OpenRA/OpenRA/wiki/Dedicated-Server)
- [Lockstep Protocol Explanation](https://www.gamedeveloper.com/programming/1500-archers-on-a-28-8-network-programming-in-age-of-empires-and-beyond)
- [Game Programming Patterns - Game Loop](https://gameprogrammingpatterns.com/game-loop.html)

---

*Created: 2025-12-29*
*Purpose: Document potential engine-level multi-threading improvements for upstream contribution*

---

## Engine-Level Performance Hotspots (Single-Threaded Fixes)

These are specific performance issues identified in the OpenRA engine that could be fixed without multi-threading, but require engine-level changes. These are documented here for potential upstream contribution.

### CRITICAL: Movement Order Delay (UnitOrderGenerator)

**File**: `engine/OpenRA.Mods.Common/Orders/UnitOrderGenerator.cs`
**Lines**: 162-165

**Issue**: When a player issues a movement command, the engine performs expensive target resolution synchronously on every mouse click.

**Current Behavior**:
```csharp
// Line 162-165 (conceptual)
foreach (var subject in selection)
{
    var target = ResolveTargetForSubject(subject, xy);
    // Expensive per-unit calculation on click
}
```

**Impact**: This is the PRIMARY cause of the 1-2 second input lag when clicking to move units. With 50+ selected units, the synchronous resolution causes noticeable delay.

**Proposed Fix**:
- Cache target resolution results per-tick
- Batch movement order processing
- Defer detailed pathfinding to background

### HIGH: Production Queue Tick Loop (ClassicProductionQueue)

**File**: `engine/OpenRA.Mods.Common/Traits/Player/ClassicProductionQueue.cs`
**Line**: ~56

**Issue**: Production tick iterates all production queues every game tick with LINQ operations.

**Impact**: In late-game with many production buildings, this adds unnecessary overhead to each tick.

**Proposed Fix**:
- Use dirty flag to only process queues with active production
- Cache buildable actor lists until prerequisites change

### MEDIUM: GroupBy Allocation (ClassicParallelProductionQueue)

**File**: `engine/OpenRA.Mods.Common/Traits/Player/ClassicParallelProductionQueue.cs`
**Line**: ~91

**Issue**: Uses `.GroupBy()` LINQ operation in tick-level code, causing allocations.

**Impact**: Minor per-tick allocations that accumulate during long games.

**Proposed Fix**:
- Pre-group production items by type
- Update groups only when items are added/removed

---

## Summary of Engine Fixes for Upstream

| Priority | File | Issue | Fix Type |
|----------|------|-------|----------|
| CRITICAL | UnitOrderGenerator.cs:162-165 | Synchronous target resolution | Caching/batching |
| HIGH | ClassicProductionQueue.cs:56 | Tick loop overhead | Dirty flag |
| MEDIUM | ClassicParallelProductionQueue.cs:91 | GroupBy allocation | Pre-grouping |

These fixes would require changes to the OpenRA engine and should be submitted as pull requests to the upstream repository after proper testing.
