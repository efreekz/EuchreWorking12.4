using System;
using Controllers;
using Cysharp.Threading.Tasks;
using Managers;
using UIArchitecture;
using UnityEngine;

namespace GamePlay
{
    public class GameInitializer : MonoBehaviour
    {
        [SerializeField] private GamePlayControllerNetworked gameplayControllerNetworkedPrefab;

        private GamePlayControllerNetworked _gameplayControllerNetworkedInstance;

        private static GameInitializer Instance { get; set; }
        private async void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            UiManager.Instance.LoadSceneUi(SceneName.GamePlay);

            var runner = MultiplayerManager.Instance.Runner;

            if (runner == null)
            {
                GameLogger.ShowLog("Runner not available for spawning!", GameLogger.LogType.Error);
                return;
            }

            if (runner.IsServer)
            {

                _gameplayControllerNetworkedInstance = runner.Spawn(
                    gameplayControllerNetworkedPrefab,
                    Vector3.zero,
                    Quaternion.identity
                );
            }
            // Wait until the gameplay controller is replicated to this client
            await UniTask.WaitUntil(() =>
            {
                if (_gameplayControllerNetworkedInstance != null) 
                    return true;

                var found = FindFirstObjectByType<GamePlayControllerNetworked>();
                        
                if (found == null) return false;
                        
                _gameplayControllerNetworkedInstance = found;
                return true;

            });

            await _gameplayControllerNetworkedInstance.Initialize();
            
            // Deduct entry fee for local player before game starts
            var lobbyData = GameManager.JoinedSessionData;
            if (lobbyData != null)
            {
                GameLogger.LogNetwork($"💰 Deducting entry fee: {lobbyData.Fee} FZ for lobby {lobbyData.ID}");
                var deducted = await CurrencyManager.DeductEntryFee(lobbyData.ID, lobbyData.Fee);
                
                if (deducted)
                {
                    GameLogger.LogNetwork($"✅ Entry fee deducted successfully. New balance: {CurrencyManager.Freekz} FZ");
                }
                else
                {
                    GameLogger.LogNetwork($"⚠️ Failed to deduct entry fee. Game will continue anyway.", GameLogger.LogType.Warning);
                }
            }
            else
            {
                GameLogger.LogNetwork("⚠️ No lobby data found. Skipping entry fee deduction.", GameLogger.LogType.Warning);
            }
                    
            _gameplayControllerNetworkedInstance.StartGame().Forget();

            // Only show the panel once controller is confirmed available
            UiManager.Instance.ShowPanel(UiScreenName.GamePlayScreens, null);
        }
    }
}