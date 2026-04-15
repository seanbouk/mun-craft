# Mun Craft

A first-person voxel game where you walk around a small handmade planet — like Minecraft meets Mario Galaxy.

## What it's about

You stand on the surface of a little world built out of chunky 14-sided blocks. Look down and the ground curves away in every direction; walk far enough in any direction and you come back to where you started. Mine into the ground and the gravity will start to shift — because gravity here isn't a force pointing "down" toward the centre of the planet, it's the combined pull of every block in the world.

That means the shape of the world *is* the gravity field. Dig a tunnel right through the middle and at some point your sense of which way is up will flip. Mine away enough mass on one side of the planet and the rest will start to feel oddly tilted. The whole game is built around that one idea — that you can change a planet's gravity by changing its shape.

It's single player, browser-based (WebGL), and the look is deliberately stripped back: flat colours, no shadows, no textures. The mining is hold-to-break, with each block type taking longer than the one above it as you dig deeper.

### Controls

| Input | Action |
|---|---|
| W / A / S / D | Move on the surface |
| Mouse | Look around |
| Space | Jump |
| Left mouse (hold) | Mine the targeted block |
| Backtick (`` ` ``) | Toggle the developer overlay |
| Escape | Release the mouse cursor |

---

# For developers

This section is the working reference for anyone reading or extending the code.

## Stack

- Unity **6.4** (`6000.4.2f1`) with the **Universal Render Pipeline**
- Build target: **WebGL**
- Input: the **new Input System** package (`UnityEngine.InputSystem`)
- Player Settings → Active Input Handling: **Both** (keeps legacy code paths from blowing up if anything still touches `UnityEngine.Input`)

## First-time setup

If you're cloning this repo fresh:

1. Open the project folder in **Unity Hub** (Add → Open). It will fetch packages on first launch.
2. **File → Build Profiles → WebGL → Switch Platform.**
3. **Edit → Preferences → External Tools → External Script Editor → VS Code** (or your editor of choice).
4. Open `Assets/Scenes/SampleScene.unity` and hit Play.

The scene contains a single `GameBootstrap` GameObject. Everything else (chunks, gravity, player, debug UI) is created at runtime by that component's `Start()`.

## Architecture

```
                          GameBootstrap
                (creates the world, spawns the player,
                 wires events, owns the debug overlay)
                /                |                 \
               v                 v                  v
       ChunkManager        GravityField          DebugUI
       single source       Barnes-Hut octree    IMGUI overlay
       of block data       owns gravity queries  for tuning
            |                    ^
            | OnBlockChanged     | incremental
            |                    | add/remove
            v                    |
       ChunkRenderer ------------+
       per-chunk mesh,
       per-face colour edits
            ^
            | (registered in)
            |
       ChunkRendererRegistry  <----  BlockMiner
       coord -> renderer             (mutates target
                                      block's faces)

                           Player
            PlayerController  -- queries gravity, integrates motion
            PlayerCollision   -- custom capsule-vs-block
            PlayerCamera      -- mouse X rotates body, mouse Y pitches camera
            BlockMiner        -- raycast + hold-to-mine
```

`ChunkManager` is the single source of truth for block data. Any change fires `OnBlockChanged`, which `GravityField` (incremental octree update) and `GameBootstrap` (mark affected chunks dirty for remesh) listen to.

## Project layout

```
Assets/
├── Scripts/
│   ├── Core/                     # Block data + lattice math (no Unity refs in most of it)
│   │   ├── BlockAddress.cs       # (parity, x, y, z) struct, neighbour math, world-pos conversion
│   │   ├── BlockType.cs          # Block enum + 3-shade colour palette + per-type mining times
│   │   ├── Chunk.cs              # Two byte[] arrays (grid A and grid B), 8x8x8 each
│   │   ├── ChunkFaceMap.cs       # block -> face -> vertex range, built by the mesher
│   │   └── ChunkManager.cs       # Owns chunks, fires OnBlockChanged
│   │
│   ├── Meshing/
│   │   ├── TruncOctGeometry.cs   # Vertex/face/normal tables for the 14-sided block
│   │   ├── ChunkMesher.cs        # Builds one Mesh per chunk, also builds the face map
│   │   ├── ChunkRenderer.cs      # MonoBehaviour per chunk, exposes per-face colour edits
│   │   └── ChunkRendererRegistry.cs  # Static coord -> renderer lookup
│   │
│   ├── Gravity/
│   │   ├── GravityOctree.cs      # Barnes-Hut tree with incremental Add/Remove
│   │   └── GravityField.cs       # Singleton, exposes GetGravityAt(worldPos)
│   │
│   ├── Player/
│   │   ├── PlayerController.cs   # Custom motion (no Rigidbody), substepped
│   │   ├── PlayerCollision.cs    # Capsule-vs-circumsphere multi-pass + ground probe
│   │   └── PlayerCamera.cs       # Mouse X rotates body, mouse Y pitches camera
│   │
│   ├── Interaction/
│   │   └── BlockMiner.cs         # Hold-to-mine with whole-brick flash feedback
│   │
│   └── Debug/
│       ├── SphereGenerator.cs    # Builds the initial planet
│       ├── DebugUI.cs            # IMGUI overlay (toggle with backtick)
│       └── GameBootstrap.cs      # Wires the whole scene together at Start
│
├── Shaders/
│   ├── FlatBlock.shader          # Unlit vertex-colour shader (cull off so tunnel interiors render)
│   ├── PieMask.shader            # (Unused — earlier mining-feedback experiment, kept for reference)
│   └── MiningOverlay.shader      # (Unused — earlier mining-feedback experiment, kept for reference)
│
└── Scenes/
    └── SampleScene.unity
```

## Systems

### BCC lattice

Truncated octahedrons tile 3D space on a **body-centred cubic lattice** — two interleaved cubic grids:

- **Grid A** (parity 0): block at integer indices `(x, y, z)` lives at world position `(x, y, z) * blockSize`
- **Grid B** (parity 1): block at integer indices `(x, y, z)` lives at world position `(x + 0.5, y + 0.5, z + 0.5) * blockSize`

Alternating y-layers are offset by half in x and z. Each block has **14 neighbours**:

- 6 same-grid (across square faces): `(±1, 0, 0)`, `(0, ±1, 0)`, `(0, 0, ±1)`
- 8 cross-grid (across hex faces): all combos of `(0 or -1, 0 or -1, 0 or -1)` for Grid A, `(0 or +1, ...)` for Grid B

A `BlockAddress` is `(byte parity, int x, int y, int z)`. Neighbour math is integer addition; world-position math is integer-plus-optional-half. See `BlockAddress.GetNeighbors` and `ToWorldPosition`.

### Chunks

A `Chunk` covers an 8×8×8 region per grid — 1,024 block slots total, 1 KB of data. Chunks are stored in a `Dictionary<Vector3Int, Chunk>` on `ChunkManager` and identified by their grid-space origin.

`ChunkManager.GetBlock(BlockAddress)` returns `Air` if the chunk doesn't exist, so cross-chunk neighbour queries are transparent.

### Meshing

For each chunk, `ChunkMesher.BuildMesh` does a single sweep:

1. Walk both grids, skip air blocks
2. For each non-air block, check each of 14 neighbours via `BlockAddress.GetNeighbors`
3. Emit faces only where the neighbour is air — interior blocks contribute nothing
4. Bake the block's colour into vertex colours (one of three random shades per block, hash of address)
5. Return the `Mesh` plus a `ChunkFaceMap` recording (block, face) → (vertStart, vertCount)

A fully exposed truncated octahedron is 44 triangles (8 hex × 4 + 6 squares × 2), so meshing cost scales with surface area. Surface chunks are cheap; interior chunks are nearly free.

### Per-face colour edits

The `ChunkFaceMap` lets `ChunkRenderer` mutate specific block faces in the existing mesh without a full remesh. The renderer keeps two parallel colour buffers — `_baseColors` (pristine, set when meshing) and `_workingColors` (uploaded to the GPU). Public methods:

- `SetFaceColor(addr, faceIdx, color)` — override one face
- `RestoreFaceColor(addr, faceIdx)` — restore to base
- `SetBlockColor(addr, color)` — override every exposed face of a block
- `RestoreBlockColors(addr)` — restore the whole block

Changes are batched: a single `Mesh.SetColors` call happens once per `LateUpdate` if anything was dirty.

`ChunkRendererRegistry` is a static `Dictionary<Vector3Int, ChunkRenderer>` so consumers (e.g. `BlockMiner`) can find a renderer by chunk coord without creating a `Core → Meshing` dependency cycle. Each `ChunkRenderer` registers itself in `Initialize` and unregisters in `OnDestroy`.

### Shader

`MunCraft/FlatBlock` is unlit, reads vertex colour straight to fragment, **cull off** so you can see chunk interiors when standing inside a tunnel. One shared material covers every chunk; all colour variation comes from vertex colours.

### Gravity (Barnes-Hut)

Every solid block is a unit point mass. The acceleration on the player is the sum of `G · m · d / |d|³` from every block — naively O(n) per frame. The Barnes-Hut octree reduces this to O(log n):

1. Build an octree of all block positions; each node stores `CenterOfMass` and `TotalMass`
2. To query gravity at a point, walk from the root. At each internal node, check `nodeSize / distance < θ` — if so, treat the whole subtree as a single point mass at its CoM. Otherwise recurse into children.

`θ` (`Theta` on the inspector) is the accuracy/speed knob: 0 = exact, 0.5 = balanced, ~1 = aggressive approximation.

**Live updates** are incremental: `RemoveBody(worldPos)` walks root → leaf, drops the body from the leaf, then walks back up recomputing `CoM` and `TotalMass` at each ancestor. O(log n) per change. `AddBody` works the same way, with a fallback to full rebuild if the new body falls outside the current root bounds.

`GravityField` exposes `GetGravityAt(Vector3)` and `GetUpAt(Vector3)`. The player controller calls `GetGravityAt` every frame.

### Player controller

`PlayerController` runs without a Rigidbody — we don't want PhysX fighting the bespoke gravity field.

Each frame:

1. Query `GravityField.GetGravityAt(transform.position)`
2. Slerp `_currentUp` toward `-gravity.normalized` (smooth re-orientation as you walk around the curve)
3. Project the body's forward onto the surface plane to keep WASD coherent
4. Apply input acceleration, drag, and (when airborne) gravity
5. Clamp velocity, then **substep** the position update so we never tunnel through the surface
6. After collision, the body's rotation is `LookRotation(forward, _currentUp)`

Mouse X rotates the **body** (so WASD always moves where you're looking horizontally); mouse Y is camera-only pitch, clamped to ±80°.

### Collision

`PlayerCollision.ResolveCollision` does **N passes** of capsule-vs-circumsphere:

1. Find the nearest lattice cell to the player on each grid
2. Scan a 5×5×5 region around the player (configurable)
3. For each solid block, compute the closest point on the capsule segment to the block centre
4. Track the **deepest** penetration this pass; resolve only that one (resolving all simultaneously is unstable with overlapping spheres)
5. Repeat for `Iterations` passes

After the resolution loop, a separate **ground probe** steps a short distance along `-up` from the player's feet looking for any solid block. This is independent of penetration depth and prevents the "grounded flickers true/false every frame" problem when the snap force balances against tiny float jitter.

`PlayerController` then layers **coyote time** (default 0.25s) on top — `IsGrounded` stays true for a quarter-second after the probe stops finding ground, so jumps don't fail off the edge of a brief flicker.

### Mining

`BlockMiner` is click-to-start, hold-to-continue:

1. Each frame, raycast from the camera (stepping along the ray, checking the nearest BCC cell at each step)
2. On a fresh `wasPressedThisFrame`, if you have a target, start mining it
3. Progress accrues at `1 / GetMiningTime(blockType)` per second
4. Releasing the mouse, drifting to a different block, or destroying the target all reset progress
5. At progress ≥ 1, set the block to `Air` (which fires `OnBlockChanged` → remesh + gravity update)

While mining, the **whole brick flashes**. The duration is divided into `FlashCount` (12) equal segments. Each segment is white for the first half, original for the second half — except the last segment which stays white throughout, so the brick is white at the moment of destruction. Slow blocks pulse slowly, fast blocks pulse rapidly; the rhythm is automatically tied to the block's hardness.

### Debug overlay

`DebugUI` is an IMGUI panel (toggle: backtick). It shows:

- FPS (always on, top-left)
- Live tunables: gravity constant, theta, max gravity, sphere radius
- Player readouts: position, distance from origin, gravity vector + magnitude, grounded, speed
- Input readouts: WASD, mouse delta, focus state
- Collision stats: blocks checked, collisions found, deepest penetration
- Toggles for gizmos: gravity vectors, chunk bounds, collision debug

## Key constants

Defaults sit on `GameBootstrap`, `GravityField`, `PlayerController`, `PlayerCollision`, and `BlockMiner` — all editable in the Inspector and most are also exposed in the debug UI for runtime tuning.

| Constant | Default | Where | Notes |
|---|---|---|---|
| `BlockSize` | 1.0 | GameBootstrap | World units per lattice cell |
| `Chunk.Size` | 8 | Chunk | Blocks per axis per grid (compile-time const) |
| `SphereRadius` | 12 | GameBootstrap | Initial planet radius in lattice cells |
| `GravityConstant` | 0.2 | GravityField | Calibrated for ~7,000 blocks → ~9.8 m/s² at the surface |
| `Theta` | 0.5 | GravityField | Barnes-Hut accuracy knob |
| `MaxGravity` | 30 | GravityField | Hard cap on returned magnitude |
| `Softening` | 0.5 | GravityField | Added to `r²` to avoid singularity at zero distance |
| `BlockCollisionRadius` | 0.559 | PlayerCollision | Circumsphere radius of a unit truncated octahedron |
| `Iterations` | 4 | PlayerCollision | Collision resolution passes per substep |
| `GroundProbeDistance` | 0.7 | PlayerCollision | How far below feet to look for ground |
| `CoyoteTime` | 0.25 | PlayerController | Seconds `IsGrounded` lingers after losing contact |
| `MaxStepDistance` | 0.3 | PlayerController | Substep size for collision integration |
| `MaxVelocity` | 20 | PlayerController | Player speed cap |
| `MoveSpeed` | 5 | PlayerController | Surface walking speed |
| `JumpForce` | 5 | PlayerController | Impulse magnitude on jump |
| `MiningRange` | 5 | BlockMiner | World-space raycast length |
| `FlashCount` | 12 | BlockMiner | White pulses spread over the mining duration |

Mining times per block are in `BlockType.GetMiningTime()` — Grass 0.4s, Dirt 0.6s, Sand 0.5s, Stone 1.6s, Iron 2.5s, Gold 3.0s, Crystal 4.0s.

## Building for WebGL

1. **File → Build Profiles → WebGL** (should already be active)
2. Player Settings:
   - Compression Format: **Gzip** (best browser support)
   - Memory Size: leave default (Unity 6 manages this automatically)
   - Enable Exceptions: **Explicitly Thrown** only
3. Build and Run

Constraints baked into the design:

- No compute shaders → all meshing happens on the CPU
- ~2GB memory ceiling → chunk data is compact `byte[]`
- One mesh per chunk, one shared material → keeps draw calls in check
- Unlit shader → minimal fragment cost

## Roadmap

Done so far:

- [x] BCC lattice + chunked block storage
- [x] Truncated-octahedron meshing with face culling
- [x] Per-face colour edits without remeshing
- [x] Barnes-Hut gravity with O(log n) incremental updates
- [x] Custom first-person controller with curved-surface gravity
- [x] Capsule-vs-block collision with stable grounding
- [x] Click-to-start hold-to-mine with per-block timing and flash feedback
- [x] Procedural sphere generator with depth-based block layers
- [x] Debug overlay for live tuning

Possible next steps:

- Block placement (right-click)
- Inventory / hotbar
- Larger worlds with chunk streaming and LOD
- More interesting terrain (caves, ore veins, biomes, structures)
- Save/load
- CRT post-processing effect with resolution downscaling
- Sound design
