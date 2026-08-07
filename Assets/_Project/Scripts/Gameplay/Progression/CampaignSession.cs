using System.Collections.Generic;
using UnityEngine;
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
                    crash.Crashed += (severity, source) => {
                        if (source != null && source.GetComponentInParent<PlayerBikeInput>() != null)
                            _wreckCounts[index]++;
                    };
            }
        }

        private void OnPlayerFinished(int position)
        {
            if (_resultRecorded) return;
            _resultRecorded = true;

            RecordResults(position);
            SaveService.Save(_save);
        }

        public void OnPlayerBusted()
        {
            SaveData save = SaveService.Load();
            int fine = 200;
            
            save.PlayerCash -= fine;
            if (save.PlayerCash < 0)
            {
                save.PlayerCash = 0;
                Debug.LogError("[CampaignSession] GAME OVER! Player couldn't pay the police fine.");
                // Transition to Game Over screen in real implementation
            }
            else
            {
                Debug.Log($"[CampaignSession] Player BUSTED! Paid ${fine} fine. Remaining cash: ${save.PlayerCash}");
            }
            
            SaveService.Save(save);
            
            // End race
            HighwayRenegade.Core.App.GameStateManager.ChangeState(HighwayRenegade.Core.App.GameState.PostRace);
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
            if (evt == null) return;                       // free run: no campaign progress

            _save.Currency += RaceRules.PrizeMoney(playerPosition, evt.Purse);

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
