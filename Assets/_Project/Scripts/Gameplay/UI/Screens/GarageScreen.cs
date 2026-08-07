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
        [SerializeField] private Text _txtCash;

        private void Start()
        {
            _btnBack.onClick.AddListener(OnBackClicked);
            _btnRepair.onClick.AddListener(OnRepairClicked);

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

        private void RefreshUI()
        {
            SaveData save = SaveService.Load();
            if (_txtCash != null)
                _txtCash.text = $"Cash: ${save.PlayerCash}";
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
