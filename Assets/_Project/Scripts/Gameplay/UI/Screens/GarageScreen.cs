using UnityEngine;
using UnityEngine.UIElements;
using HighwayRenegade.Core.App;
using HighwayRenegade.Core.Progression;
using HighwayRenegade.Gameplay.Progression;

namespace HighwayRenegade.Gameplay.UI.Screens
{
    /// <summary>
    /// Bike selection, purchase and repair over Garage.uxml.
    ///
    /// Two things changed beyond the move to UI Toolkit.
    ///
    /// Buying used to set BikeId without recording the purchase, so the only bike you
    /// were known to own was the one you were sitting on - switching back to a bike you
    /// had already paid for charged you for it again. Ownership is now tracked in the
    /// save, and BUY turns into EQUIP for anything already bought.
    ///
    /// The screen also opened on whichever bike happened to be first in the catalogue
    /// rather than the one being ridden, so the stats on show belonged to someone else's
    /// bike until you clicked.
    /// </summary>
    public sealed class GarageScreen : UIScreen
    {
        private Label _cash;
        private Label _bikeName;
        private Label _ownership;
        private Label _speedStat;
        private Label _accelStat;
        private Label _handlingStat;
        private Label _conditionStat;
        private Button _buy;

        private int _index;

        protected override void OnBind()
        {
            _cash = Require<Label>("Cash");
            _bikeName = Require<Label>("BikeName");
            _ownership = Require<Label>("Ownership");
            _speedStat = Require<Label>("SpeedStat");
            _accelStat = Require<Label>("AccelStat");
            _handlingStat = Require<Label>("HandlingStat");
            _conditionStat = Require<Label>("ConditionStat");

            OnClick("BtnPrevBike", () => Cycle(-1));
            OnClick("BtnNextBike", () => Cycle(1));
            _buy = OnClick("BtnBuyBike", BuyOrEquip);
            OnClick("BtnRepair", Repair);
            OnClick("BtnBack", Back);

            // Open on the bike actually being ridden, not catalogue entry zero.
            _index = BikeShop.IndexOf(SaveService.Load().BikeId);

            GameStateManager.OnStateChanged += HandleStateChanged;
            SetVisible(GameStateManager.CurrentState == GameState.Garage);
        }

        private void OnDestroy()
        {
            GameStateManager.OnStateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(GameState previous, GameState current) =>
            SetVisible(current == GameState.Garage);

        protected override void OnShown() => Refresh();

        private void Cycle(int direction)
        {
            int count = BikeShop.AvailableBikes.Count;
            if (count == 0) return;

            // Adding count before the modulo keeps -1 from landing on a negative index.
            _index = (_index + direction + count) % count;
            Refresh();
        }

        private void Refresh()
        {
            SaveData save = SaveService.Load();
            BikeDef bike = BikeShop.AvailableBikes[_index];

            bool owned = save.Owns(bike.Id);
            bool equipped = save.BikeId == bike.Id;
            bool affordable = save.Currency >= bike.Price;

            SetText(_cash, $"CASH: ${save.Currency}");
            SetText(_bikeName, $"{bike.Name.ToUpperInvariant()} - {(bike.Price == 0 ? "FREE" : $"${bike.Price}")}");
            SetText(_ownership, equipped ? "EQUIPPED"
                              : owned ? "OWNED"
                              : affordable ? "AVAILABLE"
                              : "NEED MORE CASH");

            SetText(_speedStat, $"{bike.TopSpeed:F0} KM/H");
            SetText(_accelStat, $"{bike.Acceleration:F1} G");
            SetText(_handlingStat, $"{bike.Handling:F1} / 10");

            // Condition belongs to the bike being ridden, so it is only meaningful while
            // that is the one on screen.
            SetText(_conditionStat, equipped ? $"{save.BikeCondition:P0}" : "-");

            if (_buy != null)
            {
                _buy.text = equipped ? "EQUIPPED" : owned ? "EQUIP" : "BUY";
                _buy.SetEnabled(!equipped && (owned || affordable));
            }
        }

        /// <summary>
        /// Buys the selected bike, or simply equips it when it has already been paid for.
        /// </summary>
        private void BuyOrEquip()
        {
            SaveData save = SaveService.Load();
            BikeDef bike = BikeShop.AvailableBikes[_index];

            if (!save.Owns(bike.Id))
            {
                if (save.Currency < bike.Price)
                {
                    Debug.LogWarning($"[Garage] {bike.Name} costs ${bike.Price}, balance is ${save.Currency}.");
                    return;
                }

                save.Currency -= bike.Price;
                save.MarkOwned(bike.Id);

                // A newly bought bike is undamaged; carrying the old bike's wear across
                // would bill the player for damage this machine never took.
                save.BikeCondition = RepairRules.PristineCondition;
            }

            save.BikeId = bike.Id;
            SaveService.Save(save);
            Refresh();
        }

        /// <summary>
        /// Repairs the bike, priced by how broken it is and what the bike is worth.
        ///
        /// This used to charge a flat $50 and change nothing else: the money vanished and
        /// the bike was exactly as damaged afterwards. Now it moves BikeCondition, and a
        /// player who cannot cover the full bill buys as much repair as they can afford
        /// rather than being stuck behind a price they can never reach.
        /// </summary>
        private void Repair()
        {
            SaveData save = SaveService.Load();
            int bikeValue = BikeShop.ValueOf(save.BikeId);

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
            Refresh();

            Debug.Log($"[Garage] Repaired to {target:P0} for ${spent}.");
        }

        private static void Back()
        {
            if (GameFlowManager.Instance != null) GameFlowManager.Instance.GoToMainMenu();
            else GameStateManager.ChangeState(GameState.MainMenu);
        }

        private static void SetText(Label label, string value)
        {
            if (label != null) label.text = value;
        }
    }
}
