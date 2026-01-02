# Aircraft Documentation

This document describes aircraft definitions in Romanov's Vengeance, including collision/stacking behavior.

## Overview

Aircraft are defined in `mods/rv/rules/aircraft.yaml`. The `Aircraft` trait controls flight behavior, including whether aircraft can overlap (stack) with each other.

---

## Aircraft Collision System

The key property controlling aircraft stacking is `Repulsable`:

| Property | Default | Description |
|----------|---------|-------------|
| `Repulsable` | `true` | Whether the aircraft is pushed away by nearby aircraft |
| `IdealSeparation` | `1706` | Distance aircraft try to maintain from each other |
| `RepulsionSpeed` | `-1` | Speed at which aircraft is pushed away (-1 = normal speed) |
| `CruiseAltitude` | varies | Only aircraft at the same altitude repulse each other |

### Behavior Summary

| Setting | Behavior |
|---------|----------|
| `Repulsable: true` | Aircraft push each other apart, cannot stack/overlap |
| `Repulsable: false` | Aircraft ignore each other, can stack/overlap freely |

---

## Aircraft List with Collision Status

### Buildable Combat Aircraft

| ID | Name | Type | Faction | Repulsable | Can Stack |
|----|------|------|---------|------------|-----------|
| `shad` | Nighthawk | Helicopter | Allies | `true` (default) | No |
| `zep` | Kirov Airship | Helicopter | Soviets | `false` | **Yes** |
| `orca` | Harrier | Plane | Allies | `true` (default) | No |
| `beag` | Black Eagle | Plane | Korea | `true` (default) | No |
| `schp` | Siege Chopper | Helicopter | Soviets | `true` (default) | No |
| `disk` | Floating Disc | Helicopter | Yuri | `true` (default) | No |
| `hind` | Hind | Helicopter | Soviets | `true` (default) | No |
| `fortress` | B2 Spirit | Plane | Allies | `true` (default) | No |
| `txdx` | Mosquito | Helicopter | Yuri | `true` (default) | No |
| `kite` | Black Kite | Helicopter | Vietnam | `true` | No |
| `magnedisk` | Magnetron Disc | Helicopter | Lazarus | `true` (default) | No |
| `havoc` | Havoc | Helicopter | Soviets | `true` | No |
| `badgr` | Badger Bomber | Plane | Baku | `true` (default) | No |
| `bpln.buildable` | MiG | Plane | Baku | `true` (default) | No |

### Support/Non-Combat Aircraft

| ID | Name | Type | Repulsable | Can Stack | Notes |
|----|------|------|------------|-----------|-------|
| `hornet` | Hornet | Plane | `false` | **Yes** | Carrier-launched |
| `asw` | Osprey | Plane | `true` (default) | No | Anti-sub aircraft |
| `spyp` | Spy Plane | Plane | `false` | **Yes** | Recon aircraft |
| `sdrn` | Spy Drone | Plane | `false` | **Yes** | Drone |
| `repdron` | Repair Drone | Helicopter | `true` | No | Repair support |

### Paradrop/Airstrike Aircraft (AI-controlled)

| ID | Name | Repulsable | Can Stack | Notes |
|----|------|------------|-----------|-------|
| `pdplane` | Paradrop Plane | `false` | **Yes** | Paradrop delivery |
| `bplndrop` | MiG Drop | `false` | **Yes** | Fast paradrop |
| `f22drop` | F22 Drop | `false` | **Yes** | Fast paradrop |
| `txbmbdrop` | Toxin Drop | `false` | **Yes** | Fast paradrop |
| `bpln` | MiG (Airstrike) | `false` | **Yes** | Boris airstrike |
| `a10` | A-10 Warthog | `true` (default) | No | Airstrike |
| `b52` | B-52 | `true` (default) | No | Carpet bomber |
| `pdplane.crate` | Crate Plane | `false` | **Yes** | Crate delivery |

---

## Aircraft Templates

Base templates are defined in `mods/rv/rules/defaults.yaml`:

| Template | CruiseAltitude | Type | Description |
|----------|----------------|------|-------------|
| `^NeutralAircraft` | default | Base | Non-combat aircraft base |
| `^Aircraft` | default | Base | Combat aircraft base |
| `^Plane` | `5c852` | Fixed-wing | Standard planes |
| `^Helicopter` | `3c852` | VTOL | Helicopters and hovering aircraft |
| `^PlaneHusk` | - | Husk | Falling plane wreckage |
| `^HelicopterHusk` | - | Husk | Falling helicopter wreckage |

---

## Example Definitions

### Kirov (Can Stack)
```yaml
zep:
    Inherits: ^Helicopter
    Aircraft:
        CruiseAltitude: 5c852
        TurnSpeed: 20
        Speed: 45
        Repulsable: false      # Allows stacking!
        AltitudeVelocity: 200
```

### Floating Disc (Cannot Stack)
```yaml
disk:
    Inherits: ^Helicopter
    Aircraft:
        TurnSpeed: 1023
        Speed: 180
        AltitudeVelocity: 200
        # Repulsable not set - defaults to true (no stacking)
```

### Black Kite (Explicitly No Stack)
```yaml
kite:
    Inherits: ^Helicopter
    Aircraft:
        TurnSpeed: 12
        Speed: 165
        Repulsable: true       # Explicitly set (same as default)
        AltitudeVelocity: 200
```

---

## How to Enable/Disable Stacking

### To allow an aircraft to stack (like Kirov):
```yaml
Aircraft:
    Repulsable: false
```

### To prevent stacking (default behavior):
```yaml
Aircraft:
    Repulsable: true    # Or just don't set it (default is true)
```

### To adjust separation distance:
```yaml
Aircraft:
    IdealSeparation: 512    # Smaller = closer together before repulsion
```

---

## Technical Notes

1. **Repulsion only works at CruiseAltitude** - Aircraft that are taking off, landing, or at different altitudes don't repulse each other.

2. **Same altitude required** - Only aircraft at the same `CruiseAltitude` repulse each other. Kirovs at `5c852` won't repulse helicopters at `3c852`.

3. **Friendly only** - Repulsion only affects friendly aircraft. Enemy aircraft can overlap.

4. **Performance** - Setting `Repulsable: false` reduces CPU overhead for large groups of aircraft.

---

## Distance Units

OpenRA uses a custom distance system:
- `1c0` = 1 cell = 1024 internal units
- `3c852` = 3 cells + 852 units (helicopter altitude)
- `5c852` = 5 cells + 852 units (high altitude for Kirovs, planes)
