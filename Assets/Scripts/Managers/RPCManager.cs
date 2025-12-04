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
            botData.Name = $"Bot {botData.PlayerId + 1}";
            botData.PLayerInfoId = "BOT";
            botData.IsInitialized = true;
            
            JoinedPlayers.Set(seatIndex, botData);

            RPC_AddPlayer(botData);
            
            GameLogger.ShowLog($"🤖 Bot {seatIndex + 1} joined with simulated PlayerRef {botData.PlayerId}");
        }

        public void ClearSeat(int seatIndex) {
            if (seatIndex < 0 || seatIndex >= JoinedPlayers.Length) return;
            var d = JoinedPlayers[seatIndex];
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