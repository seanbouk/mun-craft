using MunCraft.Core;
using MunCraft.Gravity;
using MunCraft.Player;
using UnityEngine;

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

        [Header("Debug Visualization")]
        public bool ShowGravityVectors;
        public bool ShowChunkBounds;
        public int GravityVectorGridSize = 5;
        public float GravityVectorScale = 0.5f;

        bool _showUI = true;
        float _sphereRadius = 12;
        float _fps;
        float _fpsTimer;
        int _fpsFrames;

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.BackQuote))
                _showUI = !_showUI;

            // FPS counter
            _fpsFrames++;
            _fpsTimer += Time.unscaledDeltaTime;
            if (_fpsTimer >= 0.5f)
            {
                _fps = _fpsFrames / _fpsTimer;
                _fpsFrames = 0;
                _fpsTimer = 0;
            }
        }

        void OnGUI()
        {
            // Always show FPS in corner
            GUI.Label(new Rect(10, 10, 200, 20), $"FPS: {_fps:F0}");

            if (!_showUI) return;

            GUILayout.BeginArea(new Rect(10, 40, 320, 500));
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

                // Collision debug
                var col = Player.GetComponent<MunCraft.Player.PlayerCollision>();
                if (col != null)
                {
                    GUILayout.Label($"Blocks checked: {col.LastBlocksChecked}");
                    GUILayout.Label($"Collisions: {col.LastCollisionsFound}");
                    GUILayout.Label($"Deepest pen: {col.LastDeepestPenetration:F3}");
                    GUILayout.Label($"Push dir: ({col.LastPushDir.x:F2}, {col.LastPushDir.y:F2}, {col.LastPushDir.z:F2})");
                    col.ShowDebug = GUILayout.Toggle(col.ShowDebug, "Show Collision Debug");
                }
            }

            GUILayout.Space(10);

            // Debug visualization toggles
            ShowGravityVectors = GUILayout.Toggle(ShowGravityVectors, "Show Gravity Vectors");
            ShowChunkBounds = GUILayout.Toggle(ShowChunkBounds, "Show Chunk Bounds");

            GUILayout.EndVertical();
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
