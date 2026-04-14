# Mun Craft

A first-person voxel game built in Unity 6.4 for WebGL. Walk around a ball of truncated octahedron blocks with per-block gravity — like Minecraft meets Mario Galaxy.

## How It Works

The world is made of **truncated octahedrons** — 14-faced polyhedra that tessellate 3D space on a **BCC (body-centered cubic) lattice**. Blocks are arranged in a roughly spherical shape. Gravity isn't "point toward center" — every block exerts gravitational pull, approximated with a **Barnes-Hut octree** so distant blocks are cheap. Mine through the sphere and feel gravity shift as the mass distribution changes.

---

## Prerequisites

- **Unity 6.4** (6000.4.2f1) with **Web Build Support**
- **VS Code** with the Unity extension (or any C# editor)
- A modern browser for WebGL testing

---

## Phase 0: Create the Unity Project

This step is done manually in Unity Hub.

1. Open **Unity Hub → New Project**
2. Select the **3D (URP)** template
3. Set the project name to match this repo folder (or create inside it)
4. Unity version: **6.4 (6000.4.2f1)**
5. Click **Create project**

Once the editor opens:

6. **Switch build target**: File → Build Profiles → Add **WebGL** → Switch Platform
7. **Set external editor**: Edit → Preferences → External Tools → External Script Editor → **Visual Studio Code**
8. **Disable skybox** (optional, for the look): Window → Rendering → Lighting → Environment → Skybox Material → None, Ambient Color → dark gray

The project is now ready for the scaffold code.

---

## Project Structure

```
Assets/
├── Scripts/
│   ├── Core/                  # BCC lattice math, block data, chunk storage
│   │   ├── BlockAddress.cs    # (parity, x, y, z) with neighbor lookups
│   │   ├── BlockType.cs       # Block type enum + color mapping
│   │   ├── Chunk.cs           # Dual byte[] arrays for grid A and grid B
│   │   └── ChunkManager.cs    # Owns all chunks, handles cross-chunk lookups
│   │
│   ├── Meshing/               # Truncated octahedron mesh generation
│   │   ├── TruncOctGeometry.cs  # Vertex/face definitions for the shape
│   │   ├── ChunkMesher.cs     # Generates one Mesh per chunk (Job System)
│   │   └── ChunkRenderer.cs   # MonoBehaviour: MeshFilter + MeshRenderer
│   │
│   ├── Gravity/               # Barnes-Hut N-body gravity
│   │   ├── GravityOctree.cs   # Spatial tree with center-of-mass aggregation
│   │   └── GravityField.cs    # Singleton: query gravity at any point
│   │
│   ├── Player/                # First-person controller
│   │   ├── PlayerController.cs  # Movement + gravity orientation
│   │   ├── PlayerCollision.cs   # Custom capsule-vs-sphere collision
│   │   └── PlayerCamera.cs     # Gravity-aligned first-person camera
│   │
│   ├── Interaction/           # Block mining
│   │   └── BlockMiner.cs      # Ray-vs-lattice, click to destroy
│   │
│   └── Debug/                 # Development tools
│       ├── SphereGenerator.cs # Creates the initial ball of blocks
│       ├── DebugUI.cs         # IMGUI overlay with sliders and toggles
│       └── GameBootstrap.cs   # Wires up the scene on Start
│
├── Shaders/
│   └── FlatBlock.shader       # Unlit vertex-color shader
│
├── Materials/
│   └── BlockMaterial.mat      # Single shared material (vertex colors)
│
└── Scenes/
    └── Main.unity             # The only scene
```

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                         GameBootstrap                           │
│              (creates sphere, places player, starts game)       │
└──────────┬────────────────────┬─────────────────────┬───────────┘
           │                    │                     │
           ▼                    ▼                     ▼
┌─────────────────┐  ┌──────────────────┐  ┌──────────────────┐
│  ChunkManager   │  │  GravityField    │  │  DebugUI         │
│                 │  │                  │  │                  │
│ - Block get/set │  │ - Owns octree   │  │ - Sliders        │
│ - Chunk lookup  │◄─┤ - Gravity query │  │ - Toggles        │
│ - OnBlockChanged│  │ - Incremental   │  │ - Reset          │
│   event         │──┤   update        │  │                  │
└────────┬────────┘  └────────▲────────┘  └──────────────────┘
         │                    │
         ▼                    │
┌─────────────────┐           │
│ ChunkRenderer   │           │
│                 │           │
│ - ChunkMesher   │           │
│ - MeshFilter    │           │
│ - Remesh on     │           │
│   block change  │           │
└─────────────────┘           │
                              │
┌─────────────────────────────┴───────────────────────────────┐
│                      Player                                  │
│                                                              │
│  PlayerController ←── GravityField.GetGravityAt()           │
│  PlayerCollision  ←── ChunkManager (nearby block lookup)    │
│  PlayerCamera     ←── PlayerController (orientation)        │
│  BlockMiner       ──► ChunkManager.SetBlock(Air)            │
└──────────────────────────────────────────────────────────────┘
```

**Data flow**: `ChunkManager` is the single source of truth for block data. When a block changes, it fires `OnBlockChanged`, which triggers both `ChunkRenderer` (remesh) and `GravityField` (octree update). The player queries `GravityField` for gravity direction and `ChunkManager` for collision/mining.

---

## System Deep Dives

### 1. BCC Lattice (Core)

Truncated octahedrons tessellate 3D space on a **body-centered cubic lattice** — two interleaved cubic grids:

- **Grid A** (parity 0): positions at integer coordinates `(x, y, z)`
- **Grid B** (parity 1): positions offset by `(0.5, 0.5, 0.5)`

Viewed as horizontal layers, alternating y-layers are offset by half in x and z.

Each block has **14 neighbors**:
- **6 same-grid** (across square faces): `(±1,0,0)`, `(0,±1,0)`, `(0,0,±1)` — same parity
- **8 cross-grid** (across hex faces): all combos of `(0 or -1, 0 or -1, 0 or -1)` — flip parity

A block address is `(byte parity, int x, int y, int z)`. World position:
```
Grid A: worldPos = (x, y, z) * blockSize
Grid B: worldPos = (x + 0.5, y + 0.5, z + 0.5) * blockSize
```

**Why this addressing?** Two flat arrays per chunk. No wasted memory (unlike a doubled-resolution checkerboard). O(1) lookup. Neighbor math is integer addition.

### 2. Chunks

Each chunk covers an **8×8×8 region per grid** (so 2 × 512 = 1,024 block slots per chunk, ~1KB of block data). Chunks are identified by their grid-space origin `(chunkX, chunkY, chunkZ)` and stored in a dictionary.

When a block near a chunk boundary is queried for neighbors, `ChunkManager` handles the cross-chunk lookup transparently.

**Why 8?** Small enough to remesh quickly (critical for mining responsiveness). Large enough that the chunk overhead is manageable. With radius-12 sphere, we need ~50-80 chunks.

### 3. Meshing

For each chunk, `ChunkMesher` produces a single Unity `Mesh`:

1. Iterate every non-air block in both grids
2. For each of its 14 faces, check the neighbor — if air (or out of bounds), emit that face
3. Transform the face vertices to world position
4. Assign vertex color based on `BlockType`
5. Combine into one mesh (vertices, triangles, colors)

**Geometry cost**: A truncated octahedron has 8 hexagonal faces (4 triangles each) + 6 square faces (2 triangles each) = **44 triangles** when fully exposed. That's 3.7× more than a cube. With face culling (only air-adjacent faces), interior blocks cost nothing and surface blocks typically expose 3-5 faces.

The mesher uses Unity's **Job System** (which compiles to web workers in WebGL) for background mesh generation.

### 4. Shader

`FlatBlock.shader` — the simplest possible shader:
- **Unlit**: no lighting calculations
- **Vertex colors**: block type determines color, baked into mesh vertices
- **Self-emissive appearance**: just outputs the color directly
- **WebGL compatible**: no features beyond WebGL 2.0

One shared material for all chunks. Color variety comes from vertex colors, not separate materials.

### 5. Gravity (Barnes-Hut)

Every non-air block has mass. The gravity on the player is the sum of pull from every block — but computing that directly is O(n) per frame for ~7,000 blocks. The **Barnes-Hut algorithm** makes it O(log n):

1. Build an **octree** over all block positions
2. Each node stores: **center of mass**, **total mass**, **bounds**
3. To query gravity at a point, walk the tree:
   - If a node is "far enough" (size/distance < θ), use its aggregate mass
   - Otherwise, recurse into children
4. θ (theta) controls accuracy vs speed: 0 = exact, 0.5 = good balance, 1.0 = fast/approximate

**Incremental updates**: When a block is mined, the octree is updated locally — remove the block's mass and propagate up. No full rebuild needed.

**Why not just "gravity toward center"?** Because mining changes the mass distribution. Dig a tunnel and gravity shifts. Build an extension and the pull changes. The gravity field *is* the shape of the world — that's the core game feel.

### 6. Player Controller

A custom first-person controller with no Rigidbody (we don't want PhysX fighting our gravity model):

- **Each frame**: query `GravityField.GetGravityAt(playerPosition)` for the current "down" vector
- **Orientation**: slerp the player's up vector to align with -gravity (smooth reorientation as you walk around the sphere)
- **Movement**: WASD mapped to the surface plane (perpendicular to gravity), at configurable speed
- **Jump**: impulse in the -gravity direction
- **Velocity**: custom integration — apply gravity + movement + jump, then resolve collisions

### 7. Collision

Custom capsule-vs-world collision (no Unity physics colliders on blocks):

1. Find blocks near the player (query ChunkManager by position, check ~27 nearby lattice positions)
2. For each non-air block: test capsule vs **circumscribed sphere** (radius ≈ 0.559 × blockSize)
3. If penetrating: push the capsule out along the penetration normal
4. **Ground detection**: if any push is roughly opposite to gravity, the player is grounded (can jump)

**Why spheres for collision?** The circumsphere of a truncated octahedron overlaps with neighbors by ~29%, creating a gently undulating surface — smooth enough to walk on without catching on edges. The valleys between block-spheres are ~6% of a block radius deep, giving subtle surface texture without gameplay impact.

### 8. Mining

- **Raycast**: step along a ray from the camera in small increments. At each step, find the nearest BCC lattice block (check both grids). If non-air, that's the hit.
- **On click**: set the target block to `Air` via `ChunkManager`
- `ChunkManager` fires `OnBlockChanged` → mesh regenerates + gravity octree updates
- **Visual feedback**: tint or wireframe highlight on the targeted block
- **Range**: 5 world units (configurable)

### 9. Debug Tools

An IMGUI overlay (toggled with backtick/tilde) with:

| Control | What it does |
|---|---|
| Reset Scene | Regenerate sphere, reset player to surface |
| Gravity Constant | Slider — tune pull strength |
| Theta | Slider — Barnes-Hut accuracy vs speed |
| Sphere Radius | Slider — regenerate with different size |
| Show Gravity Vectors | Toggle — draw arrows at sample points |
| Show Chunk Bounds | Toggle — wireframe boxes around chunks |
| Position / Gravity | Readout — player world pos + gravity vector |
| FPS | Counter |

---

## Controls

| Input | Action |
|---|---|
| WASD | Move on surface |
| Mouse | Look around |
| Space | Jump |
| Left Click | Mine (destroy) block |
| ` (backtick) | Toggle debug UI |
| Escape | Release mouse cursor |

---

## Key Constants

| Constant | Default | Notes |
|---|---|---|
| `blockSize` | 1.0 | World units per lattice cell |
| `chunkSize` | 8 | Blocks per grid per axis per chunk |
| `sphereRadius` | 12 | Initial test sphere radius in blocks |
| `gravityConstant` | 9.81 | Strength of gravitational pull |
| `barnesHutTheta` | 0.5 | 0 = exact, 1 = fast approximate |
| `collisionRadius` | 0.559 | Circumsphere of truncated octahedron |
| `miningRange` | 5.0 | World units |
| `playerHeight` | 1.8 | Capsule height |
| `playerRadius` | 0.3 | Capsule radius |
| `moveSpeed` | 5.0 | Units/second |
| `jumpForce` | 5.0 | Impulse magnitude |
| `cameraSmoothTime` | 0.15 | Gravity reorientation slerp speed |

All constants are exposed in the debug UI for runtime tuning.

---

## Building for WebGL

1. File → Build Profiles → WebGL (should already be active)
2. Player Settings:
   - Compression Format: **Gzip** (best browser support)
   - Memory Size: leave default (Unity 6 manages this automatically)
   - Enable **Exceptions**: "Explicitly Thrown" only (better performance)
3. Build and Run

**WebGL constraints baked into the design**:
- No compute shaders → mesh generation on CPU via Job System (compiles to web workers)
- ~2GB memory ceiling → chunk data is compact byte arrays
- No dynamic batching relied upon → one mesh per chunk, one shared material
- Simple unlit shader → minimal fragment cost

---

## Known Limitations (Scaffold)

- **No block placement** — mining only for now
- **No save/load** — sphere regenerates each play
- **No LOD** — all chunks render at full detail (fine for radius 12, not for larger worlds)
- **No sound**
- **Instant mining** — no break animation or progress
- **Single block sphere** — no terrain variety, biomes, or structures

---

## Next Steps (Post-Scaffold)

- Block placement (right click)
- Larger worlds with chunk streaming and LOD
- Terrain generation (layers: grass → dirt → stone, with ore veins)
- CRT post-processing effect + resolution downscaling
- Save/load (serialize chunk data)
- Inventory / block selection
- Sound design
