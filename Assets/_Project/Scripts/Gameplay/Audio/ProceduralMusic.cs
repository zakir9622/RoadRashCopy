using UnityEngine;

namespace HighwayRenegade.Gameplay.Audio
{
    /// <summary>Procedurally generated looping music beds when no authored clips exist.</summary>
    public static class ProceduralMusic
    {
        private static readonly System.Collections.Generic.Dictionary<string, AudioClip> Cache =
            new System.Collections.Generic.Dictionary<string, AudioClip>();

        public static AudioClip ForKey(string key)
        {
            if (string.IsNullOrEmpty(key)) key = "menu";
            if (Cache.TryGetValue(key, out AudioClip cached)) return cached;

            float baseFreq = key switch
            {
                "night" => 98f,
                "race" => 110f,
                "desert" => 103f,
                "results" => 88f,
                _ => 92f
            };

            AudioClip clip = SynthLoop(baseFreq, key);
            Cache[key] = clip;
            return clip;
        }

        private static AudioClip SynthLoop(float rootHz, string name)
        {
            const int sampleRate = 44100;
            const float duration = 8f;
            int samples = Mathf.CeilToInt(sampleRate * duration);
            var data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float beat = Mathf.Sin(t * rootHz * Mathf.PI * 2f) * 0.12f;
                float pad = Mathf.Sin(t * rootHz * 0.5f * Mathf.PI * 2f) * 0.06f;
                float pulse = (Mathf.Sin(t * 2.4f * Mathf.PI * 2f) > 0.6f ? 0.04f : 0f);
                data[i] = beat + pad + pulse;
            }

            var clip = AudioClip.Create($"Music_{name}", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
