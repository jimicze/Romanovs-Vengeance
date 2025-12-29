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

## Implemented Engine-Level Performance Fixes

These fixes have been implemented in the engine to address performance bottlenecks. They require engine changes and will need to be submitted upstream to MustaphaTR/OpenRA.

### FIX 1: IssueOrder Trait Caching (Actor.cs)

**Status**: IMPLEMENTED

**Files Modified**:
- `engine/OpenRA.Game/Actor.cs`
- `engine/OpenRA.Mods.Common/Orders/UnitOrderGenerator.cs`

**Problem**: Every mouse click in `UnitOrderGenerator.OrderForUnit()` executed:
```csharp
var orders = self.TraitsImplementing<IIssueOrder>()
    .SelectMany(trait => trait.Orders.Select(x => new { Trait = trait, Order = x }))
    .OrderByDescending(x => x.Order.OrderPriority)
    .ToList();
```
This created anonymous objects, allocated a list, and sorted by priority **per actor per click**. With 50+ selected units, this caused massive allocations and CPU work, resulting in 1-2 second input lag.

**Solution**: Cache `IIssueOrder` traits with their order targeters, pre-sorted by priority, at actor creation time.

In `Actor.cs`:
```csharp
// Added field
readonly (IIssueOrder Trait, IOrderTargeter Order)[] issueOrderTargeters;
public (IIssueOrder Trait, IOrderTargeter Order)[] IssueOrderTargeters => issueOrderTargeters;

// In constructor - collect and pre-sort once
var issueOrderTargetersList = new List<(IIssueOrder Trait, IOrderTargeter Order)>();
foreach (var issueOrder in issueOrdersList)
    foreach (var order in issueOrder.Orders)
        issueOrderTargetersList.Add((issueOrder, order));

issueOrderTargetersList.Sort((a, b) => b.Order.OrderPriority.CompareTo(a.Order.OrderPriority));
issueOrderTargeters = issueOrderTargetersList.ToArray();
```

In `UnitOrderGenerator.cs`:
```csharp
// PERF: Use pre-cached and pre-sorted IssueOrderTargeters from Actor
var orders = self.IssueOrderTargeters;
```

**Impact**: Eliminates per-click allocations and sorting. Mouse click handling is now O(1) lookup instead of O(n log n) per actor.

---

### FIX 2: ClassicParallelProductionQueue GroupBy Optimization

**Status**: IMPLEMENTED

**File Modified**: `engine/OpenRA.Mods.Common/Traits/Player/ClassicParallelProductionQueue.cs`

**Problem**: Used `GroupBy().ToList().Count` to count distinct items every tick:
```csharp
var parallelBuilds = Queue.FindAll(i => !i.Paused && !i.Done)
    .GroupBy(i => i.Item)
    .ToList()
    .Count - 1;
```
This allocated IGrouping objects and intermediate lists every tick.

**Solution**: Use HashSet for O(1) distinct counting without allocations:
```csharp
// PERF: Use HashSet to count distinct items instead of GroupBy().ToList().Count
var distinctItems = new HashSet<string>();
foreach (var i in Queue)
{
    if (!i.Paused && !i.Done)
        distinctItems.Add(i.Item);
}
var parallelBuilds = distinctItems.Count - 1;
```

**Impact**: Reduces per-tick allocations in production queue processing.

---

## Summary of Implemented Engine Fixes

| Priority | File | Issue | Status |
|----------|------|-------|--------|
| CRITICAL | Actor.cs + UnitOrderGenerator.cs | Per-click trait lookup and sorting | **FIXED** |
| MEDIUM | ClassicParallelProductionQueue.cs | GroupBy allocation per tick | **FIXED** |

---

## Future Engine Optimization Candidates

These are identified but not yet implemented optimizations:

### Production Actor Caching

**Issue**: Multiple places iterate `self.World.ActorsWithTrait<Production>()` every tick, then filter by owner.

**Proposed Fix**: Create per-player cache of Production trait actors, updated when Production trait is added/removed/enabled/disabled.

**Estimated Impact**: Medium - reduces iteration overhead in production-heavy scenarios.

---

*Last Updated: 2025-12-30*
*Implementation branch: perf/engine-fix*
