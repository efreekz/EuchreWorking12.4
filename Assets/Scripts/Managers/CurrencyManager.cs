using System;
using Cysharp.Threading.Tasks;
using Data;
using Network;
using UIArchitecture;

namespace Managers
{
    public static class CurrencyManager
    {
        public static Action<float> UpdateFreekz;

        private static float _freekz;

        public static float Freekz 
        {
            get => _freekz;
            set
            {
                // Prevent circular updates - if value is the same, don't sync
                if (System.Math.Abs(_freekz - value) < 0.01f) return;
                
                _freekz = value;
                UpdateFreekz?.Invoke(_freekz);
                
                // Note: Don't sync back to SupabaseTokenManager here to avoid circular loop
                // SupabaseTokenManager will update CurrencyManager, not the other way around
            }
        }
        
        /// <summary>
        /// Checks if user has sufficient balance (soft check - no deduction)
        /// </summary>
        public static async UniTask<bool> HasSufficientBalance(int requiredAmount)
        {
            var result = await SupabaseCurrencyController.CheckBalance(requiredAmount);
            
            if (!result.Success)
            {
                GameLogger.LogNetwork($"❌ Balance check failed: {result.Error}", GameLogger.LogType.Error);
                return false;
            }
            
            // Update local balance with server value
            Freekz = result.CurrentBalance;
            
            return result.HasSufficientBalance;
        }

        /// <summary>
        /// Deducts entry fee when game starts (hard check + deduction)
        /// </summary>
        public static async UniTask<bool> DeductEntryFee(string lobbyId, int fee)
        {
            var result = await SupabaseCurrencyController.DeductEntryFee(lobbyId, fee);
            if (result.Success)
            {
                Freekz = result.Balance;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Credits winner reward or logs loser at game end
        /// </summary>
        public static async UniTask<bool> CreditGameReward(string lobbyId, int fee, int reward, bool won)
        {
            var result = await SupabaseCurrencyController.CreditGameReward(lobbyId, fee, reward, won);
            if (result.Success)
            {
                Freekz = result.Balance;
                return true;
            }
            return false;
        }
    }
}