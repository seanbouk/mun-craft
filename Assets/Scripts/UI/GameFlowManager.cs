using MunCraft.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MunCraft.UI
{
    /// <summary>
    /// Single-scene state machine: Title → LevelSelect → Playing.
    /// Draws the title screen and level select UI. During Playing,
    /// draws nothing — existing systems handle gameplay.
    /// </summary>
    public class GameFlowManager : MonoBehaviour
    {
        // Blueprint palette
        static readonly Color Bg = new Color(0f, 0f, 0f, 1f);
        static readonly Color Ink = new Color(0.92f, 0.96f, 1f, 1f);
        static readonly Color InkDim = new Color(0.65f, 0.75f, 0.85f, 1f);
        static readonly Color InkFaint = new Color(0.45f, 0.55f, 0.65f, 1f);
        static readonly Color Accent = new Color(1f, 0.81f, 0.29f, 1f);
        static readonly Color BtnBg = new Color(0.06f, 0.16f, 0.24f, 1f);
        static readonly Color BtnHover = new Color(0.08f, 0.22f, 0.32f, 1f);
        static readonly Color BtnDisabled = new Color(0.04f, 0.10f, 0.16f, 1f);

        Texture2D _pixel;
        Texture2D _bgGradient;
        Texture2D _titleImage;
        Material _titleFadeMat;
        float _titleStartTime;
        const float TitleFadeDuration = 10f;
        const float TitleMaxDelay = 40f;
        Camera _uiCamera; // fallback camera for title/level-select screens
        GUIStyle _titleStyle;
        GUIStyle _subtitleStyle;
        GUIStyle _promptStyle;
        GUIStyle _btnStyle;
        GUIStyle _backStyle;
        GUIStyle _mapLabelStyle;
        GUIStyle _loadingStyle;
        float _promptPulse;
        float _inputCooldown; // suppress input briefly after state transitions
        int _loadingMapId = -1; // -1 = not loading, >=0 = loading that map
        int _loadingFrames;     // count down frames before actually loading

        AudioSource _titleAudio;
        AudioClip _titleClip;
        AudioSource _gameAudio;
        AudioClip _gameClip;
        FlowState _lastFlow;
        float _titleAudioFadeOutRemaining;
        float _gameAudioFadeOutRemaining;
        const float AudioFadeOutDuration = 0.15f;
        const float TitleMusicVolume = 1f;
        const float GameMusicVolume = 0.5f;

        struct Star { public float X, Y, Brightness; }
        Star[] _stars;
        int _starScreenW, _starScreenH;
        const int StarCount = 140;
        const float StarBaseSpeed = 28f;   // px/sec for a brightness-1 star
        const float StarMinBrightness = 0.25f;

        public static GameFlowManager Instance { get; private set; }

        void Awake()
        {
            Instance = this;
            // Single, persistent AudioListener: cameras come and go (Main Camera
            // gets destroyed when entering a map, PlayerCamera when leaving), so
            // pin the listener to this manager which lives through every state.
            foreach (var existing in FindObjectsByType<AudioListener>())
                Destroy(existing);
            gameObject.AddComponent<AudioListener>();
        }

        void Start()
        {
            _pixel = new Texture2D(1, 1);
            _pixel.SetPixel(0, 0, Color.white);
            _pixel.Apply();

            _titleImage = Resources.Load<Texture2D>("Title/title");

            var titleFadeShader = Shader.Find("MunCraft/TitleFade");
            if (titleFadeShader != null)
                _titleFadeMat = new Material(titleFadeShader) { hideFlags = HideFlags.HideAndDontSave };
            _titleStartTime = Time.unscaledTime;

            // Create a simple camera for title/level-select (destroyed when game loads)
            CreateUICamera();

            _titleClip = Resources.Load<AudioClip>("Title/title");
            _titleAudio = gameObject.AddComponent<AudioSource>();
            _titleAudio.clip = _titleClip;
            _titleAudio.loop = false;
            _titleAudio.playOnAwake = false;
            _titleAudio.spatialBlend = 0f;

            _gameClip = Resources.Load<AudioClip>("Game/music");
            _gameAudio = gameObject.AddComponent<AudioSource>();
            _gameAudio.clip = _gameClip;
            _gameAudio.loop = true;
            _gameAudio.playOnAwake = false;
            _gameAudio.spatialBlend = 0f;

            // Force the first Update to detect a transition into Title and Play.
            _lastFlow = FlowState.LevelSelect;
            GameState.CurrentFlow = FlowState.Title;
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_pixel != null) Destroy(_pixel);
            if (_bgGradient != null) Destroy(_bgGradient);
            if (_titleFadeMat != null) Destroy(_titleFadeMat);
            DestroyUICamera();
        }

        void CreateUICamera()
        {
            if (_uiCamera != null) return;
            var camObj = new GameObject("UICamera");
            _uiCamera = camObj.AddComponent<Camera>();
            _uiCamera.clearFlags = CameraClearFlags.SolidColor;
            _uiCamera.backgroundColor = Color.black;
            _uiCamera.cullingMask = 0; // render nothing — IMGUI draws on top
            _uiCamera.depth = -100;
        }

        void DestroyUICamera()
        {
            if (_uiCamera != null)
            {
                Destroy(_uiCamera.gameObject);
                _uiCamera = null;
            }
        }

        void Update()
        {
            _promptPulse += Time.unscaledDeltaTime * 2f;

            TrackTitleAudio();

            if (GameState.CurrentFlow != FlowState.Playing)
                UpdateStars(Time.unscaledDeltaTime);

            if (_inputCooldown > 0)
            {
                _inputCooldown -= Time.unscaledDeltaTime;
                return;
            }

            // Deferred map loading — wait a few frames so the "Loading..." text
            // actually reaches the screen before the synchronous generation blocks
            if (_loadingMapId >= 0)
            {
                _loadingFrames--;
                if (_loadingFrames <= 0)
                {
                    int id = _loadingMapId;
                    _loadingMapId = -1;
                    LaunchMapDeferred(id);
                }
                return;
            }

            if (GameState.CurrentFlow == FlowState.Title)
            {
                var kb = Keyboard.current;
                var mouse = Mouse.current;
                if ((kb != null && kb.anyKey.wasPressedThisFrame) ||
                    (mouse != null && mouse.leftButton.wasPressedThisFrame))
                {
                    GameState.CurrentFlow = FlowState.LevelSelect;
                    _inputCooldown = 0.2f;
                }
            }
        }

        void EnsureStyles()
        {
            if (_titleStyle != null) return;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 64, fontStyle = FontStyle.Normal,
                alignment = TextAnchor.LowerCenter,
            };
            _titleStyle.normal.textColor = InkDim;

            _subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 80, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperCenter,
            };
            _subtitleStyle.normal.textColor = Ink;

            _promptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14, alignment = TextAnchor.MiddleCenter,
            };
            _promptStyle.normal.textColor = InkDim;

            _btnStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            _btnStyle.normal.textColor = Ink;

            _backStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14, alignment = TextAnchor.MiddleLeft,
            };
            _backStyle.normal.textColor = InkDim;

            _mapLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12, alignment = TextAnchor.MiddleCenter,
            };
            _mapLabelStyle.normal.textColor = InkDim;

            _loadingStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            _loadingStyle.normal.textColor = Accent;
        }

        void OnGUI()
        {
            if (GameState.CurrentFlow == FlowState.Playing) return;

            EnsureStyles();

            // Full-screen background
            EnsureBgGradient();
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _bgGradient);
            DrawStars();

            if (GameState.CurrentFlow == FlowState.Title)
                DrawTitle();
            else if (GameState.CurrentFlow == FlowState.LevelSelect)
                DrawLevelSelect();
        }

        void TrackTitleAudio()
        {
            DriveFadeOut(_titleAudio, ref _titleAudioFadeOutRemaining, TitleMusicVolume);
            DriveFadeOut(_gameAudio, ref _gameAudioFadeOutRemaining, GameMusicVolume);

            var current = GameState.CurrentFlow;
            if (_lastFlow == current) return;

            bool wasInGame = _lastFlow == FlowState.LevelSelect || _lastFlow == FlowState.Playing;
            bool nowInGame = current == FlowState.LevelSelect || current == FlowState.Playing;

            // Title music: play on entry to Title, fade on exit.
            if (current == FlowState.Title)
            {
                _titleStartTime = Time.unscaledTime;
                if (_titleAudio != null && _titleClip != null)
                {
                    _titleAudioFadeOutRemaining = 0f;
                    _titleAudio.volume = TitleMusicVolume;
                    _titleAudio.Stop();
                    _titleAudio.Play();
                }
            }
            else if (_lastFlow == FlowState.Title && _titleAudio != null && _titleAudio.isPlaying)
            {
                _titleAudioFadeOutRemaining = AudioFadeOutDuration;
            }

            // Game music: play continuously across LevelSelect <-> Playing,
            // start on first entry, fade on return to Title.
            if (nowInGame && !wasInGame && _gameAudio != null && _gameClip != null)
            {
                _gameAudioFadeOutRemaining = 0f;
                _gameAudio.volume = GameMusicVolume;
                if (!_gameAudio.isPlaying) _gameAudio.Play();
            }
            else if (wasInGame && !nowInGame && _gameAudio != null && _gameAudio.isPlaying)
            {
                _gameAudioFadeOutRemaining = AudioFadeOutDuration;
            }

            _lastFlow = current;
        }

        void DriveFadeOut(AudioSource src, ref float remaining, float baseVolume)
        {
            if (remaining <= 0f || src == null) return;
            remaining -= Time.unscaledDeltaTime;
            if (remaining <= 0f)
            {
                src.Stop();
                src.volume = baseVolume;
                remaining = 0f;
            }
            else
            {
                src.volume = baseVolume * (remaining / AudioFadeOutDuration);
            }
        }

        void EnsureBgGradient()
        {
            if (_bgGradient != null) return;
            const int H = 128;
            _bgGradient = new Texture2D(1, H, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var mustard = new Color(0.20f, 0.16f, 0.04f, 1f);
            // Texture y=0 → bottom of screen (mustard), y=H-1 → top (black).
            // Curve so half-way up is already 75% black: pow(t, 0.415).
            for (int i = 0; i < H; i++)
            {
                float t = i / (float)(H - 1); // 0 at bottom, 1 at top
                float blackAmount = Mathf.Pow(t, 0.415f);
                _bgGradient.SetPixel(0, i, Color.Lerp(mustard, Color.black, blackAmount));
            }
            _bgGradient.Apply();
        }

        void InitStars()
        {
            _stars = new Star[StarCount];
            _starScreenW = Screen.width;
            _starScreenH = Screen.height;
            for (int i = 0; i < _stars.Length; i++)
            {
                _stars[i].X = Random.Range(0f, _starScreenW);
                _stars[i].Y = Random.Range(0f, _starScreenH);
                _stars[i].Brightness = Random.Range(StarMinBrightness, 1f);
            }
        }

        void UpdateStars(float dt)
        {
            if (_stars == null
                || _starScreenW != Screen.width
                || _starScreenH != Screen.height)
            {
                InitStars();
                return;
            }

            for (int i = 0; i < _stars.Length; i++)
            {
                _stars[i].X -= _stars[i].Brightness * StarBaseSpeed * dt;
                if (_stars[i].X < 0f)
                {
                    _stars[i].X = _starScreenW;
                    _stars[i].Y = Random.Range(0f, _starScreenH);
                    _stars[i].Brightness = Random.Range(StarMinBrightness, 1f);
                }
            }
        }

        void DrawStars()
        {
            if (_stars == null) return;
            for (int i = 0; i < _stars.Length; i++)
            {
                var s = _stars[i];
                Solid(new Rect(s.X, s.Y, 1f, 1f), new Color(1f, 1f, 1f, s.Brightness));
            }
        }

        void DrawTitle()
        {
            float cy = Screen.height / 2f;

            if (_titleImage != null)
            {
                float targetH = 260f;
                float aspect = (float)_titleImage.width / _titleImage.height;
                float targetW = targetH * aspect;
                float maxW = Screen.width * 0.8f;
                if (targetW > maxW)
                {
                    targetW = maxW;
                    targetH = targetW / aspect;
                }
                var imgRect = new Rect((Screen.width - targetW) / 2f, cy - targetH / 2f, targetW, targetH);
                if (_titleFadeMat != null && Event.current.type == EventType.Repaint)
                {
                    _titleFadeMat.SetFloat("_Elapsed", Time.unscaledTime - _titleStartTime);
                    _titleFadeMat.SetFloat("_FadeDuration", TitleFadeDuration);
                    _titleFadeMat.SetFloat("_MaxDelay", TitleMaxDelay);
                    Graphics.DrawTexture(imgRect, _titleImage, _titleFadeMat);
                }
                else if (_titleFadeMat == null)
                {
                    GUI.DrawTexture(imgRect, _titleImage, ScaleMode.ScaleToFit);
                }
            }
            else
            {
            // mün
            var munRect = new Rect(0, cy - 90, Screen.width, 90);
            GUI.Label(munRect, "m\u00FCn", _titleStyle);

            // CRAFT
            var craftRect = new Rect(0, cy - 10, Screen.width, 100);
            GUI.Label(craftRect, "CRAFT", _subtitleStyle);
            }

            // (press any key) — pulsing opacity
            float alpha = 0.3f + 0.4f * (0.5f + 0.5f * Mathf.Sin(_promptPulse));
            _promptStyle.normal.textColor = new Color(InkDim.r, InkDim.g, InkDim.b, alpha);
            var promptRect = new Rect(0, cy + 180, Screen.width, 30);
            GUI.Label(promptRect, "(press any key)", _promptStyle);
        }

        void DrawLevelSelect()
        {
            bool isLoading = _loadingMapId >= 0;
            float cx = Screen.width / 2f;

            // Back button (top-left)
            if (!isLoading)
            {
                var backRect = new Rect(30, 30, 100, 36);
                bool backHover = backRect.Contains(Event.current.mousePosition);
                if (backHover)
                    Solid(backRect, new Color(1, 1, 1, 0.06f));
                _backStyle.normal.textColor = backHover ? Ink : InkDim;
                GUI.Label(backRect, "\u25C1 Back", _backStyle);
                if (Event.current.type == EventType.MouseDown
                    && Event.current.button == 0
                    && backRect.Contains(Event.current.mousePosition))
                {
                    GameState.CurrentFlow = FlowState.Title;
                    _inputCooldown = 0.3f;
                    Event.current.Use();
                }
            }

            // Title
            var headerRect = new Rect(0, 60, Screen.width, 40);
            _btnStyle.fontSize = 24;
            GUI.Label(headerRect, "SELECT MAP", _btnStyle);
            _btnStyle.fontSize = 18;

            // 2×2 grid of map buttons
            float btnW = 200, btnH = 140, gap = 24;
            float gridW = btnW * 2 + gap;
            float gridH = btnH * 2 + gap;
            float startX = cx - gridW / 2;
            float startY = (Screen.height - gridH) / 2 + 20;

            string[] mapNames = { "Round World", "Donut World", "Peanut World", "Worlds World" };

            for (int row = 0; row < 2; row++)
            {
                for (int col = 0; col < 2; col++)
                {
                    int idx = row * 2 + col;
                    float x = startX + col * (btnW + gap);
                    float y = startY + row * (btnH + gap);
                    var rect = new Rect(x, y, btnW, btnH);

                    bool isSelected = isLoading && _loadingMapId == idx;
                    bool isDisabled = isLoading && _loadingMapId != idx;
                    bool hover = !isLoading && rect.Contains(Event.current.mousePosition);

                    // Background
                    Color bg = isDisabled ? BtnDisabled : (hover ? BtnHover : BtnBg);
                    Solid(rect, bg);

                    // Border
                    float b = 2;
                    Color borderCol = isSelected ? Accent
                        : (isDisabled ? InkFaint
                        : (hover ? Accent : new Color(Ink.r, Ink.g, Ink.b, 0.25f)));
                    Solid(new Rect(x, y, btnW, b), borderCol);
                    Solid(new Rect(x, y + btnH - b, btnW, b), borderCol);
                    Solid(new Rect(x, y, b, btnH), borderCol);
                    Solid(new Rect(x + btnW - b, y, b, btnH), borderCol);

                    // Map name
                    var nameRect = new Rect(x, y + btnH / 2 - 16, btnW, 32);
                    _btnStyle.normal.textColor = isDisabled ? InkFaint
                        : (isSelected ? Accent : (hover ? Accent : Ink));
                    GUI.Label(nameRect, mapNames[idx], _btnStyle);

                    // Subtitle or loading text
                    var subRect = new Rect(x, y + btnH / 2 + 16, btnW, 20);
                    if (isSelected)
                    {
                        _loadingStyle.fontSize = 12;
                        GUI.Label(subRect, "Loading...", _loadingStyle);
                        _loadingStyle.fontSize = 16;
                    }
                    else
                    {
                        _mapLabelStyle.normal.textColor = isDisabled ? InkFaint : InkDim;
                        string[] subLabels = { "Classic sphere", "Walk the ring", "Twin lobes", "Jump between" };
                        GUI.Label(subRect, subLabels[idx], _mapLabelStyle);
                    }

                    // Click (only if not loading)
                    if (!isLoading
                        && Event.current.type == EventType.MouseDown
                        && Event.current.button == 0
                        && rect.Contains(Event.current.mousePosition))
                    {
                        _loadingMapId = idx;
                        _loadingFrames = 3; // give 3 frames for the "Loading..." to render
                        Event.current.Use();
                    }
                }
            }
        }

        void LaunchMapDeferred(int mapId)
        {
            DestroyUICamera();

            GameState.CurrentFlow = FlowState.Playing;
            var bootstrap = MunCraft.Debug.GameBootstrap.Instance;
            if (bootstrap != null)
                bootstrap.LoadMap(mapId);
        }

        public void GoToLevelSelect()
        {
            var bootstrap = MunCraft.Debug.GameBootstrap.Instance;
            if (bootstrap != null)
                bootstrap.UnloadMap();

            CreateUICamera();

            GameState.CurrentFlow = FlowState.LevelSelect;
            GameState.MenuOpen = false;
            _inputCooldown = 0.3f;
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void Solid(Rect r, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _pixel);
            GUI.color = prev;
        }
    }
}
