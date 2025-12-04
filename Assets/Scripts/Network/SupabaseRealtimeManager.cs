using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NativeWebSocket;
using Newtonsoft.Json;
using UnityEngine;

namespace Network
{
    public class SupabaseRealtimeManager : MonoBehaviour
    {
        private const string SupabaseUrl = "wss://evbrcrmyvxqeuomaocvz.supabase.co/realtime/v1/websocket";
        private const string SupabaseAnonKey = "sb_publishable_Hy7pxKobWznbLtBXrpklUA_-z-uPVdN";

        private WebSocket _websocket;

        public event Action<SessionData> OnLobbyAdded;
        public event Action<SessionData> OnLobbyUpdated;
        public event Action<string> OnLobbyDeleted;
        
        private const float HeartbeatInterval = 30f;
        private CancellationTokenSource _heartbeatToken;
        private bool _isShuttingDown;
        private bool _isReconnecting;

        
        public static SupabaseRealtimeManager Instance { get; private set; }
        
        public bool Initialized { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private async void OnDestroy()
        {
            _isShuttingDown = true; // ✅ Prevent reconnection
            Initialized = false;
            StopHeartbeatLoop();

            if (_websocket != null)
            {
                try
                {
                    await _websocket.Close();
                }
                catch (Exception ex)
                {
                    GameLogger.LogNetwork($"⚠️ Error while closing websocket: {ex.Message}");
                }

                _websocket = null;
            }
        }


        
        private void Start()
        {
            ConnectToRealtime().Forget();
        }

        private void Update()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (_websocket != null)
            {
                _websocket.DispatchMessageQueue();
            }
#endif
        }
        private async void LateUpdate()
        {
            if (_websocket == null) return;
            if (_websocket.State == WebSocketState.Closed)
                GameLogger.LogNetwork("⚠️ WebSocket closed unexpectedly");
            if (_websocket.State == WebSocketState.Closing)
                GameLogger.LogNetwork("⏳ WebSocket closing");
        }

        
        private async UniTask ConnectToRealtime()
        {
            string fullUrl = $"{SupabaseUrl}?apikey={SupabaseAnonKey}&vsn=1.0.0";
            _websocket = new WebSocket(fullUrl);

            _websocket.OnOpen += OnOpenConnection;
            _websocket.OnError += e => GameLogger.LogNetwork($"❌ Socket error: {e}", GameLogger.LogType.Error);
            _websocket.OnClose += OnWebsocketOnOnClose;

            _websocket.OnMessage += HandleRealtimeMessage;
        
            await _websocket.Connect();
        }

        private async void OnWebsocketOnOnClose(WebSocketCloseCode e)
        {
            if (_isShuttingDown) // ✅ Skip reconnection during shutdown
            {
                GameLogger.LogNetwork("🛑 WebSocket closed due to shutdown — not reconnecting.");
                return;
            }

            if (_isReconnecting) return;
            _isReconnecting = true;

            GameLogger.LogNetwork($"⚠️ WebSocket closed ({e}), reconnecting...");
            StopHeartbeatLoop();

            await UniTask.Delay(TimeSpan.FromSeconds(1), cancellationToken: this.GetCancellationTokenOnDestroy());

            await ConnectToRealtime();

            _isReconnecting = false;
        }



        private async void OnOpenConnection()
        {
            GameLogger.LogNetwork("✅ Connected bare socket");
                
            // ✅ Correct join message format
            var refId = Guid.NewGuid().ToString();

            var joinMessage = new
            {
                topic = "realtime:public:lobbies",
                @event = "phx_join",
                payload = new { },
                @ref = refId
            };
                
            var json = JsonConvert.SerializeObject(joinMessage);
            GameLogger.LogNetwork($"📡 Joining channel: {json}");
            await _websocket.SendText(json);
               
            
            refId = Guid.NewGuid().ToString();
            // ✅ Subscribe to Postgres changes for INSERT/UPDATE/DELETE
            var subscribeMessage = new
            {
                topic = "realtime:public:lobbies",
                @event = "postgres_changes",
                payload = new
                {
                    @event = "*", // can be "INSERT", "UPDATE", "DELETE", or "*" for all
                    schema = "public",
                    table = "lobbies"
                },
                @ref = refId
            };
            
            var subJson = JsonConvert.SerializeObject(subscribeMessage);
            GameLogger.LogNetwork($"📡 Subscribing to changes: {subJson}");
            await _websocket.SendText(subJson);
            
            StopHeartbeatLoop();
            StartHeartbeatLoopAsync().Forget(); // fire and forget UniTask
            
            Initialized = true;
        }

