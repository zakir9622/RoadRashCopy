using UnityEngine;
using UnityEngine.UI;
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
            _btnBack.onClick.AddListener(OnBackClicked);
            _btnRepair.onClick.AddListener(OnRepairClicked);
            
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
            
            if (save.PlayerCash >= bike.Price)
            {
                save.PlayerCash -= bike.Price;
                // Ideally add to owned bikes, but for now just set active.
                save.ActiveBikeId = bike.Id;
                SaveService.Save(save);
                Debug.Log($"[Garage] Bought {bike.Name}!");
                RefreshUI();
            }
            else
            {
                Debug.LogWarning("[Garage] Not enough cash!");
            }
        }

        private void RefreshUI()
        {
            SaveData save = SaveService.Load();
            if (_txtCash != null)
                _txtCash.text = $"Cash: ${save.PlayerCash}";

            var bike = BikeShop.AvailableBikes[_currentBikeIndex];
            if (_txtBikeInfo != null)
            {
                _txtBikeInfo.text = $"{bike.Name}\nPrice: ${bike.Price}\nTop Speed: {bike.TopSpeed}";
            }
        }

        private void OnRepairClicked()
        {
            SaveData save = SaveService.Load();
            if (save.PlayerCash >= 50)
            {
                save.PlayerCash -= 50;
                SaveService.Save(save);
                RefreshUI();
                Debug.Log("[Garage] Bike repaired.");
            }
        }

        private void OnBackClicked()
        {
            GameStateManager.ChangeState(GameState.MainMenu);
        }
    }
}
