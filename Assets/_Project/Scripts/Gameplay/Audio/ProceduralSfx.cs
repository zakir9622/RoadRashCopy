using UnityEngine;

namespace HighwayRenegade.Gameplay.Audio
{
    /// <summary>
    /// Synthesises the game's impact sounds at startup instead of loading recordings.
    ///
    /// Why: the project ships no audio assets at all, so every AudioClip reference is
    /// null and every PlaySfx call returns immediately - crashes, melee hits and scrapes
    /// were all completely silent. EngineAudio already proves the approach works by
    /// synthesising the engine on the audio thread; this does the same for one-shots, so
    /// the game has a full soundscape before a single wav file exists.
    ///
    /// Each clip is built once, cached, and reused. Replacing these with recorded audio is
    /// a matter of assigning clips on AudioManager - nothing else has to change.
    /// </summary>
    public static class ProceduralSfx
    {
        private const int SampleRate = 44100;

        private static AudioClip _crash;
        private static AudioClip _hit;
        private static AudioClip _scrape;

        /// <summary>Heavy impact: bike meets road or barrier.</summary>
        public static AudioClip Crash => _crash ??= BuildCrash();

        /// <summary>Melee connect: a bat, chain or boot landing on a rider.</summary>
        public static AudioClip Hit => _hit ??= BuildHit();

        /// <summary>Metal sliding on tarmac, for a downed bike.</summary>
        public static AudioClip Scrape => _scrape ??= BuildScrape();

        /// <summary>
        /// A crash is a low body thump plus a wide noise burst: the thump carries the mass
        /// and the noise carries the debris. A pure noise burst reads as static rather
        /// than as something heavy hitting the ground.
        /// </summary>
        private static AudioClip BuildCrash()
        {
            const float duration = 0.85f;
            int length = (int)(SampleRate * duration);
            var data = new float[length];

            var random = new System.Random(20260807);

            for (int i = 0; i < length; i++)
            {
                float t = (float)i / SampleRate;
                float progress = (float)i / length;

                // Fast attack, long tail. Exponential rather than linear so it decays the
                // way a real impact does instead of fading out mechanically.
                float envelope = Mathf.Exp(-4.5f * progress);

                // Body: a low tone that drops in pitch as the energy dissipates.
                float bodyHz = Mathf.Lerp(120f, 45f, progress);
                float body = Mathf.Sin(2f * Mathf.PI * bodyHz * t) * 0.55f;

                // Debris: white noise, rolled off over time so the top end disappears first.
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                float noiseLevel = Mathf.Lerp(0.7f, 0.15f, progress);

                data[i] = (body + (noise * noiseLevel)) * envelope * 0.8f;
            }

            return FromSamples("SFX_Crash", data);
        }

        /// <summary>
        /// A connecting blow: much shorter than a crash, with a mid-range crack so it cuts
        /// through engine noise. Combat feedback that gets buried is feedback the player
        /// does not receive.
        /// </summary>
        private static AudioClip BuildHit()
        {
            const float duration = 0.22f;
            int length = (int)(SampleRate * duration);
            var data = new float[length];

            var random = new System.Random(19940301);

            for (int i = 0; i < length; i++)
            {
                float t = (float)i / SampleRate;
                float progress = (float)i / length;

                float envelope = Mathf.Exp(-14f * progress);

                float crackHz = Mathf.Lerp(420f, 180f, progress);
                float crack = Mathf.Sin(2f * Mathf.PI * crackHz * t) * 0.6f;
                float noise = (float)(random.NextDouble() * 2.0 - 1.0) * 0.45f;

                data[i] = (crack + noise) * envelope * 0.85f;
            }

            return FromSamples("SFX_Hit", data);
        }

        /// <summary>
        /// Looping scrape: filtered noise with slow amplitude wobble, so a sliding bike
        /// sounds like it is grinding rather than hissing.
        /// </summary>
        private static AudioClip BuildScrape()
        {
            const float duration = 1.0f;
            int length = (int)(SampleRate * duration);
            var data = new float[length];

            var random = new System.Random(20030621);
            float previous = 0f;

            for (int i = 0; i < length; i++)
            {
                float t = (float)i / SampleRate;

                float noise = (float)(random.NextDouble() * 2.0 - 1.0);

                // One-pole low pass. Raw white noise is far too bright to read as metal
                // on tarmac; this leaves the grit without the hiss.
                previous = Mathf.Lerp(previous, noise, 0.28f);

                float wobble = 0.75f + (0.25f * Mathf.Sin(2f * Mathf.PI * 7f * t));

                // Cosine fade at both ends so the loop point is inaudible.
                float edge = Mathf.Sin(Mathf.PI * ((float)i / length));

                data[i] = previous * wobble * edge * 0.5f;
            }

            return FromSamples("SFX_Scrape", data);
        }

        private static AudioClip FromSamples(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
