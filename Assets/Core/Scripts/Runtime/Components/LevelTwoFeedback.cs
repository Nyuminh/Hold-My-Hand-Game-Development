using UnityEngine;
using Unity.Cinemachine;

namespace Blocks.Gameplay.Core
{
    [RequireComponent(typeof(AudioSource), typeof(CinemachineImpulseSource))]
    public sealed class LevelTwoFeedback : MonoBehaviour
    {
        public AudioClip clip;
        public AudioSource emitter;
        public ParticleSystem particles;
        public void Play(float shake = 0f)
        {
            if (clip != null) (emitter != null ? emitter : GetComponent<AudioSource>()).PlayOneShot(clip);
            if (particles != null) particles.Play();
            if (shake > 0f) new CameraShakeBuilder(GetComponent<CinemachineImpulseSource>())
                .WithDuration(0.2f).WithVelocity(shake).Execute();
        }
    }
}
