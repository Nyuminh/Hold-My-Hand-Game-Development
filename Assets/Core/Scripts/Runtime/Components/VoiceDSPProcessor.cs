using System;
using UnityEngine;

namespace Blocks.Gameplay.Core
{
    /// <summary>Streaming 16 kHz mono voice processor. Call Process on fixed-size PCM blocks.</summary>
    public sealed class VoiceDSPProcessor
    {
        private const int DelayMask = 4095;
        private const int GrainLength = 768;
        private readonly float[] m_Delay = new float[DelayMask + 1];
        private readonly int m_SampleRate;
        private int m_WriteIndex;
        private float m_Phase;
        private float m_Pitch = 1f;
        private float m_TargetPitch = 1f;
        private float m_Wet = 1f;
        private bool m_Enabled = true;
        private bool m_IsDad;
        private Biquad m_FirstFormant;
        private Biquad m_SecondFormant;
        private float m_LowPass;

        private struct Biquad
        {
            private float b0, b2, a1, a2, z1, z2;

            public void SetBandPass(float frequency, float sampleRate)
            {
                float omega = 2f * Mathf.PI * frequency / sampleRate;
                float cosine = Mathf.Cos(omega);
                float alpha = Mathf.Sin(omega) / 1.4f;
                float inverse = 1f / (1f + alpha);
                b0 = alpha * inverse;
                b2 = -b0;
                a1 = -2f * cosine * inverse;
                a2 = (1f - alpha) * inverse;
            }

            public float Process(float input)
            {
                float output = b0 * input + z1;
                z1 = z2 - a1 * output;
                z2 = b2 * input - a2 * output;
                return output;
            }
        }

        public VoiceDSPProcessor(int sampleRate = 16000)
        {
            if (sampleRate < 8000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            m_SampleRate = sampleRate;
            SetRole(true);
        }

        public bool Enabled
        {
            get => m_Enabled;
            set => m_Enabled = value;
        }

        public void SetRole(bool isDad)
        {
            if (m_IsDad == isDad && m_TargetPitch != 1f) return;
            m_IsDad = isDad;
            m_TargetPitch = Mathf.Pow(2f, (isDad ? -4f : 5f) / 12f);
            ConfigureFormants();
        }

        private void ConfigureFormants()
        {
            // Move the first two vocal resonances down for Dad, up for Child.
            m_FirstFormant.SetBandPass(m_IsDad ? 470f : 880f, m_SampleRate);
            m_SecondFormant.SetBandPass(m_IsDad ? 1650f : 2700f, m_SampleRate);
        }

        public void Reset()
        {
            Array.Clear(m_Delay, 0, m_Delay.Length);
            m_FirstFormant = default;
            m_SecondFormant = default;
            m_WriteIndex = 0;
            m_Phase = 0f;
            m_LowPass = 0f;
            m_Pitch = m_TargetPitch;
            ConfigureFormants();
        }

        public void Process(float[] samples, int count)
        {
            if (samples == null) throw new ArgumentNullException(nameof(samples));
            if (count < 0 || count > samples.Length) throw new ArgumentOutOfRangeException(nameof(count));
            float targetWet = m_Enabled ? 1f : 0f;
            for (int i = 0; i < count; i++)
            {
                float dry = samples[i];
                m_Delay[m_WriteIndex] = dry;
                m_Pitch += (m_TargetPitch - m_Pitch) * 0.0015f;
                m_Wet += (targetWet - m_Wet) * 0.005f;

                float secondPhase = m_Phase + 0.5f;
                if (secondPhase >= 1f) secondPhase -= 1f;
                float firstWeight = Mathf.Sin(Mathf.PI * m_Phase);
                float secondWeight = Mathf.Sin(Mathf.PI * secondPhase);
                firstWeight *= firstWeight;
                secondWeight *= secondWeight;
                float shifted = (ReadGrain(m_Phase) * firstWeight + ReadGrain(secondPhase) * secondWeight) /
                                (firstWeight + secondWeight + 0.0001f);
                float formant1 = m_FirstFormant.Process(shifted);
                float formant2 = m_SecondFormant.Process(shifted);
                m_LowPass += 0.16f * (shifted - m_LowPass);
                float colored = m_IsDad
                    ? shifted * 0.72f + m_LowPass * 0.22f + formant1 * 0.28f + formant2 * 0.16f
                    : shifted * 0.78f + (shifted - m_LowPass) * 0.16f + formant1 * 0.18f + formant2 * 0.22f;
                samples[i] = Mathf.Clamp(dry * (1f - m_Wet) + colored * m_Wet, -1f, 1f);

                m_WriteIndex = (m_WriteIndex + 1) & DelayMask;
                m_Phase += 1f / GrainLength;
                if (m_Phase >= 1f) m_Phase -= 1f;
            }
        }

        private float ReadGrain(float phase)
        {
            float position = m_WriteIndex - 720f + (m_Pitch - 1f) * phase * GrainLength;
            int first = Mathf.FloorToInt(position);
            float fraction = position - first;
            float left = m_Delay[first & DelayMask];
            float right = m_Delay[(first + 1) & DelayMask];
            return left + (right - left) * fraction;
        }
    }
}
