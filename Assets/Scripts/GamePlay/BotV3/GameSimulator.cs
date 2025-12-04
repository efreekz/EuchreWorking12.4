using System.Collections.Generic;
using System.Linq;
using GamePlay.Cards;

namespace GamePlay.BotV3
{
    public static class GameSimulator
    {
        public static void ApplyMove(SimulatedGameState simState, CardData move)
        {
            // Apply the bot's move to the simulation
            simState.Hands[simState.CurrentPlayer].Remove(move);
            simState.Trick[simState.CurrentPlayer] = move;
            
            // Set lead suit if this is the first card
            if (simState.LeadSuit == Suit.None)
                simState.LeadSuit = move.GetEffectiveSuit(simState.TrumpSuit);
            
#if UNITY_EDITOR
            GameLogger.ShowLog($"Applied move: Player {simState.CurrentPlayer} plays {move}");
#endif
        }

        public static List<CardData> GetValidMoves(List<CardData> hand, Suit trickSuit, Suit trumpSuit)
        {
            if (trickSuit == Suit.None)
                return hand;

            var matchingSuit = hand.Where(cardData => cardData.GetEffectiveSuit(trumpSuit) == trickSuit).ToList();

            return matchingSuit.Count > 0 ? matchingSuit : hand;

        }


        public static int GetTrickWinner(Dictionary<int, CardData> playedCards, Suit leadSuit, Suit trumpSuit)
        {
            CardData winningCard = null;
            int winner = -1;

            var debugString = string.Empty;
            foreach (var (playerIndex, cardData) in playedCards)
            {
                if (cardData is null) continue;
                debugString += $"Card : {cardData.rank} of {cardData.suit} Power : {cardData.GetCardPower(trumpSuit, leadSuit)}\n";
                if (winningCard != null && cardData.GetCardPower(trumpSuit, leadSuit) <= winningCard.GetCardPower(trumpSuit, leadSuit)) 
                    continue;
                winner = playerIndex;
                winningCard = cardData;
            }
#if UNITY_EDITOR
            debugString = $"Winner : {winningCard}\n" + debugString;
            GameLogger.ShowLog(debugString);
#endif

            return winner;
        }
        
        private static bool PlayersHaveCards(SimulatedGameState state)
        {
            return state.Hands.Values.Any(h => h.Count > 0);
        }

        public static bool SimulatePlayout(SimulatedGameState simState, Suit trumpSuit, int botTeam)
        {
#if UNITY_EDITOR
            GameLogger.ShowLog($"Starting simulated playout for team {botTeam} with trump suit {trumpSuit}");
#endif

            int[] teamScores = simState.TeamScores;

            while (PlayersHaveCards(simState))
            {
                var trickCards = new Dictionary<int, CardData>(simState.Trick);
                int startPlayer = simState.CurrentPlayer;

                for (int i = 0; i < 4; i++)
                {
                    int player = (startPlayer + i) % 4;
                    
                    if (trickCards.ContainsKey(player))
                        continue;
                    
                    var hand = simState.Hands[player];
                    if (hand.Count == 0)
                    {
#if UNITY_EDITOR
                        GameLogger.ShowLog($"Warning: Player {player} has no cards left during playout", GameLogger.LogType.Warning);
#endif
                        continue;
                    }
                    
                    var playable = GetValidMoves(hand, simState.LeadSuit, trumpSuit);

                    var card = SelectIntelligentSimulationCard(playable, trickCards, trumpSuit, simState.LeadSuit, player);
                    
                    if (simState.LeadSuit == Suit.None)
                        simState.LeadSuit = card.GetEffectiveSuit(trumpSuit); 

                    if (trickCards.TryAdd(player, card))
                    {
                        hand.Remove(card);
#if UNITY_EDITOR
                        GameLogger.ShowLog($"Playout: Player {player} plays {card}");
#endif
                    }
                    else
                    {
#if UNITY_EDITOR
                        GameLogger.ShowLog($"Error: Player {player} has already played the card, Can not Add {card}", GameLogger.LogType.Error);
#endif
                    }
                }

                int winningPlayer = GetTrickWinner(trickCards, simState.LeadSuit, trumpSuit);
                int winningTeam = winningPlayer % 2;
                teamScores[winningTeam]++;
#if UNITY_EDITOR
                GameLogger.ShowLog($"Trick won by Player {winningPlayer} (Team {winningTeam})");
#endif

                // Clear the trick for the next round
                simState.Trick.Clear();
                simState.LeadSuit = Suit.None;
                simState.CurrentPlayer = winningPlayer;
            }
            
            bool botWon = teamScores[botTeam] > teamScores[1 - botTeam];
#if UNITY_EDITOR
            GameLogger.ShowLog($"Playout complete. Bot's team (Team {botTeam}) Won: {botWon}");
#endif
            return botWon;
        }
        
