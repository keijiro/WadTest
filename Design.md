# Doom WAD → Unity Visual Importer — Design Overview

## 1 Purpose

Provide an AI coding agent with a clear, implementation‑oriented overview for
building an **Editor‑time importer** that converts classic Doom WAD files into
**prefabs that reproduce level visuals only** inside a Unity project (URP). No
gameplay, physics, AI, or BSP‑tree occlusion is required.

## 2 Scope

- **In‑scope:** static geometry (floors, ceilings, walls) and their textures;
  palette‑accurate colour reproduction; automated prefab creation through a
  `ScriptedImporter` workflow.
- **Out‑of‑scope:** sprites, animated textures, sector lighting effects, sound,
  interactivity, BSP/NODES for visibility, multiplayer data, DEH patches.

## 3 High‑Level Architecture

```
┌────────────────────┐    ┌──────────────────┐   ┌──────────────────────┐
│  .wad file (IWAD)  │ →  │  Custom Importer │ → │  LevelPackage.asset  │
└────────────────────┘    └──────────────────┘   ├─────────┬────────────┤
                                                 │ Prefab  │ Sub‑assets │
                                                 └─────────┴────────────┘
```

### Components

| Module              | Responsibility                                                           |
| ------------------- | ------------------------------------------------------------------------ |
| **WadReader**       | Parse header & directory; stream lump payloads on demand.                |
| **TextureBuilder**  | Merge patch lumps via PNAMES+TEXTURE1/2; apply PLAYPAL[0] → `Texture2D`. |
| **MeshGenerator**   | Extrude 2‑D sectors into floors/ceilings; build wall quads.              |
| **MaterialFactory** | Create URP‑Unlit materials per unique texture.                           |
| **PrefabAssembler** | Compose GameObjects, assign meshes/materials, save as prefab.            |

## 4 Data‑Mapping Guidelines

| Doom Lump                       | Unity Construct                         | Notes                                    |
| ------------------------------- | --------------------------------------- | ---------------------------------------- |
| `VERTEXES`                      | `Vector2[]` (XY map vertices)           | Source plane, centimetres per unit = 1.  |
| `LINEDEFS` + `SIDEDEFS`         | Floor plan edges → **wall quads**, UVs  | Use middle/upper/lower texture codes.    |
| `SECTORS`                       | **Floor & ceiling planes**, heights → Y | Ceiling‑floor delta = wall height.       |
| `TEXTURE1/2` + `PNAMES` + `P_*` | `Texture2D` atlas (128×128 etc.)        | Patch compositing required.              |
| `PLAYPAL[0]`                    | Palette lookup → true‑colour            | Nearest‑RGB conversion, point filtering. |
| Other lumps                     | **Ignore** (for visuals‑only goal)      | Keep parser tolerant.                    |

## 5 Processing Pipeline

1. **Import trigger** — User drags `.wad` into Project window.
2. `ScriptedImporter.OnImportAsset` executes:
   1. Read directory; lazy‑load only lumps listed in §4.
   2. Build complete `LevelModel` (geometry + texture refs).
   3. Generate `Mesh` objects per sector (floor, ceiling) and per linedef wall.
   4. Synthesize textures and cache as `.asset` files; create materials.
   5. Instantiate hierarchy:
      - `LevelRoot` (empty GameObject)
      - `Sector_xxx` children with `MeshFilter`, `MeshRenderer`.
   6. Save hierarchy as prefab; mark as `ctx.SetMainObject(...)`.

## 6 Asset Layout

All generated data is embedded in **one `LevelPackage.asset` file** as the main
asset, with all artefacts stored as Unity sub‑assets. This keeps projects tidy
and guarantees prefab/mesh/texture version synchronisation.

```text
Assets/ImportedLevels/<WadName>.asset   ← LevelPackage (ScriptableObject, Main Asset)
 ├─ Prefab_<WadName>                   (GameObject prefab)
 ├─ Mesh_Floor_xxx                     (Mesh)
 ├─ Mesh_Wall_xxx                      (Mesh)
 ├─ Tex_xxx                            (Texture2D)
 └─ Mat_xxx                            (Material)
```

## 7 Key Technical Decisions

- **No runtime generation** — All assets baked at import for fastest play‑mode
  startup.
- **URP‑Unlit shader** — Matches Doom’s flat lighting; avoids normal mapping
  complexity.
- **Earcut triangulation** — Simple polygon → triangles for sector
  floors/ceilings.
- **Coordinate system** — Doom (X‑east, Y‑north) → Unity (X‑east, Z‑north);
  heights mapped to Y.

## 8 Test Strategy

### 8.1 Goal

Confirm—through **automated EditMode tests only**—that the importer converts a
WAD file into a logically consistent `LevelPackage.asset` with the expected
sub‑assets (prefab, meshes, textures, materials) and that these assets contain
non‑trivial data.

### 8.2 Unity Test Framework (EditMode) Tests

| Test Name                    | Purpose                                                                                 | Key Assertions                                                                                                                                         |
| ---------------------------- | --------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------ |
| **ImporterCreatesMainAsset** | Ensure the importer runs without exceptions and produces the main `LevelPackage.asset`. | `AssetDatabase.FindAssets` returns exactly **one** asset at `*.asset` root path and its `mainObject` is not null.                                      |
| **PrefabIntegrity**          | Verify the prefab hierarchy references valid meshes and materials.                      | Load prefab → iterate `MeshFilter` components → `mesh.triangles.Length > 0`; iterate `MeshRenderer` components → `sharedMaterial.mainTexture != null`. |
| **TextureDimensions**        | Check that generated textures are of expected power‑of‑two size.                        | Each `Texture2D` in sub‑assets: `width == 128 && height == 128` (or other legal size).                                                                 |

### 8.3 Test Assets

- **`DOOM1.WAD`** (id Software shareware) copied under
  `Assets/Tests/Resources/`. Only this single file is needed for basic
  correctness tests.
