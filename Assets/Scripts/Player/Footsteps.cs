using MunCraft.Core;
using UnityEngine;

namespace MunCraft.Player
{
    /// <summary>
    /// Plays random footstep clips while walking (alternating L/R pan) and a
    /// louder one-two on jump. Clips have long echo tails — overlap is fine,
    /// so we round-robin through a pool of AudioSources.
    /// </summary>
    public class Footsteps : MonoBehaviour
    {
        public float StepInterval = 0.42f;
        public float MinSpeed = 1.0f;
        public float Pan = 0.5f;
        public float Pitch = 0.92f;
        public float StepVolume = 0.7f;
        public float JumpVolume = 1.0f;
        public float JumpStepGap = 0.08f;
        public float LandStepGap = 0.04f;

        const int PoolSize = 10;

        AudioClip[] _clips;
        PlayerController _controller;
        AudioSource[] _pool;
        int _poolHead;
        float _stepTimer;
        bool _leftFoot;

        // Pending second jump-step
        float _pendingJumpStepIn;
        float _pendingJumpStepPan;
        float _pendingJumpStepVolume;
        AudioClip _pendingJumpClip;

        void Awake()
        {
            _clips = Resources.LoadAll<AudioClip>("Game/Steps");
            _controller = GetComponent<PlayerController>();
            _stepTimer = StepInterval * 0.5f;

            _pool = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                _pool[i] = src;
            }
        }

        void OnEnable()
        {
            if (_controller != null)
            {
                _controller.OnJumped += HandleJumped;
                _controller.OnLanded += HandleLanded;
            }
        }

        void OnDisable()
        {
            if (_controller != null)
            {
                _controller.OnJumped -= HandleJumped;
                _controller.OnLanded -= HandleLanded;
            }
        }

        void Update()
        {
            if (GameState.MenuOpen) return;
            float dt = Time.deltaTime;

            if (_pendingJumpStepIn > 0f)
            {
                _pendingJumpStepIn -= dt;
                if (_pendingJumpStepIn <= 0f && _pendingJumpClip != null)
                {
                    PlayClip(_pendingJumpClip, _pendingJumpStepPan, _pendingJumpStepVolume);
                    _pendingJumpClip = null;
                }
            }

            if (_clips == null || _clips.Length == 0 || _controller == null) return;

            Vector3 v = _controller.Velocity;
            Vector3 up = _controller.CurrentUp;
            Vector3 lateral = v - Vector3.Dot(v, up) * up;
            float speed = lateral.magnitude;

            bool walking = _controller.IsGrounded && speed > MinSpeed;
            if (walking)
            {
                _stepTimer -= dt;
                if (_stepTimer <= 0f)
                {
                    PlayClip(RandomClip(), _leftFoot ? -Pan : Pan, StepVolume);
                    _leftFoot = !_leftFoot;
                    _stepTimer = StepInterval;
                }
            }
            else
            {
                _stepTimer = StepInterval * 0.5f;
            }
        }

        void HandleJumped()
        {
            if (_clips == null || _clips.Length == 0) return;
            PlayClip(RandomClip(), -Pan, JumpVolume);
            _pendingJumpClip = RandomClip();
            _pendingJumpStepPan = Pan;
            _pendingJumpStepVolume = JumpVolume;
            _pendingJumpStepIn = JumpStepGap;
            _leftFoot = true;
            _stepTimer = StepInterval;
        }

        void HandleLanded()
        {
            if (_clips == null || _clips.Length == 0) return;
            PlayClip(RandomClip(), -Pan, JumpVolume);
            _pendingJumpClip = RandomClip();
            _pendingJumpStepPan = Pan;
            _pendingJumpStepVolume = JumpVolume;
            _pendingJumpStepIn = LandStepGap;
            _leftFoot = true;
            _stepTimer = StepInterval;
        }

        AudioClip RandomClip() => _clips[Random.Range(0, _clips.Length)];

        void PlayClip(AudioClip clip, float pan, float volume)
        {
            if (clip == null) return;
            var src = _pool[_poolHead];
            _poolHead = (_poolHead + 1) % _pool.Length;
            src.clip = clip;
            src.pitch = Pitch;
            src.panStereo = Mathf.Clamp(pan, -1f, 1f);
            src.volume = volume;
            src.Play();
        }
    }
}
