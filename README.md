# Red Alert 2: Romanov's Vengeance

Romanov's Vengeance is a 3rd party [OpenRA](http://www.openra.net) mod based on OpenRA [Red Alert 2](http://www.github.com/OpenRA/ra2) mod. It aims to create a Red Alert 2 with balanced multiplayer experience, improvements that comes from OpenRA and other improvements from more modern Command & Conquer games. A custom campaign is also being planned, but not much of a work is done on it yet.

Please note that mod is still under development, even the playtest versions are susceptible to bugs. There are still a few features from the original game that are still missing, there are several placeholder artwork and new stuff and balancing are always subject to change.

Installing the mod is done the same way as another [OpenRAModSDK](http://www.github.com/OpenRA/OpenRAModSDK) mod.

You can join our Discord server [here](https://discord.gg/SrvArjQ).

You can also follow the development on [our ModDB Page](https://www.moddb.com/mods/romanovs-vengeance).

## Building and Testing

### Prerequisites

- **Mono** (version 6.4 or greater) - required for running the game
- **Python 3** (or Python 2) - required for build scripts
- **Git** - for version control and engine fetching
- **curl** or **wget** - for downloading the engine

### Build Commands

```bash
# Build the mod (fetches engine automatically if needed)
make

# Build using Mono instead of .NET
make RUNTIME=mono

# Clean build artifacts
make clean

# Set mod version
make version [VERSION="custom-version"]
```

### Testing Commands

```bash
# Run StyleCop code analysis on C# files
make check

# Validate mod YAML files
make test

# Check Lua script syntax (requires luac)
make check-scripts
```

### Windows (PowerShell)

```powershell
.\make.ps1           # Build
.\make.ps1 check     # StyleCop analysis
.\make.ps1 test      # YAML validation
```

### Standalone Scripts

```bash
# Fetch/update the OpenRA engine (called automatically by make)
./fetch-engine.sh

# Launch the game
./launch-game.sh

# Launch a dedicated multiplayer server
./launch-dedicated.sh

# Run mod utilities (YAML validation, map tools, etc.)
./utility.sh [command]
```

### Running the Game

```bash
# Linux/macOS
./launch-game.sh

# Windows
launch-game.cmd
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

## Performance Investigation

This fork includes performance optimization work targeting input lag issues observed in multiplayer games. See:

- [ROMANOVS-LAG-INVESTIGATION.md](ROMANOVS-LAG-INVESTIGATION.md) - Root cause analysis and code-level performance hotspots
- [ENGINE-MULTITHREADING-NOTES.md](ENGINE-MULTITHREADING-NOTES.md) - Engine-level multi-threading considerations

### Optimizations Implemented

| Component | Optimization |
|-----------|--------------|
| Missile System | Cached speed modifiers, pre-computed trigonometry |
| Mirage System | Cached player references |
| Warheads | Replaced LINQ with manual loops in hot paths |
| Deploy System | HashSet for O(1) actor lookups |

## Forking with wiki, issues and pull requests

The README and code are copied automatically when you fork. To keep the rest of the project metadata:

- **Wiki:** clone the original wiki and push it to your fork (replace `<your-username>`).
  ```sh
  git clone https://github.com/MustaphaTR/Romanovs-Vengeance.wiki.git
  cd Romanovs-Vengeance.wiki
  git remote set-url origin https://github.com/<your-username>/Romanovs-Vengeance.wiki.git
  git push --mirror
  ```
- **Issues:** GitHub does not copy issues on fork. With the GitHub CLI (`gh`) and `jq` you can export and recreate them:
  ```
  gh api repos/MustaphaTR/Romanovs-Vengeance/issues --paginate > issues.json
  while IFS= read -r issue; do
    readarray -t labels < <(echo "$issue" | jq -r '.labels[]? // empty')
    label_args=""
    if [ ${#labels[@]} -gt 0 ]; then
      label_args=$(printf ' --label "%s"' "${labels[@]}")
    fi
    gh issue create --repo <your-username>/Romanovs-Vengeance \
      --title "$(echo "$issue" | jq -r .title)" \
      --body "$(echo "$issue" | jq -r .body)" \
      $label_args
  done < <(jq -c '.[] | {title, body, labels: [.labels[].name]}' issues.json)
  ```
  Authors and timestamps cannot be preserved.
- **Pull requests:** GitHub cannot transfer PRs to a fork. Keep the original repository as an `upstream` remote to reference old PRs, fetch the source branches, or ask contributors to reopen their PRs against your fork.
