# AGENTS.md - AI Coding Agent Guidelines

Guidelines for AI agents working on Romanov's Vengeance, an OpenRA mod for Red Alert 2.

---

## Current Project Status

**Last Updated:** 2026-01-03

### Release Status

| Tag | Status | Notes |
|-----|--------|-------|
| `release-20260102` | Released | Stable release with all performance fixes |
| `playtest-20260102-v1` | Released | Same as release-20260102, GitHub Actions completed |

### Engine Version

```
ENGINE_VERSION="b74bd2094b21862478941ff218dee6950188db98"
```

### Bug Summary

| Priority | Open | Fixed |
|----------|------|-------|
| High | 1 | 6 |
| Medium | 4 | 0 |
| Low | 2 | 2 |

**High Priority (Open):**
1. Micro lag / GC pauses (root cause found, Server GC reverted due to memory bloat)

**Medium Priority (Open):**
1. Harrier doesn't return to base after expending ammo (fix ready, not deployed)
2. Lightning Storm shows ready but can't cast after Chrono Bomb (needs investigation)
3. B-2 Bomber production queue stuck at "ready" (needs investigation)
4. Vehicles rendering under bridge instead of on top (needs investigation)

See [bugs.md](bugs.md) for full details with root cause analysis and proposed fixes.

### Recommended Next Actions

1. **Implement Harrier Fix** - Return-to-base fix is ready, needs implementation
2. **Investigate New Bugs** - Lightning Storm, B-2 queue, bridge rendering issues
3. **Performance Optimization** - Address remaining bot module spikes, AttackAircraft, Cloak traits

See [ENGINE-DIAGNOSTICS.md](ENGINE-DIAGNOSTICS.md) for performance optimization opportunities.

---

## Project Overview

