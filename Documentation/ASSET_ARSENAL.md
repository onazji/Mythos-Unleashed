# Mythosmith Asset Arsenal

- **Studio:** Abundant Mythosmith Studios
- **Project:** Mythos Unleashed
- **Authority:** Mythosmith Arsenal Protocol Concept Sheet v1.0, November 17, 2026
- **Repository audit:** November 17, 2026
- **Owned packages:** 65

## Purpose and evidence rules

This registry records technology owned by the studio and the repository evidence available at the audit date. It is a context and ownership inventory, not an autonomous development plan or approval to integrate anything.

- **Owned — import unverified:** ownership is established by the Concept Sheet, but this audit found no exact package identity and version evidence in the inspected repository locations.
- **Imported — confirmed:** package content and Asset Store origin metadata are present in `Assets`. This does not imply that the package is evaluated, integrated, validated, or approved for new use.
- Concept Sheet versions are values displayed in the creator's Unity Package Manager screenshots. They are not treated as repository versions unless local evidence independently confirms them.
- `3.4…` and `1.1…` are intentionally retained as truncated source values. No missing digits are inferred.

The audit inspected `Assets`, `Packages/manifest.json`, and `Packages/packages-lock.json`. Asset Store import evidence came from local `AssetOrigin` metadata and matching package directories. The package manifests contain Unity registry and built-in dependencies; they do not identify any of the 65 Asset Store packages by the Concept Sheet names.

## Audit summary

| Classification | Count | Meaning |
|---|---:|---|
| Imported — confirmed | 6 | Exact or clearly corresponding Concept Sheet identity, local content, and matching local version evidence |
| Owned — import unverified | 59 | Owned according to the Concept Sheet; no exact local import/version evidence found in this audit |
| Total owned packages | 65 | Authoritative Concept Sheet total |

### Confirmed imports

| Concept Sheet package | Sheet version | Local metadata identity and version | Confirmed project location |
|---|---:|---|---|
| TopDown Engine | 4.4 | `TopDown Engine` 4.4 | `Assets/_ThirdParty/TopDownEngine` |
| Dungeon Architect | 1.22.0 | `Dungeon Architect` 1.22.0 | `Assets/CodeRespawn` (with related Dungeon Architect content under `Assets/_ThirdParty`) |
| Feel | 5.8 | `Feel` 5.8 | `Assets/_ThirdParty/Feel` |
| POLYGON Dungeons Pack | 1.9.5 | `POLYGON Dungeons - Low Poly 3D Art by Synty` 1.9.5 | `Assets/_ThirdParty/Synty/PolygonDungeon`; related project prefabs under `Assets/Prefabs/Dungeon` |
| POLYGON Ancient Egypt Pack | 1.0.1 | `Ancient Egypt Pack - Synty POLYGON - Stylized Low Poly Assets` 1.0.1 | `Assets/_ThirdParty/Synty/PolygonAncientEgypt` |
| Low Poly Environment – Nature Free | 1.1.1 | `Lowpoly Environment - Nature Free - MEDIEVAL FANTASY SERIES` 1.1.1 | `Assets/_ThirdParty/Polytope Studio` |

### Unmatched local package evidence

| Local package identity | Local version | Location | Classification decision |
|---|---:|---|---|
| Stylized Nature Kit Lite | 1.2 | `Assets/_ThirdParty/Proxy Games/Stylized Nature Kit Lite` | Locally present, but not mapped to an owned registry entry. It is not assumed to be `Hand Painted Nature Kit LITE` 2.0 because both identity and version differ. |

## Complete owned-package registry

Locations are supplied only where this audit confirmed them. “No exact evidence found” means import state is unverified, not that the package is absent from the creator's machine or Unity account.

### A. Core Gameplay Architecture — 6

| ID | Package | Concept Sheet version | Repository status | Confirmed evidence/location |
|---:|---|---:|---|---|
| 01 | TopDown Engine | 4.4 | Imported — confirmed | AssetOrigin 4.4; `Assets/_ThirdParty/TopDownEngine` |
| 02 | Dungeon Architect | 1.22.0 | Imported — confirmed | AssetOrigin 1.22.0; `Assets/CodeRespawn` |
| 03 | Dialogue System for Unity | 2.2.59 | Owned — import unverified | No exact evidence found |
| 04 | Quest Machine | 1.2.62 | Owned — import unverified | No exact evidence found |
| 05 | Easy Save | 3.5.24 | Owned — import unverified | No exact evidence found |
| 06 | Feel | 5.8 | Imported — confirmed | AssetOrigin 5.8; `Assets/_ThirdParty/Feel` |

### B. Developer Infrastructure — 7

