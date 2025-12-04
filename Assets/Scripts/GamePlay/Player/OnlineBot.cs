using System;
using Controllers;
using Cysharp.Threading.Tasks;
using GamePlay.Cards;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using GamePlay.Bot;
using Newtonsoft.Json;
using NUnit.Framework;
using CardDataDto = GamePlay.Cards.CardDataDto;
using Random = UnityEngine.Random;
using Suit = GamePlay.Cards.Suit;

namespace GamePlay.Player
{
    public class OnlineBot : PlayerBase
    {
    [Header("Testing")] 
    [SerializeField] private bool handIsFaceUp;

    [SerializeField] private int simulationCount = 75; // Reduced from 100 to prevent Photon timeout
    [SerializeField] private float uctC = 1.41f;        public override async UniTask<Card> PlayTurn(float time = 10f)
        {
            if (IsDisabled) return null;
        
            RevealHand(handIsFaceUp);
        
            // var playableCards = CheckPlayableCards(); // Cards that follow suit or any if void
            // var card = SelectBestCard(playableCards);
            
            var botGameState = BuildBotGameState(); // Helper method to extract state
            var decisionEngine = new GamePlay.BotV3.EuchreBotDecisionEngine(simulationCount);
            
            // Run the decision-making logic with yielding to prevent blocking
            var selectedCardData = await decisionEngine.SelectCardToPlay(botGameState);
            var card = hand.FirstOrDefault(c => c.cardData == selectedCardData);
        
            // Async simulate thinking
            await UniTask.Delay(Random.Range(1200, 2400), cancellationToken: GamePlayControllerNetworked.CancellationTokenSource.Token);
            
            hand.Remove(card);
            GamePlayControllerNetworked.Instance.currentTrickCards[PlayerIndex] = card;
        
            var cardDataDto = JsonConvert.SerializeObject(new CardDataDto()
            {
                rank = card.cardData.rank,
                suit = card.cardData.suit
            });
        
            RPC_PlayCard(PlayerIndex, cardDataDto);
            await AnimateCardPlay(card);
        
            return card;
        }

        #region Helper Funtrions

        // private Card SelectBestCard(Card[] playableCards)
        // {
        //     var game = GamePlayControllerNetworked.Instance;
        //     var trumpSuit = game.TrumpSuit;
        //     var leadSuit = game.CurrentTrickSuit;
        //     var currentTrick = game.currentTrickCards;
        //
        //     // Identify trick info
        //     var leadCard = currentTrick.Values.FirstOrDefault(c => c != null);
        //     var cardsPlayed = currentTrick.Values.Count(c => c != null);
        //     var partnerIndex = (PlayerIndex + 2) % 4;
        //     var partnerCard = currentTrick.GetValueOrDefault(partnerIndex);
        //
        //     Card bestCard = null;
        //     var bestScore = float.MinValue;
        //
        //     foreach (var card in playableCards)
        //     {
        //         var score = 0f;
        //         float power = card.GetCardPower(trumpSuit, leadSuit);
        //
        //         // --- BASE POWER VALUE ---
        //         score += power * 10f;
        //
        //         // --- POSITIONAL LOGIC ---
        //         if (cardsPlayed == 0)
        //         {
        //             // Leading
        //             // Avoid wasting strongest trump early unless safe
        //             score -= (card.cardData.suit == trumpSuit ? power * 5f : 0);
        //             // Prefer leading with mid-high non-trump
        //             score -= Mathf.Abs(power - 0.6f) * 3f;
        //         }
        //         else
        //         {
        //             // Following
        //             bool canWin = CanWinTrick(card, currentTrick, trumpSuit, leadSuit);
        //             score += canWin ? 15f : -10f;
        //
        //             // If partner is currently winning, avoid overtaking them
        //             if (partnerCard != null && CanWinTrick(partnerCard, currentTrick, trumpSuit, leadSuit))
        //                 score -= power * 6f;
        //         }
        //
        //         // --- SUIT & CONTEXT LOGIC ---
        //         // Favor following lead suit
        //         if (card.cardData.suit == leadSuit) score += 3f;
        //         // Discourage off-suit unless it's trump or you're void
        //         else if (card.cardData.suit == trumpSuit) score += 5f;
        //         else score -= 2f;
        //
        //         // --- GAME CONTEXT ---
        //         // If nearing win (team needs 1 more trick), prefer higher cards
        //         if (game.playerManager.GetMyTeam(PlayerIndex).score == 2)
        //             score += power * 5f;
        //
        //         // --- RANDOM NOISE (prevents predictable play) ---
        //         score += Random.Range(-0.25f, 0.25f);
        //
        //         if (score > bestScore)
        //         {
        //             bestScore = score;
        //             bestCard = card;
        //         }
        //     }
        //
        //     return bestCard ?? playableCards.First();
        // }
        
