using System;
using UnityEngine;
using HighwayRenegade.Core.Progression;

namespace HighwayRenegade.Gameplay.UI
{
    /// <summary>
    /// Interactive Garage UI managing bike tuning, part upgrades, and weapon loadouts.
    ///
    /// Communicates directly with <see cref="SaveData"/> to persist player progression
    /// across sessions without runtime allocations.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GarageUI : MonoBehaviour
    {
        [Header("State")]
        [SerializeField] private bool _isOpen;

        private SaveData _saveData;

        public bool IsOpen => _isOpen;

        public void BindSaveData(SaveData saveData) => _saveData = saveData;

        public void Open() => _isOpen = true;
        public void Close() => _isOpen = false;
        public void Toggle() => _isOpen = !_isOpen;

        /// <summary>Upgrades engine tuning (improves peak torque and max RPM).</summary>
        public bool BuyEngineUpgrade()
        {
            if (_saveData == null) return false;
            return _saveData.TryPurchaseUpgrade(ref _saveData.EngineStage, 5);
        }

        /// <summary>Upgrades racing tires (improves friction multiplier and slip peak).</summary>
        public bool BuyTireUpgrade()
        {
            if (_saveData == null) return false;
            return _saveData.TryPurchaseUpgrade(ref _saveData.TireStage, 5);
        }

        /// <summary>Upgrades braking system (improves deceleration force).</summary>
        public bool BuyBrakeUpgrade()
        {
            if (_saveData == null) return false;
            return _saveData.TryPurchaseUpgrade(ref _saveData.BrakeStage, 5);
        }

        /// <summary>Upgrades chassis armor (increases max health).</summary>
        public bool BuyArmorUpgrade()
        {
            if (_saveData == null) return false;
            return _saveData.TryPurchaseUpgrade(ref _saveData.ArmorStage, 5);
        }

        /// <summary>Equips selected weapon index (0 = Fists, 1 = Chain, 2 = Bat).</summary>
        public void EquipWeapon(int weaponIndex)
        {
            if (_saveData == null) return;
            _saveData.EquippedWeapon = Mathf.Clamp(weaponIndex, 0, 2);
        }

        /// <summary>Sets driving assist mode (0 = Full, 1 = Sport, 2 = Off).</summary>
        public void SetAssistMode(int assistIndex)
        {
            if (_saveData == null) return;
            _saveData.AssistSetting = Mathf.Clamp(assistIndex, 0, 2);
        }

        /// <summary>Toggles native gyroscope steering control.</summary>
        public void ToggleGyro(bool enabled)
        {
            if (_saveData == null) return;
            _saveData.GyroSteering = enabled;
        }

        private void OnGUI()
        {
            if (!_isOpen || _saveData == null) return;

            // Render lightweight high-contrast Garage menu
            float w = Mathf.Min(Screen.width * 0.85f, 420f);
            float h = Mathf.Min(Screen.height * 0.85f, 480f);
            float x = (Screen.width - w) * 0.5f;
            float y = (Screen.height - h) * 0.5f;

            GUI.Box(new Rect(x, y, w, h), "GARAGE & TUNING");

            GUILayout.BeginArea(new Rect(x + 15f, y + 30f, w - 30f, h - 45f));

            GUILayout.Label($"Cash: ${_saveData.Currency:N0}");
            GUILayout.Space(6f);

            // Engine Upgrade
            int eCost = SaveData.UpgradeCost(_saveData.EngineStage);
            string eText = _saveData.EngineStage < 5 ? $"Engine (Stage {_saveData.EngineStage}/5) - ${eCost}" : "Engine (MAX)";
            if (GUILayout.Button(eText) && _saveData.EngineStage < 5)
            {
                BuyEngineUpgrade();
            }

            // Tire Upgrade
            int tCost = SaveData.UpgradeCost(_saveData.TireStage);
            string tText = _saveData.TireStage < 5 ? $"Tires (Stage {_saveData.TireStage}/5) - ${tCost}" : "Tires (MAX)";
            if (GUILayout.Button(tText) && _saveData.TireStage < 5)
            {
                BuyTireUpgrade();
            }

            // Brake Upgrade
            int bCost = SaveData.UpgradeCost(_saveData.BrakeStage);
            string bText = _saveData.BrakeStage < 5 ? $"Brakes (Stage {_saveData.BrakeStage}/5) - ${bCost}" : "Brakes (MAX)";
            if (GUILayout.Button(bText) && _saveData.BrakeStage < 5)
            {
                BuyBrakeUpgrade();
            }

            // Armor Upgrade
            int aCost = SaveData.UpgradeCost(_saveData.ArmorStage);
            string aText = _saveData.ArmorStage < 5 ? $"Armor (Stage {_saveData.ArmorStage}/5) - ${aCost}" : "Armor (MAX)";
            if (GUILayout.Button(aText) && _saveData.ArmorStage < 5)
            {
                BuyArmorUpgrade();
            }

            GUILayout.Space(8f);
            GUILayout.Label($"Equipped Weapon: {(Core.Combat.WeaponType)_saveData.EquippedWeapon}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Fists")) EquipWeapon(0);
            if (GUILayout.Button("Chain")) EquipWeapon(1);
            if (GUILayout.Button("Bat")) EquipWeapon(2);
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            if (GUILayout.Button("Close Garage"))
            {
                Close();
            }

            GUILayout.EndArea();
        }
    }
}