| ID | Package | Concept Sheet version | Repository status | Confirmed evidence/location |
|---:|---|---:|---|---|
| 07 | Odin Inspector and Serializer | 3.3.1.13 | Owned — import unverified | No exact evidence found |
| 08 | Draw XXL | 1.0.6 | Owned — import unverified | No exact evidence found |
| 09 | Build Report Tool | 3.13.11 | Owned — import unverified | No exact evidence found |
| 10 | RapidIcon | 1.7.4 | Owned — import unverified | No exact evidence found |
| 11 | SRP Material Converter | 1.2.0 | Owned — import unverified | No exact evidence found |
| 12 | Ultimate LOD System MT | 3.4… | Owned — import unverified | No exact evidence found; source version is truncated |
| 13 | Playmaker | 1.9.9 | Owned — import unverified | No exact evidence found |

### C. Interface and Navigation — 5

| ID | Package | Concept Sheet version | Repository status | Confirmed evidence/location |
|---:|---|---:|---|---|
| 14 | Heat – Complete Modern UI | 1.2.0 | Owned — import unverified | No exact evidence found |
| 15 | Clean Settings UI | 1.0 | Owned — import unverified | No exact evidence found |
| 16 | Northstar Navigation | 1.2.2 | Owned — import unverified | No exact evidence found |
| 17 | Dynamic Camera Project | 1.0 | Owned — import unverified | No exact evidence found |
| 18 | Realistic Volume Profiles | 2.0 Major | Owned — import unverified | No exact evidence found |

### D. Visual Effects and Atmosphere — 6

| ID | Package | Concept Sheet version | Repository status | Confirmed evidence/location |
|---:|---|---:|---|---|
| 19 | Casual RPG VFX | 1.2 | Owned — import unverified | No exact evidence found |
| 20 | Spells Pack | 1.3.14 | Owned — import unverified | No exact evidence found |
| 21 | Magic Arsenal | 2.75 | Owned — import unverified | No exact evidence found |
| 22 | Stylized VFX Bundle | 1.0.1 | Owned — import unverified | No exact evidence found |
| 23 | Lux URP Essentials | 2.07 | Owned — import unverified | No exact evidence found |
| 24 | UniStorm | 5.4.1 | Owned — import unverified | No exact evidence found |

### E. Music and Audio — 9

| ID | Package | Concept Sheet version | Repository status | Confirmed evidence/location |
|---:|---|---:|---|---|
| 25 | Koreographer | 1.6.3 | Owned — import unverified | No exact evidence found |
| 26 | FMOD for Unity | 2.02.29 | Owned — import unverified | No exact evidence found |
| 27 | Reactional Music System | 1.2 | Owned — import unverified | No exact evidence found |
| 28 | LMNT Emotive AI Speech | 0.2.1 | Owned — import unverified | No exact evidence found |
| 29 | The Complete UI Sound Effects Library | 1.0.1 | Owned — import unverified | No exact evidence found |
| 30 | Free Casual & Relaxing Game Music Pack | 1.0 | Owned — import unverified | No exact evidence found |
| 31 | Free RPG Music Collection | 1.0 | Owned — import unverified | No exact evidence found |
| 32 | Free Meditative Music Pack | 1.0 | Owned — import unverified | No exact evidence found |
| 33 | 8-Bit Studio Fantasy Music Bundle Vol. 1 | 1.0.0 | Owned — import unverified | No exact evidence found |

### F. World Construction — 16

| ID | Package | Concept Sheet version | Repository status | Confirmed evidence/location |
|---:|---|---:|---|---|
| 34 | POLYGON Dungeons Pack | 1.9.5 | Imported — confirmed | AssetOrigin 1.9.5; `Assets/_ThirdParty/Synty/PolygonDungeon` |
| 35 | POLYGON Ancient Egypt Pack | 1.0.1 | Imported — confirmed | AssetOrigin 1.0.1; `Assets/_ThirdParty/Synty/PolygonAncientEgypt` |
| 36 | Cozy Nature FREE – Voxel Style | 1.0 | Owned — import unverified | No exact evidence found |
| 37 | Ultimate Nature Starter | 1.2 | Owned — import unverified | No exact evidence found |
| 38 | Hand Painted Nature Kit LITE | 2.0 | Owned — import unverified | No exact evidence found; not assumed equivalent to the unmatched local Stylized Nature Kit Lite 1.2 |
| 39 | Ultimate StampIT! Bundle for GAIA | 1.5.0 | Owned — import unverified | No exact evidence found |
| 40 | StampIT! Collection | 1.1… | Owned — import unverified | No exact evidence found; source version is truncated |
| 41 | Terrain Textures FREE | 1.1.0 | Owned — import unverified | No exact evidence found |
| 42 | AllSky Free | 1.1.0 | Owned — import unverified | No exact evidence found |
| 43 | MINIFANTASY Dungeon | 1.1.1 | Owned — import unverified | No exact evidence found |
| 44 | Low Poly Environment – Nature Free | 1.1.1 | Imported — confirmed | AssetOrigin 1.1.1; `Assets/_ThirdParty/Polytope Studio` |
| 45 | Cube World Farming Set | 1.1 | Owned — import unverified | No exact evidence found |
| 46 | Autotiles 3D | 1.3.4 | Owned — import unverified | No exact evidence found |
| 47 | RealBlend | 1.3.2 | Owned — import unverified | No exact evidence found |
| 48 | Easy Grid Builder Pro | 1.1.2 | Owned — import unverified | No exact evidence found |
| 49 | Low Poly Melee & Ranged Medieval Weapons | 1.0 | Owned — import unverified | No exact evidence found |

