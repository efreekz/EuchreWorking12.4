using System;
using System.Collections.Generic;
using System.Linq;
using Controllers;
using Fusion;
using Network;
using Newtonsoft.Json;
using UnityEngine;

namespace Managers
{
    public class RPCManager : NetworkBehaviour
    {
        [Networked, Capacity(4)]
        public NetworkArray<PlayerGameData> JoinedPlayers => default;

        public static RPCManager Instance;

        public bool AllPlayersInitialized
        {
            get
            {
                return JoinedPlayers.All(joinedPlayer => joinedPlayer.IsInitialized);
            }
        }

        // Bot name pool
        private static readonly string[] BotNames = new string[]
        {
            "RichW", "UncleJoe", "LannyW", "RickV", "MattB", "BethL", "SeanW", "DanP",
            "AlexaS", "DavidC", "PatrickF", "LonnieA", "JenniferD", "LyndaT", "ChuckH",
            "KevinF", "HarveyH", "KenZ", "AndyJ", "DerekK", "BillN", "JeffA",
            "JedT", "RussellH", "TimK", "DebN", "DorothyS", "JenniferR", "JonathanP",
            "TimD", "AndrewF"
        };

        // Track used bot names in current game to avoid duplicates
        private List<string> _usedBotNames = new List<string>();
        
        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(this);
        }

        #region Seat
        
        public int GetFilledCount() {
            int c = 0;
            for (int i = 0; i < JoinedPlayers.Length; i++) {
                if (JoinedPlayers[i].Occupied) c++;
            }
            return c;
        }
        
        private int FindSeatForHumanJoin() {
            for (int i = 0; i < JoinedPlayers.Length; i++) {
                if (!JoinedPlayers[i].Occupied) return i;
            }
            for (int i = 0; i < JoinedPlayers.Length; i++) {
                if (JoinedPlayers[i].Occupied && JoinedPlayers[i].IsBot) return i;
            }
            return -1;
        }

        private void AssignHumanToSeat(PlayerInfo playerInfo, PlayerRef player) 
        {
            int seat = FindSeatForHumanJoin();
            if (seat < 0) {
                Runner.Disconnect(player);
                return;
            }

            // If replacing a bot, despawn it here (if you actually spawned bot objects)
            if (JoinedPlayers[seat].Occupied && JoinedPlayers[seat].IsBot) {
                // Despawn bot entity here if needed
            }

            var playerData = JoinedPlayers[seat];
            playerData.Occupied = true;
            playerData.IsBot = false;
            playerData.PlayerId = seat;
            playerData.PlayerRef = player;
            playerData.Name = playerInfo.Name;
            playerData.PLayerInfoId = playerInfo.Id;
            playerData.IsInitialized = true;
            
            JoinedPlayers.Set(seat, playerData);
            
            RPC_AddPlayer(playerData);
            
            GameLogger.ShowLog($"Player with Id {player.PlayerId} added to list");
        }

        public void SpawnBotAtSeat(int seatIndex) 
        {
            if (seatIndex < 0 || seatIndex >= JoinedPlayers.Length) return;
            
            var botData = JoinedPlayers[seatIndex];
            botData.Occupied = true;
            botData.IsBot = true;
            botData.PlayerId = seatIndex;
            botData.PlayerRef = MultiplayerManager.Instance.LocalPlayerRef;
            botData.Name = GetRandomBotName();
            botData.PLayerInfoId = "BOT";
            botData.IsInitialized = true;
            
            JoinedPlayers.Set(seatIndex, botData);

            RPC_AddPlayer(botData);
            
            GameLogger.ShowLog($"🤖 Bot '{botData.Name}' joined at seat {seatIndex}");
        }

        /// <summary>
        /// Gets a random bot name from the pool, avoiding duplicates when possible
        /// </summary>
        private string GetRandomBotName()
        {
            // If all names are used, reset the pool
            if (_usedBotNames.Count >= BotNames.Length)
            {
                _usedBotNames.Clear();
            }

            // Get available names (not yet used in this game)
            var availableNames = BotNames.Where(name => !_usedBotNames.Contains(name)).ToList();

            // If somehow no names available (shouldn't happen), use all names
            if (availableNames.Count == 0)
            {
                availableNames = BotNames.ToList();
            }

            // Select random name from available pool
            var selectedName = availableNames[UnityEngine.Random.Range(0, availableNames.Count)];
            _usedBotNames.Add(selectedName);

            return selectedName;
        }

        public void ClearSeat(int seatIndex) {
            if (seatIndex < 0 || seatIndex >= JoinedPlayers.Length) return;
            var d = JoinedPlayers[seatIndex];
            
            // If clearing a bot, remove its name from used list
            if (d.IsBot && !string.IsNullOrEmpty(d.Name.ToString()))
            {
                _usedBotNames.Remove(d.Name.ToString());
            }
            
            d.Occupied = false;
            d.IsBot = false;
            d.PlayerRef = default;
            JoinedPlayers.Set(seatIndex, d);
        }
        
        
        #endregion
        
        #region Main Callbacks

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_AddPlayer(PlayerGameData playerData)
        {
            GameLogger.ShowLog($"Added Player : {playerData.PlayerRef.PlayerId} : playerName ; {playerData.Name}");
            MainMenuSceneController.Instance.OnPlayerJoined(JoinedPlayers);
        }
        
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_SendPlayerData(string playerInfoJson, PlayerRef playerRef)
        {
            var playerInfo = JsonConvert.DeserializeObject<PlayerInfo>(playerInfoJson);
            GameLogger.ShowLog($"Received Player Data for Index {playerRef.PlayerId}: {playerInfo.Name}");

            AssignHumanToSeat(playerInfo, playerRef);
        }

        #endregion
    }
}