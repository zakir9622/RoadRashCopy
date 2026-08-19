using UnityEngine;
using HighwayRenegade.Core.Progression;
using HighwayRenegade.Core.Vehicle;
using HighwayRenegade.Gameplay.Bike;

namespace HighwayRenegade.Gameplay.Progression
{
    /// <summary>
    /// Applies the player's saved bike, upgrades and bike condition to the machine they
    /// actually ride.
    ///
    /// Why this exists: the garage let the player spend money on engine and exhaust
    /// stages, SaveData recorded them, and Powertrain.ApplyUpgrades implemented them - but
    /// nothing ever called it, and BikeController.SetEngineSpec had no callers at all.
    /// Every upgrade the player bought was money burnt for no effect, and switching bikes
    /// changed nothing but a string in the save file. This is the missing link between
    /// progression and physics.
    ///
    /// Attach to the player's bike. Runs before the race starts.
    /// </summary>
    [RequireComponent(typeof(BikeController))]
    [DisallowMultipleComponent]
    public sealed class BikeLoadout : MonoBehaviour
    {
        [Tooltip("Apply the condition penalty from the save. Off means a pristine bike, " +
                 "which is useful when testing handling without the economy in the way.")]
        [SerializeField] private bool _applyConditionPenalty = true;

        private BikeController _bike;

        /// <summary>The spec actually installed, after upgrades and wear.</summary>
        public EngineSpec InstalledSpec { get; private set; }

        private void Awake() => _bike = GetComponent<BikeController>();

        private void Start()
        {
            SaveData save = SaveService.Load();
            if (save == null) return;

            InstalledSpec = BuildSpec(save, _applyConditionPenalty);
            _bike.SetEngineSpec(InstalledSpec);
            TyreSpec tyres = BuildTyreSpec(save);
            _bike.SetTyreSpec(tyres, tyres);
        }

        public static TyreSpec BuildTyreSpec(SaveData save) =>
            TyreModel.ApplyStage(TyreSpec.SportRoad, save?.TireStage ?? 0);

        /// <summary>
        /// Composes the ridden spec from the save: base bike, then purchased upgrades,
        /// then wear. Static and free of scene state so the result can be unit-tested.
        /// </summary>
        public static EngineSpec BuildSpec(SaveData save, bool applyCondition = true)
        {
            EngineSpec spec = BaseSpecFor(save.BikeId);
            spec = Powertrain.ApplyUpgrades(spec, save.EngineStage, 0);

            if (applyCondition)
            {
                // Wear is modelled as lost torque rather than a speed cap: a tired engine
                // should feel gutless out of corners, not fine until it hits a wall.
                float multiplier = RepairRules.PerformanceMultiplier(save.BikeCondition);
                spec.PeakTorqueNm *= multiplier;
            }

            return spec;
        }

        /// <summary>
        /// Maps a shop bike onto a physical engine specification.
        ///
        /// BikeShop describes bikes in abstract ratings while the physics wants real
        /// torque curves and gearing, so the mapping is explicit rather than derived from
        /// those numbers - an invented conversion would make the shop's stated ratings
        /// disagree with how the bike actually rides.
        /// </summary>
        public static EngineSpec BaseSpecFor(string bikeId)
        {
            switch (bikeId)
            {
                case "bike_super":  return EngineSpec.Superbike;
                case "bike_sport":  return EngineSpec.Streetfighter;
                case "bike_rat":    return StarterSpec();
                default:            return StarterSpec();
            }
        }

        /// <summary>
        /// The free starter: a soft, short-geared 250 that is forgiving and slow. Derived
        /// from the Streetfighter so the two share a character, then detuned.
        /// </summary>
        private static EngineSpec StarterSpec()
        {
            EngineSpec spec = EngineSpec.Streetfighter;
            spec.PeakTorqueNm *= 0.55f;
            spec.RedlineRpm *= 0.9f;
            return spec;
        }
    }
}
