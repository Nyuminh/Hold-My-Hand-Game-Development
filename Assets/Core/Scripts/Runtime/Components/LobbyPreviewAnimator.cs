using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Blocks.Gameplay.Core
{
    /// <summary>Plays independently randomized humanoid idles on lobby previews only.</summary>
    public class LobbyPreviewAnimator : MonoBehaviour
    {
        private const float BlendDuration = 0.6f;
        private Animator m_Animator;
        private AnimationClip[] m_Clips;
        private AnimationClipPlayable[] m_Playables;
        private PlayableGraph m_Graph;
        private AnimationMixerPlayable m_Mixer;
        private int m_Current = -1;
        private int m_Previous = -1;
        private float m_BlendTime;
        private bool m_OriginalRootMotion;
        private RuntimeAnimatorController m_OriginalController;

        public bool IsPlaying => m_Graph.IsValid();
        public AnimationClip CurrentClip => m_Current >= 0 ? m_Clips[m_Current] : null;

        private void Awake()
        {
            InitializeAnimator();
        }

        private void InitializeAnimator()
        {
            if (m_Animator != null) return;
            foreach (var coreAnimator in GetComponentsInChildren<CoreAnimator>(true))
                coreAnimator.enabled = false;
            m_Animator = GetComponentInChildren<Animator>(true);
            if (m_Animator == null) return;
            m_OriginalRootMotion = m_Animator.applyRootMotion;
            m_OriginalController = m_Animator.runtimeAnimatorController;
            m_Animator.enabled = true;
            m_Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        public void Configure(AnimationClip[] clips)
        {
            // Lobby refreshes call this repeatedly; do not restart a running idle.
            if (m_Clips == clips && m_Graph.IsValid()) return;
            StopPlayback();
            m_Clips = clips;
            if (isActiveAndEnabled) StartPlayback();
        }

        private void OnEnable() => StartPlayback();
        private void OnDisable() => StopPlayback();
        private void OnDestroy() => StopPlayback();

        private void StartPlayback()
        {
            InitializeAnimator();
            if (m_Graph.IsValid() || m_Animator == null || m_Clips == null || m_Clips.Length == 0) return;
            foreach (var clip in m_Clips)
            {
                if (clip == null || clip.length <= 0f || !clip.humanMotion) return;
            }
            // Keep mocap root movement from moving the characters off their lobby marks.
            m_Animator.applyRootMotion = false;
            // The controller otherwise evaluates after Update and overwrites the mocap pose.
            m_Animator.runtimeAnimatorController = null;
            m_Graph = PlayableGraph.Create(name + " Lobby Idle");
            m_Graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            m_Mixer = AnimationMixerPlayable.Create(m_Graph, m_Clips.Length);
            m_Playables = new AnimationClipPlayable[m_Clips.Length];
            for (int i = 0; i < m_Clips.Length; i++)
            {
                m_Playables[i] = AnimationClipPlayable.Create(m_Graph, m_Clips[i]);
                m_Playables[i].SetApplyFootIK(true);
                m_Playables[i].SetSpeed(0);
                m_Graph.Connect(m_Playables[i], 0, m_Mixer, i);
            }
            var output = AnimationPlayableOutput.Create(m_Graph, "Lobby Idle", m_Animator);
            output.SetSourcePlayable(m_Mixer);
            m_Current = Random.Range(0, m_Clips.Length);
            m_Previous = -1;
            m_Mixer.SetInputWeight(m_Current, 1f);
            m_Playables[m_Current].SetSpeed(1);
            m_Graph.Play();
            m_Graph.Evaluate(0);
        }

        private void Update()
        {
            if (!m_Graph.IsValid()) return;
            float delta = Time.unscaledDeltaTime;
            float blend = Mathf.Min(BlendDuration, m_Clips[m_Current].length * 0.25f);
            if (m_Previous < 0 && m_Playables[m_Current].GetTime() >= m_Clips[m_Current].length - blend)
            {
                if (m_Clips.Length == 1)
                {
                    m_Playables[m_Current].SetTime(0);
                }
                else
                {
                    m_Previous = m_Current;
                    // Uniform random choice among every clip except the one just played.
                    m_Current = (m_Current + Random.Range(1, m_Clips.Length)) % m_Clips.Length;
                    m_Playables[m_Current].SetTime(0);
                    m_Playables[m_Current].SetSpeed(1);
                    m_BlendTime = 0f;
                }
            }
            if (m_Previous >= 0)
            {
                m_BlendTime += delta;
                float weight = Mathf.Clamp01(m_BlendTime / blend);
                m_Mixer.SetInputWeight(m_Previous, 1f - weight);
                m_Mixer.SetInputWeight(m_Current, weight);
                if (weight >= 1f)
                {
                    m_Playables[m_Previous].SetSpeed(0);
                    m_Previous = -1;
                }
            }
            m_Graph.Evaluate(delta);
        }

        private void StopPlayback()
        {
            if (m_Graph.IsValid())
            {
                m_Graph.Destroy();
                if (m_Animator != null)
                {
                    m_Animator.runtimeAnimatorController = m_OriginalController;
                    m_Animator.applyRootMotion = m_OriginalRootMotion;
                }
            }
            m_Current = m_Previous = -1;
        }
    }
}
