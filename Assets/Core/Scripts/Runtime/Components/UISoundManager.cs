using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UIElements;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Quản lý âm thanh UI tập trung cho toàn bộ game (Login, Lobby, In-Game HUD, Pause Menu).
    /// - Hỗ trợ DontDestroyOnLoad, tự động khởi tạo khi load scene.
    /// - Tự động định tuyến vào AudioMixerGroup "Menus".
    /// - Tự động liên kết sự kiện Hover và Click cho UI Toolkit Buttons.
    /// - Tự động sinh âm thanh Procedural Synth dự phòng nếu chưa gán file AudioClip.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class UISoundManager : MonoBehaviour
    {
        private static UISoundManager s_Instance;
        public static UISoundManager Instance
        {
            get
            {
                if (s_Instance == null)
                {
                    EnsureInstance();
                }
                return s_Instance;
            }
        }

        [Header("Audio Mixer")]
        [Tooltip("Audio Mixer chứa group 'Menus'. Nếu để trống sẽ tự động tìm DefaultMixer.")]
        [SerializeField] private AudioMixer audioMixer;
        [SerializeField] private string menuGroupName = "Menus";

        [Header("Audio Clips (Tùy chọn - Tự động dùng Synth nếu trống)")]
        [SerializeField] private AudioClip hoverClip;
        [SerializeField] private AudioClip clickClip;
        [SerializeField] private AudioClip confirmClip;
        [SerializeField] private AudioClip cancelClip;
        [SerializeField] private AudioClip swapRoleClip;
        [SerializeField] private AudioClip successClip;
        [SerializeField] private AudioClip errorClip;

        [Header("Volume Scales")]
        [Range(0f, 1f)] [SerializeField] private float masterUIVolume = 1.0f;
        [Range(0f, 1f)] [SerializeField] private float hoverVolume = 0.35f;
        [Range(0f, 1f)] [SerializeField] private float clickVolume = 0.7f;
        [Range(0f, 1f)] [SerializeField] private float confirmVolume = 0.85f;
        [Range(0f, 1f)] [SerializeField] private float cancelVolume = 0.65f;
        [Range(0f, 1f)] [SerializeField] private float swapRoleVolume = 0.75f;
        [Range(0f, 1f)] [SerializeField] private float successVolume = 0.85f;
        [Range(0f, 1f)] [SerializeField] private float errorVolume = 0.8f;

        private AudioSource m_AudioSource;

        // Fallback procedural synthesized clips
        private AudioClip m_SynthHover;
        private AudioClip m_SynthClick;
        private AudioClip m_SynthConfirm;
        private AudioClip m_SynthCancel;
        private AudioClip m_SynthSwapRole;
        private AudioClip m_SynthSuccess;
        private AudioClip m_SynthError;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInit()
        {
            EnsureInstance();
        }

        public static void EnsureInstance()
        {
            if (s_Instance != null) return;

            s_Instance = FindAnyObjectByType<UISoundManager>();
            if (s_Instance != null) return;

            var go = new GameObject("[UISoundManager]");
            s_Instance = go.AddComponent<UISoundManager>();
            if (Application.isPlaying) DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_Instance = this;
            if (Application.isPlaying) DontDestroyOnLoad(gameObject);

            InitAudioSource();
            GenerateProceduralFallbacks();
        }

        private void InitAudioSource()
        {
            m_AudioSource = GetComponent<AudioSource>();
            if (m_AudioSource == null)
            {
                m_AudioSource = gameObject.AddComponent<AudioSource>();
            }

            m_AudioSource.playOnAwake = false;
            m_AudioSource.spatialBlend = 0f; // 2D UI sound
            m_AudioSource.ignoreListenerPause = true; // Vẫn phát khi game pause

            // Định tuyến vào AudioMixer Menus nếu có
            if (audioMixer == null)
            {
                audioMixer = Resources.Load<AudioMixer>("DefaultMixer");
            }

            if (audioMixer != null)
            {
                var groups = audioMixer.FindMatchingGroups(menuGroupName);
                if (groups != null && groups.Length > 0)
                {
                    m_AudioSource.outputAudioMixerGroup = groups[0];
                }
            }
        }

        #region Procedural Audio Synthesizer (Zero-Asset Fallback)

        private void GenerateProceduralFallbacks()
        {
            if (hoverClip == null) m_SynthHover = CreateToneClip("Synth_Hover", 880f, 1100f, 0.035f, WaveType.Sine, 0.5f);
            if (clickClip == null) m_SynthClick = CreateClickClip("Synth_Click", 600f, 0.04f);
            if (confirmClip == null) m_SynthConfirm = CreateChordClip("Synth_Confirm", new float[] { 523.25f, 659.25f, 783.99f }, 0.12f);
            if (cancelClip == null) m_SynthCancel = CreateToneClip("Synth_Cancel", 440f, 260f, 0.09f, WaveType.Triangle, 0.6f);
            if (swapRoleClip == null) m_SynthSwapRole = CreateDoublePopClip("Synth_SwapRole", 480f, 720f, 0.08f);
            if (successClip == null) m_SynthSuccess = CreateArpeggioClip("Synth_Success", new float[] { 523.25f, 659.25f, 783.99f, 1046.50f }, 0.22f);
            if (errorClip == null) m_SynthError = CreateBuzzClip("Synth_Error", 160f, 0.14f);
        }

        private enum WaveType { Sine, Triangle, Square }

        private AudioClip CreateToneClip(string name, float startFreq, float endFreq, float duration, WaveType wave, float decayPower = 1f)
        {
            int sampleRate = 44100;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];
            float phase = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleCount;
                float currentFreq = Mathf.Lerp(startFreq, endFreq, t);
                phase += 2f * Mathf.PI * currentFreq / sampleRate;

                float sample = 0f;
                switch (wave)
                {
                    case WaveType.Sine:
                        sample = Mathf.Sin(phase);
                        break;
                    case WaveType.Triangle:
                        sample = Mathf.PingPong(phase / Mathf.PI, 1f) * 2f - 1f;
                        break;
                    case WaveType.Square:
                        sample = Mathf.Sin(phase) >= 0 ? 0.6f : -0.6f;
                        break;
                }

                // Envelope: Fast attack, exponential decay
                float attack = Mathf.Clamp01((float)i / (sampleRate * 0.005f));
                float decay = Mathf.Pow(1f - t, decayPower);
                samples[i] = sample * attack * decay;
            }

            var clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateClickClip(string name, float baseFreq, float duration)
        {
            int sampleRate = 44100;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];
            float phase = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleCount;
                float freq = baseFreq * (1f - t * 0.6f);
                phase += 2f * Mathf.PI * freq / sampleRate;
                float sine = Mathf.Sin(phase);
                float noise = (UnityEngine.Random.value * 2f - 1f) * 0.15f;
                float envelope = Mathf.Exp(-t * 28f);
                samples[i] = (sine + noise) * envelope;
            }

            var clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateChordClip(string name, float[] frequencies, float duration)
        {
            int sampleRate = 44100;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleCount;
                float envelope = Mathf.Sin(t * Mathf.PI) * Mathf.Pow(1f - t, 0.4f);
                float val = 0f;
                foreach (var f in frequencies)
                {
                    val += Mathf.Sin(2f * Mathf.PI * f * i / sampleRate);
                }
                samples[i] = (val / frequencies.Length) * envelope;
            }

            var clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateDoublePopClip(string name, float f1, float f2, float duration)
        {
            int sampleRate = 44100;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];
            int midPoint = sampleCount / 2;

            for (int i = 0; i < sampleCount; i++)
            {
                if (i < midPoint)
                {
                    float t = (float)i / midPoint;
                    samples[i] = Mathf.Sin(2f * Mathf.PI * f1 * i / sampleRate) * Mathf.Exp(-t * 18f);
                }
                else
                {
                    int j = i - midPoint;
                    float t = (float)j / (sampleCount - midPoint);
                    samples[i] = Mathf.Sin(2f * Mathf.PI * f2 * j / sampleRate) * Mathf.Exp(-t * 16f);
                }
            }

            var clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateArpeggioClip(string name, float[] notes, float duration)
        {
            int sampleRate = 44100;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];
            int noteLength = sampleCount / notes.Length;

            for (int n = 0; n < notes.Length; n++)
            {
                float freq = notes[n];
                int start = n * noteLength;
                int end = (n == notes.Length - 1) ? sampleCount : (n + 1) * noteLength;

                for (int i = start; i < end; i++)
                {
                    int subIndex = i - start;
                    float t = (float)subIndex / (end - start);
                    float env = Mathf.Sin(t * Mathf.PI * 0.9f) * Mathf.Pow(1f - t, 0.5f);
                    samples[i] = Mathf.Sin(2f * Mathf.PI * freq * i / sampleRate) * env * 0.9f;
                }
            }

            var clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateBuzzClip(string name, float freq, float duration)
        {
            int sampleRate = 44100;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleCount;
                float phase = 2f * Mathf.PI * freq * i / sampleRate;
                float square = Mathf.Sign(Mathf.Sin(phase));
                float noise = (UnityEngine.Random.value * 2f - 1f) * 0.2f;
                float env = Mathf.Pow(1f - t, 0.7f);
                samples[i] = (square * 0.5f + noise) * env;
            }

            var clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        #endregion

        #region Public Playback API

        public static void PlayHover()
        {
            var mgr = Instance;
            if (mgr == null) return;
            var clip = mgr.hoverClip != null ? mgr.hoverClip : mgr.m_SynthHover;
            mgr.PlayInternal(clip, mgr.hoverVolume);
        }

        public static void PlayClick()
        {
            var mgr = Instance;
            if (mgr == null) return;
            var clip = mgr.clickClip != null ? mgr.clickClip : mgr.m_SynthClick;
            mgr.PlayInternal(clip, mgr.clickVolume);
        }

        public static void PlayConfirm()
        {
            var mgr = Instance;
            if (mgr == null) return;
            var clip = mgr.confirmClip != null ? mgr.confirmClip : mgr.m_SynthConfirm;
            mgr.PlayInternal(clip, mgr.confirmVolume);
        }

        public static void PlayCancel()
        {
            var mgr = Instance;
            if (mgr == null) return;
            var clip = mgr.cancelClip != null ? mgr.cancelClip : mgr.m_SynthCancel;
            mgr.PlayInternal(clip, mgr.cancelVolume);
        }

        public static void PlaySwapRole()
        {
            var mgr = Instance;
            if (mgr == null) return;
            var clip = mgr.swapRoleClip != null ? mgr.swapRoleClip : mgr.m_SynthSwapRole;
            mgr.PlayInternal(clip, mgr.swapRoleVolume);
        }

        public static void PlaySuccess()
        {
            var mgr = Instance;
            if (mgr == null) return;
            var clip = mgr.successClip != null ? mgr.successClip : mgr.m_SynthSuccess;
            mgr.PlayInternal(clip, mgr.successVolume);
        }

        public static void PlayError()
        {
            var mgr = Instance;
            if (mgr == null) return;
            var clip = mgr.errorClip != null ? mgr.errorClip : mgr.m_SynthError;
            mgr.PlayInternal(clip, mgr.errorVolume);
        }

        private void PlayInternal(AudioClip clip, float volume)
        {
            if (clip == null || m_AudioSource == null) return;
            m_AudioSource.PlayOneShot(clip, volume * masterUIVolume);
        }

        #endregion

        #region UI Toolkit Auto-Binding

        private const string BoundClassTag = "hmh-sound-bound";

        /// <summary>
        /// Tự động tìm tất cả Button trong cây VisualElement và gán âm thanh Hover / Click tương ứng.
        /// </summary>
        public static void BindButtons(VisualElement root)
        {
            if (root == null) return;

            var buttons = root.Query<Button>().ToList();
            foreach (var btn in buttons)
            {
                if (btn.ClassListContains(BoundClassTag)) continue;
                btn.AddToClassList(BoundClassTag);

                // Hover sound
                btn.RegisterCallback<PointerEnterEvent>(_ =>
                {
                    if (btn.enabledSelf) PlayHover();
                });

                // Click sound routing based on button semantics
                btn.RegisterCallback<ClickEvent>(_ =>
                {
                    if (!btn.enabledSelf) return;

                    string name = btn.name?.ToLowerInvariant() ?? "";
                    
                    if (name.Contains("play") || name.Contains("confirm") || name.Contains("start") || name.Contains("create"))
                    {
                        PlayConfirm();
                    }
                    else if (name.Contains("cancel") || name.Contains("leave") || name.Contains("close") || name.Contains("logout") || name.Contains("exit"))
                    {
                        PlayCancel();
                    }
                    else if (name.Contains("swap"))
                    {
                        PlaySwapRole();
                    }
                    else
                    {
                        PlayClick();
                    }
                });
            }
        }

        #endregion
    }
}
