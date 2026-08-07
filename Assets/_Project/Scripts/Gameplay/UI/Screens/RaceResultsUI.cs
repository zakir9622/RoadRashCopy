using UnityEngine;
using UnityEngine.UI;
using HighwayRenegade.Core.Progression;
using HighwayRenegade.Core.App;
using HighwayRenegade.Gameplay.Progression;

namespace HighwayRenegade.Gameplay.UI.Screens
{
    public class RaceResultsUI : MonoBehaviour
    {
        [SerializeField] private Text _txtPlacement;
        [SerializeField] private Text _txtPayout;
        [SerializeField] private Text _txtKnockouts;
        [SerializeField] private Text _txtMedal;
        [SerializeField] private Button _btnContinue;

        private void Start()
        {
            if (_btnContinue != null) _btnContinue.onClick.AddListener(OnContinueClicked);
            
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
                var race = FindFirstObjectByType<HighwayRenegade.Gameplay.Race.RaceManager>();
                int place = race != null && race.PlayerPosition > 0 ? race.PlayerPosition : 1;
                DisplayResults(place, 2); // Sample 2 knockouts
            }
        }

        public void DisplayResults(int placement, int knockouts = 0)
        {
            string medal = placement == 1 ? "🥇 GOLD TROPHY" : (placement == 2 ? "🥈 SILVER TROPHY" : (placement == 3 ? "🥉 BRONZE TROPHY" : "4TH+ FINISHER"));
            if (_txtMedal != null) _txtMedal.text = medal;
            if (_txtPlacement != null) _txtPlacement.text = $"POSITION: P{placement}";
            
            int basePayout = CalculatePayout(placement);
            int combatBonus = knockouts * 150;
            int total = basePayout + combatBonus;

            if (_txtKnockouts != null) _txtKnockouts.text = $"Knockouts ({knockouts}): +${combatBonus}";
            if (_txtPayout != null) _txtPayout.text = $"TOTAL PRIZE: ${total}\n(Race: ${basePayout} | Combat: ${combatBonus})";
            
            SaveData save = SaveService.Load();
            save.Currency += total;
            SaveService.Save(save);
        }

        private int CalculatePayout(int placement)
        {
            switch (placement)
            {
                case 1: return 1000;
                case 2: return 600;
                case 3: return 350;
                default: return 100;
            }
        }

        private void OnContinueClicked()
        {
            GameStateManager.ChangeState(GameState.MainMenu);
        }
    }
}
