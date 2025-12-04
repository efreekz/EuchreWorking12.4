using System.Collections;
using System.Threading.Tasks;
using Controllers;
using DG.Tweening;
using GamePlay;
using GamePlay.Cards;
using TMPro;
using UIArchitecture;
using UnityEngine;
using UnityEngine.UI;
using Object = System.Object;

namespace Ui.GamePlayScreens
{
    public class ChooseTrumpSuit : PopUpView
    {
        [SerializeField] private Button passButton;
        [SerializeField] private Button orderUpButton;
        [SerializeField] private Button orderUpAndGoAloneButton;
        [SerializeField] private TMP_Text orderUpText;
        [SerializeField] private string dealerText = "It Lives";
        [SerializeField] private Image suitImage;
        [SerializeField] private Transform cardDisplayParent;
        [SerializeField] private Slider timer;
        [SerializeField] private float time = 10f;

        private float _timeToPass; 
        private Card _selectedCard;
        private TaskCompletionSource<int> _choiceTaskSource;
        
        // 0 means player has passed
        // 1 means player has accepted
        // 2 means player has accepted and decided go go alone
        public Task<int> GetChoice => _choiceTaskSource.Task;
        private Tweener _shakeTween;
        
        protected override void Initialize(Object data)
        {
            if (data is Card card)
            {
                _selectedCard = card;
                
                // Display the actual card instead of just the suit icon
                if (cardDisplayParent != null)
                {
                    // Hide the suit icon image
                    suitImage.gameObject.SetActive(false);
                    
                    // Create a copy of the card to display in the popup
                    var cardCopy = Instantiate(_selectedCard.gameObject, cardDisplayParent);
                    cardCopy.transform.localPosition = Vector3.zero;
                    cardCopy.transform.localRotation = Quaternion.identity;
                    cardCopy.transform.localScale = Vector3.one;
                    
                    // Make sure the card is face-up and not interactable
                    var cardComponent = cardCopy.GetComponent<Card>();
                    if (cardComponent != null)
                    {
                        cardComponent.SetFaceUp(true);
                        cardComponent.SetInteractable(false);
                    }
                }
                else
                {
                    // Fallback to suit icon if cardDisplayParent is not assigned
                    suitImage.sprite = GamePlayControllerNetworked.Instance.cardsController
                        .GetSuitData(_selectedCard.cardData.suit).sprite;
                }
                
                _choiceTaskSource = new TaskCompletionSource<int>();

                if (GamePlayControllerNetworked.Instance.playerManager.GetLocalPlayerBase().PlayerIndex == GamePlayControllerNetworked.Instance.playerManager.DealerIndex)
                    orderUpText.text = dealerText;
                else
                    orderUpText.text = "Order Up";
                
                StartCoroutine(TimerCoroutine());
            }
            else
            {
                Debug.LogError("Data is is not is expected Format");
            }

            passButton.onClick.AddListener(OnClickPassButton);
            orderUpButton.onClick.AddListener(OnClickOrderUpButton);
            orderUpAndGoAloneButton.onClick.AddListener(OnClickOrderUpAndGoAloneButton);

        }


        protected override void Cleanup()
        {
            _shakeTween?.Kill();
            
            passButton.onClick.RemoveAllListeners();
            orderUpButton.onClick.RemoveAllListeners();
        }

        private IEnumerator TimerCoroutine()
        {
            _timeToPass = time;

            // Start shaking the timer using DOTween on localPosition
            _shakeTween = timer.transform
                .DOShakePosition(duration: time, strength: new Vector3(5f, 5f, 0), vibrato: 20, randomness: 90, snapping: false, fadeOut: false)
                .SetRelative()
                .SetLoops(-1, LoopType.Restart)
                .SetUpdate(true)
                .SetEase(Ease.Linear);

            while (_timeToPass > 0)
            {
                _timeToPass -= Time.deltaTime;
                timer.value = Mathf.Clamp01(_timeToPass / time);
                yield return null;
            }

            
            timer.transform.localPosition = Vector3.zero; // Reset position in case of residual offset

            OnClickPassButton();
        }

        
        private void OnClickPassButton()
        {
            _choiceTaskSource?.TrySetResult(0);
            UiManager.Instance.HidePanel(this);
        }

        private void OnClickOrderUpButton()
        {
            _choiceTaskSource?.TrySetResult(1);
            UiManager.Instance.HidePanel(this);;
        }
        
        private void OnClickOrderUpAndGoAloneButton()
        {
            _choiceTaskSource?.TrySetResult(2);
            UiManager.Instance.HidePanel(this);
        }

    
    }
}
