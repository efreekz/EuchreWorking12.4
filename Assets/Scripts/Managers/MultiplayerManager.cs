using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Controllers;
using Cysharp.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using Network;
using Newtonsoft.Json;
using Test;
using Ui.MainMenuScreens;
using UIArchitecture;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using static System.String;
using GameMode = Fusion.GameMode;
using Random = UnityEngine.Random;

namespace Managers
{
    public struct PlayerGameData : INetworkStruct, IEquatable<PlayerGameData>
    {
        public int PlayerId;
        public PlayerRef PlayerRef;
        public NetworkBool IsBot;
        public NetworkBool Occupied;

        public NetworkString<_16> PLayerInfoId;
        public NetworkString<_64> Name;
        public NetworkBool IsInitialized; 
        
        public bool Equals(PlayerGameData other)
        {
            return PlayerId == other.PlayerId && PlayerRef.Equals(other.PlayerRef) && IsBot.Equals(other.IsBot);
        }

        public override bool Equals(object obj)
        {
            return obj is PlayerGameData other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(PlayerId, PlayerRef, IsBot);
        }
    }
    
    public class MultiplayerManager : MonoBehaviour, INetworkRunnerCallbacks
    {
        public static MultiplayerManager Instance;

        public NetworkRunner Runner { get; private set; }
        private NetworkSceneManagerDefault SceneManager { get; set; }

        public Action<List<SessionInfo>> OnSessionListUpdate;
        
        private List<SessionInfo> _sessionList = new();
        private bool _sessionListReceived = false;
        
        private int _localPlayerIndex = -1;
        private PlayerRef _localPlayerRef;
        public int LocalPlayerIndex => _localPlayerIndex;
        public PlayerRef LocalPlayerRef => _localPlayerRef;
        
        private CancellationTokenSource _botSpawningCts;
        private CancellationTokenSource _genericCts;
        public float timeToAutomaticStartGame = 10f;
        public float timeToWaitForPlayersInQueue = 5f;
        private const int MaxPlayerCount = 4;

        public RPCManager rpcManagerPrefab;
        private bool _isPrivateGame;
        
        [Header("Testing")]
        [SerializeField] private bool immediateBotSpawning;
        public RPCManager RPCManager { get; set; }

        // public TestPlayer PlayerPrefab;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _genericCts = new CancellationTokenSource();
        }

        private void OnDestroy()
        {
            _botSpawningCts?.Cancel();
            _botSpawningCts?.Dispose();
            _genericCts?.Cancel();
            _genericCts?.Dispose();
        }
        
