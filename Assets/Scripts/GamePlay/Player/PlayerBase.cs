using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Controllers;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Fusion;
using GamePlay.Cards;
using GamePlay.Interfaces;
using GamePlay.Ui;
using Helper;
using Managers;
using Network;
using Newtonsoft.Json;
using UnityEngine;

namespace GamePlay.Player
{
    public abstract class PlayerBase : NetworkBehaviour
    {
        [Networked] public int PlayerIndex { get; private set; }
        
        public List<Card> hand = new List<Card>();
        public List<CardData> handData = new List<CardData>();
        public PlayerElementUi PlayerElementUi { get; private set; }

        public bool IsBot => this is OnlineBot;

        private static IGameController Controller => GamePlayControllerNetworked.Instance;

        public string PlayerName => RPCManager.Instance.JoinedPlayers[PlayerIndex].Name.ToString();
        public PlayerInfo PlayerInfo =>
            new()
            {
                Name = RPCManager.Instance.JoinedPlayers[PlayerIndex].Name.ToString(),
                Id = RPCManager.Instance.JoinedPlayers[PlayerIndex].PLayerInfoId.ToString()
            };

        private int _trickWon = 0;
        private bool _isDisabled;

        public bool IsDisabled
        {
            get => _isDisabled;
            set
            {
                if (value)
                {
                    DisablePlayer();
                }
                else
                {
                    EnablePlayer();
                }
                _isDisabled = value;

            }
        }

        public int TricksWon
        {
            get => _trickWon;
            set
            {
                _trickWon = value;
                // PlayerElementUi.SetTrickCount(_trickWon);
            }
        }

        public virtual void Initialize(int index, PlayerElementUi thisPlayerElementUi)
        {
            PlayerIndex = index;
            PlayerElementUi = thisPlayerElementUi;
            PlayerElementUi.AttachPlayer(this);
        }
        
        private void EnablePlayer()
        {
            PlayerElementUi.PlayerIsDisabled(false);
        }

        private void DisablePlayer()
        {
            PlayerElementUi.PlayerIsDisabled(true);
        }

        public abstract UniTask<Card> PlayTurn(float time = 10f);
        public abstract UniTask<bool> AskToAcceptTrump(Card topCard);
        public abstract UniTask<Suit> ChooseTrumpSuit(Card topCard, bool forceFullSuit = false); 
        public abstract UniTask<Card> AskToExchangeTrumpCard(Card topKittyCard);
        public abstract UniTask<bool> AskToGoAlone();
        
        public async UniTask AddCardToHandUI(Card card, float animationTime)
        {
            var moveSequence = DOTween.Sequence();
            moveSequence.Append(card.transform.DOMove(PlayerElementUi.handTransform.position, animationTime));
            moveSequence.Join(card.transform.DORotate(PlayerElementUi.handTransform.rotation.eulerAngles, animationTime));

            await moveSequence.AsyncWaitForCompletion();
            card.transform.SetParent(PlayerElementUi.handTransform);
        }
        public async UniTask AddCardToWinDeckUI(Card[] cards, float animationTime)
        {
            List<Task> animationTasks = new List<Task>();

            foreach (var card in cards)
            { 
                var targetPos = PlayerElementUi.winDeckTransform.position;
                var targetRot = PlayerElementUi.winDeckTransform.rotation.eulerAngles;

                var moveTween = card.transform.DOMove(targetPos, animationTime);
                var rotateTween = card.transform.DORotate(targetRot, animationTime);
                animationTasks.Add(moveTween.AsyncWaitForCompletion());
                animationTasks.Add(rotateTween.AsyncWaitForCompletion());
                card.SetFaceUp(false);
            }

            await Task.WhenAll(animationTasks);

            foreach (var card in cards)
            {
                card.transform.SetParent(PlayerElementUi.winDeckTransform);

                card.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                card.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                card.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                card.rectTransform.anchoredPosition3D = Vector3.zero; // Immediately reset
            }
        }

        public void ReassignHand(List<CardData> overrideCards)
        {
            if (overrideCards.Count != hand.Count)
            {       
                GameLogger.ShowLog("Can Not Assign Hand Cards Because Hand Card Counts Are Not Equal", GameLogger.LogType.Error);
                return;
            }

            var i = 0;
            foreach (var card in hand)
            {
                card.SetCardData(overrideCards[i]);
                i++;
            }
        }

