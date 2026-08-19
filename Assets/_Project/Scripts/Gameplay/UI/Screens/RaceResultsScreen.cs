using UnityEngine.UIElements;
using HighwayRenegade.Core.App;
using HighwayRenegade.Core.Race;
using HighwayRenegade.Core.Story;
using HighwayRenegade.Gameplay.Progression;

namespace HighwayRenegade.Gameplay.UI.Screens
{
    /// <summary>
    /// End-of-race summary over Results.uxml — Road Rash rank sheet styling.
    /// </summary>
    public sealed class RaceResultsScreen : UIScreen
    {
        private Label _rankNumber;
        private Label _placement;
        private Label _medal;
        private Label _ruleHint;
        private Label _basePayout;
        private Label _knockoutLabel;
        private Label _combatBonus;
        private Label _repairLabel;
        private Label _repairCost;
        private Label _totalPayout;

        protected override void OnBind()
        {
            _rankNumber = Optional<Label>("RankNumber");
            _placement = Require<Label>("Placement");
            _medal = Require<Label>("Medal");
            _ruleHint = Optional<Label>("RuleHint");
            _basePayout = Require<Label>("BasePayout");
            _knockoutLabel = Require<Label>("KnockoutLabel");
            _combatBonus = Require<Label>("CombatBonus");
            _repairLabel = Require<Label>("RepairLabel");
            _repairCost = Require<Label>("RepairCost");
            _totalPayout = Require<Label>("TotalPayout");

            OnClick("BtnContinue", Continue);

            GameStateManager.OnStateChanged += HandleStateChanged;
            SetVisible(IsEndState(GameStateManager.CurrentState));
        }

        private void OnDestroy()
        {
            GameStateManager.OnStateChanged -= HandleStateChanged;
        }

        private static bool IsEndState(GameState state) =>
            state == GameState.PostRace || state == GameState.GameOver;

        private void HandleStateChanged(GameState previous, GameState current) =>
            SetVisible(IsEndState(current));

        protected override void OnShown()
        {
            var session = FindFirstObjectByType<CampaignSession>();
            if (session != null)
                Present(session.LastSummary, GameStateManager.CurrentState == GameState.GameOver);
        }

        public void Present(RaceSummary summary, bool gameOver = false)
        {
            SetText(_rankNumber, summary.IsValid && summary.Position > 0
                ? summary.Position.ToString()
                : "-");

            SetText(_placement, gameOver ? "GAME OVER"
                              : summary.IsWin ? "EVENT WON"
                              : summary.IsValid ? "RACE OVER"
                              : "RACE ABANDONED");

            SetText(_medal, MedalFor(summary.Position));
            SetText(_ruleHint, RuleHintFor(summary, gameOver));
            SetText(_basePayout, $"${summary.Prize}");
            SetText(_knockoutLabel, $"KNOCKOUTS ({summary.Knockouts})");
            SetText(_combatBonus, $"+${summary.CombatBonus}");
            SetText(_repairLabel, summary.RepairBill > 0 ? "REPAIRS DUE" : "REPAIRS (NONE)");
            SetText(_repairCost, $"-${summary.RepairBill}");
            SetText(_totalPayout, $"${summary.Balance}");
        }

        private static string MedalFor(int placement) => placement switch
        {
            1 => "GOLD MEDAL",
            2 => "SILVER MEDAL",
            3 => "BRONZE MEDAL",
            4 => "QUALIFIED",
            _ => "NO MEDAL",
        };

        private static string RuleHintFor(RaceSummary summary, bool gameOver)
        {
            if (gameOver) return "BUSTED — REPAIR OR RETIRE";
            if (!summary.IsValid) return string.Empty;
            if (summary.IsWin) return $"TOP {Campaign.RequiredFinishPosition} — CHAPTER PROGRESS";
            if (summary.Position > 0 && summary.Position <= Campaign.RequiredFinishPosition + 2)
                return $"NEED TOP {Campaign.RequiredFinishPosition} TO ADVANCE";
            return "TRY AGAIN — FIGHT HARDER NEXT LAP";
        }

        private static void SetText(Label label, string value)
        {
            if (label != null) label.text = value;
        }

        private static void Continue()
        {
            if (GameFlowManager.Instance != null) GameFlowManager.Instance.GoToMainMenu();
            else GameStateManager.ChangeState(GameState.MainMenu);
        }
    }
}