        public async UniTask<bool> StartPublicGame(SessionData sessionData)
        {
            // If Runner exists, wait for shutdown to complete
            if (Runner != null)
            {
                GameLogger.LogNetwork("Runner exists from previous session. Waiting for cleanup...", GameLogger.LogType.Warning);
                await UniTask.WaitUntil(() => Runner == null, cancellationToken: _genericCts.Token);
                await UniTask.Delay(500); // Extra safety delay
            }

            // Validate user data before connecting
            if (GameManager.UserData == null)
            {
                GameLogger.LogNetwork("Cannot start game - User not logged in", GameLogger.LogType.Error);
                UiManager.Instance.ShowToast("Please log in again");
                return false;
            }

            Runner = gameObject.AddComponent<NetworkRunner>();
            Runner.ProvideInput = true;
            Runner.AddCallbacks(this);

            _isPrivateGame = false;

            var scene = SceneRef.FromIndex(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();

            var startGameArgs = new StartGameArgs()
            {
                GameMode = GameMode.AutoHostOrClient,
                SessionName = sessionData.ID,
                Scene = scene,
                SceneManager = SceneManager
            };
            
            var result = await Runner.StartGame(startGameArgs);

            if (result.Ok)
            {
                Debug.Log("Game started successfully.");
                ActuallyStartGame().Forget();
                return true;
            }
            else
            {
                Debug.LogError($"StartGame failed. Reason: {result.ShutdownReason}, Error: {result.ErrorMessage}");
                UiManager.Instance.ShowToast(result.ErrorMessage);
                return false;
            }
        }
        
        public async UniTask<(bool, string)> StartPrivateGame(string sessionName = "")
        {
            // If Runner exists, wait for shutdown to complete
            if (Runner != null)
            {
                Debug.LogWarning("Runner exists from previous session. Waiting for cleanup...");
                await UniTask.WaitUntil(() => Runner == null, cancellationToken: _genericCts.Token);
                await UniTask.Delay(500); // Extra safety delay
            }

            // Validate user data before connecting
            if (GameManager.UserData == null)
            {
                Debug.LogError("Cannot start game - User not logged in");
                UiManager.Instance.ShowToast("Please log in again");
                return (false, null);
            }
            
            _isPrivateGame = true; // ✅ Mark as private game
            
            Runner = gameObject.AddComponent<NetworkRunner>();
            Runner.ProvideInput = true;
            Runner.AddCallbacks(this);
            
            var scene = SceneRef.FromIndex(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();
            
            var isHost = string.IsNullOrEmpty(sessionName);
            if (isHost)
                sessionName = Guid.NewGuid().ToString("N").Substring(0, 6);

            var startGameArgs = new StartGameArgs()
            {
                GameMode = isHost ? Fusion.GameMode.Host : Fusion.GameMode.Client,
                SessionName = sessionName,
                Scene = scene,
                SceneManager = SceneManager,
                // ✅ Important: don't advertise in public lobby
                IsVisible = false,  

                // ✅ (Optional) Tag it as private for extra safety
                CustomLobbyName = null,
                PlayerCount = 4
            };

            var result = await Runner.StartGame(startGameArgs);

            if (result.Ok)
            {
                Debug.Log(isHost ? $"Created room: {sessionName}" : $"Joined room: {sessionName}");
                ActuallyStartGame().Forget();
                return (true, sessionName);
            }
            else
            {
                Debug.LogError($"Failed to start/join room: {result.ShutdownReason}");
                UiManager.Instance.ShowToast(result.ErrorMessage);
                return (false, null);
            }
        }

        public async UniTask ShutDown()
        {
            Debug.Log("🔻 Shutting down MultiplayerManager...");

            try
            {
                // --- Runner cleanup ---
                if (Runner != null)
                {
                    // Remove callbacks first to avoid dangling events
                    Runner.RemoveCallbacks(this);

                    // Shut down the Fusion runner safely
                    try
                    {
                        if (!Runner.IsShutdown)
                        {
                            await Runner.Shutdown();
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"❌ Error shutting down Runner: {ex.Message}");
                    }

                    // Destroy the runner GameObject
                    Destroy(Runner);
                    Runner = null;
                }

                // --- Scene Manager cleanup ---
                if (SceneManager != null)
                {
                    Destroy(SceneManager);
                    SceneManager = null;
                }

                // --- (Optional) RPC Manager cleanup ---
                // If you keep a persistent RPCManager, despawn & clear players here.
                if (RPCManager != null && RPCManager.TryGetComponent(out NetworkObject netObj))
                {
                    if (Runner != null && Runner.IsServer)
                    {
                        Runner.Despawn(netObj);
                    }
                    Destroy(RPCManager.gameObject);
                    RPCManager = null;
                }

                // --- Session cleanup ---
                _sessionList.Clear();
                _sessionListReceived = false;

                if (gameObject == null)
                {
                    Destroy(gameObject);
                }
                
                Debug.Log("✅ MultiplayerManager shutdown completed.");
            }
            catch (Exception e)
            {
                Debug.LogError($"⚠️ MultiplayerManager shutdown encountered an error: {e.Message}");
            }
        }

        private CancellationTokenSource _cts = new CancellationTokenSource();
        private bool _isWaitingForPlayers;
        
        private async UniTask StartPublicGameAutomatically(SessionData sessionData = null)
        {
            if (_isWaitingForPlayers)
            {
                GameLogger.LogNetwork("⚠️ StartPublicGameAutomatically already running, ignoring duplicate call.");
                return;
            }

            _isWaitingForPlayers = true; // Mark as running
            
            var waitTime = 30f;
            var elapsed = 0f;

            if (_cts != null)
            {
                _cts?.Cancel();
                _cts?.Dispose();
            }
            
            _cts = new CancellationTokenSource();

            GameLogger.LogNetwork("⏳ Waiting for players to join the session...");

            try
            {
                while (elapsed < waitTime)
                {
                    var filled = RPCManager.GetFilledCount();
                    var required = sessionData != null ? sessionData.Players.Count : MaxPlayerCount;

                    if (filled >= required)
                    {
                        GameLogger.LogNetwork("🎮 All players joined early. Starting game...");
                        break;
                    }

                    await UniTask.Delay(500, cancellationToken: _cts.Token);
                    elapsed += 0.5f;
                }

                var filled1 = RPCManager.GetFilledCount();
                var missing = MaxPlayerCount - filled1;

                GameLogger.LogNetwork($"⏱️ Wait time over. Filled: {filled1}, Spawning {missing} bots...");

                if (missing > 0)
                {
                    await SpawnBots(missing);
                }

                GameLogger.LogNetwork("🚀 All seats filled. Loading gameplay scene...");
            }
            catch (Exception ex)
            {
                GameLogger.LogNetwork($"❌ Error in StartGameAutomatically: {ex.Message}", GameLogger.LogType.Error);
            }
            finally
            {
                _cts?.Cancel();
                _cts?.Dispose();
                _cts = null;
                _isWaitingForPlayers = false; // Reset flag when finished
            }
        }

        private async UniTask SpawnBots(int botCount)
        {
            Debug.Log($"[Server] Spawning {botCount} bots...");
            _botSpawningCts?.Cancel();
            _botSpawningCts?.Dispose();
            _botSpawningCts = new CancellationTokenSource();

            int timeSpan;
            for (var i = 0; i < botCount; i++)
            {
                
                if (!immediateBotSpawning)
                {
                    timeSpan = Random.Range(500, 1000);
                    await UniTask.Delay(timeSpan,  cancellationToken: _botSpawningCts.Token);
                }
                
                var currentIndex = RPCManager.GetFilledCount();
                RPCManager.SpawnBotAtSeat(currentIndex);

            }
            
            timeSpan = Random.Range(2000, 4000);
            await UniTask.Delay(timeSpan,  cancellationToken: _botSpawningCts.Token);
        }

        private async UniTask ActuallyStartGame()
        {
            await UniTask.WaitUntil(() =>
            {
                RPCManager = FindFirstObjectByType<RPCManager>();
                return RPCManager;
            });
            
            Debug.Log("RpcManager has been Spawned");
            
            await UniTask.WaitUntil(() => RPCManager.GetFilledCount() == MaxPlayerCount);
            
            if (RPCManager.GetFilledCount() == MaxPlayerCount)
            {
                await UniTask.WaitUntil(() => RPCManager.Instance.AllPlayersInitialized);
                await UniTask.Delay(3000,  cancellationToken: _genericCts.Token);
                GameManager.LoadScene(SceneName.GamePlay);
            }
            else
            {
                Debug.LogError($"Invalid Player Count {RPCManager.GetFilledCount()}");
            }
        }

        public async void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"Player joined : {player.PlayerId}");
            if (runner.IsServer)
            {
                RPCManager ??= runner.Spawn(rpcManagerPrefab, Vector3.zero, Quaternion.identity, player);

                if (!_isPrivateGame)
                {
                    _ = StartPublicGameAutomatically(GameManager.JoinedSessionData);
                }
            }
            
            if (player == runner.LocalPlayer)
            {
                if (RPCManager == null)
                {
                    await UniTask.WaitUntil(() => FindAnyObjectByType<RPCManager>() != null);
                    RPCManager = FindAnyObjectByType<RPCManager>();
                    Debug.Log("RPCManager found and initialized!");
                }
                
                var playerInfo = new PlayerInfo()
                {
                    Id = GameManager.UserData.id.ToString(),
                    Name = GameManager.UserData.username
                };
                
                var playerInfoJson = JsonConvert.SerializeObject(playerInfo);

                RPCManager.RPC_SendPlayerData(playerInfoJson, player);
                
                await UniTask.WaitUntil(() => RPCManager.JoinedPlayers.Any(p => p.PlayerRef == player));
                var myData = RPCManager.JoinedPlayers.First(p => p.PlayerRef == player);
            
                Debug.Log($"Setting Up Local Player at index {player.PlayerId - 1}");
                _localPlayerRef = player;
                _localPlayerIndex = myData.PlayerId;

            }
        }


        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"Player Left : {player.PlayerId}");
        }
        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            Debug.LogWarning($"🔻 Runner shutdown callback. Reason: {shutdownReason}");
            // Don't call ShutDown() here - already shutting down!
            // Calling it creates a loop and race conditions
            // ShutDown() will be called explicitly when needed
        }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            Debug.LogWarning($"⚠️ Disconnected from server. Reason: {reason}");
            
            // Only return to main menu if we're not already there
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != SceneName.MainMenu.ToString())
            {
                UiManager.Instance.ShowToast($"Disconnected: {reason}");
                _ = ShutDown();
                GameManager.LoadScene(SceneName.MainMenu);
            }
        }

        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }

        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
        {
            _sessionList = sessionList;
            _sessionListReceived = true;
            OnSessionListUpdate?.Invoke(sessionList);
        }
        
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player){ }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player){ }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data){ }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress){ }
    }

}
