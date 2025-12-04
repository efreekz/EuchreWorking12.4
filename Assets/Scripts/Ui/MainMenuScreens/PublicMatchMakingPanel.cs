using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Helper;
using MainMenu;
using Managers;
using Network;
using TMPro;
using UIArchitecture;
using UnityEngine;
using UnityEngine.UI;
using Object = System.Object;

namespace Ui.MainMenuScreens
{
    public class PublicMatchMakingPanel : MonoBehaviour
    {
        public Transform content10;
        public Transform content20;
        public Transform content40;
        public MotivationCardObject motivationCardObjects10;
        public MotivationCardObject motivationCardObjects20;
        public MotivationCardObject motivationCardObjects40;

        public SessionCard sessionCardPrefab;

        public Button creatNewSession;


        private Dictionary<string, SessionCard> _sessionCards = new Dictionary<string, SessionCard>();

        public List<SessionCard> GetGroupedSessionCards(int fee) => (from sessionCard in _sessionCards where sessionCard.Value.Data.Fee == fee select sessionCard.Value).ToList();
        
        private SessionData _joinSessionData;
        private SessionCard _joinSessionCard;

        private CancellationTokenSource _timerCts;
        private MainMenuHomeScreen _homeScreen;
        
        public SessionData JoinedSession
        {
            get => _joinSessionData;
            set
            {
                _joinSessionData = value;
                OnUpdateJoinedSession(value);
            }
        }

        public async UniTask Initialize()
        {
            if (GameManager.UserData == null)
                return;
            
            _homeScreen ??= UiManager.Instance.GetUiView(UiScreenName.MainMenuHomeScreen) as MainMenuHomeScreen;
            
            creatNewSession.onClick.AddListener(OnClickCreateNewSession);

            await InitLobbies();

            SupabaseRealtimeManager.Instance.OnLobbyAdded += AddLobby;
            SupabaseRealtimeManager.Instance.OnLobbyDeleted += RemoveLobby;
            SupabaseRealtimeManager.Instance.OnLobbyUpdated += UpdateLobby;
        }

        public void Cleanup()
        {
            _timerCts?.Cancel();
            _timerCts?.Dispose();
            _timerCts = null;
            
            SupabaseRealtimeManager.Instance.OnLobbyAdded -= AddLobby;
            SupabaseRealtimeManager.Instance.OnLobbyDeleted -= RemoveLobby;
            SupabaseRealtimeManager.Instance.OnLobbyUpdated -= UpdateLobby;
            
            CloseSupabaseRealtime();
            
            creatNewSession.onClick.RemoveListener(OnClickCreateNewSession);

            foreach (var sessionCard in _sessionCards.Values.ToList())
            {
                RemoveSession(sessionCard);
            }
            
            _sessionCards.Clear();
        }
        private async UniTask StartSupabaseRealtime()
        {
            var token = this.GetCancellationTokenOnDestroy();

            if (SupabaseRealtimeManager.Instance == null)
            {
                var go = new GameObject("SupabaseRealtimeManager");
                go.AddComponent<SupabaseRealtimeManager>();
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, token);
            }
            await UniTask.WaitUntil(() => SupabaseRealtimeManager.Instance != null, cancellationToken: token);
            await UniTask.WaitUntil(() => SupabaseRealtimeManager.Instance.Initialized, cancellationToken: token);
        }

        private void CloseSupabaseRealtime()
        {
            Destroy(SupabaseRealtimeManager.Instance);
        }

        private void OnClickCreateNewSession()
        {
            UiManager.Instance.ShowPanel(UiScreenName.CreatNewSessionScreen, null);
        }

        private async UniTask InitLobbies()
        {
            await StartSupabaseRealtime();
            await SupabaseApiController.SyncServerTime();
            var lobbies = await SupabaseApiController.GetAllLobbies();
            SessionListUpdated(lobbies);
            
            CheckForEmptyScroller(10, motivationCardObjects10);
            CheckForEmptyScroller(20, motivationCardObjects20);
            CheckForEmptyScroller(40, motivationCardObjects40);
        }

