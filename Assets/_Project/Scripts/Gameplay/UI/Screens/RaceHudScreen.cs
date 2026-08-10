using UnityEngine;
using UnityEngine.UIElements;
using HighwayRenegade.Gameplay.Bike;
using HighwayRenegade.Gameplay.Combat;
using HighwayRenegade.Gameplay.Race;

namespace HighwayRenegade.Gameplay.UI.Screens
{
    /// <summary>
    /// In-race HUD over GameUI.uxml: speed, gear, bike condition, nitrous, position, distance
    /// to the finish, knockouts and cash.
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

        private BikeController _bike;
        private Damageable _health;
        private RaceManager _race;
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
            UpdateRacePosition();
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
