using UnityEngine;
using UnityEngine.UIElements;
using HighwayRenegade.Core.AI;
using HighwayRenegade.Core.Race;
using HighwayRenegade.Gameplay.AI;
using HighwayRenegade.Gameplay.Bike;
using HighwayRenegade.Gameplay.Combat;
using HighwayRenegade.Gameplay.Race;

namespace HighwayRenegade.Gameplay.UI.Screens
{
    /// <summary>
    /// In-race HUD over GameUI.uxml: speed, gear, bike condition, nitrous, position, distance
    /// to the finish, knockouts, cash, police heat, live standings, a start countdown, and a
    /// damage-direction edge flash.
    ///
    /// Finds the player itself rather than waiting to be wired in the scene. The HUD is
    /// created by the track generator alongside the bike, and a serialized reference into
    /// a procedurally built scene is exactly the kind of link that silently comes back
    /// null - which is how the previous HUD ended up displaying nothing.
    ///
    /// Position and progress were left as a comment saying a real implementation would
    /// fill them in, so the two numbers that tell a player whether they are winning were
    /// permanently blank. They are read from RaceManager here - as position and metres to
    /// the finish, because this race is point-to-point and has no laps to count.
    /// </summary>
    public sealed class RaceHudScreen : UIScreen
    {
        private const float MetresPerSecondToKmh = 3.6f;

        private Label _speed;
        private Label _gear;
        private Label _position;
        private Label _distance;
        private Label _knockouts;
        private Label _cash;
        private VisualElement _healthFill;
        private VisualElement _nitrousFill;
        private VisualElement _heatStars;
        private Label _countdown;
        private VisualElement _standingsList;
        private VisualElement _flashLeft;
        private VisualElement _flashRight;

        // Countdown display state.
        private RacePhase _lastPhase = RacePhase.Staging;
        private float _goUntil;
        private string _lastCountdownText;

        // Damage-flash: current glow level per side (1 on a hit, decaying to 0).
        private const float FlashFadeSeconds = 0.4f;
        private float _flashLeftLevel, _flashRightLevel;
        private float _flashLeftApplied = -1f, _flashRightApplied = -1f;
        private Damageable _subscribedHealth;

        // Standings refresh throttle - positions do not change fast enough for every frame.
        private const float StandingsInterval = 0.4f;
        private float _nextStandingsRefresh;

        // Heat tuning. Range roughly matches the police escape distance, so the stars track
        // the same "a cop is on me" pressure the pursuit itself uses.
        private const float HeatRange = 120f;
        private const float HeatRisePerSec = 0.5f;
        private const float HeatDecayPerSec = 0.35f;
        private float _heat;

        private BikeController _bike;
        private Damageable _health;
        private RaceManager _race;
        private PoliceAI[] _police = System.Array.Empty<PoliceAI>();
        private Progression.CampaignSession _session;

        // Last values pushed to the UI. Comparing before assigning keeps the HUD from
        // dirtying the visual tree on every frame when nothing has actually changed.
        private int _lastSpeed = int.MinValue;
        private int _lastGear = int.MinValue;
        private int _lastPosition = int.MinValue;
        private int _lastDistance = int.MinValue;
        private int _lastKnockouts = int.MinValue;
        private int _lastCash = int.MinValue;
        private float _lastHealth = -1f;
        private int _lastNitrous = int.MinValue;
        private int _lastStars = int.MinValue;

        protected override void OnBind()
        {
            _speed = Require<Label>("SpeedLabel");
            _gear = Require<Label>("GearLabel");
            _healthFill = Require<VisualElement>("HealthBarFill");
            _position = Optional<Label>("PositionLabel");
            _distance = Optional<Label>("DistanceLabel");
            _knockouts = Optional<Label>("KnockoutLabel");
            _cash = Optional<Label>("CashLabel");
            _nitrousFill = Optional<VisualElement>("NitrousBarFill");
            _heatStars = Optional<VisualElement>("HeatStars");
            _countdown = Optional<Label>("CountdownLabel");
            _standingsList = Optional<VisualElement>("StandingsList");
            _flashLeft = Optional<VisualElement>("DamageFlashLeft");
            _flashRight = Optional<VisualElement>("DamageFlashRight");

            AcquirePlayer();
        }