        private void SessionListUpdated(List<SessionData> sessionList)
        {
            foreach (var session in sessionList)
            {
                var existingCard = _sessionCards.Values.FirstOrDefault(c => c.Data.ID == session.ID);

                if (existingCard != null)
                {
                    existingCard.Setup(session, this, _homeScreen);
                    continue;
                }
                
                AddLobby(session);
            }
        }
        
        private void AddLobby(SessionData session)
        {
            if (session == null)
                return;

            if (_sessionCards.ContainsKey(session.ID)) 
                return;

            if (session.SessionState == SessionState.Expired)
                return;

            SessionCard sessionCard;
            switch (session.Fee)
            {
                case 10:
                    sessionCard = Instantiate(sessionCardPrefab, content10);
                    _sessionCards.Add(session.ID, sessionCard);
                    sessionCard.Setup(session, this, _homeScreen);
                    CheckForEmptyScroller(10, motivationCardObjects10);
                    break;
                case 20:
                    sessionCard = Instantiate(sessionCardPrefab, content20);
                    _sessionCards.Add(session.ID, sessionCard);
                    sessionCard.Setup(session, this, _homeScreen);
                    CheckForEmptyScroller(20, motivationCardObjects20);
                    break;
                case 40:
                    sessionCard = Instantiate(sessionCardPrefab, content40);
                    _sessionCards.Add(session.ID, sessionCard);
                    sessionCard.Setup(session, this, _homeScreen);
                    CheckForEmptyScroller(40, motivationCardObjects40);
                    break;
                default:
                    GameLogger.ShowLog($"Unknown fee type: {session.Fee}", GameLogger.LogType.Error);
                    return;
            }
            
            

            GameLogger.LogNetwork($"🟢 Added lobby: {session.ID}");
        }

        private void UpdateLobby(SessionData lobby)
        {
            if (lobby == null)
            {
                GameLogger.LogNetwork("⚠️ UpdateLobby called with null lobby", GameLogger.LogType.Error);
                return;
            }

            if (!_sessionCards.TryGetValue(lobby.ID, out var card))
            {
                GameLogger.LogNetwork($"➕ Lobby not found in UI, adding new one: {lobby.ID}");
                AddLobby(lobby);
                return;
            }

            card.Setup(lobby, this, _homeScreen);
            GameLogger.LogNetwork($"🟡 Lobby updated in UI: {lobby.ID}");
        }


        private void RemoveLobby(string lobbyId)
        {
            if (!_sessionCards.TryGetValue(lobbyId, out var card)) return;
            
            RemoveSession(card);
            GameLogger.LogNetwork($"🔴 Removed lobby: {lobbyId}");
        }

        public async UniTask JoinSession(SessionData data)
        {
            if (data == null)
            {
                GameLogger.LogNetwork("⚠️ Cannot join session — data is null.", GameLogger.LogType.Error);
                return;
            }

            if (CurrencyManager.Freekz - data.Fee < 0)
            {
                UiManager.Instance.ShowToast("You dont have enough fee to join this lobby.");
                return;
            }

            if (JoinedSession != null)
            {
                GameLogger.LogNetwork($"⚠️ Already in session {JoinedSession.ID}. Leaving before joining new one...");

                var wait = UiManager.Instance.ShowPanel(UiScreenName.WaitingPanel, null);

                bool left = await SupabaseApiController.LeaveLobby(
                    JoinedSession.ID,
                    GameManager.UserData.id.ToString()
                );

                UiManager.Instance.HidePanel(wait);

                if (!left)
                {
                    UiManager.Instance.ShowToast("Failed to leave current session. Try again.");
                    GameLogger.LogNetwork("❌ Failed to leave previous session. Join aborted.", GameLogger.LogType.Error);
                    return;
                }

                JoinedSession = null;
                GameLogger.LogNetwork("✅ Successfully left previous session.");
            }

            var waitPanel = UiManager.Instance.ShowPanel(UiScreenName.WaitingPanel, null);


            var player = new PlayerInfo
            {
                Id = GameManager.UserData.id.ToString(),
                Name = GameManager.UserData.username
            };

            bool joined = await SupabaseApiController.JoinLobby(data.ID, player);
            UiManager.Instance.HidePanel(waitPanel);


            if (joined)
            {
                JoinedSession = data;
                UiManager.Instance.ShowToast($"You have joined the session");
                GameLogger.LogNetwork($"✅ {player.Name} joined session {data.ID}");
            }
            else
            {
                UiManager.Instance.ShowToast("Failed to join session. Lobby might be full.");
                GameLogger.LogNetwork($"❌ Failed to join session {data.ID} (possibly full or invalid).", GameLogger.LogType.Error);
            }
        }


