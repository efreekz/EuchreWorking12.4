using System.Collections.Generic;
using System.Linq;
using GamePlay.Cards;

namespace GamePlay.BotV3
{
    public class SimulatedGameState
    {
        public Dictionary<int, List<CardData>> Hands;
        public Dictionary<int, CardData> Trick;
        public Suit LeadSuit; // Note: This is equivalent to CurrentTrickSuit in GameState - the suit that was led this trick
        public Suit TrumpSuit;
        public int CurrentPlayer;
        public int[] TeamScores = new int[2];

        public SimulatedGameState(GameState gameState)
        {
            Hands = new Dictionary<int, List<CardData>>();
            Trick = new Dictionary<int, CardData>();

            // Clone known hands (bot's hand is certain)
            foreach (var kvp in gameState.PlayerHands)
            {
                Hands[kvp.Key] = new List<CardData>();
                foreach (var card in kvp.Value)
                {
                    Hands[kvp.Key].Add(new CardData(card.suit, card.rank)); // Clone card
                }
            }

            // Use CurrentTrickCards if available, otherwise fall back to PlayedCards
            var trickSource = gameState.CurrentTrickCards.Count > 0 ? gameState.CurrentTrickCards : gameState.PlayedCards;
            foreach (var kvp in trickSource)
            {
                Trick[kvp.Key] = new CardData(kvp.Value.suit, kvp.Value.rank); // Clone
            }

            CurrentPlayer = gameState.PlayerIndex;
            LeadSuit = gameState.CurrentTrickSuit; // CurrentTrickSuit in GameState = LeadSuit in SimulatedGameState
            TrumpSuit = gameState.TrumpSuit;
            TeamScores = new int[] { gameState.Team0Tricks, gameState.Team1Tricks };

            // Deal unknown cards to opponents with constraints
            DealUnknownCards(gameState);
        }

        /// <summary>
        /// Deals unknown cards to players with empty hands, respecting constraints:
        /// - AllPlayedCards (cards already played)
        /// - KittyCards (cards out of play)
        /// - VoidSuits (players who can't have certain suits)
        /// - Current trick cards (cards being played right now)
        /// </summary>
        private void DealUnknownCards(GameState gameState)
        {
            // Build pool of all 24 Euchre cards
            var allCards = new List<CardData>();
            foreach (Suit suit in new[] { Suit.Hearts, Suit.Diamonds, Suit.Clubs, Suit.Spades })
            {
                foreach (Rank rank in new[] { Rank.Nine, Rank.Ten, Rank.Jack, Rank.Queen, Rank.King, Rank.Ace })
                {
                    allCards.Add(new CardData(suit, rank));
                }
            }

            // Remove cards we KNOW are not available
            var knownCards = new HashSet<CardData>();

            // Add bot's hand (known for certain)
            if (Hands.ContainsKey(gameState.PlayerIndex))
            {
                foreach (var card in Hands[gameState.PlayerIndex])
                    knownCards.Add(card);
            }

            // Add all played cards (from previous tricks)
            foreach (var card in gameState.AllPlayedCards)
                knownCards.Add(card);

            // Add kitty cards (out of play)
            foreach (var card in gameState.KittyCards)
                knownCards.Add(card);

            // Add current trick cards (already played this trick)
            foreach (var card in Trick.Values)
                knownCards.Add(card);

            // Unknown cards = All cards - Known cards
            var unknownCards = allCards.Where(c => !knownCards.Contains(c)).ToList();

            GameLogger.ShowLog($"🃏 Card Dealing: {unknownCards.Count} unknown cards to distribute");

            // Shuffle unknown cards
            var random = new System.Random();
            unknownCards = unknownCards.OrderBy(x => random.Next()).ToList();

            // Deal to all 4 players (skip bot's hand which is already known)
            for (int playerIndex = 0; playerIndex < 4; playerIndex++)
            {
                // Skip bot's hand (already known)
                if (playerIndex == gameState.PlayerIndex)
                    continue;

                // Initialize empty hand if player doesn't exist in dictionary yet
                if (!Hands.ContainsKey(playerIndex))
                    Hands[playerIndex] = new List<CardData>();

                // Skip if already has cards (shouldn't happen but safety check)
                if (Hands[playerIndex].Count > 0)
                    continue;

                // Calculate how many cards this player should have
                int cardsNeeded = CalculateHandSize(gameState, playerIndex);

                GameLogger.ShowLog($"🎴 Player {playerIndex} needs {cardsNeeded} cards");

                // Deal cards respecting void suit constraints
                var dealtCards = DealCardsWithConstraints(
                    unknownCards, 
                    cardsNeeded, 
                    playerIndex, 
                    gameState.VoidSuits,
                    TrumpSuit);

                Hands[playerIndex].AddRange(dealtCards);
                
                // Remove dealt cards from unknown pool
                foreach (var card in dealtCards)
                    unknownCards.Remove(card);

                GameLogger.ShowLog($"✅ Dealt {dealtCards.Count} cards to Player {playerIndex}");
            }
        }

        /// <summary>
        /// Calculate how many cards a player should have based on game state
        /// </summary>
        private int CalculateHandSize(GameState gameState, int playerIndex)
        {
            // Start with 5 cards dealt
            int startingCards = 5;

            // Subtract cards already played by this player (in completed tricks)
            int cardsPlayed = gameState.AllPlayedCards.Count(c => 
                gameState.CurrentTrickCards.Values.All(tc => tc != c)); // Don't count current trick

            // Each player plays roughly equal cards, but track by player if possible
            int cardsPlayedByPlayer = cardsPlayed / 4; // Rough estimate

            // Subtract card in current trick if they've played
            if (Trick.ContainsKey(playerIndex))
                cardsPlayedByPlayer++;

            return startingCards - cardsPlayedByPlayer;
        }

        /// <summary>
        /// Deal cards to a player respecting void suit constraints
        /// </summary>
        private List<CardData> DealCardsWithConstraints(
            List<CardData> availableCards,
            int count,
            int playerIndex,
            Dictionary<Suit, List<int>> voidSuits,
            Suit trumpSuit)
        {
            var dealtCards = new List<CardData>();

            // Build list of suits this player is void in
            var voidInSuits = new HashSet<Suit>();
            foreach (var kvp in voidSuits)
            {
                if (kvp.Value.Contains(playerIndex))
                    voidInSuits.Add(kvp.Key);
            }

            // Deal cards that don't violate void constraints
            foreach (var card in availableCards.ToList())
            {
                if (dealtCards.Count >= count)
                    break;

                var effectiveSuit = card.GetEffectiveSuit(trumpSuit);

                // If player is void in this suit, they can't have it
                if (voidInSuits.Contains(effectiveSuit))
                {
                    GameLogger.ShowLog($"⚠️ Skipping {card} for Player {playerIndex} (void in {effectiveSuit})");
                    continue;
                }

                dealtCards.Add(card);
            }

            // If we couldn't deal enough cards respecting constraints, loosen them
            // (This can happen if constraints are too strict or card pool is limited)
            if (dealtCards.Count < count)
            {
                GameLogger.ShowLog($"⚠️ Warning: Could only deal {dealtCards.Count}/{count} cards respecting constraints", GameLogger.LogType.Warning);
                
                // Deal remaining cards from available pool regardless of constraints
                foreach (var card in availableCards)
                {
                    if (dealtCards.Count >= count)
                        break;
                    
                    if (!dealtCards.Contains(card))
                        dealtCards.Add(card);
                }
            }

            return dealtCards;
        }
    }
}