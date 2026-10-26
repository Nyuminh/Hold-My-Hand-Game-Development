using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Quản lý phát nhạc nền (Soundtrack) tuần tự cho Scene Lobby:
    /// - Phát Track 1 (Lobby-1) -> hết Track 1 chuyển sang Track 2 (Lobby-2)
    /// - Hết Track 2 quay vòng lại Track 1 và lặp vô tận.
    /// - Hỗ trợ fade in / fade out mượt mà giữa các bài nhạc.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class LobbyMusicPlayer : MonoBehaviour
    {
        [Header("Soundtracks Playlist")]
        [Tooltip("Danh sách các bản nhạc sẽ phát lần lượt.")]
        [SerializeField] private AudioClip[] tracks;

        [Header("Playback Settings")]
        [Tooltip("Âm lượng bài nhạc (0 đến 1).")]
        [Range(0f, 1f)]
        [SerializeField] private float volume = 0.75f;

        [Tooltip("Thời gian làm mượt (fade in / fade out) khi chuyển bài (giây).")]
        [Range(0f, 5f)]
        [SerializeField] private float fadeDuration = 2f;

        [Tooltip("Tự động phát khi nạp Scene.")]
        [SerializeField] private bool autoPlay = true;

        [Tooltip("Tự động lặp lại từ đầu danh sách khi hết bài cuối.")]
        [SerializeField] private bool loopPlaylist = true;

        [Header("Audio Routing")]
        [Tooltip("Mixer Group dành cho Music (tùy chọn).")]
        [SerializeField] private AudioMixerGroup musicMixerGroup;

        // Runtime fields
        private AudioSource m_AudioSource;
        private int m_CurrentTrackIndex = 0;
        private Coroutine m_PlaylistCoroutine;
        private bool m_IsFading = false;

        public int CurrentTrackIndex => m_CurrentTrackIndex;
        public AudioClip CurrentTrack => (tracks != null && m_CurrentTrackIndex >= 0 && m_CurrentTrackIndex < tracks.Length) ? tracks[m_CurrentTrackIndex] : null;
        public bool IsPlaying => m_AudioSource != null && m_AudioSource.isPlaying;

        private void Awake()
        {
            InitAudioSource();
        }

        private void Start()
        {
            if (autoPlay && tracks != null && tracks.Length > 0)
            {
                PlayPlaylist();
            }
        }

        private void OnDisable()
        {
            StopPlaylist();
        }

        private void InitAudioSource()
        {
            m_AudioSource = GetComponent<AudioSource>();
            if (m_AudioSource == null)
            {
                m_AudioSource = gameObject.AddComponent<AudioSource>();
            }

            m_AudioSource.playOnAwake = false;
            m_AudioSource.loop = false; // Quản lý lặp thủ công theo playlist
            m_AudioSource.spatialBlend = 0f; // Nhạc nền 2D stereo không phụ thuộc vị trí 3D
            m_AudioSource.volume = volume;

            if (musicMixerGroup != null)
            {
                m_AudioSource.outputAudioMixerGroup = musicMixerGroup;
            }
        }

        /// <summary>
        /// Bắt đầu phát danh sách nhạc từ vị trí hiện tại.
        /// </summary>
        public void PlayPlaylist()
        {
            if (tracks == null || tracks.Length == 0)
            {
                Debug.LogWarning("[LobbyMusicPlayer] Danh sách tracks rỗng, không thể phát nhạc.");
                return;
            }

            if (m_PlaylistCoroutine != null)
            {
                StopCoroutine(m_PlaylistCoroutine);
            }

            m_PlaylistCoroutine = StartCoroutine(PlaylistRoutine());
        }

        /// <summary>
        /// Dừng phát nhạc.
        /// </summary>
        public void StopPlaylist()
        {
            if (m_PlaylistCoroutine != null)
            {
                StopCoroutine(m_PlaylistCoroutine);
                m_PlaylistCoroutine = null;
            }

            if (m_AudioSource != null)
            {
                m_AudioSource.Stop();
            }
        }

        /// <summary>
        /// Chuyển ngay sang bài kế tiếp với hiệu ứng fade.
        /// </summary>
        public void SkipToNextTrack()
        {
            if (tracks == null || tracks.Length == 0) return;

            int nextIndex = (m_CurrentTrackIndex + 1) % tracks.Length;
            PlayTrack(nextIndex);
        }

        /// <summary>
        /// Phát một track cụ thể theo chỉ số index.
        /// </summary>
        public void PlayTrack(int trackIndex)
        {
            if (tracks == null || trackIndex < 0 || trackIndex >= tracks.Length) return;

            m_CurrentTrackIndex = trackIndex;
            if (m_PlaylistCoroutine != null)
            {
                StopCoroutine(m_PlaylistCoroutine);
            }

            m_PlaylistCoroutine = StartCoroutine(PlaylistRoutine());
        }

        /// <summary>
        /// Thay đổi âm lượng khi đang chạy.
        /// </summary>
        public void SetVolume(float newVolume)
        {
            volume = Mathf.Clamp01(newVolume);
            if (!m_IsFading && m_AudioSource != null)
            {
                m_AudioSource.volume = volume;
            }
        }

        private IEnumerator PlaylistRoutine()
        {
            while (true)
            {
                if (tracks == null || tracks.Length == 0) yield break;

                // Lấy clip hiện tại
                AudioClip clip = tracks[m_CurrentTrackIndex];
                if (clip == null)
                {
                    Debug.LogWarning($"[LobbyMusicPlayer] Track ở vị trí {m_CurrentTrackIndex} là null, bỏ qua.");
                    m_CurrentTrackIndex = (m_CurrentTrackIndex + 1) % tracks.Length;
                    yield return null;
                    continue;
                }

                Debug.Log($"[LobbyMusicPlayer] 🎵 Đang phát track [{m_CurrentTrackIndex + 1}/{tracks.Length}]: {clip.name} (Thời lượng: {clip.length:F1}s)");

                m_AudioSource.clip = clip;
                m_AudioSource.volume = 0f;
                m_AudioSource.Play();

                // 1. Fade in
                if (fadeDuration > 0f)
                {
                    m_IsFading = true;
                    float elapsed = 0f;
                    while (elapsed < fadeDuration && m_AudioSource.isPlaying)
                    {
                        elapsed += Time.unscaledDeltaTime;
                        m_AudioSource.volume = Mathf.Lerp(0f, volume, elapsed / fadeDuration);
                        yield return null;
                    }
                    m_AudioSource.volume = volume;
                    m_IsFading = false;
                }
                else
                {
                    m_AudioSource.volume = volume;
                }

                // 2. Chờ phát cho đến gần hết bài (trừ phần fade out)
                float waitDuration = clip.length - (fadeDuration * 2f);
                if (waitDuration > 0f)
                {
                    float timer = 0f;
                    while (timer < waitDuration && m_AudioSource.isPlaying)
                    {
                        timer += Time.unscaledDeltaTime;
                        yield return null;
                    }
                }
                else
                {
                    // Nếu bài quá ngắn so với thời gian fade
                    while (m_AudioSource.isPlaying && m_AudioSource.time < clip.length - fadeDuration)
                    {
                        yield return null;
                    }
                }

                // 3. Fade out
                if (fadeDuration > 0f && m_AudioSource.isPlaying)
                {
                    m_IsFading = true;
                    float startVol = m_AudioSource.volume;
                    float elapsed = 0f;
                    while (elapsed < fadeDuration && m_AudioSource.isPlaying)
                    {
                        elapsed += Time.unscaledDeltaTime;
                        m_AudioSource.volume = Mathf.Lerp(startVol, 0f, elapsed / fadeDuration);
                        yield return null;
                    }
                    m_AudioSource.volume = 0f;
                    m_IsFading = false;
                }

                m_AudioSource.Stop();

                // 4. Chuyển sang track tiếp theo (1 -> 2 -> 1)
                m_CurrentTrackIndex++;
                if (m_CurrentTrackIndex >= tracks.Length)
                {
                    if (loopPlaylist)
                    {
                        m_CurrentTrackIndex = 0;
                        Debug.Log("[LobbyMusicPlayer] 🔄 Đã phát hết danh sách, quay lại bài 1 (Lobby-1).");
                    }
                    else
                    {
                        yield break;
                    }
                }

                // Khoảng nghỉ nhỏ 0.2s giữa 2 bài
                yield return new WaitForSecondsRealtime(0.2f);
            }
        }
    }
}
