using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cysharp.Threading.Tasks;
using Managers;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace Network
{
    public enum SessionState { Available, Expired }
    [Serializable]
    public class PlayerInfo
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("name")] public string Name;
    }

    [Serializable]
    public class SessionData
    {
        [JsonProperty("id")] public string ID;
        [JsonProperty("created_at")] public DateTime CreatedAt;
        [JsonProperty("time_to_live")] public int TimeToStart;
        [JsonProperty("fee")] public int Fee;
        [JsonProperty("players")] public List<PlayerInfo> Players = new();
        [JsonProperty("created_by")] public string CreatedBy;

        public SessionState SessionState
        {
            get
            {
                var expiryTime = CreatedAt.AddMinutes(TimeToStart);
                return SupabaseApiController.Now > expiryTime ? SessionState.Expired : SessionState.Available;
            }
        }

        public bool PlayerHasJoined
        {
            get
            {
                return Players.Any(player => player.Id == GameManager.UserData.id.ToString());
            }
        }

        [JsonIgnore]
        public int RemainingTime { get; set; }
    }
    
    public static class SupabaseApiController
    {
        private const string BaseURL = "https://evbrcrmyvxqeuomaocvz.supabase.co/rest/v1";
        private const string APIKey = "sb_publishable_Hy7pxKobWznbLtBXrpklUA_-z-uPVdN";
        private const string AuthorizationKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6ImV2YnJjcm15dnhxZXVvbWFvY3Z6Iiwicm9sZSI6ImFub24iLCJpYXQiOjE3NTk2NzkzMjgsImV4cCI6MjA3NTI1NTMyOH0.X16_eXjdOEmZdRVJPjYg3gAnl1y5TBrLHXuoJkjjFQ0";

        private static double ServerOffsetSeconds { get; set; } = 0;

        public static DateTime Now =>
            ServerOffsetSeconds == 0 ? DateTime.Now : DateTime.Now.AddSeconds(ServerOffsetSeconds);

        public static async UniTask SyncServerTime()
        {
            string url = $"{BaseURL}/rpc/now_utc";
            using var request = new UnityWebRequest(url, "POST");
            request.SetRequestHeader("apikey", APIKey);
            request.SetRequestHeader("Authorization", $"Bearer {AuthorizationKey}");
            request.SetRequestHeader("Content-Type", "application/json");
            request.downloadHandler = new DownloadHandlerBuffer();

            var operation = request.SendWebRequest();
            while (!operation.isDone)
                await UniTask.Yield();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var json = request.downloadHandler.text; 
                var serverTime = JsonConvert.DeserializeObject<string>(json);
                var parsedTime = DateTime.Parse(serverTime).ToLocalTime();

                ServerOffsetSeconds = (parsedTime - DateTime.Now).TotalSeconds;
                GameLogger.LogNetwork($"✅ Synced server time. Offset: {ServerOffsetSeconds:F2}s");
            }
            else
            {
                GameLogger.LogNetwork($"⚠️ Failed to sync server time: {request.error}", GameLogger.LogType.Warning);
                ServerOffsetSeconds = 0;
            }
        }
        public static async UniTask<List<SessionData>> GetAllLobbies()
        {
            try
            {
                // Create JSON payload (empty object, since RPC doesn't take params)
                var bodyRaw = Encoding.UTF8.GetBytes("{}");

                using var request = new UnityWebRequest($"{BaseURL}/rpc/get_active_lobbies", "POST");
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("apikey", APIKey);
                request.SetRequestHeader("Authorization", $"Bearer {AuthorizationKey}");
                request.SetRequestHeader("Content-Type", "application/json");

                var operation = request.SendWebRequest();
                while (!operation.isDone)
                    await UniTask.Yield();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"❌ Failed to fetch lobbies: {request.error}");
                    return new List<SessionData>();
                }

                var json = request.downloadHandler.text;

                if (string.IsNullOrEmpty(json) || json == "null")
                    return new List<SessionData>();

                var lobbies = JsonConvert.DeserializeObject<List<SessionData>>(json) ?? new List<SessionData>();
                return lobbies;
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Exception while getting lobbies: {ex.Message}");
                return new List<SessionData>();
            }
        }



        // ✅ CREATE a new lobby
        public static async UniTask<bool> CreateNewLobby(SessionData sessionData)
        {
            try
            {
                var payload = new
                {
                    id = sessionData.ID,
                    time_to_live = sessionData.TimeToStart,
                    fee = sessionData.Fee,
                    players = new List<PlayerInfo>(),
                    created_by = sessionData.CreatedBy,
                };

                var jsonBody = JsonConvert.SerializeObject(payload);
                var bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

                using var request = new UnityWebRequest($"{BaseURL}/lobbies", "POST");
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("apikey", APIKey);
                request.SetRequestHeader("Authorization", $"Bearer {AuthorizationKey}");
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Prefer", "return=representation");

                var operation = request.SendWebRequest();
                while (!operation.isDone)
                    await UniTask.Yield();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"❌ Failed to create lobby: {request.error}");
                    return false;
                }

                if (request.responseCode >= 200 && request.responseCode < 300)
                {
                    Debug.Log($"✅ Lobby created successfully: {request.downloadHandler.text}");
                    return true;
                }

                Debug.LogError($"❌ Server returned error {request.responseCode}: {request.downloadHandler.text}");
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Exception while creating lobby: {ex.Message}");
                return false;
            }
        }
        public static async UniTask<bool> JoinLobby(string lobbyId, PlayerInfo player)
        {
            var url = $"{BaseURL}/rpc/join_lobby";
            var json = JsonConvert.SerializeObject(new
            {
                lobby_id = lobbyId,
                new_player = player
            });

            using var request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("apikey", APIKey);
            request.SetRequestHeader("Authorization", $"Bearer {AuthorizationKey}");
            request.SetRequestHeader("Content-Type", "application/json");

            var op = request.SendWebRequest();
            while (!op.isDone)
                await UniTask.Yield();

            if (request.result == UnityWebRequest.Result.Success)
            {
                GameLogger.LogNetwork($"✅ Joined lobby {lobbyId} successfully. Response: {request.downloadHandler.text}");
                return true;
            }
            else
            {
                GameLogger.LogNetwork($"❌ Failed to join lobby: {request.error} - {request.downloadHandler.text}", GameLogger.LogType.Error);
                return false;
            }
        }
        public static async UniTask<bool> LeaveLobby(string lobbyId, string playerId)
        {
            var url = $"{BaseURL}/rpc/leave_session";
            var json = JsonConvert.SerializeObject(new
            {
                session_id = lobbyId,
                player_id = playerId
            });

            using var request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("apikey", APIKey);
            request.SetRequestHeader("Authorization", $"Bearer {AuthorizationKey}");
            request.SetRequestHeader("Content-Type", "application/json");

            var op = request.SendWebRequest();
            while (!op.isDone)
                await UniTask.Yield();

            if (request.result == UnityWebRequest.Result.Success)
            {
                GameLogger.LogNetwork($"✅ Player {playerId} left lobby {lobbyId}. Response: {request.downloadHandler.text}");
                return true;
            }
            else
            {
                GameLogger.LogNetwork($"❌ Failed to leave lobby: {request.error} - {request.downloadHandler.text}", GameLogger.LogType.Error);
                return false;
            }
        }
        
        public static async UniTask<bool> IsLobbyReady(string lobbyId)
        {
            var url = $"{BaseURL}/rpc/is_lobby_ready";
            var json = JsonConvert.SerializeObject(new
            {
                p_lobby_id = lobbyId
            });

            using var request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("apikey", APIKey);
            request.SetRequestHeader("Authorization", $"Bearer {AuthorizationKey}");
            request.SetRequestHeader("Content-Type", "application/json");

            var op = request.SendWebRequest();
            while (!op.isDone)
                await UniTask.Yield();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var responseText = request.downloadHandler.text;
                // Supabase RPC returns "true" or "false" as a string
                if (bool.TryParse(responseText, out var isReady))
                {
                    GameLogger.LogNetwork($"✅ Lobby {lobbyId} readiness check: {isReady}");
                    return isReady;
                }
                GameLogger.LogNetwork($"⚠️ Could not parse lobby readiness response for {lobbyId}: {responseText}", GameLogger.LogType.Warning);
                return false;
            }
            else
            {
                GameLogger.LogNetwork($"❌ Failed to check lobby readiness for {lobbyId}: {request.error} - {request.downloadHandler.text}", GameLogger.LogType.Error);
                return false;
            }
        }
    }
}