using System.Collections.Generic;
using System.Linq;
using GamePlay.Cards;

namespace GamePlay.BotV3
{
    public class EuchreBotDecisionEngine
    {
        private readonly int _count;

        public EuchreBotDecisionEngine(int count)
        {
            _count = count;
        }

        public async Cysharp.Threading.Tasks.UniTask<CardData> SelectCardToPlayWrapper(GameState gameState)
        {
            Dictionary<CardData, int> results = new();
            
            var validCards = GameSimulator.GetValidMoves(
                gameState.PlayerHand,
                gameState.CurrentTrickSuit,
                gameState.TrumpSuit
            );
            
            foreach (var card in validCards)
                results[card] = 0;

            const int runs = 100;
            for (int i = 0; i < runs; i++)
            {
                var card = await SelectCardToPlay(gameState);
                results[card]++;
            }
            
            // Log results
            var report = $"Finished {runs} simulations\n";
            report = results.Aggregate(report, (current, kvp) => current + $"{kvp.Key} → {kvp.Value} times chosen\n");
            
            GameLogger.ShowLog(report, GameLogger.LogType.Error);
            
            return results.OrderByDescending(x => x.Value).First().Key;
        }

        public async Cysharp.Threading.Tasks.UniTask<CardData> SelectCardToPlay(GameState gameState)
        {
#if UNITY_EDITOR
            GameLogger.ShowLog("🧠 ELITE: Starting MCTS decision...");
#endif

            var validMoves = GameSimulator.GetValidMoves(gameState.PlayerHand, 
                gameState.CurrentTrickSuit, 
                gameState.TrumpSuit);

            if (validMoves.Count == 1)
            {
#if UNITY_EDITOR
                GameLogger.ShowLog($"Only one valid move: {validMoves[0]}");
#endif
                return validMoves[0];
            }

#if UNITY_EDITOR
            GameLogger.ShowLog($"🎯 Testing ALL {validMoves.Count} valid moves with soft penalties");
#endif

            var moveResults = new Dictionary<CardData, List<float>>();
            foreach (var m in validMoves)
                moveResults[m] = new List<float>();

            var botTeam = gameState.PlayerIndex % 2;

            // Run simulations for each valid move
            for (var i = 0; i < _count; i++)
            {
                // Yield every 10 simulations to prevent main thread blocking
                if (i % 10 == 0)
                    await Cysharp.Threading.Tasks.UniTask.Yield();

                foreach (var move in validMoves)
                {
                    var sim = new SimulatedGameState(gameState);
                    GameSimulator.ApplyMove(sim, move);

                    var won = GameSimulator.SimulatePlayout(sim, sim.TrumpSuit, botTeam);
                    float result = won ? 1.0f : 0.0f;

                    moveResults[move].Add(result);
                }
            }

            // Select best move with soft penalties
            var bestMove = SelectBestMoveWithSoftPenalties(moveResults, gameState);

#if UNITY_EDITOR
            // Log results
            string report = $"✅ SELECTED: {bestMove}\n";
            foreach (var (card, results) in moveResults)
            {
                float avgScore = results.Average();
                report += $"{card} → Win Rate: {avgScore:F3}\n";
            }
            GameLogger.ShowLog(report);
#endif

            return bestMove;
        }

