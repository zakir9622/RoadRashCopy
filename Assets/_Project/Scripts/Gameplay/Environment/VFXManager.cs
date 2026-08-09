using UnityEngine;

namespace HighwayRenegade.Gameplay.Environment
{
    /// <summary>
    /// Global manager for triggering zero-allocation particle effects.
    /// Road Rash relies heavily on sparks for combat/scraping and smoke for burnouts.
    /// </summary>
    public class VFXManager : MonoBehaviour
    {
        public static VFXManager Instance { get; private set; }

        private ParticleSystem _sparksSystem;
        private ParticleSystem _smokeSystem;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                InitParticleSystems();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        // See AudioManager.OnDestroy: null the static so `VFXManager.Instance?.` sees a real
        // null after teardown instead of a destroyed object, guarded so it cannot clear a
        // scene-load replacement.
        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void InitParticleSystems()
        {
            // Instead of loading heavy prefabs, we build lightweight particle systems dynamically
            var sparksGo = new GameObject("VFX_Sparks");
            sparksGo.transform.SetParent(transform);
            _sparksSystem = sparksGo.AddComponent<ParticleSystem>();
            ConfigureSparks(_sparksSystem);

            var smokeGo = new GameObject("VFX_Smoke");
            smokeGo.transform.SetParent(transform);
            _smokeSystem = smokeGo.AddComponent<ParticleSystem>();
            ConfigureSmoke(_smokeSystem);
        }

        private void ConfigureSparks(ParticleSystem ps)
        {
            var main = ps.main;
            main.duration = 1f;
            main.loop = false;
            main.startLifetime = 0.5f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 15f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
            main.startColor = new Color(1f, 0.7f, 0.1f, 1f); // Orange-yellow sparks
            main.gravityModifier = 1f;
            main.playOnAwake = false;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 30) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 2f;
            // Material will use default particle shader
        }

        private void ConfigureSmoke(ParticleSystem ps)
        {
            var main = ps.main;
            main.duration = 1f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
            main.startSize = new ParticleSystem.MinMaxCurve(1f, 3f);
            main.startColor = new Color(0.8f, 0.8f, 0.8f, 0.5f); // Grey smoke
            main.gravityModifier = -0.1f; // Slight rise
            main.playOnAwake = false;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 10) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.5f;
        }

        public void PlaySparks(Vector3 position, Vector3 normal, int count = 30)
        {
            if (_sparksSystem == null) return;
            
            _sparksSystem.transform.position = position;
            _sparksSystem.transform.rotation = Quaternion.LookRotation(normal);
            _sparksSystem.Emit(count);
        }

        public void PlayCrashBurst(Vector3 position)
        {
            if (_sparksSystem != null)
            {
                _sparksSystem.transform.position = position;
                _sparksSystem.transform.rotation = Quaternion.identity;
                _sparksSystem.Emit(75); // Massive spark burst
            }

            if (_smokeSystem != null)
            {
                _smokeSystem.transform.position = position;
                _smokeSystem.Emit(25); // Dust & smoke cloud
            }
        }

        public void PlaySmoke(Vector3 position)
        {
            if (_smokeSystem == null) return;

            _smokeSystem.transform.position = position;
            _smokeSystem.Emit(10);
        }
    }
}
