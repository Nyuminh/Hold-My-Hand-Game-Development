using System;
using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Authentication;
#if UNITY_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Blocks.Gameplay.Core
{
    /// <summary>Persistent voice connection for the active Unity Lobby.</summary>
    public sealed class VoiceChatManager : MonoBehaviour
    {
        private const int SampleRate = 16000;
        private const int SamplesPerFrame = 320;
        private const int FrameBytes = 4 + SamplesPerFrame * 2;
        private const int JitterCapacity = 8192;
        private static VoiceChatManager s_Instance;

        [SerializeField] private string serverUrl = "wss://hold-my-hand-backend.vercel.app/voice";
        [SerializeField] private bool muted = true;
        [SerializeField] private bool pushToTalk = true;
        [Range(0.005f, 0.2f)] [SerializeField] private float voiceThreshold = 0.025f;
        [Range(0f, 1f)] [SerializeField] private float playbackVolume = 1f;

        private string m_LobbyId;
        private string m_LobbyCode;
        private ClientWebSocket m_Socket;
        private CancellationTokenSource m_Cancellation;
        private readonly SemaphoreSlim m_SendGate = new SemaphoreSlim(1, 1);
        private readonly object m_JitterLock = new object();
        private readonly float[] m_Jitter = new float[JitterCapacity];
        private readonly float[] m_Capture = new float[SamplesPerFrame];
        private VoiceDSPProcessor m_Dsp;
        private AudioClip m_MicrophoneClip;
        private AudioSource m_Playback;
        private int m_LastMicrophonePosition;
        private int m_JitterRead;
        private int m_JitterWrite;
        private int m_JitterCount;
        private uint m_NextSequence;
        private uint m_LastReceivedSequence;
        private bool m_HasReceivedSequence;
        private bool m_Primed;
        private bool m_Ready;
        private bool m_LocalSpeaking;
        private float m_RemoteSpeakingUntil;

        [Serializable]
        private sealed class AuthFrame
        {
            public string type = "auth";
            public string lobbyId;
            public string lobbyCode;
            public string playerId;
            public string token;
        }

        [Serializable]
        private sealed class ControlFrame
        {
            public string type;
        }

        public static VoiceChatManager Instance => s_Instance;
        public bool IsConnected => m_Ready && m_Socket?.State == WebSocketState.Open;
        public bool IsMuted => muted;
        public bool PushToTalk { get => pushToTalk; set => pushToTalk = value; }
        public float VoiceThreshold { get => voiceThreshold; set => voiceThreshold = Mathf.Clamp(value, 0.005f, 0.2f); }
        public float PlaybackVolume { get => playbackVolume; set { playbackVolume = Mathf.Clamp01(value); if (m_Playback != null) m_Playback.volume = playbackVolume; } }
        public bool IsLocalSpeaking => m_LocalSpeaking;
        public bool IsRemoteSpeaking => Time.unscaledTime < m_RemoteSpeakingUntil;
        public string ServerUrl { get => serverUrl; set => serverUrl = value; }

        public static VoiceChatManager EnsureInstance()
        {
            if (s_Instance != null) return s_Instance;
            var gameObject = new GameObject("VoiceChatManager");
            DontDestroyOnLoad(gameObject);
            return gameObject.AddComponent<VoiceChatManager>();
        }

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this) { Destroy(gameObject); return; }
            s_Instance = this;
            DontDestroyOnLoad(gameObject);
            m_Dsp = new VoiceDSPProcessor(SampleRate);
            m_Playback = gameObject.AddComponent<AudioSource>();
            m_Playback.spatialBlend = 0f;
            m_Playback.loop = true;
            m_Playback.volume = playbackVolume;
            m_Playback.clip = AudioClip.Create("RemoteVoice", SampleRate, 1, SampleRate, true, OnAudioRead);
            m_Playback.Play();
        }

        public async Task JoinLobbyAsync(string lobbyId, string lobbyCode)
        {
            if (string.IsNullOrEmpty(lobbyId) || string.IsNullOrEmpty(lobbyCode)) return;
            if (m_LobbyId == lobbyId && m_Ready) return;
            LeaveLobby();
            m_LobbyId = lobbyId;
            m_LobbyCode = lobbyCode;
            m_Cancellation = new CancellationTokenSource();
            await ConnectAsync(m_Cancellation.Token);
        }

        public void LeaveLobby()
        {
            m_LobbyId = null;
            m_LobbyCode = null;
            m_Ready = false;
            m_LocalSpeaking = false;
            m_RemoteSpeakingUntil = 0f;
            m_HasReceivedSequence = false;
            m_NextSequence = 0;
            StopMicrophone();
            m_Cancellation?.Cancel();
            m_Cancellation?.Dispose();
            m_Cancellation = null;
            m_Socket?.Abort();
            m_Socket?.Dispose();
            m_Socket = null;
            lock (m_JitterLock) { m_JitterRead = m_JitterWrite = m_JitterCount = 0; m_Primed = false; }
        }

        public void SetMuted(bool value)
        {
            muted = value;
            if (muted) StopMicrophone();
            else
            {
                StartMicrophone();
                if (m_MicrophoneClip == null) muted = true;
            }
        }

        public bool SuppressPushToTalk { get; set; }

        private async Task ConnectAsync(CancellationToken cancellation)
        {
            ClientWebSocket socket = null;
            try
            {
                if (!AuthenticationService.Instance.IsAuthorized) throw new InvalidOperationException("Unity Authentication is not ready.");
                var url = new Uri(serverUrl);
                if (url.Scheme != "ws" && url.Scheme != "wss") throw new InvalidOperationException("Voice server URL must use ws or wss.");
                socket = new ClientWebSocket();
                m_Socket = socket;
                await socket.ConnectAsync(url, cancellation);
                var frame = new AuthFrame {
                    lobbyId = m_LobbyId, lobbyCode = m_LobbyCode,
                    playerId = AuthenticationService.Instance.PlayerId,
                    token = AuthenticationService.Instance.AccessToken,
                };
                byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(frame));
                await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellation);
                _ = ReceiveLoopAsync(socket, cancellation);
            }
            catch (OperationCanceledException)
            {
                if (m_Socket == socket) m_Socket = null;
                socket?.Dispose();
            }
            catch (Exception error)
            {
                if (m_Socket == socket) m_Socket = null;
                socket?.Dispose();
                Debug.LogWarning($"[VoiceChat] Cannot connect to {serverUrl}: {error.Message}");
                if (!cancellation.IsCancellationRequested) _ = ReconnectAsync(cancellation);
            }
        }

        private async Task ReconnectAsync(CancellationToken cancellation)
        {
            try
            {
                await Task.Delay(3000, cancellation);
                if (!string.IsNullOrEmpty(m_LobbyId)) await ConnectAsync(cancellation);
            }
            catch (OperationCanceledException) { }
        }

        private async Task ReceiveLoopAsync(ClientWebSocket socket, CancellationToken cancellation)
        {
            byte[] receive = new byte[4096];
            try
            {
                while (!cancellation.IsCancellationRequested && socket.State == WebSocketState.Open)
                {
                    int length = 0;
                    WebSocketReceiveResult received;
                    do
                    {
                        received = await socket.ReceiveAsync(new ArraySegment<byte>(receive, length, receive.Length - length), cancellation);
                        if (received.MessageType == WebSocketMessageType.Close) return;
                        length += received.Count;
                        if (length >= receive.Length && !received.EndOfMessage) throw new InvalidOperationException("Voice frame too large.");
                    } while (!received.EndOfMessage);

                    if (received.MessageType == WebSocketMessageType.Text)
                    {
                        var control = JsonUtility.FromJson<ControlFrame>(Encoding.UTF8.GetString(receive, 0, length));
                        if (control?.type == "ready" && m_Socket == socket)
                        {
                            m_HasReceivedSequence = false;
                            m_Ready = true;
                        }
                    }
                    else if (m_Ready && m_Socket == socket && received.MessageType == WebSocketMessageType.Binary) ReceiveAudioFrame(receive, length);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { Debug.LogWarning($"[VoiceChat] Receive failed: {error.Message}"); }
            finally
            {
                if (m_Socket == socket)
                {
                    m_Ready = false;
                    lock (m_JitterLock) { m_JitterRead = m_JitterWrite = m_JitterCount = 0; m_Primed = false; }
                    m_Socket = null;
                    if (!cancellation.IsCancellationRequested && !string.IsNullOrEmpty(m_LobbyId)) _ = ReconnectAsync(cancellation);
                }
                socket.Dispose();
            }
        }

        private void ReceiveAudioFrame(byte[] receive, int length)
        {
            if (length < 6 || (length - 4) % 2 != 0) return;
            uint sequence = (uint)(receive[0] | receive[1] << 8 | receive[2] << 16 | receive[3] << 24);
            if (m_HasReceivedSequence && unchecked((int)(sequence - m_LastReceivedSequence)) <= 0) return;
            m_LastReceivedSequence = sequence;
            m_HasReceivedSequence = true;
            float squareSum = 0f;
            lock (m_JitterLock)
            {
                for (int offset = 4; offset < length; offset += 2)
                {
                    short sample = (short)(receive[offset] | receive[offset + 1] << 8);
                    float pcm = sample / 32768f;
                    squareSum += pcm * pcm;
                    if (m_JitterCount == JitterCapacity) { m_JitterRead = (m_JitterRead + 1) % JitterCapacity; m_JitterCount--; }
                    m_Jitter[m_JitterWrite] = pcm;
                    m_JitterWrite = (m_JitterWrite + 1) % JitterCapacity;
                    m_JitterCount++;
                }
            }
            if (squareSum / ((length - 4) / 2) > 0.0001f) m_RemoteSpeakingUntil = Time.unscaledTime + 0.18f;
        }

        private void OnAudioRead(float[] data)
        {
            lock (m_JitterLock)
            {
                if (!m_Primed && m_JitterCount >= 960) m_Primed = true;
                for (int i = 0; i < data.Length; i++)
                {
                    if (m_Primed && m_JitterCount > 0)
                    {
                        data[i] = m_Jitter[m_JitterRead];
                        m_JitterRead = (m_JitterRead + 1) % JitterCapacity;
                        m_JitterCount--;
                    }
                    else { data[i] = 0f; m_Primed = false; }
                }
            }
        }

        private void Update()
        {
            if (muted || !m_Ready || m_MicrophoneClip == null) { m_LocalSpeaking = false; return; }
            if (!Microphone.IsRecording(null)) { StopMicrophone(); return; }
            int position = Microphone.GetPosition(null);
            if (position < 0) { StopMicrophone(); return; }
            int available = (position - m_LastMicrophonePosition + m_MicrophoneClip.samples) % m_MicrophoneClip.samples;
            int processed = 0;
            while (available >= SamplesPerFrame && processed++ < 8)
            {
                m_MicrophoneClip.GetData(m_Capture, m_LastMicrophonePosition);
                m_LastMicrophonePosition = (m_LastMicrophonePosition + SamplesPerFrame) % m_MicrophoneClip.samples;
                available -= SamplesPerFrame;
                float squareSum = 0f;
                for (int i = 0; i < SamplesPerFrame; i++) squareSum += m_Capture[i] * m_Capture[i];
                bool talking = pushToTalk ? IsPushToTalkPressed() : squareSum / SamplesPerFrame >= voiceThreshold * voiceThreshold;
                m_LocalSpeaking = talking;
                if (!talking || !m_SendGate.Wait(0)) continue;
                m_Dsp.SetRole(Lobby3DManager.AmIDad);
                m_Dsp.Process(m_Capture, SamplesPerFrame);
                byte[] frame = ArrayPool<byte>.Shared.Rent(FrameBytes);
                uint sequence = m_NextSequence++;
                frame[0] = (byte)sequence; frame[1] = (byte)(sequence >> 8);
                frame[2] = (byte)(sequence >> 16); frame[3] = (byte)(sequence >> 24);
                for (int i = 0; i < SamplesPerFrame; i++)
                {
                    short value = (short)Mathf.RoundToInt(Mathf.Clamp(m_Capture[i], -1f, 1f) * 32767f);
                    frame[4 + i * 2] = (byte)value;
                    frame[5 + i * 2] = (byte)(value >> 8);
                }
                _ = SendFrameAsync(m_Socket, frame, m_Cancellation.Token);
            }
        }

        private async Task SendFrameAsync(ClientWebSocket socket, byte[] frame, CancellationToken cancellation)
        {
            try
            {
                if (socket?.State == WebSocketState.Open)
                    await socket.SendAsync(new ArraySegment<byte>(frame, 0, FrameBytes), WebSocketMessageType.Binary, true, cancellation);
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { Debug.LogWarning($"[VoiceChat] Send failed: {error.Message}"); }
            finally { ArrayPool<byte>.Shared.Return(frame); m_SendGate.Release(); }
        }

        private static bool IsPushToTalkPressed()
        {
#if UNITY_INPUT_SYSTEM
            return s_Instance != null && !s_Instance.SuppressPushToTalk && Keyboard.current?.vKey.isPressed == true;
#else
            return s_Instance != null && !s_Instance.SuppressPushToTalk && Input.GetKey(KeyCode.V);
#endif
        }

        private void StartMicrophone()
        {
            if (m_MicrophoneClip != null) return;
            if (Microphone.devices.Length == 0) { Debug.LogWarning("[VoiceChat] No microphone found."); return; }
            m_MicrophoneClip = Microphone.Start(null, true, 1, SampleRate);
            if (m_MicrophoneClip == null) { Debug.LogWarning("[VoiceChat] Microphone could not start."); return; }
            m_LastMicrophonePosition = 0;
            m_Dsp.Reset();
        }

        private void StopMicrophone()
        {
            if (m_MicrophoneClip == null) return;
            Microphone.End(null);
            m_MicrophoneClip = null;
            m_LocalSpeaking = false;
        }

        private void OnDestroy()
        {
            if (s_Instance != this) return;
            LeaveLobby();
            s_Instance = null;
        }
    }
}
