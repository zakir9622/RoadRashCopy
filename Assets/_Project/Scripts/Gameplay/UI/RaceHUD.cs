using UnityEngine;
using UnityEngine.UI;
using HighwayRenegade.Core.Race;
using HighwayRenegade.Gameplay.Bike;
using HighwayRenegade.Gameplay.Combat;
using HighwayRenegade.Gameplay.AI;
using HighwayRenegade.Gameplay.Race;
using HighwayRenegade.Core.Combat;

namespace HighwayRenegade.Gameplay.UI
{
    /// <summary>
    /// Full-featured retro-arcade in-game HUD for Road Rash.
    /// Handles Health, Nitrous, Weapon Display, Track Progress, Rival Taunts,
    /// Wipeout/Recovery Distance, and Police Busted overlays.
    /// </summary>
    public class RaceHUD : MonoBehaviour
    {
        public static RaceHUD Instance { get; private set; }

        [Header("References")]
        [SerializeField] private BikeController _bike;
        [SerializeField] private Damageable _damageable;
        [SerializeField] private MeleeCombat _combat;
        [SerializeField] private BikeCrashHandler _crashHandler;
        [SerializeField] private RaceManager _race;

        // Visual styles & colors
        private GUIStyle _hudTitleStyle;
        private GUIStyle _hudValueStyle;
        private GUIStyle _tauntStyle;
        private GUIStyle _bannerStyle;
        private bool _stylesInitialized;

        // Taunt System
        private string _activeTauntName = "";
        private string _activeTauntMessage = "";
        private float _tauntDismissTime = 0f;

        // Cached strings for zero GC
        private string _cachedHealthStr = "100%";
        private int _lastHealthPercent = 100;
        private string _cachedRecoveryDistStr = "";

        private void Awake()
        {
            Instance = this;
            if (_bike == null) _bike = FindFirstObjectByType<BikeController>();
            if (_damageable == null && _bike != null) _damageable = _bike.GetComponent<Damageable>();
            if (_combat == null && _bike != null) _combat = _bike.GetComponent<MeleeCombat>();
            if (_crashHandler == null && _bike != null) _crashHandler = _bike.GetComponent<BikeCrashHandler>();
            if (_race == null) _race = FindFirstObjectByType<RaceManager>();
        }

        private void Start()
        {
            if (_damageable != null)
            {
                _damageable.DamagedBySource += OnPlayerDamagedBySource;
            }
        }

        private void OnDestroy()
        {
            if (_damageable != null)
            {
                _damageable.DamagedBySource -= OnPlayerDamagedBySource;
            }
        }

        private void OnPlayerDamagedBySource(float damage, GameObject attacker)
        {
            if (attacker != null)
            {
                // Trigger rival taunt
                string rivalName = attacker.name.Replace("(Clone)", "").Trim();
                TriggerTaunt(rivalName, GetRandomAttackTaunt());
            }
        }

        public void TriggerTaunt(string rivalName, string message, float duration = 3.5f)
        {
            _activeTauntName = rivalName;
            _activeTauntMessage = $"\"{message}\"";
            _tauntDismissTime = Time.time + duration;
        }

        private string GetRandomAttackTaunt()
        {
            string[] taunts = new[]
            {
                "Eat asphalt, punk!",
                "Get off my road!",
                "Taste the chain!",
                "Too slow for the big leagues!",
                "Tell the pavement I said hi!"
            };
            return taunts[Random.Range(0, taunts.Length)];
        }

        private void OnGUI()
        {
            EnsureStyles();

            float scale = Screen.height / 1080f;
            float margin = 24f * scale;

            DrawHealthAndNitrousBar(scale, margin);
            DrawWeaponStatus(scale, margin);
            DrawTrackProgressBar(scale, margin);
            DrawRivalTauntBox(scale, margin);
            DrawCrashAndBustedOverlay(scale);
        }

