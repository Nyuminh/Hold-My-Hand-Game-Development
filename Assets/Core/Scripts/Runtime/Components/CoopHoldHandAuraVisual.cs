using UnityEngine;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Hiệu ứng thị giác Nắm Tay Ấm Áp (Warm Amber Hold-Hand Aura):
    /// - Khi Ba và Con nắm tay nhau (IsHoldingHands = true):
    ///   + Tự động sinh ra một điểm sáng ấm vàng hổ phách dịu nhẹ (Warm Light) giữa 2 bàn tay.
    ///   + Hiệu ứng ánh sáng thở (Gentle Breathing Glow Pulse) nhịp nhàng như nhịp tim phụ tử.
    ///   + Sinh các hạt đom đóm / tia sáng ấm (Warm Sparks) bay lượn nhẹ nhàng xung quanh.
    /// - Khi buông tay: Ánh sáng fade-out êm ái, biến mất tự nhiên.
    /// </summary>
    [RequireComponent(typeof(CoopHoldHandAbility))]
    public class CoopHoldHandAuraVisual : MonoBehaviour
    {
        [Header("Ánh sáng Ấm Áp (Warm Amber Light)")]
        [SerializeField] private Color auraColor = new Color(1.0f, 0.78f, 0.35f, 1.0f);
        [SerializeField] private float baseIntensity = 1.8f;
        [SerializeField] private float lightRange = 3.6f;
        [SerializeField] private float pulseSpeed = 2.2f;
        [SerializeField] private float pulseAmplitude = 0.25f;

        private CoopHoldHandAbility m_HoldAbility;
        private Light m_WarmLight;
        private ParticleSystem m_SparksParticle;
        private float m_CurrentIntensity = 0f;
        private float m_TargetIntensity = 0f;

        private void Awake()
        {
            m_HoldAbility = GetComponent<CoopHoldHandAbility>();
            CreateLightAndParticles();
        }

        private void CreateLightAndParticles()
        {
            // 1. Tạo GameObject phụ để chứa Light & VFX
            GameObject vfxObj = new GameObject("HoldHand_WarmAura");
            vfxObj.transform.SetParent(this.transform, false);
            vfxObj.transform.localPosition = new Vector3(0f, 1.0f, 0f);

            // 2. Light component
            m_WarmLight = vfxObj.AddComponent<Light>();
            m_WarmLight.type = LightType.Point;
            m_WarmLight.color = auraColor;
            m_WarmLight.range = lightRange;
            m_WarmLight.intensity = 0f;
            m_WarmLight.shadows = LightShadows.None;
            m_WarmLight.enabled = false;

            // 3. Procedural Particle System (hạt ánh sáng bay lên nhẹ nhàng)
            m_SparksParticle = vfxObj.AddComponent<ParticleSystem>();
            var main = m_SparksParticle.main;
            main.startLifetime = 1.6f;
            main.startSpeed = 0.45f;
            main.startSize = 0.12f;
            main.startColor = auraColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;

            var emission = m_SparksParticle.emission;
            emission.rateOverTime = 14f;

            var shape = m_SparksParticle.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.4f;

            var colorOverLifetime = m_SparksParticle.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(auraColor, 0f), new GradientColorKey(new Color(1f, 0.95f, 0.7f), 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 0.3f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = grad;

            var renderer = m_SparksParticle.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = 5;

            var particleMat = Resources.GetBuiltinResource<Material>("Default-Particle.mat");
            if (particleMat != null)
            {
                renderer.sharedMaterial = particleMat;
            }
        }

        private void Update()
        {
            if (m_HoldAbility == null) return;

            bool isHolding = m_HoldAbility.IsHoldingHands;
            m_TargetIntensity = isHolding ? baseIntensity : 0f;

            // Fade intensity smoothly
            m_CurrentIntensity = Mathf.MoveTowards(m_CurrentIntensity, m_TargetIntensity, Time.deltaTime * 3.5f);

            if (m_CurrentIntensity > 0.01f)
            {
                if (!m_WarmLight.enabled) m_WarmLight.enabled = true;

                // Vị trí ánh sáng đặt giữa 2 nhân vật (nếu có Partner)
                Transform partner = m_HoldAbility.PartnerTransform;
                if (partner != null)
                {
                    Vector3 midPoint = (transform.position + partner.position) * 0.5f;
                    midPoint.y += 0.95f; // Ngang tầm tay nắm
                    m_WarmLight.transform.position = midPoint;
                }
                else
                {
                    m_WarmLight.transform.localPosition = new Vector3(0.35f, 0.95f, 0.2f);
                }

                // Nhịp thở nhịp nhàng (Breathing pulse)
                float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmplitude;
                m_WarmLight.intensity = m_CurrentIntensity * pulse;

                if (!m_SparksParticle.isPlaying && isHolding)
                {
                    m_SparksParticle.Play();
                }
            }
            else
            {
                if (m_WarmLight.enabled) m_WarmLight.enabled = false;
                if (m_SparksParticle.isPlaying) m_SparksParticle.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private void OnDestroy()
        {
            if (m_WarmLight != null && m_WarmLight.gameObject != null)
            {
                Destroy(m_WarmLight.gameObject);
            }
        }
    }
}
