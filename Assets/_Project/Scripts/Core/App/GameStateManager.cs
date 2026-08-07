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
        GameOver
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
