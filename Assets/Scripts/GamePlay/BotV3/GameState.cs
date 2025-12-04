using System.Collections.Generic;
using GamePlay.Cards;

namespace GamePlay.BotV3
{
    public class GameState
    {
        public Dictionary<int, List<CardData>> PlayerHands = new Dictionary<int, List<CardData>>();
        public Dictionary<int, CardData> CurrentTrickCards = new Dictionary<int, CardData>(); // Current trick being played
        public Dictionary<int, CardData> PlayedCards = new Dictionary<int, CardData>(); // Deprecated/alternative name

        // whose turn it currently is (absolute player index 0..P-1)
        public int PlayerIndex;

        public Suit TrumpSuit = Suit.None;
        public Suit CurrentTrickSuit = Suit.None; // What suit was led this trick

        public int Team0Tricks = 0;
        public int Team1Tricks = 0;
        
        // New infrastructure for card counting and inference
        public List<CardData> AllPlayedCards = new List<CardData>(); // All cards played across all tricks this hand
        public List<CardData> KittyCards = new List<CardData>(); // Cards in the kitty (out of play)
        public Dictionary<Suit, List<int>> VoidSuits = new Dictionary<Suit, List<int>>(); // Track which players are void in which suits
        
        // Additional game state for better decision making
        public int MakerTeam = -1; // Which team made trump (0 or 1)
        public int TeamScore = 0; // Bot's team score
        public int OpponentScore = 0; // Opponent team score

        public List<CardData> PlayerHand => PlayerHands.ContainsKey(PlayerIndex) ? PlayerHands[PlayerIndex] : new List<CardData>();
    }
}