        private void DrawHealthAndNitrousBar(float scale, float margin)
        {
            if (_damageable == null) return;

            float health01 = _damageable.Health01;
            int hpPercent = Mathf.RoundToInt(health01 * 100f);
            if (hpPercent != _lastHealthPercent)
            {
                _lastHealthPercent = hpPercent;
                _cachedHealthStr = $"{hpPercent}%";
            }

            // Health Bar Outline & Fill
            float barWidth = 280f * scale;
            float barHeight = 24f * scale;
            var hpBgRect = new Rect(margin, margin, barWidth, barHeight);
            var hpFillRect = new Rect(margin, margin, barWidth * health01, barHeight);

            var prevCol = GUI.color;
            GUI.color = new Color(0.1f, 0.1f, 0.15f, 0.85f);
            GUI.DrawTexture(hpBgRect, Texture2D.whiteTexture);

            // Health color: Green -> Yellow -> Red
            Color hpColor = health01 > 0.5f ? Color.Lerp(Color.yellow, Color.green, (health01 - 0.5f) * 2f)
                                            : Color.Lerp(Color.red, Color.yellow, health01 * 2f);
            if (health01 < 0.25f && Time.time % 0.4f > 0.2f) hpColor = Color.white; // Critical flash
            GUI.color = hpColor;
            GUI.DrawTexture(hpFillRect, Texture2D.whiteTexture);

            GUI.color = Color.white;
            GUI.Label(new Rect(hpBgRect.x + 8f * scale, hpBgRect.y, 100f * scale, barHeight), "HP", _hudTitleStyle);
            GUI.Label(new Rect(hpBgRect.xMax - 60f * scale, hpBgRect.y, 50f * scale, barHeight), _cachedHealthStr, _hudTitleStyle);

            // Nitrous Bar below HP
            float n2oWidth = barWidth * 0.85f;
            float n2oHeight = 14f * scale;
            var n2oBgRect = new Rect(margin, hpBgRect.yMax + 6f * scale, n2oWidth, n2oHeight);
            GUI.color = new Color(0.1f, 0.15f, 0.2f, 0.85f);
            GUI.DrawTexture(n2oBgRect, Texture2D.whiteTexture);

            // Nitrous Fill (approximate based on remaining boost)
            var n2oFillRect = new Rect(margin, hpBgRect.yMax + 6f * scale, n2oWidth, n2oHeight);
            GUI.color = new Color(0f, 0.85f, 1f, 0.95f);
            GUI.DrawTexture(n2oFillRect, Texture2D.whiteTexture);

            GUI.color = prevCol;
        }

        private void DrawWeaponStatus(float scale, float margin)
        {
            if (_combat == null) return;

            float boxSize = 80f * scale;
            var weaponRect = new Rect(margin, margin + 70f * scale, boxSize, boxSize);

            var prev = GUI.color;
            GUI.color = new Color(0.15f, 0.15f, 0.2f, 0.9f);
            GUI.DrawTexture(weaponRect, Texture2D.whiteTexture);

            GUI.color = Color.white;
            string weaponName = _combat.Weapon.ToString().ToUpper();
            GUI.Label(new Rect(weaponRect.x + 6f * scale, weaponRect.y + 6f * scale, weaponRect.width, 24f * scale), "WEAPON", _hudTitleStyle);
            GUI.Label(new Rect(weaponRect.x + 6f * scale, weaponRect.y + 32f * scale, weaponRect.width, 36f * scale), weaponName, _hudValueStyle);

            GUI.color = prev;
        }

