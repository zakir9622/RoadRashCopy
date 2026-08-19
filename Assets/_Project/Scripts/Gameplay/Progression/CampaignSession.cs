using System.Collections.Generic;
using UnityEngine;
using HighwayRenegade.Core.App;
using HighwayRenegade.Core.Progression;
using HighwayRenegade.Core.Race;
using HighwayRenegade.Core.Story;
using HighwayRenegade.Gameplay.AI;
using HighwayRenegade.Gameplay.Bike;
using HighwayRenegade.Gameplay.Combat;
using HighwayRenegade.Gameplay.Race;

namespace HighwayRenegade.Gameplay.Progression
{
    /// <summary>
    /// Ties the save file to a live race. This is the component that turns a stored grudge
    /// into behaviour the player can feel.
    ///
    /// Before the race: every rival on the grid is matched to their saved record and their
    /// starting aggression is seeded from the grudge they have been carrying. A rider the
    /// player wrecked last race comes off the line already hunting.
    ///
    /// After the race: what happened is folded back into each record and written to disk.
    ///
    /// It attaches to whatever bikes exist in the scene rather than spawning them, so the
    /// placeholder test track works unchanged and a real event-driven spawner can replace
    /// the matching step later without touching the persistence logic.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CampaignSession : MonoBehaviour
    {
        [Header("Event")]
        [Tooltip("Campaign event id for this race. Blank means a free run: rivalries still " +
                 "evolve, but no campaign progress is recorded.")]
        [SerializeField] private string _eventId = "";

        [Tooltip("Purse used when this race is not a campaign event. Without it a free " +
                 "run pays nothing, which leaves the placeholder track with no economy " +
                 "at all - you can crash and be billed, but never earn.")]
        [SerializeField] private int _freeRunPurse = 800;

        [Tooltip("Cash awarded per rival the player puts down.")]
        [SerializeField] private int _knockoutBonus = 150;

        [Header("Debug")]
        [Tooltip("Ignore any save on disk and start from a clean slate. Editor testing only.")]
        [SerializeField] private bool _startFresh;

        private SaveData _save;
        private RaceManager _race;

        // Parallel arrays rather than a dictionary: the grid is a handful of riders, this
        // is walked every time a result is recorded, and it allocates nothing.
        private RivalAIController[] _rivals = System.Array.Empty<RivalAIController>();
        private RivalRecord[] _records = System.Array.Empty<RivalRecord>();
        private int[] _wreckCounts = System.Array.Empty<int>();
        private bool[] _disarmed = System.Array.Empty<bool>();

        private bool _resultRecorded;

        private int[] _playerWreckedByRival = System.Array.Empty<int>();

        /// <summary>The loaded save. Never null after Awake.</summary>
        public SaveData Save => _save;

        /// <summary>
        /// How the last finished race was costed. Invalid until the player finishes.
        ///
        /// This is the only account of the race's money. The results screen renders it
        /// rather than recomputing a prize of its own, which is what previously caused a
        /// campaign race to be paid for twice by two disagreeing formulas.
        /// </summary>
        public RaceSummary LastSummary { get; private set; }

        /// <summary>Rivals the player has put down this race.</summary>
        public int Knockouts
        {
            get
            {
                int total = 0;
                for (int i = 0; i < _wreckCounts.Length; i++) total += _wreckCounts[i];
                return total;
            }
        }

        /// <summary>Intro text for the current chapter, for the pre-race beat.</summary>
        public string ChapterIntro
        {
            get
            {
                Chapter c = Campaign.GetChapter(_save != null ? _save.ChapterIndex : 0);
                return c != null ? c.IntroText : string.Empty;
            }
        }

        private void Awake()
        {
            if (!string.IsNullOrEmpty(RaceLaunchContext.EventId))
                _eventId = RaceLaunchContext.EventId;

            _save = _startFresh ? new SaveData() : SaveService.Load();
            Campaign.EnsureRivalsRegistered(_save);
        }

        private void Start()
        {
            _race = FindFirstObjectByType<RaceManager>();
            if (_race != null) _race.PlayerFinished += OnPlayerFinished;

            var player = FindFirstObjectByType<PlayerBikeInput>();
            if (player != null && player.TryGetComponent(out BikeCrashHandler playerCrash))
            {
                playerCrash.Crashed += (severity, source) => {
                    // Going down costs the bike, not just time. This is what makes a race
                    // a wager rather than a free attempt.
                    _save.BikeCondition = RepairRules.ApplyCrashDamage(_save.BikeCondition, severity);

                    if (source != null)
                    {
                        var rival = source.GetComponentInParent<RivalAIController>();
                        if (rival != null)
                        {
                            for (int i = 0; i < _rivals.Length; i++)
                            {
                                if (_rivals[i] == rival)
                                {
                                    _playerWreckedByRival[i]++;
                                    break;
                                }
                            }
                        }
                    }
                };
            }

            BindRivals();
        }

        private void OnDestroy()
        {
            if (_race != null) _race.PlayerFinished -= OnPlayerFinished;
        }

        /// <summary>
        /// Matches scene rivals to saved records and applies their carried grudge.
        /// </summary>
        private void BindRivals()
        {
            _rivals = FindObjectsByType<RivalAIController>(FindObjectsSortMode.None);
            int n = _rivals.Length;

            _records = new RivalRecord[n];
            _wreckCounts = new int[n];
            _disarmed = new bool[n];
            _playerWreckedByRival = new int[n];

            for (int i = 0; i < n; i++)
            {
                // Roster order is the fallback pairing for the placeholder track, which has
                // generic "Rival 1/2/3" objects rather than named campaign riders.
                RivalDefinition def = i < Campaign.Roster.Length ? Campaign.Roster[i] : null;
                if (def == null) continue;

                RivalRecord record = _save.GetOrCreateRival(def.Id, def.Name);
                _records[i] = record;

                _rivals[i].ApplyProfile(def.Name, def.Skill,
                                        RivalMemory.StartingAggression(record),
                                        RivalMemory.HuntsThePlayer(record));

                if (_rivals[i].TryGetComponent(out MeleeCombat combat))
                    combat.SetWeapon((HighwayRenegade.Core.Combat.WeaponType)record.Weapon);

                if (_rivals[i].TryGetComponent(out BikeCrashHandler crash))
                {
                    // Copy the loop variable: a lambda would otherwise capture the shared
                    // `i` and every rival would credit its wrecks to the last slot.
                    int index = i;
                    crash.Crashed += (severity, source) => {
                        if (source != null && source.GetComponentInParent<PlayerBikeInput>() != null)
                            _wreckCounts[index]++;
                    };
                }
            }
        }

        private void OnPlayerFinished(int position)
        {
            if (_resultRecorded) return;
            _resultRecorded = true;

            RecordResults(position);
            SaveService.Save(_save);

            // Crossing the line has to move the app into PostRace, exactly as a bust does
            // in OnPlayerBusted. Without this the race set Phase = Finished, computed the
            // payout, saved - and then left the player on a dead track with the HUD still
            // up and no results screen, because RaceResultsScreen only shows on the
            // PostRace state change. GameOver takes precedence: a wrecked, unaffordable
            // bike is the end of the run, not a results-then-continue.
            HighwayRenegade.Core.App.GameStateManager.ChangeState(
                IsGameOver ? HighwayRenegade.Core.App.GameState.GameOver
                           : HighwayRenegade.Core.App.GameState.PostRace);
        }

        /// <summary>
        /// Busted by the police: the race ends and the fine comes out of the balance.
        ///
        /// Operates on the live session save rather than reloading from disk. The old code
        /// called SaveService.Load(), fined that copy and wrote it back, which silently
        /// discarded everything this race had accumulated - grudges, wreck counts, bike
        /// damage - because the in-memory _save was never the object being saved.
        /// </summary>
        public void OnPlayerBusted()
        {
            int fine = RaceRules.BustedPenalty(_save.Currency);
            _save.Currency -= fine;
            if (_save.Currency < 0) _save.Currency = 0;

            Debug.Log($"[CampaignSession] Player BUSTED. Fine ${fine}. Remaining: ${_save.Currency}.");

            _resultRecorded = true;      // a bust is a result: no prize money, no re-entry
            SaveService.Save(_save);

            HighwayRenegade.Core.App.GameStateManager.ChangeState(
                IsGameOver ? HighwayRenegade.Core.App.GameState.GameOver
                           : HighwayRenegade.Core.App.GameState.PostRace);
        }

        /// <summary>
        /// True when the player cannot get back on the road: the bike is wrecked and the
        /// repair is unaffordable. This is Road Rash's actual fail state - you lose by
        /// going broke, not by losing a race.
        /// </summary>
        public bool IsGameOver =>
            _save != null && RepairRules.IsGameOver(_save.Currency, _save.BikeCondition, CurrentBikeValue);

        /// <summary>Shop price of the bike currently being ridden, for scaling repairs.</summary>
        public int CurrentBikeValue
        {
            get
            {
                BikeDef def = _save != null ? BikeShop.GetBike(_save.BikeId) : null;
                return def != null ? def.Price : 0;
            }
        }

        /// <summary>Cost to restore the current bike to pristine.</summary>
        public int RepairCost => _save != null
            ? RepairRules.RepairCost(_save.BikeCondition, CurrentBikeValue)
            : 0;

        /// <summary>
        /// Buys as much repair as the player can afford and returns what it cost. A player
        /// who cannot cover a full repair still gets partial value rather than a hard wall.
        /// </summary>
        public int RepairBike()
        {
            if (_save == null) return 0;

            float target = RepairRules.AffordableCondition(_save.Currency, _save.BikeCondition, CurrentBikeValue);
            if (target <= _save.BikeCondition) return 0;

            // Charge for the repair actually performed, not the full bill.
            int spent = RepairRules.RepairCost(_save.BikeCondition, CurrentBikeValue)
                      - RepairRules.RepairCost(target, CurrentBikeValue);
            if (spent > _save.Currency) spent = _save.Currency;

            _save.Currency -= spent;
            _save.BikeCondition = target;
            SaveService.Save(_save);
            return spent;
        }

        /// <summary>
        /// Folds the race into every rival's permanent record, then advances the campaign
        /// if this event was won.
        /// </summary>
        private void RecordResults(int playerPosition)
        {
            _save.RacesFinished++;

            for (int i = 0; i < _rivals.Length; i++)
            {
                RivalRecord record = _records[i];
                if (record == null || _rivals[i] == null) continue;

                // Compare actual finishing positions. Assuming the player beat everyone
                // just because they finished would log a loss against rivals who genuinely
                // won, and those rivals would wrongly shed grudge for a race they took.
                int rivalPosition = _race != null
                    ? _race.PositionOf(_rivals[i].GetComponent<BikeController>())
                    : 0;

                bool playerAhead = rivalPosition <= 0 || playerPosition < rivalPosition;

                RivalMemory.ApplyRaceOutcome(record, new RaceOutcome(
                    playerFinishedAhead: playerAhead,
                    wreckedByPlayer: _wreckCounts[i],
                    disarmedByPlayer: _disarmed[i],
                    wreckedThePlayer: _playerWreckedByRival[i] > 0));

                // A disarmed rival turns up next race with whatever they are left holding,
                // which is how a stolen bat stays stolen.
                if (_rivals[i].TryGetComponent(out MeleeCombat combat))
                    record.Weapon = (int)combat.Weapon;
            }

            RaceEvent evt = Campaign.FindEvent(_eventId);

            // A free run still pays, just from a default purse rather than an event's.
            // Racing for nothing is not a game loop.
            int purse = evt != null ? evt.Purse : _freeRunPurse;

            int prize = RaceRules.PrizeMoney(playerPosition, purse);
            int knockouts = Knockouts;
            int combatBonus = knockouts * _knockoutBonus;

            // Quoted with the same rules the garage bills, so the figure the player is
            // shown is the figure they are actually charged.
            int repairBill = RepairCost;

            // Clamped at zero: a repair bill bigger than the purse leaves the player
            // broke, never in debt, which nothing downstream knows how to represent.
            _save.Currency = Mathf.Max(0, _save.Currency + prize + combatBonus);

            LastSummary = new RaceSummary(playerPosition, knockouts, prize, combatBonus,
                                          repairBill, fine: 0, balance: _save.Currency);

            if (evt == null) return;                       // free run: no campaign progress

            if (playerPosition > 0 && playerPosition <= evt.RequiredPosition)
            {
                _save.MarkCompleted(evt.Id);
                if (Campaign.TryAdvanceChapter(_save))
                    Debug.Log($"[Campaign] Chapter cleared. Now on chapter {_save.ChapterIndex}.");
            }
        }

        /// <summary>Notes that the player took a weapon from a rival, for the grudge.</summary>
        public void NotifyDisarmed(RivalAIController rival)
        {
            for (int i = 0; i < _rivals.Length; i++)
                if (_rivals[i] == rival) { _disarmed[i] = true; return; }
        }

        /// <summary>
        /// One line per rival describing where things stand, for the pre-race beat.
        /// Only rivals who actually have history are worth mentioning.
        /// </summary>
        public void GetRivalryBeats(List<string> into)
        {
            if (into == null) return;
            into.Clear();
            if (_save?.Rivals == null) return;

            for (int i = 0; i < _save.Rivals.Count; i++)
            {
                RivalRecord r = _save.Rivals[i];
                if (r == null) continue;
                if (RivalMemory.RelationshipOf(r) == Relationship.Neutral) continue;

                into.Add(RivalMemory.DescribeRivalry(r));
            }
        }
    }
}
