using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace HighwayRenegade.Gameplay.UI.Screens
{
    /// <summary>Text-only chapter beat shown before the first race of a chapter.</summary>
    public sealed class ChapterIntroScreen : UIScreen
    {
        private Label _title;
        private Label _intro;
        private Action _onContinue;

        protected override void OnBind()
        {
            _title = Require<Label>("ChapterTitle");
            _intro = Require<Label>("IntroText");
            OnClick("BtnContinue", Continue);
        }

        public void Present(string chapterTitle, string introText, Action onContinue)
        {
            _onContinue = onContinue;
            if (_title != null) _title.text = chapterTitle?.ToUpperInvariant() ?? string.Empty;
            if (_intro != null) _intro.text = introText ?? string.Empty;
            base.Show();
        }

        private void Continue()
        {
            Hide();
            _onContinue?.Invoke();
            _onContinue = null;
        }
    }
}