- **Type:** OpenRA game mod (Red Alert 2)
- **Languages:** C# (.NET 6.0), YAML, Lua, Fluent (.ftl), Shell/PowerShell
- **License:** GPL-3.0 (all C# files require license header)
- **Engine:** OpenRA (auto-fetched via `fetch-engine.sh`, do not edit `engine/`)
- **Minimum macOS:** 10.15 (Catalina)

## Build Commands

```bash
make                    # Build the mod (fetches engine if needed)
make RUNTIME=mono       # Build with Mono instead of .NET
make clean              # Clean build artifacts
make check              # Run StyleCop code analysis on C#
make test               # Validate mod YAML files
make check-scripts      # Check Lua syntax (requires luac)
```

**Windows (PowerShell):** `.\make.ps1 check`, `.\make.ps1 test`, `.\make.ps1 check-scripts`

**Single test runs:** No granular unit tests. Use `./utility.sh --check-yaml` for YAML validation.

## Engine Linking and Build Modes

### Repository Structure

This workspace contains two repositories:
- `Romanovs-Vengeance/` - The mod (YAML, Lua, mod-specific C#)
- `jimicze-OpenRA/` - Forked OpenRA engine with fixes

### Engine Configuration

The mod fetches engine based on `mod.config`:
```bash
ENGINE_VERSION="<commit-hash>"
AUTOMATIC_ENGINE_SOURCE="https://github.com/jimicze/OpenRA/archive/${ENGINE_VERSION}.zip"
```

### Build Modes

| Mode | Engine Source | Use Case |
|------|---------------|----------|
| **Local Development** | Symlink to `jimicze-OpenRA/` | Testing engine changes immediately |
| **Release Build** | Fetched from GitHub | Creating distributable DMG/installer |

### Local Development (Testing Engine Changes)

```bash
# 1. Remove auto-fetched engine
cd Romanovs-Vengeance
rm -rf engine

# 2. Create symlink to local engine fork
ln -s ../jimicze-OpenRA engine

# 3. Build and run
make clean && make
./launch-game.sh
```

**Important:** Changes in `jimicze-OpenRA/` are immediately available without re-fetching.

### Creating Test DMG (Local Engine)

```bash
# From Romanovs-Vengeance directory with symlinked engine
cd packaging

# Clean previous build artifacts
rm -rf macos/build macos/build.dmg

# Build and package
./package-all.sh <version-tag> ../build

# Move DMG to standard location
mv "../build/Romanovs Vengeance-<version-tag>.dmg" ../build/macos/
```

**Important:** Always create a proper DMG and place it in `build/macos/` for consistency.

### Creating Release DMG (Fetched Engine)

```bash
# 1. Push engine changes to GitHub
cd jimicze-OpenRA
git push origin <branch>

# 2. Update mod.config with new commit hash
cd ../Romanovs-Vengeance
# Edit mod.config: ENGINE_VERSION="<new-commit-hash>"

# 3. Clean and let it fetch fresh
rm -rf engine engine_temp
make clean

# 4. Build release package
cd packaging
./package-all.sh <release-tag> ../../rv-packages
```

### Switching Between Modes

```bash
# Switch to local development
rm -rf engine && ln -s ../jimicze-OpenRA engine

# Switch to release mode (fetch from GitHub)
rm engine  # removes symlink
make       # auto-fetches based on ENGINE_VERSION
```

## Project Structure

```
├── OpenRA.Mods.RA2/      # C# mod code (traits, activities, warheads)
│   ├── Activities/       # Actor activities (movement, actions)
│   ├── Traits/           # Actor behaviors and properties
│   └── Warheads/         # Weapon damage effects
├── mods/rv/              # Main mod content
│   ├── rules/            # Unit/building definitions (YAML)
│   ├── weapons/          # Weapon definitions (YAML)
│   ├── scripts/          # Campaign/map scripts (Lua)
│   └── fluent/           # Localization files (.ftl)
├── packaging/            # Installer build scripts
└── engine/               # OpenRA engine (auto-fetched, DO NOT EDIT)
```

## C# Code Style

**Indentation:** Tabs (4-space width), LF line endings only

**Required File Header:**
```csharp
#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion
```

**Naming Conventions:**
| Element | Style | Example |
|---------|-------|---------|
| Classes, Methods, Public Fields | PascalCase | `MirageInfo`, `RevealDelay` |
| Interfaces | I + PascalCase | `INotifyDamage` |
| Private/Protected Fields | camelCase | `remainingTime`, `lastPos` |
| Parameters, Locals | camelCase | `self`, `info`, `target` |

**Key Patterns:**
- Use `var` when type is apparent
- Use `readonly` for immutable fields
- Omit `this.` qualification
- Use `[Desc("...")]` to document trait properties
- Single-line conditionals may omit braces (project convention)

**OpenRA Trait Pattern:**
```csharp
public class MyTraitInfo : TraitInfo
{
    [Desc("Description for documentation.")]
    public readonly int SomeValue = 100;
    public override object Create(ActorInitializer init) { return new MyTrait(init, this); }
}

public class MyTrait : INotifyCreated, ITick
{
    readonly MyTraitInfo info;
    readonly Actor self;
    public MyTrait(ActorInitializer init, MyTraitInfo info)
    {
        this.info = info;
        self = init.Self;
    }
}
```

## YAML Conventions

**Indentation:** Tabs

```yaml
unitname:
    Inherits: ^BaseUnit           # Use ^ prefix for base templates
    Buildable:
        Prerequisites: building1, ~techcenter  # ~ = soft prerequisite
        BuildPaletteOrder: 100
    Tooltip:
        Name: actor-unitname-name  # Reference fluent localization key
```

## Lua Scripting

**Indentation:** Tabs

```lua
local myUnit = Actor.Create("unittype", true, { Owner = player })
Trigger.OnKilled(actor, function() end)
Trigger.AfterDelay(DateTime.Seconds(5), function() end)
```

## Fluent Localization (.ftl)

```ftl
## Comments use double hash
actor-unitname-name = Unit Display Name
actor-unitname-description = Description text here.
```

## Adding New Content

### New Trait
1. Create `YourTraitInfo` + `YourTrait` classes in `OpenRA.Mods.RA2/Traits/`
2. Add to actors in `mods/rv/rules/*.yaml`
3. Run `make check && make test`

### New Unit
1. Define in `mods/rv/rules/*.yaml`
2. Add sequences in `mods/rv/sequences/*.yaml`
3. Add localization in `mods/rv/fluent/*.ftl`
4. Run `make test`

## Pre-Commit Checklist

- [ ] `make check` passes (StyleCop)
- [ ] `make test` passes (YAML validation)
- [ ] `make check-scripts` passes (if Lua modified)
- [ ] Game launches and features work

## Important Notes

- **Engine pinned** in `mod.config` (`ENGINE_VERSION`) - do not modify `engine/`
- **Line endings** must be LF (Unix-style), never CRLF
- **Shell scripts** must be POSIX-compatible (verify at shellcheck.net)
- **CI runs** on Linux (.NET 6.0) and Windows (.NET 6.0)
- **macOS packaging** requires macOS 10.15+ (Mono support removed)

## GitHub Authentication

A GitHub token is stored in `/Users/lasakondrej/Projects/Romanov/.env` for use with `gh` CLI and git operations.

**Usage:**
```bash
# Source the token for gh CLI
export GITHUB_TOKEN=$(grep GITHUB_TOKEN /Users/lasakondrej/Projects/Romanov/.env | cut -d= -f2)

# Or use directly with git push
git push https://jimicze:$GITHUB_TOKEN@github.com/jimicze/Romanovs-Vengeance.git <branch-or-tag>
```

**Repositories:**
- **Mod:** `jimicze/Romanovs-Vengeance`
- **Engine:** `jimicze/OpenRA`

---

## Related Documentation

| File | Purpose |
|------|---------|
| [bugs.md](bugs.md) | All known bugs with status, root cause analysis, and proposed fixes |
| [ENGINE-DIAGNOSTICS.md](ENGINE-DIAGNOSTICS.md) | Diagnostic logging and future optimization opportunities |
| [Romanovs-Vengeance/ENGINE-MULTITHREADING-NOTES.md](Romanovs-Vengeance/ENGINE-MULTITHREADING-NOTES.md) | Implemented engine fixes and performance analysis |
| [Romanovs-Vengeance/ROMANOVS-LAG-INVESTIGATION.md](Romanovs-Vengeance/ROMANOVS-LAG-INVESTIGATION.md) | Original lag investigation and mod-level fixes |

---

## Useful Commands

### Build and Run

```bash
cd Romanovs-Vengeance
make clean && make
./launch-game.sh
```

### Check Game Logs

```bash
cat ~/Library/Application\ Support/OpenRA/Logs/debug.log
cat ~/Library/Application\ Support/OpenRA/Logs/perf.log
```

### GitHub Operations

```bash
# Set up token
export GITHUB_TOKEN=$(grep GITHUB_TOKEN /Users/lasakondrej/Projects/Romanov/.env | cut -d= -f2)

# Check GitHub Actions status
GITHUB_TOKEN=$GITHUB_TOKEN gh run list --repo jimicze/Romanovs-Vengeance

# View releases
open https://github.com/jimicze/Romanovs-Vengeance/releases
```