        private bool CanWinTrick(Card candidate, Dictionary<int, Card> currentTrick, Suit trump, Suit leadSuit)
        {
            float candidatePower = candidate.GetCardPower(trump, leadSuit);

            foreach (var kvp in currentTrick)
            {
                var other = kvp.Value;
                if (other == null) continue;

                float otherPower = other.GetCardPower(trump, leadSuit);
                if (otherPower > candidatePower)
                    return false; // someone beats this card
            }

            return true;
        }


        private BotV3.GameState BuildBotGameState()
        {
            var gameController = GamePlayControllerNetworked.Instance;

            // Get current trick cards (player index -> card)
            var currentTrick = gameController.currentTrickCards
                .Where(pair => pair.Value != null)
                .ToDictionary(pair => pair.Key, pair => pair.Value.cardData);
            
            // Build player hands dictionary - bot only knows its own hand (FAIR BOT)
            var playerHands = new Dictionary<int, List<CardData>>();

            // Only add bot's own hand - opponent hands will be dealt randomly in simulations
            playerHands.Add(PlayerIndex, hand.Select(card => card.cardData).ToList());

            GameLogger.ShowLog($"🤖 Fair Bot: Only tracking own hand ({hand.Count} cards), opponents unknown");

            // Determine which team made trump by checking teamType
            int makerTeam = -1;
            if (gameController.playerManager.TeamA.teamType == TeamType.Makers)
                makerTeam = 0; // Team A (players 0 and 2) made trump
            else if (gameController.playerManager.TeamB.teamType == TeamType.Makers)
                makerTeam = 1; // Team B (players 1 and 3) made trump
            
            // Get team scores
            int botTeam = PlayerIndex % 2;
            int opponentTeam = 1 - botTeam;

            // Build complete list of all played cards (previous tricks + current trick)
            var allPlayedCardsList = new List<CardData>();
            // Add cards from completed tricks
            allPlayedCardsList.AddRange(gameController.allPlayedCards.Keys);
            // Add cards from current trick in progress
            allPlayedCardsList.AddRange(currentTrick.Values);
            
            GameLogger.ShowLog($"📊 All Played Cards: {allPlayedCardsList.Count} total cards known");

            // Track kitty cards (4 cards out of play)
            var kittyCardsList = gameController.kitty.Select(card => card.cardData).ToList();
            GameLogger.ShowLog($"📦 Kitty Cards: {kittyCardsList.Count} cards in kitty");

            // Infer void suits from trick history
            var voidSuits = InferVoidSuits(gameController);
            if (voidSuits.Any())
            {
                foreach (var kvp in voidSuits)
                {
                    var playerIndices = string.Join(", ", kvp.Value);
                    GameLogger.ShowLog($"🚫 Void Inference: Suit {kvp.Key} - Players [{playerIndices}] are void");
                }
            }

            return new BotV3.GameState
            {
                PlayerHands = playerHands,
                CurrentTrickCards = currentTrick, // Use CurrentTrickCards instead of PlayedCards
                PlayedCards = currentTrick, // Keep for backward compatibility
                TrumpSuit = gameController.TrumpSuit,
                CurrentTrickSuit = gameController.CurrentTrickSuit,
                PlayerIndex = PlayerIndex,
                Team0Tricks = gameController.playerManager.GetMyTeam(PlayerIndex).score,
                Team1Tricks = gameController.playerManager.GetMyTeam((PlayerIndex + 1) % 4).score,
                
                // Card counting - now fully populated!
                AllPlayedCards = allPlayedCardsList,
                KittyCards = kittyCardsList,
                VoidSuits = voidSuits,
                MakerTeam = makerTeam,
                TeamScore = gameController.playerManager.GetMyTeam(PlayerIndex).score,
                OpponentScore = gameController.playerManager.GetMyTeam((PlayerIndex + 1) % 4).score,
            };
        }

        /// <summary>
        /// Infer which players are void in which suits by analyzing trick history.
        /// When a player doesn't follow the lead suit, they must be void in that suit.
        /// </summary>
        private Dictionary<Suit, List<int>> InferVoidSuits(GamePlayControllerNetworked gameController)
        {
            var voidSuits = new Dictionary<Suit, List<int>>();
            var trumpSuit = gameController.TrumpSuit;

            // Analyze completed tricks from allPlayedCards
            var completedCards = gameController.allPlayedCards.Keys.ToList();
            var completedPlayerIndices = gameController.allPlayedCards.Values.ToList();

            // Process in groups of 4 (each trick)
            for (int trickStart = 0; trickStart < completedCards.Count; trickStart += 4)
            {
                if (trickStart + 3 >= completedCards.Count) break; // Need full trick

                // First card of trick determines lead suit
                var leadCard = completedCards[trickStart];
                var leadSuit = leadCard.GetEffectiveSuit(trumpSuit);

                // Check if other players followed suit
                for (int i = 1; i < 4; i++)
                {
                    int cardIndex = trickStart + i;
                    if (cardIndex >= completedCards.Count) break;

                    var card = completedCards[cardIndex];
                    var cardSuit = card.GetEffectiveSuit(trumpSuit);
                    var playerIndex = completedPlayerIndices[cardIndex];

                    // If player didn't follow suit, they must be void
                    if (cardSuit != leadSuit)
                    {
                        if (!voidSuits.ContainsKey(leadSuit))
                            voidSuits[leadSuit] = new List<int>();

                        if (!voidSuits[leadSuit].Contains(playerIndex))
                            voidSuits[leadSuit].Add(playerIndex);
                    }
                }
            }

            return voidSuits;
        }
        