### G. Characters and Animation — 6

| ID | Package | Concept Sheet version | Repository status | Confirmed evidence/location |
|---:|---|---:|---|---|
| 50 | Animation Composer System | 1.3.9 | Owned — import unverified | No exact evidence found |
| 51 | FREE – 32 RPG Animations | 1.1 | Owned — import unverified | No exact evidence found |
| 52 | Human Basic Motions FREE | 2.4.1 | Owned — import unverified | No exact evidence found |
| 53 | Human Crafting Animations FREE | 2.2 | Owned — import unverified | No exact evidence found |
| 54 | Human Character Dummy | 2.0 | Owned — import unverified | No exact evidence found |
| 55 | Knight Sprite Sheet (Free) | 1.0 | Owned — import unverified | No exact evidence found |

### H. Experimental Systems — 10

| ID | Package | Concept Sheet version | Repository status | Confirmed evidence/location |
|---:|---|---:|---|---|
| 56 | Convai NPC AI Engine | 3.3.0 | Owned — import unverified | No exact evidence found |
| 57 | Maze Generator | 1.0 | Owned — import unverified | No exact evidence found |
| 58 | 2D Procedural Dungeon Generator | 1.0.0 | Owned — import unverified | No exact evidence found |
| 59 | Corgi Engine | 9.1 | Owned — import unverified | No exact evidence found |
| 60 | Realistic Car Controller | 6.0.0 | Owned — import unverified | No exact evidence found |
| 61 | Mobile Pedestrian System | 1.5.12 | Owned — import unverified | No exact evidence found |
| 62 | Dialogue System (separate package) | 1.1.4 | Owned — import unverified | No exact evidence found |
| 63 | VNCreator | 1.1 | Owned — import unverified | No exact evidence found |
| 64 | Unity Learn – 2D Beginner | 1.1.0 | Owned — import unverified | No exact evidence found |
| 65 | Creator Kit: FPS | 3.0 | Owned — import unverified | No exact evidence found |

## Architectural ownership map

These are preferred or candidate responsibilities from the Concept Sheet. Each integration must still be verified independently.

| Gameplay domain | Preferred technology or authority |
|---|---|
| Player and combat | TopDown Engine |
| Inventory data | More Mountains Inventory Engine |
| Procedural mazes | Dungeon Architect |
| World persistence | Existing Mythos architecture |
| NPC dialogue | Dialogue System for Unity |
| Quest management | Quest Machine |
| Permanent save/load | Existing save capabilities; evaluate Easy Save |
| Inventory presentation | Inventory Engine UI first; Heat for styling |
| Item icons | RapidIcon |
| Gameplay feedback | Feel |
| Navigation | Northstar Navigation |
| Editor tooling | Odin Inspector / Draw XXL |
| Adaptive audio | Evaluate FMOD and Koreographer |
| Architectural verification | World Transit Validator / World State Witness |

Every gameplay domain must have one clearly defined authoritative system. Presentation assets consume authoritative gameplay data rather than create competing copies.

## Unity package manifest evidence

The following direct, non-module dependencies are recorded by both `Packages/manifest.json` and `Packages/packages-lock.json`. They are project dependencies, but they are not part of the 65-package owned Arsenal count.

| Package ID | Locked/direct version |
|---|---:|
| `com.unity.ai.navigation` | 2.0.12 |
| `com.unity.cinemachine` | 3.1.5 |
| `com.unity.collab-proxy` | 2.12.4 |
| `com.unity.ide.rider` | 3.0.39 |
| `com.unity.ide.visualstudio` | 2.0.27 |
| `com.unity.inputsystem` | 1.19.0 |
| `com.unity.multiplayer.center` | 1.0.1 |
| `com.unity.pipeline` | 0.7.0-exp.1 |
| `com.unity.postprocessing` | 3.5.4 |
| `com.unity.render-pipelines.universal` | 17.4.0 |
| `com.unity.test-framework` | 1.6.0 |
| `com.unity.timeline` | 1.8.12 |
| `com.unity.ugui` | 2.0.0 |
| `com.unity.visualscripting` | 1.9.11 |

## Operating doctrine

Before implementing a new system:

1. Determine whether the existing project already supports it.
2. Investigate TopDown Engine's native capabilities.
3. Consult this registry.
4. Verify relevant documentation and integration evidence.
5. Extend existing technology where appropriate.
6. Build a custom solution only when justified by the approved scope.

Use what the studio owns before buying what it lacks. Use the engine before extending the engine; extend the engine before replacing it. Do not confuse ownership, download, import, evaluation, integration, validation, deferral, or retirement—each lifecycle state requires independent evidence.