        /// <summary>
        /// Intelligent simulation card selection using expert Euchre heuristics
        /// Replaces random play with realistic opponent modeling
        /// </summary>
        private static CardData SelectIntelligentSimulationCard(
            List<CardData> playableCards,
            Dictionary<int, CardData> currentTrick,
            Suit trumpSuit,
            Suit leadSuit,
            int playerIndex)
        {
            // HEURISTIC 1: If partner is winning, slough lowest card
            int partnerIndex = (playerIndex + 2) % 4;
            if (currentTrick.ContainsKey(partnerIndex) && IsPartnerWinning(currentTrick, partnerIndex, trumpSuit, leadSuit))
            {
                var lowestCard = playableCards.OrderBy(c => c.GetCardPower(trumpSuit, leadSuit)).First();
#if UNITY_EDITOR
                GameLogger.ShowLog($"🤝 Simulation: Partner winning, sloughing lowest {lowestCard}");
#endif
                return lowestCard;
            }

            // HEURISTIC 2: If can win trick, play lowest winning card
            int highestPowerInTrick = GetHighestPowerInTrick(currentTrick, trumpSuit, leadSuit);
            var winningCards = playableCards.Where(c => c.GetCardPower(trumpSuit, leadSuit) > highestPowerInTrick).ToList();
            if (winningCards.Any())
            {
                var lowestWinner = winningCards.OrderBy(c => c.GetCardPower(trumpSuit, leadSuit)).First();
#if UNITY_EDITOR
                GameLogger.ShowLog($"🎯 Simulation: Can win, playing lowest winner {lowestWinner}");
#endif
                return lowestWinner;
            }

            // HEURISTIC 3: Can't win, slough lowest card
            var sloughCard = playableCards.OrderBy(c => c.GetCardPower(trumpSuit, leadSuit)).First();
#if UNITY_EDITOR
            GameLogger.ShowLog($"🗑️ Simulation: Can't win, sloughing {sloughCard}");
#endif
            return sloughCard;
        }

        /// <summary>
        /// Check if partner's card is currently winning the trick
        /// </summary>
        private static bool IsPartnerWinning(Dictionary<int, CardData> trick, int partnerIndex, Suit trumpSuit, Suit leadSuit)
        {
            if (!trick.ContainsKey(partnerIndex)) return false;

            var partnerCard = trick[partnerIndex];
            int partnerPower = partnerCard.GetCardPower(trumpSuit, leadSuit);

            foreach (var kvp in trick)
            {
                if (kvp.Key != partnerIndex)
                {
                    if (kvp.Value.GetCardPower(trumpSuit, leadSuit) > partnerPower)
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Get the highest card power in the current trick
        /// </summary>
        private static int GetHighestPowerInTrick(Dictionary<int, CardData> trick, Suit trumpSuit, Suit leadSuit)
        {
            if (trick.Count == 0) return -1;

            return trick.Values.Max(c => c.GetCardPower(trumpSuit, leadSuit));
        }
    }
}
