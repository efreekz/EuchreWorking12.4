using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Fusion;
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
    public class MatchMakingPanel : FullScreenView
    {
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text roomNameText;
        [SerializeField] private Button copyCode;

        [SerializeField] private MatchMakingPlayerUi[] playersUI;

        [SerializeField] private MultiplayerManager multiplayerManagerPrefab;
        
        private CancellationTokenSource _timeoutCts;
        private MatchMakingPanelData _matchMakingPanelData;

        protected async override void Initialize(Object obj)
        {
            if (obj is MatchMakingPanelData matchMakingPanelData)
            {
                copyCode.gameObject.SetActive(false);
                _matchMakingPanelData = matchMakingPanelData;

                if (_matchMakingPanelData.IsPrivate)
                {
                    await StartPrivateGame();
                }
                else
                {
                    await StartPublicGame(matchMakingPanelData.SessionData);
                }
            }
            else
            {
                GameLogger.ShowLog("Invalid Data", GameLogger.LogType.Error);
            }
        }

        protected override void Cleanup()
        {
            statusText.text = "";
            roomNameText.text = "";
            copyCode.onClick.RemoveAllListeners();

            _timeoutCts?.Cancel();
            _timeoutCts?.Dispose();
            _timeoutCts = null;
        }

        
        private async UniTask StartPublicGame(SessionData sessionData)
        {
            var roomName = $"{sessionData.ID}";
            statusText.text = $"Joining Room: {roomName}";

            if (MultiplayerManager.Instance == null)
                Instantiate(multiplayerManagerPrefab);

            await UniTask.WaitForEndOfFrame();

            // var result = await CurrencyManager.SubtractFreekz(sessionData.Fee, "Game Played",
            //     $"You played a game : {sessionData.ID}");
            // if (!result)
            // {
            //     UiManager.Instance.ShowToast("You dont have Freekz to start this game");
            //     UiManager.Instance.HidePanel(this);
            //     return;
            // }

            var joined = await MultiplayerManager.Instance.StartPublicGame(sessionData);

            if (joined)
            {
                statusText.text = "Connected to room!";
                GameManager.JoinedSessionData = sessionData;
                GameManager.GamesResult = null;
            }
            else
            {
                statusText.text = "Failed to connect to room.";
            }
        }

        private async UniTask StartPrivateGame()
        {
            var roomName = _matchMakingPanelData.RoomName;
            var shouldBeHost = string.IsNullOrEmpty(roomName);
            statusText.text = shouldBeHost ? $"Creating Room {roomName}" : $"Joining Room : {roomName}";
            
            Instantiate(multiplayerManagerPrefab);

            await UniTask.WaitForEndOfFrame();

            var joined = await MultiplayerManager.Instance.StartPrivateGame(roomName);

            if (!joined.Item1)
            {
                statusText.text = $"Failed to join {roomName}";
                await MultiplayerManager.Instance.ShutDown();
                UiManager.Instance.HidePanel(this);
                return;
            }

            statusText.text = "Waiting For Players";
            roomNameText.text = $"Room Code : {joined.Item2}";
            copyCode.gameObject.SetActive(true);
            copyCode.onClick.AddListener(() => OnCopyCodeClick(joined.Item2));
            for (var index = 0; index < playersUI.Length; index++)
            {
                var playerUi = playersUI[index];
                playerUi.Initialize(shouldBeHost, index);
            }
        }

        public void OnCopyCodeClick(string roomCode)
        {
            GUIUtility.systemCopyBuffer = roomCode; // Works for standalone/editor
        }

        public async void OnPlayerJoined(NetworkArray<PlayerGameData> players)
        {
            await UniTask.WaitUntil(() => MultiplayerManager.Instance.LocalPlayerIndex >= 0);

            for (int i = 0; i < players.Length; i++)
            {
                if (!players[i].Occupied) continue;
                
                var playerName = players[i].IsBot
                    ? $"Bot {players[i].PlayerId + 1}"
                    : $"{players[i].Name}";
                var displayIndex = (players[i].PlayerId - MultiplayerManager.Instance.LocalPlayerIndex + 4) % 4;
                
                GameLogger.LogNetwork(
                    $"playersList[i].PlayerId : {players[i].PlayerId} " +
                    $"\n MultiplayerManager.Instance.LocalPlayerIndex : {MultiplayerManager.Instance.LocalPlayerIndex} " +
                    $"\n Player Name : {playerName}");
                
                playersUI[displayIndex].SetData(playerName);
            }
        }
    }

    public class MatchMakingPanelData
    {
        // Private room Data
        public string RoomName = "";
        public bool IsPrivate = false;
        
        // public room Data
        public SessionData SessionData;
    }

}