        /// <summary>
        /// Select best move with soft penalties for bad tactics (doesn't forbid moves, just discourages them)
        /// </summary>
        private CardData SelectBestMoveWithSoftPenalties(Dictionary<CardData, List<float>> moveResults, GameState gameState)
        {
            var adjustedScores = new Dictionary<CardData, float>();

            foreach (var kvp in moveResults)
            {
                var move = kvp.Key;
                float baseScore = kvp.Value.Average(); // 0.0 to 1.0 win rate
                float finalScore = baseScore;

                // SOFT PENALTY 1: Leading trump when opponent made it (unless we have both bowers)
                if (gameState.CurrentTrickCards.Count == 0 && gameState.MakerTeam != (gameState.PlayerIndex % 2))
                {
                    if (move.IsTrump(gameState.TrumpSuit))
                    {
                        bool hasBothBowers = HasBothBowers(gameState.PlayerHand, gameState.TrumpSuit);
                        float penalty = hasBothBowers ? -0.05f : -0.30f;
                        finalScore += penalty;
#if UNITY_EDITOR
                        GameLogger.ShowLog($"⚠️ SOFT PENALTY: {move} leading trump vs opponent trump {penalty:F2}");
#endif
                    }
                }

                // SOFT PENALTY 2: Overtrumping winning partner (except last trick)
                if (IsOvertrumpingPartner(move, gameState))
                {
                    int cardsLeft = gameState.PlayerHand.Count;
                    float penalty = cardsLeft == 1 ? 0.0f : -0.20f; // No penalty on last trick
                    finalScore += penalty;
#if UNITY_EDITOR
                    GameLogger.ShowLog($"⚠️ SOFT PENALTY: {move} overtrumping partner {penalty:F2}");
#endif
                }

                // SOFT PENALTY 3: Wasting high cards when can't win (rank-based penalties)
                if (!CanWinTrick(move, gameState))
                {
                    float penalty = GetWastingHighCardPenalty(move, gameState.TrumpSuit);
                    if (penalty < 0)
                    {
                        finalScore += penalty;
#if UNITY_EDITOR
                        GameLogger.ShowLog($"⚠️ SOFT PENALTY: {move} wasting high card {penalty:F2}");
#endif
                    }
                }

                adjustedScores[move] = finalScore;
            }

            var bestMove = adjustedScores.OrderByDescending(kvp => kvp.Value).First().Key;
#if UNITY_EDITOR
            GameLogger.ShowLog($"✅ Best move after penalties: {bestMove} (Score: {adjustedScores[bestMove]:F3})");
#endif
            return bestMove;
        }

        private bool HasBothBowers(List<CardData> hand, Suit trumpSuit)
        {
            bool hasRight = hand.Any(c => c.IsRightBower(trumpSuit));
            bool hasLeft = hand.Any(c => c.IsLeftBower(trumpSuit));
            return hasRight && hasLeft;
        }

        private bool IsOvertrumpingPartner(CardData move, GameState gameState)
        {
            if (gameState.CurrentTrickCards.Count < 1) return false;
            if (!move.IsTrump(gameState.TrumpSuit)) return false;

            int playerIndex = gameState.PlayerIndex;
            int partnerIndex = (playerIndex + 2) % 4;

            // Check if partner has played and is winning
            if (gameState.CurrentTrickCards.ContainsKey(partnerIndex))
            {
                var partnerCard = gameState.CurrentTrickCards[partnerIndex];
                int partnerPower = partnerCard.GetCardPower(gameState.TrumpSuit, gameState.CurrentTrickSuit);

                // Is partner currently winning?
                bool partnerWinning = true;
                foreach (var kvp in gameState.CurrentTrickCards)
                {
                    if (kvp.Key != partnerIndex)
                    {
                        if (kvp.Value.GetCardPower(gameState.TrumpSuit, gameState.CurrentTrickSuit) > partnerPower)
                        {
                            partnerWinning = false;
                            break;
                        }
                    }
                }

                return partnerWinning;
            }

            return false;
        }

        private bool CanWinTrick(CardData move, GameState gameState)
        {
            if (gameState.CurrentTrickCards.Count == 0) return true; // Leading, can always "win"

            int movePower = move.GetCardPower(gameState.TrumpSuit, gameState.CurrentTrickSuit);
            int highestPower = gameState.CurrentTrickCards.Values.Max(c => c.GetCardPower(gameState.TrumpSuit, gameState.CurrentTrickSuit));

            return movePower > highestPower;
        }

        private float GetWastingHighCardPenalty(CardData card, Suit trumpSuit)
        {
            // Rank-based penalties for wasting cards when voiding
            if (card.IsRightBower(trumpSuit) || card.IsLeftBower(trumpSuit)) return -0.40f; // Massive penalty for bowers
            if (card.rank == Rank.Ace) return -0.35f;
            if (card.rank == Rank.King) return -0.25f;
            if (card.rank == Rank.Queen) return -0.15f;
            if (card.rank == Rank.Ten) return -0.08f;
            if (card.rank == Rank.Nine) return 0.0f; // No penalty - dump 9s first!

            return 0.0f;
        }


    }
}