        private void HandleRealtimeMessage(byte[] data)
        {
            var message = System.Text.Encoding.UTF8.GetString(data);
            
            GameLogger.LogNetwork($"📡 RealTime message: {message}");

            if (string.IsNullOrEmpty(message) || !message.Contains("\"event\""))
                return;

            RealtimeMessageWrapper wrapper;

            try
            {
                wrapper = JsonConvert.DeserializeObject<RealtimeMessageWrapper>(message);
            }
            catch (Exception ex)
            {
                GameLogger.LogNetwork($"❌ Failed to deserialize realtime message: {ex.Message}", GameLogger.LogType.Error);
                return;
            }

            if (wrapper?.payload == null)
            {
                GameLogger.LogNetwork("⚠️ Received realtime message with no payload", GameLogger.LogType.Warning);
                return;
            }

            var eventType = wrapper.payload.type?.ToUpperInvariant();
            if (string.IsNullOrEmpty(eventType))
                return;

            try
            {
                switch (eventType)
                {
                    case "INSERT":
                        var newLobby = ConvertToSessionData(wrapper.payload.record);
                        OnLobbyAdded?.Invoke(newLobby);
                        GameLogger.LogNetwork($"🟢 New lobby added: {newLobby.ID}");
                        break;

                    case "UPDATE":
                        var updatedLobby = ConvertToSessionData(wrapper.payload.record);
                        OnLobbyUpdated?.Invoke(updatedLobby);
                        GameLogger.LogNetwork($"🟡 Lobby updated: {updatedLobby.ID}");
                        break;

                    case "DELETE":
                        string deletedId = wrapper.payload.old_record?.id;
                        if (!string.IsNullOrEmpty(deletedId))
                        {
                            OnLobbyDeleted?.Invoke(deletedId);
                            GameLogger.LogNetwork($"🔴 Lobby deleted: {deletedId}");
                        }
                        break;

                    default:
                        GameLogger.LogNetwork($"⚪ Unhandled realtime event type: {eventType}");
                        break;
                }
            }
            catch (Exception ex)
            {
                GameLogger.LogNetwork($"❌ Error handling realtime message: {ex.Message}", GameLogger.LogType.Error);
            }
        }

        
        private SessionData ConvertToSessionData(RealtimeLobbyRecord record)
        {
            if (record == null) return null;

            if (record.created_at != null)
                return new SessionData()
                {
                    ID = record.id,
                    CreatedAt = record.created_at.Value,
                    TimeToStart = record.time_to_live ?? 0,
                    Fee = record.fee ?? 0,
                    Players = record.players
                };
            else
                return null;
        }
       
        private void StopHeartbeatLoop()
        {
            if (_heartbeatToken != null)
            {
                _heartbeatToken.Cancel();
                _heartbeatToken.Dispose();
                _heartbeatToken = null;
            }
        }
        private async UniTaskVoid StartHeartbeatLoopAsync()
        {
            _heartbeatToken = new CancellationTokenSource();

            while (!_heartbeatToken.Token.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(HeartbeatInterval), cancellationToken: _heartbeatToken.Token);

                if (_websocket is not { State: WebSocketState.Open })
                    continue;

                try
                {
                    // Phoenix Realtime heartbeat (official Supabase format)
                    var heartbeat = new
                    {
                        topic = "phoenix",
                        @event = "heartbeat",
                        payload = new { },
                        @ref = Guid.NewGuid().ToString()
                    };

                    var json = JsonConvert.SerializeObject(heartbeat);
                    await _websocket.SendText(json);
                    GameLogger.LogNetwork("💓 Sent UniTask heartbeat to Supabase");
                }
                catch (Exception ex)
                {
                    GameLogger.LogNetwork($"❌ Heartbeat failed: {ex.Message}", GameLogger.LogType.Error);
                }
            }
        }

    }
    
    [Serializable]
    public class RealtimeMessageWrapper
    {
        public string topic { get; set; }
        public string @event { get; set; }
        public RealtimePayload payload { get; set; }
    }

    [Serializable]
    public class RealtimePayload
    {
        public string table { get; set; }
        public string type { get; set; }  // INSERT, UPDATE, DELETE
        public List<RealtimeColumn> columns { get; set; }
        public RealtimeLobbyRecord record { get; set; }
        public RealtimeLobbyRecord old_record { get; set; }
        public string schema { get; set; }
        public string commit_timestamp { get; set; }
    }

    [Serializable]
    public class RealtimeColumn
    {
        public string name { get; set; }
        public string type { get; set; }
    }

    [Serializable]
    public class RealtimeLobbyRecord
    {
        public string id { get; set; }
        public string created_by { get; set; }
        public DateTime? created_at { get; set; }
        public int? time_to_live { get; set; }
        public int? fee { get; set; }
        public List<PlayerInfo> players { get; set; }
    }

}