        private void AcquirePlayer()
        {
            var input = FindFirstObjectByType<PlayerBikeInput>();
            if (input == null) return;

            _bike = input.GetComponent<BikeController>();
            _health = input.GetComponent<Damageable>();
            _race = FindFirstObjectByType<RaceManager>();
            _session = FindFirstObjectByType<Progression.CampaignSession>();

            // The generator builds the cops alongside the bike, so this catches them in the
            // same acquisition. An empty result just means no police on this track.
            _police = FindObjectsByType<PoliceAI>(FindObjectsSortMode.None);

            // Subscribe once for the damage-direction flash. Re-resolving the player (the
            // retry loop below) must not stack handlers, so drop any prior subscription first.
            if (_health != _subscribedHealth)
            {
                if (_subscribedHealth != null) _subscribedHealth.DamagedBySource -= OnPlayerDamaged;
                if (_health != null) _health.DamagedBySource += OnPlayerDamaged;
                _subscribedHealth = _health;
            }
        }

        protected override void OnDisable()
        {
            if (_subscribedHealth != null) _subscribedHealth.DamagedBySource -= OnPlayerDamaged;
            _subscribedHealth = null;
            base.OnDisable();
        }

        private void Update()
        {
            if (Root == null) return;

            // The bike is spawned by the generator, which may not have run when the HUD
            // first bound. Retry until it appears rather than staying blank forever.
            if (_bike == null) AcquirePlayer();
            if (_bike == null) return;

            UpdateSpeedAndGear();
            UpdateHealth();
            UpdateNitrous();
            UpdateHeat();
            UpdateCountdown();
            UpdateDamageFlash();
            UpdateStandings();
            UpdateRacePosition();
        }

        private void UpdateCountdown()
        {
            if (_countdown == null || _race == null) return;

            RacePhase phase = _race.Phase;

            // Catch the countdown->racing transition so GO! flashes exactly once at the off.
            if (_lastPhase == RacePhase.Countdown && phase == RacePhase.Racing)
                _goUntil = Time.time + 0.8f;
            _lastPhase = phase;

            string text;
            if (phase == RacePhase.Countdown)
            {
                int n = _race.CountdownRemaining;
                text = n > 0 ? NumberText.Of(n) : "GO!";
            }
            else if (Time.time < _goUntil)
            {
                text = "GO!";
            }
            else
            {
                text = null; // hidden outside the start sequence
            }

            if (text == _lastCountdownText) return;
            _lastCountdownText = text;

            if (text == null)
            {
                _countdown.style.display = DisplayStyle.None;
            }
            else
            {
                _countdown.style.display = DisplayStyle.Flex;
                _countdown.text = text;
            }
        }

        private void OnPlayerDamaged(float amount, GameObject source)
        {
            // A hit with no known source lights both edges; otherwise the side the attacker
            // is on in the bike's own frame.
            if (source == null || _bike == null)
            {
                _flashLeftLevel = _flashRightLevel = 1f;
                return;
            }

            Vector3 local = _bike.transform.InverseTransformPoint(source.transform.position);
            if (local.x < 0f) _flashLeftLevel = 1f;
            else _flashRightLevel = 1f;
        }

        private void UpdateDamageFlash()
        {
            _flashLeftLevel = Mathf.Max(0f, _flashLeftLevel - Time.deltaTime / FlashFadeSeconds);
            _flashRightLevel = Mathf.Max(0f, _flashRightLevel - Time.deltaTime / FlashFadeSeconds);

            // Only touch the style when the level actually changed, so a settled (0) flash
            // stops dirtying the visual tree every frame.
            if (_flashLeft != null && !Mathf.Approximately(_flashLeftLevel, _flashLeftApplied))
            {
                _flashLeftApplied = _flashLeftLevel;
                _flashLeft.style.opacity = _flashLeftLevel;
            }
            if (_flashRight != null && !Mathf.Approximately(_flashRightLevel, _flashRightApplied))
            {
                _flashRightApplied = _flashRightLevel;
                _flashRight.style.opacity = _flashRightLevel;
            }
        }

        private void UpdateStandings()
        {
            if (_standingsList == null || _race == null) return;
            if (Time.time < _nextStandingsRefresh) return;
            _nextStandingsRefresh = Time.time + StandingsInterval;

            int slot = 1;
            foreach (VisualElement child in _standingsList.Children())
            {
                if (!(child is Label row)) { slot++; continue; }

                BikeController bike = _race.RacerInSlot(slot);
                if (bike == null)
                {
                    row.style.display = DisplayStyle.None;
                }
                else
                {
                    bool isPlayer = _race.IsPlayerBike(bike);
                    row.style.display = DisplayStyle.Flex;
                    row.text = slot + "  " + (isPlayer ? "YOU" : bike.gameObject.name);
                    row.EnableInClassList("you", isPlayer);
                }
                slot++;
            }
        }