        public async UniTask LeaveSession(SessionData data)
        {
            if (data == null)
            {
                GameLogger.LogNetwork("⚠️ Cannot leave session — session data is null.", GameLogger.LogType.Error);
                return;
            }

            var wait = UiManager.Instance.ShowPanel(UiScreenName.WaitingPanel, null);


            bool left = await SupabaseApiController.LeaveLobby(
                data.ID,
                GameManager.UserData.id.ToString()
            );

            UiManager.Instance.HidePanel(wait);

            if (left)
            {
                JoinedSession = null;
                UiManager.Instance.ShowToast($"You have left the session");
                GameLogger.LogNetwork($"✅ {GameManager.UserData.username} left session {data.ID}");
            }
            else
            {
                UiManager.Instance.ShowToast("Failed to leave session. Try again.");
                GameLogger.LogNetwork($"❌ Failed to leave session {data.ID}", GameLogger.LogType.Error);
            }
        }
        private void OnUpdateJoinedSession(SessionData data)
        {
            _timerCts?.Cancel();
            _timerCts = null;
            
            // GameManager.OnUpdateJoinSessionData(null);

            if (_joinSessionCard)
            {
                _joinSessionCard.Reset();
                _joinSessionCard.OnUpdateSessionData = null;
                _joinSessionCard = null;
                _homeScreen.OnUpdateJoinedSessionData(data);
            }
            
            
            if (data == null)
                return;

            if (!_sessionCards.TryGetValue(data.ID, out var card))
                return;

            _joinSessionCard = card;
            
            foreach (var sessionCardsValue in _sessionCards.Values)
                sessionCardsValue.Reset();

            _joinSessionCard.Reset();
            _joinSessionCard.transform.SetAsFirstSibling();
            
            _joinSessionCard.OnUpdateSessionData = _homeScreen.OnUpdateJoinedSessionData;
            _homeScreen.OnUpdateJoinedSessionData(data);

        }

        public void StartMatchMaking()
        {
            GameLogger.LogNetwork("Session timer expired — matchmaking logic will go here.");
            CloseSupabaseRealtime();
            UiManager.Instance.ShowPanel(UiScreenName.MatchMakingPanel, new MatchMakingPanelData
            {
                IsPrivate = false,
                RoomName = JoinedSession.ID,
                SessionData = JoinedSession
            });
        }
        
        public void RemoveSession(SessionCard sessionCard)
        {
            if (!sessionCard) return;
            
            Destroy(sessionCard.gameObject);
            _sessionCards.Remove(sessionCard.Data.ID);

            switch (sessionCard.Data.Fee)
            {
                case 10:
                    CheckForEmptyScroller(10, motivationCardObjects10);
                    break;
                case 20:
                    CheckForEmptyScroller(20, motivationCardObjects20);
                    break;
                case 40:
                    CheckForEmptyScroller(40, motivationCardObjects40);
                    break;
                default:
                    GameLogger.ShowLog($"Unknown fee type: {sessionCard.Data.Fee}", GameLogger.LogType.Error);
                    return;
            }
        }

        private void CheckForEmptyScroller(int fee, MotivationCardObject motivationCard)
        {
            var groupedCards = GetGroupedSessionCards(fee);
            if (groupedCards != null && groupedCards.Count > 0)
            {
                motivationCard.Set(false);
            }
            else
            {
                motivationCard.Set(true);
            }
            
        }
    }
    
}
