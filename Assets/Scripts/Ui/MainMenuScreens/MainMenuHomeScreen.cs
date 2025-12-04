using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Helper;
using Managers;
using Network;
using TMPro;
using UIArchitecture;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Object = System.Object;

namespace Ui.MainMenuScreens
{
    public class MainMenuHomeScreen : FullScreenView
    {

        [Serializable]
        public class HeaderButtons
        {
            public enum MainMenuHeaders
            {
                Account,
                PublicGame,
                PrivateGame
            }

            [Serializable]
            public class HeaderButton
            {
                public MainMenuHeaders mainMenuHeader;
                public Button button;
                public CanvasGroup selectedObject;
                public CanvasGroup parent;

                public void AddListener(UnityAction callback)
                {
                    button.onClick.AddListener(callback);
                }
                public void RemoveAllListeners()
                {
                    button.onClick.RemoveAllListeners();
                }
            }
            public List<HeaderButton> buttons = new List<HeaderButton>();

            [Header("Animation Settings")] [SerializeField]
            private float fadeDuration = 0.25f;

            [SerializeField] private float scalePop = 1.1f;
            [SerializeField] private float scaleDuration = 0.25f;


            private HeaderButton _currentButton;

            /// <summary>
            /// Deselects only the currently active button (if any).
            /// </summary>
            public void DeselectAll()
            {
                if (_currentButton == null)
                    return;

                var btn = _currentButton;

                // Fade out current selected visuals
                if (btn.selectedObject != null)
                {
                    btn.selectedObject.DOFade(0f, fadeDuration)
                        .OnComplete(() => btn.selectedObject.gameObject.SetActive(false));
                    btn.selectedObject.transform.DOScale(1f, scaleDuration);
                }

                if (btn.parent != null)
                {
                    btn.parent.DOFade(0f, fadeDuration)
                        .OnComplete(() => btn.parent.gameObject.SetActive(false));
                    btn.parent.transform.DOScale(1f, scaleDuration);
                }

                _currentButton = null;
            }

            /// <summary>
            /// Selects and animates a specific button by index.
            /// </summary>
            public void Select(int index)
            {
                if (index < 0 || index >= buttons.Count)
                    return;

                var newButton = buttons[index];

                // If already selected, ignore
                if (_currentButton == newButton)
                    return;

                // Deselect current before selecting new
                DeselectAll();

                _currentButton = newButton;

                // Fade in and pop animation
                if (newButton.parent != null)
                {
                    newButton.parent.gameObject.SetActive(true);
                    newButton.parent.alpha = 0f;
                    newButton.parent.transform.localScale = Vector3.zero;
                    newButton.parent.DOFade(1f, fadeDuration);
                    newButton.parent.transform
                        .DOScale(1f, scaleDuration)
                        .SetEase(Ease.OutBack);
                }

                if (newButton.selectedObject != null)
                {
                    newButton.selectedObject.gameObject.SetActive(true);
                    newButton.selectedObject.alpha = 0f;
                    newButton.selectedObject.transform.localScale = Vector3.zero;
                    newButton.selectedObject.DOFade(1f, fadeDuration);
                    newButton.selectedObject.transform
                        .DOScale(1f, scaleDuration)
                        .SetEase(Ease.OutBack);
                }
            }

            public void Attach(int startButtonIndex)
            {
                for (int i = 0; i < buttons.Count; i++)
                {
                    var i1 = i;
                    buttons[i].AddListener(()  => Select(i1));
                }
                buttons[startButtonIndex].button.onClick?.Invoke();
            }

            public void Detach()
            {
                foreach (var button in buttons)
                {
                    button.RemoveAllListeners();
                }
            }
        }

        public HeaderButtons headerButtons;

        public TMP_Text freekzText;
        public GameObject joinedTimerParent;
        public TMP_Text joinedTimerText;
        
        public Button transcetionsPanelButton;
        public Button refreshCoinsButton;
        
        
        public PublicMatchMakingPanel publicMatchMakingPanel;
        
        protected override void Initialize(Object obj)
        {
            transcetionsPanelButton.onClick.AddListener(OnClickTranscetionsPanelButton);
            refreshCoinsButton.onClick.AddListener(OnClickRefreshCoinsButton);

            headerButtons.Attach(1);

            CurrencyManager.UpdateFreekz += OnUpdateFreekzText;
            
            OnUpdateFreekzText(CurrencyManager.Freekz);
        }

        protected override void Cleanup()
        {
            headerButtons.Detach();
            
            transcetionsPanelButton.onClick.RemoveListener(OnClickTranscetionsPanelButton);
            refreshCoinsButton.onClick.RemoveListener(OnClickRefreshCoinsButton);
            
            CurrencyManager.UpdateFreekz -= OnUpdateFreekzText;
        }

        public void OnUpdateJoinedSessionData(SessionData sessionData)
        {
            if (sessionData == null || publicMatchMakingPanel.JoinedSession == null ||
                sessionData.ID != publicMatchMakingPanel.JoinedSession.ID || sessionData.RemainingTime <= 0)
            {
                OnUpdateFreekzText(CurrencyManager.Freekz);
                joinedTimerParent.SetActive(false);
                return;
            }

            OnUpdateFreekzText(CurrencyManager.Freekz);
            
            joinedTimerParent.SetActive(true);
            joinedTimerText.text = sessionData.RemainingTime.TimeFormat();
        }

        private async void OnClickRefreshCoinsButton()
        {
            await GameManager.RefreshPlayerData(); 
        }
        
        private void OnClickTranscetionsPanelButton()
        {
            UiManager.Instance.ShowPanel(UiScreenName.TranscetionsScreen, null);
        }
        
        private void OnClickAccountsButton()
        {
            UiManager.Instance.ShowPanel(UiScreenName.AccountDetails, null);
        }
        
        private async void OnClickPlayPublicGame()
        {
            UiManager.Instance.ShowPanel(UiScreenName.PublicMatchMakingPanel, null);
        }

        private void OnUpdateFreekzText(float freekz)
        {
            if (publicMatchMakingPanel.JoinedSession is { Fee: > 0 })
                freekzText.text =
                    $"{freekz.CurrencyFormat()}-{((float)publicMatchMakingPanel.JoinedSession.Fee).CurrencyFormat()}";
            else
                freekzText.text = freekz.CurrencyFormat();
        }

        private void OnClickJoinRoomButton()
        {
            // if (string.IsNullOrEmpty(privateRoomCodeInput.text))
            // {
            //     UiManager.Instance.ShowToast("Please enter room code.");
            //     return;
            // }
            //
            // UiManager.Instance.ShowPanel(UiScreenName.MatchMakingPanel,
            //     new MatchMakingPanelData() { IsPrivate = true, RoomName = privateRoomCodeInput.text.Trim() });
        }
    }
}