        private void UpdateHeat()
        {
            if (_heatStars == null) return;

            bool pursued = false;
            float nearest = float.MaxValue;
            Vector3 pos = _bike.transform.position;

            for (int i = 0; i < _police.Length; i++)
            {
                PoliceAI cop = _police[i];
                if (cop == null || !cop.IsPursuing || cop.HasBusted) continue;

                float d = Vector3.Distance(pos, cop.transform.position);
                if (d > HeatRange) continue;

                pursued = true;
                if (d < nearest) nearest = d;
            }

            float nearest01 = pursued ? Mathf.Clamp01(nearest / HeatRange) : 1f;
            _heat = PoliceHeat.Step(_heat, pursued, nearest01, Time.deltaTime,
                                    HeatRisePerSec, HeatDecayPerSec);

            int stars = PoliceHeat.Stars(_heat);
            if (stars == _lastStars) return;

            _lastStars = stars;
            int index = 0;
            foreach (VisualElement pip in _heatStars.Children())
            {
                pip.EnableInClassList("on", index < stars);
                index++;
            }
        }

        private void UpdateNitrous()
        {
            if (_nitrousFill == null) return;

            // Quantised to whole percent so the width is only rewritten when the meter has
            // visibly moved, not on every frame it drains a fraction.
            int pct = Mathf.RoundToInt(_bike.Nitrous01 * 100f);
            if (pct == _lastNitrous) return;

            _lastNitrous = pct;
            _nitrousFill.style.width = new Length(pct, LengthUnit.Percent);
        }

        private void UpdateSpeedAndGear()
        {
            // ForwardSpeed rather than rigidbody magnitude: the speedo should read what
            // the bike is doing along the road, not include sideways slide during a
            // drift, which would make the number jump while cornering.
            int kmh = Mathf.RoundToInt(Mathf.Abs(_bike.ForwardSpeed) * MetresPerSecondToKmh);
            if (kmh != _lastSpeed)
            {
                _lastSpeed = kmh;
                if (_speed != null) _speed.text = NumberText.Of(kmh);
            }

            // Gear is 0-based internally; riders count from one.
            int gear = _bike.Gear + 1;
            if (gear != _lastGear)
            {
                _lastGear = gear;
                if (_gear != null) _gear.text = NumberText.Of(gear);
            }
        }

        private void UpdateHealth()
        {
            if (_health == null || _healthFill == null) return;

            float health = _health.Health01;
            if (Mathf.Approximately(health, _lastHealth)) return;

            _lastHealth = health;
            _healthFill.style.width = new Length(health * 100f, LengthUnit.Percent);
            _healthFill.style.backgroundColor = new StyleColor(
                health < 0.3f ? new Color(1f, 0.2f, 0.2f) : new Color(1f, 0.2f, 0.4f));
        }

        private void UpdateRacePosition()
        {
            if (_race != null && _position != null)
            {
                int place = _race.PlayerPosition;
                if (place != _lastPosition)
                {
                    _lastPosition = place;
                    _position.text = place > 0 ? $"{place} / {_race.RacerCount}" : "-";
                }
            }

            if (_race != null && _distance != null)
            {
                // Rounded to 10 m: a metre-accurate readout changes every frame and is
                // unreadable at speed, and would defeat the change check above.
                int metres = Mathf.RoundToInt(_race.PlayerDistanceRemaining() / 10f) * 10;
                if (metres != _lastDistance)
                {
                    _lastDistance = metres;
                    _distance.text = metres > 0 ? $"{metres} m" : "FINISH";
                }
            }

            if (_session != null && _knockouts != null)
            {
                int downed = _session.Knockouts;
                if (downed != _lastKnockouts)
                {
                    _lastKnockouts = downed;
                    _knockouts.text = NumberText.Of(downed);
                }
            }

            if (_session != null && _session.Save != null && _cash != null)
            {
                int currency = _session.Save.Currency;
                if (currency != _lastCash)
                {
                    _lastCash = currency;
                    _cash.text = "$" + NumberText.Of(currency);
                }
            }
        }
    }
}
