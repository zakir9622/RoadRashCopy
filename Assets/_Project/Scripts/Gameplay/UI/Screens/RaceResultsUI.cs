using UnityEngine;
using UnityEngine.UI;
using HighwayRenegade.Core.App;
using HighwayRenegade.Gameplay.Progression;

namespace HighwayRenegade.Gameplay.UI.Screens
{
    public class RaceResultsUI : MonoBehaviour
    {
        [SerializeField] private Text _txtPlacement;
        [SerializeField] private Text _txtPayout;
        [SerializeField] private Button _btnContinue;

        private void Start()
        {
            _btnContinue.onClick.AddListener(OnContinueClicked);
            
            GameStateManager.OnStateChanged += OnStateChanged;
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            GameStateManager.OnStateChanged -= OnStateChanged;
        }

        private void OnStateChanged(GameState oldState, GameState newState)
        {
            gameObject.SetActive(newState == GameState.PostRace);
            
            if (newState == GameState.PostRace)
            {
                DisplayResults(1); // Hardcoded placement for now
            }
        }

        public void DisplayResults(int placement)
        {
            _txtPlacement.text = $"You Finished: {placement}";
            
            int payout = CalculatePayout(placement);
            _txtPayout.text = $"Prize Money: ${payout}";
            
            SaveData save = SaveService.Load();
            save.PlayerCash += payout;
            SaveService.Save(save);
        }

        private int CalculatePayout(int placement)
        {
            switch (placement)
            {
                case 1: return 1000;
                case 2: return 500;
                case 3: return 250;
                default: return 50;
            }
        }

        private void OnContinueClicked()
        {
            GameStateManager.ChangeState(GameState.MainMenu);
        }
    }
}