        public void RevealHand(bool showFaceUp)
        {
            foreach (var card in hand)
            {
                card.SetFaceUp(showFaceUp);
                // card.SetInteractable(true);
            }

            if (showFaceUp)
            {
                PlayerElementUi.AnimateSortHand(hand, 0.5f).Forget();
            }
        }
        
        protected Card[] CheckPlayableCards()
        {
            var currentSuit = Controller.CurrentTrickSuit;
            var trumpSuit = Controller.TrumpSuit;
            var playable = new List<Card>();

            if (currentSuit == Suit.None)
            {
                playable.AddRange(hand);
                return playable.ToArray();
            }

            var hasMatchingSuit = hand.Any(card => card.cardData.GetEffectiveSuit(trumpSuit) == currentSuit);

            foreach (var card in hand)
            {
                if (hasMatchingSuit)
                {
                    // Must play cards that match the effective suit (including bowers as trump)
                    if (card.cardData.GetEffectiveSuit(trumpSuit) == currentSuit)
                        playable.Add(card);
                }
                else
                {
                    // Can play any card if we can't follow suit
                    playable.Add(card);
                }
            }

            return playable.ToArray();
        }


        public async UniTask SendMessageToUi(string message, bool force)
        {
            await PlayerElementUi.ShowMessage(message, force);
        }
        
        public void ResetUiElement()
        {
            PlayerElementUi.Reset();
        }

        public void SetHimDealer(bool isDealer)
        {
            PlayerElementUi.SetDealer(isDealer);
        }
        
        public void UpdateUiOnPlayerTurn()
        {
            PlayerElementUi.PlayTurn();
        }

        public void UpdateUiOnEndTurn()
        {
            PlayerElementUi.EndTurn();
        }
        
        
        #region Animation

        protected async UniTask AnimateCardExchange(Card oldCard, Card newCard, bool isLocalPlayer, float timeToShowTrump)
        {
            var handTransform = PlayerElementUi.handTransform;
            var playPoint = PlayerElementUi.playedCardTransform;
            var deckTransform = PlayerElementUi.Deck;

            // Set face-up for the new card early so it's ready visually
            newCard.SetFaceUp(isLocalPlayer);

            // Move and rotate the new card into the hand
            var newCardMove = newCard.transform.DOMove(handTransform.position, 0.5f).AsyncWaitForCompletion();
            var newCardRotate = newCard.transform.DORotate(handTransform.rotation.eulerAngles, 0.5f).AsyncWaitForCompletion();

            // Prepare the old card to be removed - keep it face-down (discarded card stays hidden)
            oldCard.SetFaceUp(false);

            // Move old card directly to deck (no play point display)
            var oldCardMove = oldCard.transform.DOMove(deckTransform.position, 0.5f).AsyncWaitForCompletion();
            var oldCardRotate = oldCard.transform.DORotate(deckTransform.rotation.eulerAngles, 0.5f).AsyncWaitForCompletion();

            // Wait for both animations
            await Task.WhenAll(newCardMove, newCardRotate, oldCardMove, oldCardRotate);

            // Attach the new card to the hand
            newCard.transform.SetParent(handTransform);
            oldCard.transform.SetParent(deckTransform);
            
            // Immediately hide the discarded card
            oldCard.gameObject.SetActive(false);
            
            if (isLocalPlayer)
                PlayerElementUi.AnimateSortHand(hand, 0.5f).Forget(); // Sort hand (if local player)
            
            await UniTask.Delay(300);
        }


        private async UniTask AnimateCardExchange(Card newCard, CardData selectedCardData, float timeToShowTrump)
        {
            var handTransform = PlayerElementUi.handTransform;
            var playPoint = PlayerElementUi.playedCardTransform;
            var deckTransform = PlayerElementUi.Deck;

            // Set face-up for the new card early so it's ready visually
            newCard.SetFaceUp(false);

            // Move and rotate the new card into the hand
            var newCardMove = newCard.transform.DOMove(handTransform.position, 0.5f).AsyncWaitForCompletion();
            var newCardRotate = newCard.transform.DORotate(handTransform.rotation.eulerAngles, 0.5f).AsyncWaitForCompletion();

            // Pick random card from hand to replace
            var oldCard = hand.GetRandom();
            oldCard.SetCardData(selectedCardData);
            oldCard.SetFaceUp(false); // Keep face-down (rule: discarded card stays hidden)

            // Move old card directly to deck (no play point display)
            var oldCardMove = oldCard.transform.DOMove(deckTransform.position, 0.5f).AsyncWaitForCompletion();
            var oldCardRotate = oldCard.transform.DORotate(deckTransform.rotation.eulerAngles, 0.5f).AsyncWaitForCompletion();

            await Task.WhenAll(newCardMove, newCardRotate, oldCardMove, oldCardRotate);

            // Update references
            newCard.transform.SetParent(handTransform);
            oldCard.transform.SetParent(deckTransform);
            hand.Remove(oldCard);
            hand.Add(newCard);
            
            // Immediately hide the discarded card
            oldCard.gameObject.SetActive(false);
            
            await UniTask.Delay(300);
        }