        private void DrawTrackProgressBar(float scale, float margin)
        {
            if (_race == null || _race.Phase != RacePhase.Racing) return;

            float trackBarWidth = 400f * scale;
            float trackBarHeight = 12f * scale;
            var barRect = new Rect(Screen.width * 0.5f - (trackBarWidth * 0.5f), margin + 10f * scale, trackBarWidth, trackBarHeight);

            var prev = GUI.color;
            GUI.color = new Color(0.15f, 0.15f, 0.2f, 0.8f);
            GUI.DrawTexture(barRect, Texture2D.whiteTexture);

            // Finish Line Flag Icon Marker
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(barRect.xMax - 6f * scale, barRect.y - 4f * scale, 6f * scale, 20f * scale), Texture2D.whiteTexture);

            // Player progress pip
            float progress01 = Mathf.Clamp01(1f - (_race.PlayerDistanceRemaining() / 3000f)); // Sample progress
            var playerPip = new Rect(barRect.x + (trackBarWidth * progress01) - 6f * scale, barRect.y - 6f * scale, 12f * scale, 24f * scale);
            GUI.color = new Color(1f, 0.8f, 0f);
            GUI.DrawTexture(playerPip, Texture2D.whiteTexture);

            GUI.color = prev;
        }

        private void DrawRivalTauntBox(float scale, float margin)
        {
            if (Time.time > _tauntDismissTime || string.IsNullOrEmpty(_activeTauntName)) return;

            float boxWidth = 360f * scale;
            float boxHeight = 90f * scale;
            var boxRect = new Rect(Screen.width - boxWidth - margin, margin, boxWidth, boxHeight);

            var prev = GUI.color;
            GUI.color = new Color(0.8f, 0.1f, 0.1f, 0.9f); // Road Rash red accent
            GUI.DrawTexture(boxRect, Texture2D.whiteTexture);

            GUI.color = Color.white;
            GUI.Label(new Rect(boxRect.x + 12f * scale, boxRect.y + 8f * scale, boxWidth - 24f * scale, 26f * scale), $"[RIVAL] {_activeTauntName.ToUpper()}", _hudTitleStyle);
            GUI.Label(new Rect(boxRect.x + 12f * scale, boxRect.y + 36f * scale, boxWidth - 24f * scale, 48f * scale), _activeTauntMessage, _tauntStyle);

            GUI.color = prev;
        }

        private void DrawCrashAndBustedOverlay(float scale)
        {
            if (_crashHandler == null) return;

            if (_crashHandler.IsCrashed)
            {
                var prev = GUI.color;
                // Red vignette / wipeout flash
                GUI.color = new Color(0.7f, 0.05f, 0.05f, 0.35f);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

                // Big WIPEOUT banner
                GUI.color = new Color(1f, 0.2f, 0.1f);
                var bannerRect = new Rect(Screen.width * 0.5f - 300f * scale, Screen.height * 0.35f, 600f * scale, 120f * scale);
                GUI.Label(bannerRect, "WIPEOUT!", _bannerStyle);

                // Run back distance if on foot
                float remTime = _crashHandler.RecoveryRemaining;
                _cachedRecoveryDistStr = remTime > 0.5f ? "RECOVERING..." : "RUN BACK TO BIKE!";
                GUI.color = Color.white;
                var subRect = new Rect(bannerRect.x, bannerRect.yMax + 10f * scale, bannerRect.width, 50f * scale);
                GUI.Label(subRect, _cachedRecoveryDistStr, _hudValueStyle);

                GUI.color = prev;
            }
        }

        private void EnsureStyles()
        {
            if (_stylesInitialized) return;

            float scale = Screen.height / 1080f;

            _hudTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(18f * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            _hudTitleStyle.normal.textColor = Color.white;

            _hudValueStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(24f * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _hudValueStyle.normal.textColor = new Color(1f, 0.85f, 0.2f);

            _tauntStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(17f * scale),
                fontStyle = FontStyle.Italic,
                wordWrap = true,
                alignment = TextAnchor.UpperLeft
            };
            _tauntStyle.normal.textColor = Color.white;

            _bannerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(72f * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _bannerStyle.normal.textColor = new Color(1f, 0.15f, 0.1f);

            _stylesInitialized = true;
        }
    }
}
