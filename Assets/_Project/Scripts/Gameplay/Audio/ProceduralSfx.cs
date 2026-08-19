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
        private static AudioClip _wind;
        private static AudioClip _uiClick;

        /// <summary>Heavy impact: bike meets road or barrier.</summary>
        public static AudioClip Crash => _crash ??= BuildCrash();

        /// <summary>Melee connect: a bat, chain or boot landing on a rider.</summary>
        public static AudioClip Hit => _hit ??= BuildHit();

        /// <summary>Metal sliding on tarmac, for a downed bike or a tyre losing grip.</summary>
        public static AudioClip Scrape => _scrape ??= BuildScrape();

        /// <summary>Looping airflow, pitched and mixed by speed.</summary>
        public static AudioClip Wind => _wind ??= BuildWind();

        /// <summary>Short UI confirmation blip.</summary>
        public static AudioClip UiClick => _uiClick ??= BuildUiClick();

        /// <summary>Road Rash kick thud.</summary>
        public static AudioClip Kick => _kick ??= BuildKick();

        /// <summary>Police siren wail when heat is high.</summary>
        public static AudioClip PoliceSiren => _siren ??= BuildPoliceSiren();

        /// <summary>Traffic horn blast.</summary>
        public static AudioClip Horn => _horn ??= BuildHorn();

        private static AudioClip _kick;
        private static AudioClip _siren;
        private static AudioClip _horn;

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

        /// <summary>
        /// Looping airflow. Darker and smoother than the scrape - two cascaded low passes
        /// rather than one - because wind at speed is a broad rush, and anything with grit
        /// in it reads as tyre noise instead.
        ///
        /// Speed is felt more than it is seen, and wind is the cheapest way to sell it:
        /// the volume rising as the bike accelerates does more for the sense of pace than
        /// the speedometer does.
        /// </summary>
        private static AudioClip BuildWind()
        {
            const float duration = 2.0f;
            int length = (int)(SampleRate * duration);
            var data = new float[length];

            var random = new System.Random(19911214);
            float low = 0f, lower = 0f;

            for (int i = 0; i < length; i++)
            {
                float t = (float)i / SampleRate;

                float noise = (float)(random.NextDouble() * 2.0 - 1.0);

                low = Mathf.Lerp(low, noise, 0.10f);
                lower = Mathf.Lerp(lower, low, 0.10f);

                // Two slow, mutually prime swells so the two-second loop does not announce
                // its own period.
                float swell = 0.80f
                            + (0.12f * Mathf.Sin(2f * Mathf.PI * 0.37f * t))
                            + (0.08f * Mathf.Sin(2f * Mathf.PI * 0.73f * t));

                float edge = Mathf.Sin(Mathf.PI * ((float)i / length));

                data[i] = lower * swell * edge * 3.2f;
            }

            return FromSamples("SFX_Wind", data);
        }

        /// <summary>
        /// UI blip. Two stacked sine partials with a very fast decay - long enough to
        /// register as a response, short enough that pressing buttons quickly does not
        /// turn into a drone.
        /// </summary>
        private static AudioClip BuildUiClick()
        {
            const float duration = 0.07f;
            int length = (int)(SampleRate * duration);
            var data = new float[length];

            for (int i = 0; i < length; i++)
            {
                float t = (float)i / SampleRate;
                float progress = (float)i / length;

                float envelope = Mathf.Exp(-34f * progress);
                float tone = Mathf.Sin(2f * Mathf.PI * 880f * t) * 0.6f
                           + Mathf.Sin(2f * Mathf.PI * 1320f * t) * 0.25f;

                data[i] = tone * envelope * 0.5f;
            }

            return FromSamples("SFX_UiClick", data);
        }

        private static AudioClip BuildKick()
        {
            const float duration = 0.18f;
            int length = (int)(SampleRate * duration);
            var data = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-18f * i / (float)length);
                float thump = Mathf.Sin(2f * Mathf.PI * 90f * t) * 0.7f;
                data[i] = thump * env;
            }
            return FromSamples("SFX_Kick", data);
        }

        private static AudioClip BuildPoliceSiren()
        {
            const float duration = 1.2f;
            int length = (int)(SampleRate * duration);
            var data = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / SampleRate;
                float wail = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(420f, 880f, (Mathf.Sin(t * 6f) + 1f) * 0.5f) * t);
                data[i] = wail * 0.25f;
            }
            return FromSamples("SFX_Siren", data);
        }

        private static AudioClip BuildHorn()
        {
            const float duration = 0.35f;
            int length = (int)(SampleRate * duration);
            var data = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-6f * i / (float)length);
                data[i] = (Mathf.Sin(2f * Mathf.PI * 220f * t) + Mathf.Sin(2f * Mathf.PI * 330f * t) * 0.5f) * env * 0.4f;
            }
            return FromSamples("SFX_Horn", data);
        }

        private static AudioClip FromSamples(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