        protected async UniTask AnimateCardPlay(Card card)
        {
            if (card == null) return;

            card.SetFaceUp(true);
            card.transform.SetParent(PlayerElementUi.playedCardTransform);

            // Start tweens
            Tween moveTween = card.transform
                .DOMove(PlayerElementUi.playedCardTransform.position, 0.3f)
                .SetEase(Ease.InOutQuad);

            Tween rotateTween = card.transform
                .DORotate(PlayerElementUi.playedCardTransform.rotation.eulerAngles, 0.3f)
                .SetEase(Ease.OutSine);

            // Wait for both tweens to complete
            await Task.WhenAll(moveTween.AsyncWaitForCompletion(), rotateTween.AsyncWaitForCompletion());
        }

        #endregion


        #region Testing

        [Header("Testing")] [SerializeField] private bool showLog;

        protected void ShowLog(string message)
        {
            if (showLog)
                GameLogger.ShowLog(message);
        }


        #endregion

        #region RPCs
        
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_NotifyInitialized()
        {
            GamePlayControllerNetworked.Instance.SetPlayerInitialized(PlayerIndex);
        }
        
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        protected void Rpc_RespondToTrump(int playerIndex, int suit, int choice)
        {
            ShowLog($"[{gameObject.name}] {playerIndex} responded: choice = {choice}");

            GamePlayControllerNetworked.Instance.TrumpAcceptedTcs.TrySetResult(new TrumpSelectionData(playerIndex, (Suit)suit, choice));
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
        protected async void RPC_ExchangeTrumpCard(string selectedCardJson, string topKittyCardJson)
        {
            var selectedCardDataDto = JsonConvert.DeserializeObject<CardDataDto>(selectedCardJson);
            // var topKittyCardDataDto = JsonConvert.DeserializeObject<CardDataDto>(topKittyCardJson);
            var dealer = GamePlayControllerNetworked.Instance.playerManager.GetDealerPlayer();
            var topKittyCard = GamePlayControllerNetworked.Instance.TopKittyCard;
            var selectedCardData = GamePlayControllerNetworked.Instance.cardsController.ToCardData(selectedCardDataDto);
            
            ShowLog($"DealerIndex : {dealer.PlayerIndex}\nLocalPlayer Index : {GamePlayControllerNetworked.Instance.playerManager.GetLocalPlayerBase().PlayerIndex}");

            if (HasStateAuthority)
            {
                GamePlayControllerNetworked.Instance.CardExchangedTcs.TrySetResult();
            }
            
            if (HasInputAuthority)
            {
                return;
            }

            if (dealer.PlayerIndex != GamePlayControllerNetworked.Instance.playerManager.GetLocalPlayerBase().PlayerIndex)
            {
                await dealer.AnimateCardExchange(topKittyCard, selectedCardData, 5f);
            }

        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
        protected void RPC_PlayCard(int playerIndex, string cardString)
        {
            var selectedCardDataDto = JsonConvert.DeserializeObject<CardDataDto>(cardString);
            var cardData = GamePlayControllerNetworked.Instance.cardsController.ToCardData(selectedCardDataDto);
            var playerBase = GamePlayControllerNetworked.Instance.playerManager.GetPlayer(playerIndex);
            
            ShowLog($"[{gameObject.name}] {playerBase.PlayerIndex} played card {cardData.rank} of {cardData.suit}");
            
            if (HasStateAuthority)
            {
                GamePlayControllerNetworked.Instance.CardPlayedTcs.TrySetResult(cardData);
            }
            
            
            if (HasInputAuthority)
            {
                return;
            }

            HelperPlayCard(playerBase, cardData).Forget();
        }

        #region Helper RPC

        private async UniTask HelperPlayCard(PlayerBase playerBase, CardData cardData)
        {
            var card = hand.GetRandom();
            card.SetCardData(cardData);
            GamePlayControllerNetworked.Instance.currentTrickCards[playerBase.PlayerIndex] = card;
            await playerBase.AnimateCardPlay(card);
            hand.Remove(card);
        }

        #endregion
        
        #endregion

    }
}