        private bool IsCardWinning(Card contender, List<Card> trickCards, Suit trump, Suit lead)
        {
            return trickCards.All(other =>
                contender.GetCardPower(trump, lead) >= other.GetCardPower(trump, lead));
        }

        #endregion

        public override async UniTask<bool> AskToAcceptTrump(Card topCard)
        {
            if (handIsFaceUp) RevealHand(true);
            var trumpSuit = topCard.cardData.suit;
            var trumpCount = hand.Count(card => card.IsTrump(trumpSuit));

            var acceptanceChance = trumpCount switch
            {
                >= 4 => 0.9f, // 90% chance to accept
                3 => 0.6f,    // 60% chance to accept
                2 => 0.25f,   // 25% chance to accept
                1 => 0.05f,   // 5% chance to accept
                _ => 0.01f    // 1% chance to accept
            };

            var chance = Random.value;
            var acceptTrump = chance < acceptanceChance;

            ShowLog($"{chance} < {acceptanceChance} = {acceptTrump} :: {trumpCount}");

            await UniTask.Delay(Random.Range(1000, 2000), cancellationToken: GamePlayControllerNetworked.CancellationTokenSource.Token);

            Rpc_RespondToTrump(PlayerIndex, (int)topCard.cardData.suit, acceptTrump ? 1 : 0);
            return true;

        }

        public override async UniTask<Suit> ChooseTrumpSuit(Card topCard, bool forceFullSuit = false)
        {
            if (handIsFaceUp) RevealHand(true);

            var availableSuits = Enum.GetValues(typeof(Suit))
                .Cast<Suit>()
                .Where(suit => suit != topCard.cardData.suit && suit != Suit.None)
                .ToList();

            Dictionary<Suit, float> suitScores = new();

            foreach (var suit in availableSuits)
            {
                var score = EvaluateTrumpSuit(suit);
                suitScores[suit] = score;
                ShowLog($"[Bot] Potential Trump: {suit}, Hand Strength Score: {score}");
            }

            var bestSuit = suitScores.OrderByDescending(pair => pair.Value).First();

            await UniTask.Delay(Random.Range(1000, 2000), cancellationToken: GamePlayControllerNetworked.CancellationTokenSource.Token);

            if (bestSuit.Value < 250)
            {
                if (forceFullSuit)
                {
                    ShowLog($"[Bot] Choosing Trump: {bestSuit.Key}");
                    Rpc_RespondToTrump(PlayerIndex, (int)bestSuit.Key, 1);
                    return bestSuit.Key;
                }

                ShowLog("[Bot] No strong trump suit. Passing...");
                Rpc_RespondToTrump(PlayerIndex, 0, 0);
                return Suit.None;
            }

            ShowLog($"[Bot] Choosing Trump: {bestSuit.Key}");
            Rpc_RespondToTrump(PlayerIndex, (int)bestSuit.Key, 1);
            return bestSuit.Key;
        }

        private float EvaluateTrumpSuit(Suit potentialTrump)
        {
            return hand.Select(card => card.GetCardPower(potentialTrump, Suit.None)).Aggregate(0f, (current, power) => current + power);
        }
        
        public override async UniTask<Card> AskToExchangeTrumpCard(Card topKittyCard)
        {
            if (handIsFaceUp) RevealHand(true);

            var trumpSuit = topKittyCard.cardData.suit;
            var selectedCard = hand.OrderBy(card => card.GetCardPower(trumpSuit, Suit.None)).First();

            hand.Remove(selectedCard);
            hand.Add(topKittyCard);

            var selectedCardDataDto = new CardDataDto()
            {
                suit = selectedCard.cardData.suit,
                rank = selectedCard.cardData.rank
            };
            var topKittyCardDataDto = new CardDataDto()
            {
                suit = topKittyCard.cardData.suit,
                rank = topKittyCard.cardData.rank
            };

            var selectedCardJson = JsonConvert.SerializeObject(selectedCardDataDto);
            var topKittyCardJson = JsonConvert.SerializeObject(topKittyCardDataDto);
            
            RPC_ExchangeTrumpCard(selectedCardJson, topKittyCardJson);
            
            ShowLog($"Weakest Card : {selectedCard.cardData.rank} of {selectedCard.cardData.suit} :: Power {selectedCard.GetCardPower(trumpSuit, Suit.None)}");
            
            await AnimateCardExchange(selectedCard, topKittyCard, false, 10f);

            return topKittyCard;

        }

        public override UniTask<bool> AskToGoAlone()
        {
            // Default: don't go alone
            return UniTask.FromResult(false);
        }

    }

}