using System.Threading;
using Cysharp.Threading.Tasks;
using UIArchitecture;

namespace Managers
{
    public static class GameRewardService
    {
        private const int MaxAttempts = 3;
        private const int RetryDelayMs = 1500;

        public static PendingPayoutStore.PendingPayoutEntry EnqueueReward(string lobbyId, int fee, int reward, bool won)
        {
            return PendingPayoutStore.AddOrUpdate(lobbyId, fee, reward, won);
        }

        public static async UniTask<bool> TryApplyRewardAsync(PendingPayoutStore.PendingPayoutEntry entry, CancellationToken token)
        {
            for (var attempt = 0; attempt < MaxAttempts; attempt++)
            {
                token.ThrowIfCancellationRequested();
                var success = await CurrencyManager.CreditGameReward(entry.lobbyId, entry.fee, entry.reward, entry.won);
                if (success)
                {
                    PendingPayoutStore.Remove(entry.id);
                    var toastMessage = entry.reward > 0
                        ? $"Added {entry.reward} FZ to your balance"
                        : "Match result logged";
                    UiManager.Instance?.ShowToast(toastMessage);
                    return true;
                }

                var delay = (attempt + 1) * RetryDelayMs;
                await UniTask.Delay(delay, cancellationToken: token);
            }

            return false;
        }

        public static async UniTask ProcessPendingRewardsAsync(CancellationToken token)
        {
            var pending = PendingPayoutStore.GetAll();
            foreach (var entry in pending)
            {
                if (token.IsCancellationRequested)
                    break;

                await TryApplyRewardAsync(entry, token);
            }
        }
    }
}
