using MunCraft.Core;
using MunCraft.Gravity;
using MunCraft.Meshing;
using MunCraft.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using Profiler = UnityEngine.Profiling.Profiler;

namespace MunCraft.Debug
{
    /// <summary>
    /// IMGUI debug overlay with sliders, toggles, and readouts.
    /// Toggle with backtick key.
    /// </summary>
    public class DebugUI : MonoBehaviour
    {
        [Header("References")]
        public GameBootstrap Bootstrap;
        public GravityField GravityFieldRef;
        public PlayerController Player;
        public ChunkStreamingManager StreamingManager;

        [Header("Debug Visualization")]
        public bool ShowGravityVectors;
        public bool ShowChunkBounds;
        public int GravityVectorGridSize = 5;
        public float GravityVectorScale = 0.5f;

        bool _showUI = false;
        Vector2 _scrollPos;
        float _sphereRadius = 12;
        float _fps;
        float _fpsTimer;
        int _fpsFrames;

        // Memory stats — refreshed every 0.5s alongside FPS
        int _memTotalVerts;
        int _memTotalTris;
        int _memChunksRendered;
        float _memEstMB;
        float _memManagedMB;
        float _memNativeMB;

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.backquoteKey.wasPressedThisFrame)
                _showUI = !_showUI;

