using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UIArchitecture;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace MainMenu
{
    public class MotivationCardObject : MonoBehaviour
    {
         public MotivationCardData motivationCardData;

        [Header("UI References")]
        public TMP_Text title;
        public TMP_Text message;
        public Image icon;
        public Button button;
        public CanvasGroup motivationParentCanvasGroup;
        public CanvasGroup buttonCanvasGroup;

        [Header("Animation Settings")]
        public float enterScale = 0.7f;
        public float animDuration = 0.4f;
        public float autoChangeDelay = 10f;
        public float iconPulseSpeed = 0.5f;
        public float iconPulseScale = 1.05f; 

        private CancellationTokenSource _cts;
        private bool IsVisible => isActiveAndEnabled;

        private void Awake()
        {
            button.onClick.AddListener(() =>
                UiManager.Instance.ShowPanel(UiScreenName.CreatNewSessionScreen, null));
        }

        private void OnDisable()
        {
            KillAllTweens();
            StopCycling();
        }

        private void OnDestroy()
        {
            KillAllTweens();
            StopCycling();
        }

        
        private void KillAllTweens()
        {
            if (icon != null) icon.transform.DOKill();
            if (buttonCanvasGroup != null) buttonCanvasGroup.DOKill();
            if (motivationParentCanvasGroup != null) motivationParentCanvasGroup.DOKill();
            if (transform != null) transform.DOKill();
        }
        public void Set(bool enable)
        {
            if (enable)
                ActivateFlow().Forget();
            else
                DeactivateFlow().Forget();
        }
        private async UniTaskVoid ActivateFlow()
        {
            gameObject.SetActive(true);

            buttonCanvasGroup.alpha = 0f;
            transform.localScale = Vector3.one * enterScale;

            DOTween.Sequence().Append(buttonCanvasGroup.DOFade(1f, animDuration))
                .Join(transform.DOScale(1f, animDuration).SetEase(Ease.OutBack)).Play();

            StartCycling();
        }

        private async UniTaskVoid DeactivateFlow()
        {
            StopCycling();

            icon.transform.DOKill();

            var task1 = motivationParentCanvasGroup
                .DOFade(0f, animDuration * 0.6f)
                .SetEase(Ease.InOutSine)
                .AsyncWaitForCompletion().AsUniTask();

            var task2 =  buttonCanvasGroup
                .DOFade(0f, animDuration * 0.6f)
                .SetEase(Ease.InBack)
                .AsyncWaitForCompletion().AsUniTask();
            
            await UniTask.WhenAll(task1, task2);

            gameObject.SetActive(false);
        }

        private void StartCycling()
        {
            StopCycling();
            _cts = new CancellationTokenSource();
            CardCycle(_cts.Token).Forget();
        }

        private void StopCycling()
        {
            if (_cts == null) return;
            
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }


        
        private async UniTaskVoid CardCycle(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var card = motivationCardData.GetNextCard();
                    await SwitchCard(card);

                    await UniTask.Delay(TimeSpan.FromSeconds(autoChangeDelay), cancellationToken: token);
                }
            }
            catch (OperationCanceledException)
            {
                // safely ignore
            }
            catch (Exception ex)
            {
                GameLogger.ShowLog($"CardCycle Exception: {ex.Message}", GameLogger.LogType.Error);
            }
        }

        private async UniTask SwitchCard(MotivationCard card)
        {
            if (!this || !isActiveAndEnabled)
                return;

            // Defensive check before each tween
            if (motivationParentCanvasGroup == null)
                return;

            await motivationParentCanvasGroup
                .DOFade(0f, animDuration * 0.6f)
                .SetEase(Ease.InOutSine)
                .AsyncWaitForCompletion();

            if (this == null || !isActiveAndEnabled)
                return;

            title.text = card.title;
            message.text = card.message;
            icon.sprite = card.icon;

            AnimateIconLoop();

            if (motivationParentCanvasGroup == null)
                return;

            motivationParentCanvasGroup.alpha = 0f;
            motivationParentCanvasGroup
                .DOFade(1f, animDuration * 0.8f)
                .SetEase(Ease.OutQuad)
                .Play();
        }

        private void AnimateIconLoop()
        {
            if(!isActiveAndEnabled)
                return;
            
            icon.transform.DOKill();

            icon.transform.localScale = Vector3.one;
            icon.transform.DOScale(iconPulseScale, iconPulseSpeed)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }
    }

}