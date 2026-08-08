using System;
using UnityEngine;

namespace HighwayRenegade.Core.App
{
    public enum GameState
    {
        Booting,
        MainMenu,
        Garage,
        LoadingRace,
        Racing,
        PostRace,

        /// <summary>
        /// The run is over: the bike is wrecked and the repair is unaffordable. Distinct
        /// from PostRace because it is terminal - there is no "next race" to go to.
        /// </summary>
        GameOver,

        /// <summary>
        /// The game clock is stopped and the pause modal is up.
        ///
        /// Appended rather than slotted next to Racing on purpose: enum members are
        /// persisted by ordinal in save data, so inserting one in the middle silently
        /// reinterprets every existing save.
        /// </summary>
        Paused
    }

    /// <summary>
    /// Global state machine for the application flow.
    /// Provides events for systems to cleanly initialize or cleanup when the global context changes.
    /// </summary>
    public static class GameStateManager
    {
        public static GameState CurrentState { get; private set; } = GameState.Booting;

        public static event Action<GameState, GameState> OnStateChanged;

        public static void ChangeState(GameState newState)
        {
            if (CurrentState == newState) return;

            GameState oldState = CurrentState;
            CurrentState = newState;

            Debug.Log($"[GameState] Transition: {oldState} -> {newState}");
            OnStateChanged?.Invoke(oldState, newState);
        }
    }
}
