using UnityEngine;
using UnityEngine.UIElements;
using HighwayRenegade.Gameplay.Bike;
using HighwayRenegade.Core.Race;
using HighwayRenegade.Gameplay.Combat;

namespace HighwayRenegade.Gameplay.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class GameUIBinder : MonoBehaviour
    {
        [SerializeField] private BikeController _playerBike;
        [SerializeField] private Damageable _playerDamageable;

        private UIDocument _document;
        private Label _speedLabel;
        private Label _gearLabel;
        private VisualElement _healthBarFill;
        private Label _positionLabel;
        private Label _lapLabel;

        private void OnEnable()
        {
            _document = GetComponent<UIDocument>();
            var root = _document.rootVisualElement;

            if (root == null) return;

            _speedLabel = root.Q<Label>("SpeedLabel");
            _gearLabel = root.Q<Label>("GearLabel");
            _healthBarFill = root.Q<VisualElement>("HealthBarFill");
            _positionLabel = root.Q<Label>("PositionLabel");
            _lapLabel = root.Q<Label>("LapLabel");
        }

        private void Update()
        {
            if (_document == null || _document.rootVisualElement == null) return;

            if (_playerBike != null)
            {
                // ForwardSpeed rather than rigidbody magnitude: the speedo should read what
                // the bike is doing along the road, not include sideways slide during a
                // drift, which would make the number jump while cornering.
                float speedKmh = Mathf.Abs(_playerBike.ForwardSpeed) * 3.6f;
                if (_speedLabel != null) _speedLabel.text = Mathf.RoundToInt(speedKmh).ToString();

                // Gear is 0-based internally; riders count from 1.
                if (_gearLabel != null) _gearLabel.text = (_playerBike.Gear + 1).ToString();
            }

            if (_playerDamageable != null && _healthBarFill != null)
            {
                float healthPct = _playerDamageable.Health01;
                _healthBarFill.style.width = new Length(healthPct * 100f, LengthUnit.Percent);
                
                // Change color based on health
                if (healthPct < 0.3f)
                    _healthBarFill.style.backgroundColor = new StyleColor(Color.red);
                else
                    _healthBarFill.style.backgroundColor = new StyleColor(new Color(1f, 0.2f, 0.4f)); // #ff3366
            }

            // In a real scenario, TrackProgress updates Position and Lap
        }
    }
}
