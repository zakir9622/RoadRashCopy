using UnityEngine;
using UnityEngine.UI;
using HighwayRenegade.Core.Progression;
using HighwayRenegade.Core.App;
using HighwayRenegade.Gameplay.Progression;

namespace HighwayRenegade.Gameplay.UI.Screens
{
    public class GarageScreen : MonoBehaviour
    {
        [SerializeField] private Button _btnBack;
        [SerializeField] private Button _btnRepair;
        [SerializeField] private Button _btnNextBike;
        [SerializeField] private Button _btnPrevBike;
        [SerializeField] private Button _btnBuyBike;
        [SerializeField] private Text _txtCash;
        [SerializeField] private Text _txtBikeInfo;

        private int _currentBikeIndex = 0;

        private void Start()
        {
            if (_btnBack != null) _btnBack.onClick.AddListener(OnBackClicked);
            if (_btnRepair != null) _btnRepair.onClick.AddListener(OnRepairClicked);
            
            if (_btnNextBike != null) _btnNextBike.onClick.AddListener(OnNextBikeClicked);
            if (_btnPrevBike != null) _btnPrevBike.onClick.AddListener(OnPrevBikeClicked);
            if (_btnBuyBike != null) _btnBuyBike.onClick.AddListener(OnBuyBikeClicked);

            GameStateManager.OnStateChanged += OnStateChanged;
            gameObject.SetActive(false); // Hidden by default
        }

        private void OnDestroy()
        {
            GameStateManager.OnStateChanged -= OnStateChanged;
        }

        private void OnStateChanged(GameState oldState, GameState newState)
        {
            gameObject.SetActive(newState == GameState.Garage);
            
            if (newState == GameState.Garage)
            {
                RefreshUI();
            }
        }

        private void OnNextBikeClicked()
        {
            _currentBikeIndex = (_currentBikeIndex + 1) % BikeShop.AvailableBikes.Count;
            RefreshUI();
        }

        private void OnPrevBikeClicked()
        {
            _currentBikeIndex--;
            if (_currentBikeIndex < 0) _currentBikeIndex = BikeShop.AvailableBikes.Count - 1;
            RefreshUI();
        }

        private void OnBuyBikeClicked()
        {
            SaveData save = SaveService.Load();
            var bike = BikeShop.AvailableBikes[_currentBikeIndex];
            
            if (save.Currency >= bike.Price)
            {
                save.Currency -= bike.Price;
                // Ideally add to owned bikes, but for now just set active.
                save.BikeId = bike.Id;
                SaveService.Save(save);
                Debug.Log($"[Garage] Bought {bike.Name}!");
                RefreshUI();
            }
            else
            {
                Debug.LogWarning("[Garage] Not enough cash!");
            }
        }

        [SerializeField] private Text _txtSpeedStat;
        [SerializeField] private Text _txtAccelStat;
        [SerializeField] private Text _txtHandlingStat;
        [SerializeField] private Text _txtOwnershipStatus;

        private void RefreshUI()
        {
            SaveData save = SaveService.Load();
            if (_txtCash != null)
                _txtCash.text = $"CASH: ${save.Currency}";

            var bike = BikeShop.AvailableBikes[_currentBikeIndex];
            bool isOwned = save.BikeId == bike.Id;

            if (_txtBikeInfo != null)
            {
                string price = bike.Price == 0 ? "FREE" : $"${bike.Price}";
                _txtBikeInfo.text = $"{bike.Name.ToUpper()}\nPRICE: {price}";
            }

            if (_txtOwnershipStatus != null)
            {
                _txtOwnershipStatus.text = isOwned ? "✅ EQUIPPED" : (save.Currency >= bike.Price ? "🟢 AVAILABLE TO BUY" : "🔴 LOCKED (NEED CASH)");
            }

            // Stat bars visual representation
            if (_txtSpeedStat != null) _txtSpeedStat.text = $"TOP SPEED: {bike.TopSpeed} KM/H";
            if (_txtAccelStat != null) _txtAccelStat.text = $"ACCELERATION: {bike.Acceleration:F1} G";
            if (_txtHandlingStat != null) _txtHandlingStat.text = $"HANDLING: {bike.Handling:F1} / 10";
        }

        /// <summary>
        /// Repairs the bike, priced by how broken it is and what the bike is worth.
        ///
        /// This used to charge a flat $50 and change nothing else: the money vanished and
        /// the bike was exactly as damaged afterwards. Now it moves BikeCondition, and a
        /// player who cannot cover the full bill buys as much repair as they can afford
        /// rather than being stuck behind a price they can never reach.
        /// </summary>
        private void OnRepairClicked()
        {
            SaveData save = SaveService.Load();
            int bikeValue = BikeShop.GetBike(save.BikeId)?.Price ?? 0;

            if (save.BikeCondition >= RepairRules.PristineCondition)
            {
                Debug.Log("[Garage] Bike is already in perfect condition.");
                return;
            }

            float target = RepairRules.AffordableCondition(save.Currency, save.BikeCondition, bikeValue);
            if (target <= save.BikeCondition)
            {
                Debug.LogWarning("[Garage] Not enough cash to repair anything.");
                return;
            }

            // Charge for the repair actually performed, not the full bill.
            int spent = RepairRules.RepairCost(save.BikeCondition, bikeValue)
                      - RepairRules.RepairCost(target, bikeValue);
            if (spent > save.Currency) spent = save.Currency;

            save.Currency -= spent;
            save.BikeCondition = target;
            SaveService.Save(save);
            RefreshUI();

            Debug.Log($"[Garage] Repaired to {target:P0} for ${spent}.");
        }

        private void OnBackClicked()
        {
            GameStateManager.ChangeState(GameState.MainMenu);
        }
    }
}
