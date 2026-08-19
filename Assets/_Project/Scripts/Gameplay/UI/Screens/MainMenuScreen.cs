using UnityEngine;
using HighwayRenegade.Core.App;

namespace HighwayRenegade.Gameplay.UI.Screens
{
    /// <summary>
    /// Title screen. Drives the campaign, garage, settings and exit entries in
    /// MainMenu.uxml.
    /// </summary>
    public sealed class MainMenuScreen : UIScreen
    {
        [SerializeField] private SettingsScreen _settings;
        [SerializeField] private CampaignScreen _campaign;
        [SerializeField] private QuickRaceScreen _quickRace;
        [SerializeField] private ChapterIntroScreen _chapterIntro;

        protected override void OnBind()
        {
            OnClick("BtnCampaign", OpenCampaign);
            OnClick("BtnQuickRace", OpenQuickRace);
            OnClick("BtnGarage", OpenGarage);
            OnClick("BtnExit", QuitGame);

            var settingsButton = OnClick("BtnSettings", OpenSettings);
            if (settingsButton != null && _settings == null)
            {
                settingsButton.SetEnabled(false);
                Debug.LogWarning("[MainMenuScreen] No SettingsScreen assigned, so SETTINGS " +
                                 "is disabled. Assign it in the scene.", this);
            }

            if (_campaign != null && _chapterIntro != null)
                _campaign.SetChapterIntroScreen(_chapterIntro);

            GameStateManager.ChangeState(GameState.MainMenu);
        }

        private void OpenCampaign()
        {
            if (_campaign != null) _campaign.Show();
            else Debug.LogError("[MainMenuScreen] No CampaignScreen assigned.", this);
        }

        private void OpenQuickRace()
        {
            if (_quickRace != null) _quickRace.Show();
            else Debug.LogError("[MainMenuScreen] No QuickRaceScreen assigned.", this);
        }

        private void OpenGarage()
        {
            if (GameFlowManager.Instance != null) GameFlowManager.Instance.GoToGarage();
            else GameStateManager.ChangeState(GameState.Garage);
        }

        private void OpenSettings() => _settings?.Toggle();

        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Escape)) return;

            if (_chapterIntro != null && _chapterIntro.IsOpen) { _chapterIntro.Hide(); return; }
            if (_campaign != null && _campaign.IsOpen) { _campaign.Hide(); return; }
            if (_quickRace != null && _quickRace.IsOpen) { _quickRace.Hide(); return; }
            if (_settings != null && _settings.IsOpen) { _settings.Hide(); return; }
            QuitGame();
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
