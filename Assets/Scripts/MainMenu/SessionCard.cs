using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Fusion;
using Helper;
using Managers;
using Network;
using TMPro;
using Ui.MainMenuScreens;
using UIArchitecture;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MainMenu
{
    public class SessionCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("UI References")]
        public TMP_Text sessionID;
        public TMP_Text timer;
        public Transform content;
        public LobbyPlayerCard lobbyPlayerCard;
        public CanvasGroup hoverCanvasGroup;
        public Button joinButton;
        public Button leaveButton;
        
        public CanvasGroup canvasGroup;
        public float animDuration = 0.4f;

        public Image bgImage;
        public Color defaultColor = Color.white;
        public Color joinedColor = Color.white;

        private List<LobbyPlayerCard> _lobbyPlayerCards = new();
        private PublicMatchMakingPanel _publicMatchMakingPanel;
        private MainMenuHomeScreen _homeScreen;
        private bool _countdownActive = false;
        private bool isDestroying = false;

        private Tween _hoverTween;

        public SessionData Data { get; private set; }
        public Action<SessionData> OnUpdateSessionData;


        private void Awake()
        {
            joinButton.transform.localScale = Vector3.zero;
            leaveButton.transform.localScale = Vector3.zero;
        }

        private void OnEnable()
        {
            joinButton.onClick.AddListener(OnClickJoinButton);
            leaveButton.onClick.AddListener(OnClickLeaveButton);
            PlayShowAnimation(canvasGroup).Forget();
        }

        private void OnDisable()
        {
            joinButton.onClick.RemoveListener(OnClickJoinButton);
            leaveButton.onClick.RemoveListener(OnClickLeaveButton);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hoverTween?.Kill();

            _hoverTween = DOTween.Sequence()
                .Append(hoverCanvasGroup.DOFade(1f, 0.2f))
                .Join(ShowActiveButton(true))
                .SetEase(Ease.OutSine);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hoverTween?.Kill();

            _hoverTween = DOTween.Sequence()
                .Append(hoverCanvasGroup.DOFade(0f, 0.2f))
                .Join(ShowActiveButton(false))
                .SetEase(Ease.OutSine);
        }
        
        private async UniTask PlayHideAnimation(CanvasGroup canvas)
        {
            await canvas.DOFade(0f, animDuration * 0.6f)
                .SetEase(Ease.InBack).AsyncWaitForCompletion();
        }
        
        private async UniTask PlayShowAnimation(CanvasGroup canvas)
        {
            await DOTween.Sequence().Append(canvas.DOFade(1f, animDuration))
                .Join(canvas.transform.DOScale(1f, animDuration).SetEase(Ease.OutBack)).Play().AsyncWaitForCompletion();
        }


        private Tween ShowActiveButton(bool show)
        {
            if (Data == null)
                return null;
                
            var hasJoined = Data.PlayerHasJoined;
            
            var targetButton = hasJoined ? leaveButton : joinButton;
            var otherButton = hasJoined ? joinButton : leaveButton;
            otherButton.transform.localScale = Vector3.zero;

            return targetButton.transform.DOScale(show ? 1f : 0f, 0.2f);
        }

        public void Setup(SessionData sessionData, PublicMatchMakingPanel publicMatchMakingPanel, MainMenuHomeScreen homeScreen)
        {
            Data = sessionData;
            _publicMatchMakingPanel = publicMatchMakingPanel;
            _homeScreen = homeScreen;
            sessionID.text = Data.ID;

            // Sync player cards
            foreach (var player in sessionData.Players)
            {
                var existing = _lobbyPlayerCards.FirstOrDefault(c => c.PlayerInfo.Id == player.Id);
                if (existing == null)
                {
                    var newCard = Instantiate(lobbyPlayerCard, content);
                    newCard.Setup(player);
                    _lobbyPlayerCards.Add(newCard);
                    PlayShowAnimation(newCard.canvasGroup).Forget();
                }
                else
                {
                    existing.Setup(player);
                }
            }

            // Remove players no longer in session
            var playersToRemove = _lobbyPlayerCards
                .Where(c => sessionData.Players.All(p => p.Id != c.PlayerInfo.Id))
                .ToList();

            foreach (var card in playersToRemove)
            {
                _lobbyPlayerCards.Remove(card);
                PlayHideAnimation(card.canvasGroup).ContinueWith(() =>
                { 
                    Destroy(card.gameObject);
                });
            }
            Reset();

            if (_publicMatchMakingPanel.JoinedSession == null && Data.PlayerHasJoined)
            {
                _publicMatchMakingPanel.LeaveSession(Data).Forget();
            }

            if (_countdownActive) return;
            _countdownActive = true;
            StartCountdownRoutine().Forget();
        }

        private async UniTaskVoid StartCountdownRoutine()
        {
            var remaining = Data.TimeToStart;

            while (remaining > 0)
            {
                var now = SupabaseApiController.Now;
                var elapsed = (int)(now - Data.CreatedAt).TotalSeconds;
                remaining = Data.TimeToStart - elapsed;
                Data.RemainingTime = remaining;
                OnUpdateSessionData?.Invoke(Data);
                if (remaining <= 0)
                    break;

                timer.text = remaining.TimeFormat();
                await UniTask.Delay(TimeSpan.FromSeconds(1), cancellationToken: this.GetCancellationTokenOnDestroy());
            }

            timer.text = "00:00";
            HandleTimerEnd();
        }

        private async void HandleTimerEnd()
        {
            if (_publicMatchMakingPanel.JoinedSession != null && _publicMatchMakingPanel.JoinedSession.ID == Data.ID)
            {
                _publicMatchMakingPanel.StartMatchMaking();
            }
            else
            {
                await PlayHideAnimation(canvasGroup);
                _publicMatchMakingPanel.RemoveSession(this);
            }
        }

        private void OnClickJoinButton()
        {
            _publicMatchMakingPanel.JoinSession(Data).Forget();
        }

        private void OnClickLeaveButton()
        {
            _publicMatchMakingPanel.LeaveSession(Data).Forget();
        }

        public void Reset()
        {
            Color targetColor = defaultColor;

            if (Data.PlayerHasJoined)
                targetColor = joinedColor;

            if (bgImage.color == targetColor)
                return;

            bgImage.DOKill();

            bgImage.DOColor(targetColor, animDuration)
                .SetEase(Ease.InOutSine);
        }

    }
}
