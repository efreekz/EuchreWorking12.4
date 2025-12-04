using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Controllers;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using GamePlay.Cards;
using GamePlay.Player;
using Managers;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace GamePlay.Ui
{
    public class PlayerElementUi : MonoBehaviour
    {
        public HorizontalLayoutGroup handLayoutGroup;
        public Transform handTransform;
        public Transform playedCardTransform;
        public Transform winDeckTransform;
        public Transform wonTrickParent;
        public Transform disabledParent;
        public RectTransform trumpHolder;
        public Image trumpImage;
        public TMP_Text wonTrickCount;
        public TMP_Text idText;

        public GameObject turnObject;
        public GameObject dealerObject;
        public RectTransform messageParent;
        public TMP_Text messageText;
        public int messageTime = 2;

        private CancellationTokenSource _cancellationToken;
        private bool _isMessageForced = false;
        private void Awake()
        {
            _cancellationToken = new CancellationTokenSource();
            
            // Ensure text elements always face the camera
            EnsureFacingCamera(messageParent);
            EnsureFacingCamera(dealerObject.transform as RectTransform);
        }
        
        private void EnsureFacingCamera(RectTransform target)
        {
            if (target == null) return;
            
            // Add FaceCameraUI component if it doesn't exist
            if (target.GetComponent<FaceCameraUI>() == null)
            {
                target.gameObject.AddComponent<FaceCameraUI>();
            }
        }

        private void OnDestroy()
        {
            _cancellationToken?.Cancel();
            _cancellationToken?.Dispose();
        }

        public Transform Deck
        {
            get;
            private set;
        }
        public void Init(Transform deckTransform)
        {
            Deck = deckTransform;
            wonTrickCount.text = "";
        }

        public void Reset()
        {
            trumpHolder.gameObject.SetActive(false);
            // for (int i = 0; i < handTransform.childCount; i++)
            // {
            //     Destroy(handTransform.GetChild(i).gameObject);
            // }
            // for (int i = 0; i < playedCardTransform.childCount; i++)
            // {
            //     Destroy(playedCardTransform.GetChild(i).gameObject);
            // }
            // for (int i = 0; i < winDeckTransform.childCount; i++)
            // {
            //     Destroy(winDeckTransform.GetChild(i).gameObject);
            // }
        }

        public void PlayerIsDisabled(bool isDisabled)
        {
            handTransform.gameObject.SetActive(!isDisabled);
            disabledParent.gameObject.SetActive(isDisabled);
        }
        

        public async UniTask ShowMessage(string message, bool forceMessage)
        {
            messageText.text = message;
            messageParent.gameObject.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(messageParent);
            _isMessageForced = forceMessage;

            if (forceMessage)
                return;
            
            await UniTask.Delay(messageTime * 1000, cancellationToken: _cancellationToken.Token);

            messageParent.gameObject.SetActive(false);
        }

        public void AttachPlayer(PlayerBase player)
        {
            var playerName = player.PlayerName;
            idText.text = playerName;
        }


        public void PlayTurn()
        {
            turnObject.SetActive(true);
        }

        public async void EndTurn()
        {
            // Delay hiding message to allow it to be visible for 1.5 seconds
            await UniTask.Delay(1500, cancellationToken: _cancellationToken.Token);
            
            // Don't hide forced messages (like "Order Up" during dealer discard)
            if (!_isMessageForced)
            {
                messageParent.gameObject.SetActive(false);
            }
            
            turnObject.SetActive(false);
        }

        public void HideMessage()
        {
            _isMessageForced = false;
            messageParent.gameObject.SetActive(false);
        }

        public void SetDealer(bool enable)
        {
            dealerObject.SetActive(enable);
        }

        // public void SetTrickCount(int trickWon)
        // {
        //     if (trickWon > 0)
        //     {
        //         wonTrickParent.gameObject.SetActive(true);
        //         wonTrickCount.text = trickWon.ToString();
        //
        //         // Reset scale first to avoid stacking
        //         wonTrickParent.transform.localScale = Vector3.one;
        //
        //         // Punch the scale for emphasis
        //         wonTrickParent.transform
        //             .DOScale(1.2f, 0.2f)
        //             .SetEase(Ease.OutBack)
        //             .OnComplete(() =>
        //                 wonTrickParent.transform.DOScale(1f, 0.2f).SetEase(Ease.InQuad));
        //     }
        //     else
        //     {
        //         wonTrickParent.gameObject.SetActive(false);
        //     }
        // }

        public Slider turnTimer;
        public void StartTurnTimer(float duration, CancellationToken token)
        {
            turnTimer.gameObject.SetActive(true);
            turnTimer.maxValue = duration;
            turnTimer.value = duration;

            DOTween.Kill(turnTimer); // Stop any previous animations

            // Animate the slider value from duration to 0
            var tween = DOTween.To(() => duration, x => turnTimer.value = x, 0, duration)
                .SetEase(Ease.Linear)
                .OnComplete(() => turnTimer.gameObject.SetActive(false))
                .SetId(turnTimer);

            // Cancel tween if player plays early
            token.Register(() =>
            {
                tween.Kill();
                turnTimer.gameObject.SetActive(false);
            });
        }



        public async UniTask AnimateSortHand(List<Card> hand, float sortAnimationDuration)
        {
            if (hand == null || hand.Count == 0)
                return;

            // --- 1️⃣ Disable layout group temporarily
            // bool wasLayoutActive = handLayoutGroup && handLayoutGroup.enabled;
            // if (wasLayoutActive)
            //     handLayoutGroup.enabled = false;

            // --- 2️⃣ Sort cards logically
            var sortedCards = hand.OrderByDescending(c => c.cardData.suit)
                .ThenByDescending(c => c.cardData.rank)
                .ToList();

            // --- 3️⃣ Cache parent and all sibling transforms
            Transform parent = handLayoutGroup.transform;
            int totalChildren = parent.childCount;

            // Create list of siblings (to preserve order for non-card elements)
            List<Transform> allChildren = new List<Transform>();
            for (int i = 0; i < totalChildren; i++)
                allChildren.Add(parent.GetChild(i));

            // --- 4️⃣ Calculate proper target indexes based on current hierarchy
            // We only adjust the sibling order for card objects
            int nextCardIndex = 0;
            for (int i = 0; i < totalChildren; i++)
            {
                if (allChildren[i].TryGetComponent<Card>(out _))
                {
                    var sortedCard = sortedCards[nextCardIndex];
                    sortedCard.transform.SetSiblingIndex(i);
                    nextCardIndex++;
                }
            }
            // --- 5️⃣ Animate each card to its new position (based on target sibling positions)
            var moveTasks = new List<UniTask>();

            // Capture current positions of all cards (before sorting)
            var originalPositions = hand.Select(c => c.transform.localPosition).ToList();

            // --- Animate each card to the position of the card that was at its new index
            for (int i = 0; i < sortedCards.Count; i++)
            {
                var card = sortedCards[i];
                var targetPosition = originalPositions[i]; // position of the card that *was* at that index

                // Kill any previous tween on this transform
                DOTween.Kill(card.transform);

                // Animate card to target position
                var tween = card.transform.DOLocalMove(targetPosition, sortAnimationDuration)
                    .SetEase(Ease.OutQuad);

                moveTasks.Add(tween.AsyncWaitForCompletion().AsUniTask());

                // Update sibling index so layout order matches visually
                card.transform.SetSiblingIndex(i);
            }

            // --- 6️⃣ Wait for all animations
            await UniTask.WhenAll(moveTasks);

            // --- 7️⃣ Update actual list order reference
            hand.Clear();
            hand.AddRange(sortedCards);

            // --- 8️⃣ Re-enable layout group if needed
            // if (wasLayoutActive)
            //     handLayoutGroup.enabled = true;
        }

        public async UniTask SetTrump(Suit suit)
        {
            trumpImage.sprite = GamePlayControllerNetworked.Instance.cardsController.GetSuitData(suit).sprite;
            trumpHolder.gameObject.SetActive(true);
            trumpHolder.transform.localScale = Vector3.zero;
            trumpHolder.DOScale(1f, 0.2f)
                .SetEase(Ease.OutBack)
                .OnComplete(() =>
                    trumpHolder.transform.DOScale(1f, 0.2f).SetEase(Ease.InQuad));
        }
    }
}