            // FPS + memory stats (shared 0.5s throttle)
            _fpsFrames++;
            _fpsTimer += Time.unscaledDeltaTime;
            if (_fpsTimer >= 0.5f)
            {
                _fps = _fpsFrames / _fpsTimer;
                _fpsFrames = 0;
                _fpsTimer = 0;

                RefreshMemoryStats();
            }
        }

        void RefreshMemoryStats()
        {
            // Mesh stats from all active renderers
            int totalVerts = 0, totalTris = 0;
            foreach (var cr in ChunkRendererRegistry.All)
            {
                totalVerts += cr.VertexCount;
                totalTris += cr.TriangleCount;
            }
            _memTotalVerts = totalVerts;
            _memTotalTris = totalTris;
            _memChunksRendered = ChunkRendererRegistry.Count;

            // Estimate: blocks + meshes + gravity
            int chunkCount = Bootstrap != null && Bootstrap.ChunkManager != null
                ? Bootstrap.ChunkManager.Chunks.Count : 0;
            int solidBlocks = GravityFieldRef != null ? GravityFieldRef.BlockCount : 0;

            long blockBytes = (long)chunkCount * 1024;
            long meshBytes = (long)totalVerts * 40 + (long)totalTris * 3 * 4;
            meshBytes *= 2; // base + working color buffers
            long gravityBytes = (long)solidBlocks * 60;

            _memEstMB = (blockBytes + meshBytes + gravityBytes) / (1024f * 1024f);
            _memManagedMB = System.GC.GetTotalMemory(false) / (1024f * 1024f);

            long native = Profiler.GetTotalAllocatedMemoryLong();
            _memNativeMB = native > 0 ? native / (1024f * 1024f) : -1f;
        }

        void OnGUI()
        {
            if (GameState.CurrentFlow != FlowState.Playing) return;

            // Always show FPS in corner
            GUI.Label(new Rect(10, 10, 200, 20), $"FPS: {_fps:F0}");

            if (!_showUI) return;

            float panelHeight = Screen.height - 60;
            GUILayout.BeginArea(new Rect(10, 40, 340, panelHeight));
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(panelHeight - 10));
            GUILayout.BeginVertical("box");

            GUILayout.Label("<b>Mun Craft Debug</b>");
            GUILayout.Space(5);

            // Reset
            if (GUILayout.Button("Reset Scene"))
            {
                if (Bootstrap != null)
                    Bootstrap.ResetScene();
            }

            GUILayout.Space(10);

            // Gravity
            if (GravityFieldRef != null)
            {
                GUILayout.Label($"Gravity Constant: {GravityFieldRef.GravityConstant:F3}");
                GravityFieldRef.GravityConstant = GUILayout.HorizontalSlider(
                    GravityFieldRef.GravityConstant, 0.01f, 2f);

                GUILayout.Label($"Barnes-Hut Theta: {GravityFieldRef.Theta:F2}");
                GravityFieldRef.Theta = GUILayout.HorizontalSlider(
                    GravityFieldRef.Theta, 0f, 2f);

                GUILayout.Label($"Max Gravity: {GravityFieldRef.MaxGravity:F1}");
                GravityFieldRef.MaxGravity = GUILayout.HorizontalSlider(
                    GravityFieldRef.MaxGravity, 5f, 100f);

                GUILayout.Label($"Block Count: {GravityFieldRef.BlockCount}");
            }

            GUILayout.Space(10);

            // Sphere generation
            GUILayout.Label($"Sphere Radius: {_sphereRadius:F0}");
            _sphereRadius = GUILayout.HorizontalSlider(_sphereRadius, 4, 24);
            if (GUILayout.Button("Regenerate Sphere"))
            {
                if (Bootstrap != null)
                    Bootstrap.RegenerateSphere(Mathf.RoundToInt(_sphereRadius));
            }

            if (StreamingManager != null)
            {
                GUILayout.Label($"Render Distance: {StreamingManager.RenderDistance:F0}");
                StreamingManager.RenderDistance = GUILayout.HorizontalSlider(
                    StreamingManager.RenderDistance, 10f, 100f);
            }

            GUILayout.Space(10);

            // Player info
            if (Player != null)
            {
                Vector3 pos = Player.transform.position;
                GUILayout.Label($"Position: ({pos.x:F1}, {pos.y:F1}, {pos.z:F1})");
                GUILayout.Label($"Dist from origin: {pos.magnitude:F2}");
                Vector3 grav = Player.GravityDirection;
                GUILayout.Label($"Gravity: ({grav.x:F2}, {grav.y:F2}, {grav.z:F2})");
                GUILayout.Label($"Gravity Mag: {grav.magnitude:F2}");
                GUILayout.Label($"Grounded: {Player.IsGrounded}");
                GUILayout.Label($"Speed: {Player.Velocity.magnitude:F1}");

                // Input debug — visible on screen so you don't need the Console
                GUILayout.Space(5);
                GUILayout.Label("<b>--- Input ---</b>");
                GUILayout.Label($"H={Player._lastH:F2} V={Player._lastV:F2}");
                GUILayout.Label($"Mouse: ({Player._lastMouseX:F2}, {Player._lastMouseY:F2})");
                GUILayout.Label($"AnyKey={Player._lastAnyKey} Focused={Player._lastFocused}");

                // Collision debug
                GUILayout.Space(5);
                GUILayout.Label("<b>--- Collision ---</b>");
                var col = Player.GetComponent<MunCraft.Player.PlayerCollision>();
                if (col != null)
                {
                    GUILayout.Label($"Checked: {col.LastBlocksChecked} Hits: {col.LastCollisionsFound}");
                    GUILayout.Label($"Deepest: {col.LastDeepestPenetration:F3}");
                    col.ShowDebug = GUILayout.Toggle(col.ShowDebug, "Show Collision Debug");
                }
            }

            // Memory section
            GUILayout.Space(5);
            GUILayout.Label("<b>--- Memory ---</b>");
            int totalChunks = Bootstrap != null && Bootstrap.ChunkManager != null
                ? Bootstrap.ChunkManager.Chunks.Count : 0;
            GUILayout.Label($"Chunks: {_memChunksRendered} rendered / {totalChunks} total");
            GUILayout.Label($"Mesh: {_memTotalVerts:N0} verts, {_memTotalTris:N0} tris");
            GUILayout.Label($"Est. game RAM: {_memEstMB:F1} MB");
            GUILayout.Label($"Managed heap: {_memManagedMB:F1} MB");
            if (_memNativeMB >= 0)
                GUILayout.Label($"Native alloc: {_memNativeMB:F1} MB");

            GUILayout.Space(10);

            // Debug visualization toggles
            ShowGravityVectors = GUILayout.Toggle(ShowGravityVectors, "Show Gravity Vectors");
            ShowChunkBounds = GUILayout.Toggle(ShowChunkBounds, "Show Chunk Bounds");

            GUILayout.EndVertical();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;

            if (ShowGravityVectors && GravityFieldRef != null)
                DrawGravityVectors();

            if (ShowChunkBounds && Bootstrap != null && Bootstrap.ChunkManager != null)
                DrawChunkBounds();
        }

        void DrawGravityVectors()
        {
            int range = GravityVectorGridSize;
            float spacing = 2f;

            // Draw gravity vectors in a grid around the player
            Vector3 center = Player != null ? Player.transform.position : Vector3.zero;

            Gizmos.color = Color.cyan;
            for (int z = -range; z <= range; z++)
            for (int y = -range; y <= range; y++)
            for (int x = -range; x <= range; x++)
            {
                Vector3 samplePos = center + new Vector3(x, y, z) * spacing;
                Vector3 gravity = GravityFieldRef.GetGravityAt(samplePos);

                if (gravity.sqrMagnitude > 0.01f)
                {
                    Gizmos.DrawLine(samplePos, samplePos + gravity.normalized * GravityVectorScale);
                }
            }
        }

        void DrawChunkBounds()
        {
            Gizmos.color = new Color(1, 1, 0, 0.3f);
            float chunkWorldSize = Chunk.Size * Bootstrap.ChunkManager.BlockSize;

            foreach (var kvp in Bootstrap.ChunkManager.Chunks)
            {
                Vector3 chunkOrigin = new Vector3(
                    kvp.Key.x * chunkWorldSize,
                    kvp.Key.y * chunkWorldSize,
                    kvp.Key.z * chunkWorldSize
                );
                Vector3 chunkCenter = chunkOrigin + Vector3.one * chunkWorldSize * 0.5f;
                Gizmos.DrawWireCube(chunkCenter, Vector3.one * chunkWorldSize);
            }
        }
    }
}
