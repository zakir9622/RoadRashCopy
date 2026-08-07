using UnityEngine;
using UnityEngine.UIElements;
using HighwayRenegade.Core.App;

namespace HighwayRenegade.Gameplay.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class MainMenuUIBinder : MonoBehaviour
    {
        private UIDocument _document;

        private void OnEnable()
        {
            _document = GetComponent<UIDocument>();
            var root = _document.rootVisualElement;

            if (root == null) return;

            var btnCampaign = root.Q<Button>("BtnCampaign");
            var btnGarage = root.Q<Button>("BtnGarage");
            var btnSettings = root.Q<Button>("BtnSettings");
            var btnExit = root.Q<Button>("BtnExit");

            if (btnCampaign != null) btnCampaign.clicked += OnCampaignClicked;
            if (btnGarage != null) btnGarage.clicked += OnGarageClicked;
            if (btnExit != null) btnExit.clicked += OnExitClicked;
        }

        private void OnDisable()
        {
            if (_document == null || _document.rootVisualElement == null) return;
            var root = _document.rootVisualElement;

            var btnCampaign = root.Q<Button>("BtnCampaign");
            var btnGarage = root.Q<Button>("BtnGarage");
            var btnExit = root.Q<Button>("BtnExit");

            if (btnCampaign != null) btnCampaign.clicked -= OnCampaignClicked;
            if (btnGarage != null) btnGarage.clicked -= OnGarageClicked;
            if (btnExit != null) btnExit.clicked -= OnExitClicked;
        }

        private void OnCampaignClicked()
        {
            GameFlowManager.Instance?.StartRace();
        }

        private void OnGarageClicked()
        {
            GameFlowManager.Instance?.GoToGarage();
        }

        private void OnExitClicked()
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
