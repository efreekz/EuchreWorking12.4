using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Data;
using Fusion;
using Managers;
using Network;
using Ui.MainMenuScreens;
using UIArchitecture;
using UnityEngine;

namespace Controllers
{
    public class MainMenuSceneController : MonoBehaviour
    {
        public static MainMenuSceneController Instance;
        
        private Views _waitingPanel;
        
        private PublicMatchMakingPanel _publicMatchMakingPanel;

        
        private void Awake()
        {
            Instance = this;
        }

        private async void Start()
        {
            UiManager.Instance.LoadSceneUi(SceneName.MainMenu);
            _publicMatchMakingPanel = (UiManager.Instance.GetUiView(UiScreenName.MainMenuHomeScreen) as MainMenuHomeScreen)?.publicMatchMakingPanel;
            if (_publicMatchMakingPanel == null)
            {
                GameLogger.ShowLog("Could not find publicMatchMakingPanel", GameLogger.LogType.Error);
                return;
            }

            // CheckForAutomaticLogin already handled in Login scene
            await _publicMatchMakingPanel.Initialize();
            GameManager.CheckForWinScreen();
        }

        private void OnDestroy()
        {
            _publicMatchMakingPanel.Cleanup();
        }

        public void OnPlayerJoined(NetworkArray<PlayerGameData> player)
        {
            var waitingPanel = UiManager.Instance.GetUiView(UiScreenName.MatchMakingPanel) as MatchMakingPanel;
            waitingPanel?.OnPlayerJoined(player);
        }
    }
}