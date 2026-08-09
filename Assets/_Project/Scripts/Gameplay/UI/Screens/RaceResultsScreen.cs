using UnityEngine.UIElements;
using HighwayRenegade.Core.App;
using HighwayRenegade.Core.Race;
using HighwayRenegade.Gameplay.Progression;

namespace HighwayRenegade.Gameplay.UI.Screens
{
    /// <summary>
    /// End-of-race summary over Results.uxml.
    ///
    /// This screen renders and nothing else. The version it replaces computed its own
    /// prize money and added it to the balance from inside its display method - which ran
    /// on every entry into PostRace, while CampaignSession was separately awarding the
    /// event purse for the same race. A campaign race was therefore paid twice, by two
    /// formulas that disagreed, and the total shown matched neither.
    ///
    /// Now the race is costed once, by CampaignSession, into a read-only
    /// <see cref="RaceSummary"/>. This screen displays that value. It has no way to pay
    /// anybody, so showing it twice cannot pay twice.
    /// </summary>
    public sealed class RaceResultsScreen : UIScreen
    {
        private Label _placement;
        private Label _medal;
        private Label _basePayout;
        private Label _knockoutLabel;
        private Label _combatBonus;
        private Label _repairLabel;
        private Label _repairCost;
        private Label _totalPayout;

        protected override void OnBind()
        {
            _placement = Require<Label>("Placement");
            _medal = Require<Label>("Medal");
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
            // A static event outlives the scene, so an unsubscribed screen goes on being
            // invoked after its GameObject is destroyed.
            GameStateManager.OnStateChanged -= HandleStateChanged;
        }

        // GameOver as well as PostRace. GameOver was reachable - a police bust while broke
        // and wrecked sets it (CampaignSession.OnPlayerBusted / OnPlayerFinished) - but no
        // screen handled it, so every screen hid and the player was left on a live race
        // with no way out. The results screen already shows the final balance and its
        // Continue button already returns to the menu, so it is the natural home for the
        // end-of-run state; it just needs to say so.
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

        /// <summary>
        /// Draws a costed race. Pure presentation - safe to call as often as you like.
        /// </summary>
        public void Present(RaceSummary summary, bool gameOver = false)
        {
            SetText(_placement, gameOver ? "GAME OVER"
                              : summary.IsWin ? "WINNER"
                              : summary.IsValid ? $"FINISHED P{summary.Position}"
                              : "RACE ABANDONED");

            SetText(_medal, MedalFor(summary.Position));
            SetText(_basePayout, $"${summary.Prize}");
            SetText(_knockoutLabel, $"KNOCKOUTS ({summary.Knockouts})");
            SetText(_combatBonus, $"+${summary.CombatBonus}");
            SetText(_repairLabel, summary.RepairBill > 0 ? "REPAIRS DUE" : "REPAIRS (NONE)");
            SetText(_repairCost, $"-${summary.RepairBill}");

            // The balance, not the delta: after a bad race the delta is negative and
            // reads as a debt the player does not actually owe.
            SetText(_totalPayout, $"${summary.Balance}");
        }

        private static string MedalFor(int placement) => placement switch
        {
            1 => "GOLD",
            2 => "SILVER",
            3 => "BRONZE",
            _ => "NO MEDAL",
        };

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
