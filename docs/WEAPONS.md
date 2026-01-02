# Weapons & Firepower Documentation

This document explains how weapon damage (firepower) is defined in Romanov's Vengeance.

## Overview

Weapon damage is defined in two main locations:
1. **Weapon definitions** (`mods/rv/weapons/*.yaml`) - Base damage values
2. **Firepower multipliers** (`mods/rv/rules/defaults.yaml`) - Damage modifiers from veterancy, crates, etc.

---

## Weapon Definition Files

All weapon YAML files are located in `mods/rv/weapons/`:

| File | Description |
|------|-------------|
| `defaults.yaml` | Base weapon templates (^Flak, ^Missile, ^MG, ^TeslaZap, etc.) |
| `bullets.yaml` | Tank cannons, mortars, grenades (105mm, 120mm, 155mm, etc.) |
| `missiles.yaml` | Missile weapons (RedEye2, MammothTusk, APTusk, etc.) |
| `mgs.yaml` | Machine gun weapons |
| `zaps.yaml` | Tesla/electric weapons (ElectricBolt, TeslaZap) |
| `flaks.yaml` | Anti-aircraft flak weapons |
| `melee.yaml` | Close combat weapons |
| `misc.yaml` | Miscellaneous weapons |
| `gravitybombs.yaml` | Superweapon bombs |
| `explosions.yaml` | Explosion effects |
| `gatling.yaml` | Gatling gun weapons |
| `ivanbombs.yaml` | Crazy Ivan bombs |
| `debris.yaml` | Debris weapons |
| `ifvweapons.yaml` | IFV variant weapons |

---

## Weapon Definition Structure

A typical weapon definition looks like this:

```yaml
WeaponName:
    Inherits: ^BaseWeapon          # Optional: inherit from template
    ReloadDelay: 60                # Time between shots (in game ticks)
    Range: 6c0                     # Attack range (OpenRA distance units)
    Report: sound.wav              # Attack sound effect
    Projectile: BulletAS           # Projectile type
        Speed: 1024                # Projectile speed
        Image: 120mm               # Projectile sprite
    Warhead@1Dam: SpreadDamage     # Damage warhead
        Spread: 0c256              # Damage spread radius
        Damage: 9000               # BASE DAMAGE VALUE
        Falloff: 100, 75, 50, 0    # Damage falloff by distance
        Versus:                    # Damage modifiers vs armor types (%)
            None: 100
            Light: 100
            Heavy: 75
            Concrete: 50
        DamageTypes: ExplosionDeath
```

---

## Key Damage Properties

| Property | Description | Example |
|----------|-------------|---------|
| `Damage` | Base damage value (integer) | `Damage: 15000` |
| `Versus` | Percentage modifier per armor type | `Heavy: 75` = 75% damage vs Heavy |
| `Spread` | Damage radius | `Spread: 0c512` |
| `Falloff` | Damage reduction over distance | `Falloff: 100, 75, 50, 0` |
| `DamageTypes` | Special damage flags | `FlameDeath, ElectroDeath` |

---

## Armor Types

The `Versus` block defines percentage damage against each armor type:

| Armor Type | Typical Usage |
|------------|---------------|
| `None` | Unarmored infantry |
| `Flak` | Light infantry armor |
| `Plate` | Medium infantry armor |
| `Light` | Light vehicles |
| `Medium` | Medium vehicles/tanks |
| `Heavy` | Heavy tanks |
| `Wood` | Wooden structures |
| `Steel` | Metal structures |
| `Concrete` | Concrete buildings |
| `Drone` | Terror drones |
| `Rocket` | Rockets/missiles |

---

## Example Weapon Definitions

### Tank Cannon (120mm)
```yaml
120mm:
    Inherits: ^LargeBullet
    ReloadDelay: 65
    Range: 6c0
    Report: vrhiatta.wav
    Projectile: BulletAS
        Speed: 1024
    Warhead@1Dam: SpreadDamage
        Damage: 9000
```

### Tesla Weapon
```yaml
ElectricBolt:
    Inherits: ^TeslaZap
    Range: 4c0
    ReloadDelay: 60
    Warhead@1Dam: SpreadDamage
        Spread: 120
        Damage: 5000
        Versus:
            Light: 100
            Heavy: 75
            Steel: 65
```

### Anti-Air Missile
```yaml
^AAMissile:
    Inherits: ^Missile
    ValidTargets: Air
    Warhead@1Dam: SpreadDamage
        Spread: 0c307
        Damage: 7500
        ValidTargets: Air
        Versus:
            Light: 100
            Heavy: 100
            Wood: 0
            Steel: 0
```

---

## Firepower Multipliers

Units can have their damage output modified via `FirepowerMultiplier` traits defined in `mods/rv/rules/defaults.yaml`:

### Veterancy Bonuses
```yaml
FirepowerMultiplier@RANK-1:
    RequiresCondition: rank-veteran >= 1
    Modifier: 120                  # +20% damage at veteran rank
```

### Crate Powerups
```yaml
FirepowerMultiplier@CRATES:
    RequiresCondition: crate-firepower
    Modifier: 200                  # 2x damage from firepower crate
```

### Propaganda Tower Aura
```yaml
FirepowerMultiplier@PROPAGANDA1:
    Modifier: 125                  # +25% damage
    RequiresCondition: propaganda1

FirepowerMultiplier@PROPAGANDA2:
    Modifier: 150                  # +50% damage
    RequiresCondition: propaganda2

FirepowerMultiplier@PROPAGANDA3:
    Modifier: 175                  # +75% damage
    RequiresCondition: propaganda3
```

---

## Distance Units

OpenRA uses a custom distance system:
- `1c0` = 1 cell = 1024 internal units
- `0c512` = half a cell
- `6c0` = 6 cells range

---

## Adding a New Weapon

1. Create the weapon definition in the appropriate file under `mods/rv/weapons/`
2. Use `Inherits: ^TemplateName` to inherit from base templates when possible
3. Define the `Warhead` with appropriate `Damage` and `Versus` values
4. Assign the weapon to a unit in `mods/rv/rules/*.yaml` via the `Armament` trait
5. Run `make test` to validate YAML syntax
