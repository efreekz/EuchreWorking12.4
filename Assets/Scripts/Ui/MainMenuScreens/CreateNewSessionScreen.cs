using System;
using System.Collections.Generic;
using System.Linq;
using Helper;
using Managers;
using Network;
using Newtonsoft.Json;
using TMPro;
using UIArchitecture;
using UnityEngine;
using UnityEngine.UI;
using Object = System.Object;

namespace Ui.MainMenuScreens
{
    public class CreateNewSessionScreen : PopUpView
    {
        public Button createButton;
        public Button cancelButton;
        public Slider timeSlider;

        public TMP_Text costText;
        public TMP_Text timeText;

        public float maxTimer = 5f;  // default 3 min
        public float minTimer = 1f;   // default 1 min

        public Toggle tokens10;
        public Toggle tokens20;
        public Toggle tokens40;

        private int _selectedFee;
        private int _selectedTime;
        private PublicMatchMakingPanel _publicMatchMakingPanel;
        
        public bool testing = false;
        public int overrideTimer = 10;
        

        protected override void Initialize(Object obj)
        {
            // Hook up UI events
            createButton.onClick.AddListener(OnCreateButtonClicked);
            cancelButton.onClick.AddListener(OnCancelButtonClicked);

            // Hook up listeners
            timeSlider.onValueChanged.AddListener(OnTimeSliderChanged);
            tokens10.onValueChanged.AddListener(isOn => { if (isOn) OnTokenSelected(10); });
            tokens20.onValueChanged.AddListener(isOn => { if (isOn) OnTokenSelected(20); });
            tokens40.onValueChanged.AddListener(isOn => { if (isOn) OnTokenSelected(40); });
            
            // Default values
            tokens10.isOn = true;
            timeSlider.maxValue = maxTimer;
            timeSlider.minValue = minTimer;
            timeSlider.value = maxTimer;
            
            _selectedFee = 10;
            _selectedTime = Mathf.RoundToInt(maxTimer);

            _publicMatchMakingPanel = (UiManager.Instance.GetUiView(UiScreenName.MainMenuHomeScreen) as MainMenuHomeScreen)?.publicMatchMakingPanel;
            
            // Update text displays
            UpdateTimeText(_selectedTime);
            UpdateCostText(_selectedFee);
        }

        protected override void Cleanup()
        {
            createButton.onClick.RemoveAllListeners();
            cancelButton.onClick.RemoveAllListeners();
            timeSlider.onValueChanged.RemoveAllListeners();
            tokens10.onValueChanged.RemoveAllListeners();
            tokens20.onValueChanged.RemoveAllListeners();
            tokens40.onValueChanged.RemoveAllListeners();
        }
        private void OnTimeSliderChanged(float value)
        {
            _selectedTime = Mathf.RoundToInt(value);
            UpdateTimeText(_selectedTime);
        }

        private void OnTokenSelected(int fee)
        {
            _selectedFee = fee;
            UpdateCostText(fee);
        }

        private void UpdateTimeText(int seconds)
        {
            timeText.text = testing ? overrideTimer.TimeFormat() :(seconds * 60).TimeFormat();
        }

        private void UpdateCostText(int fee)
        {
            costText.text = $"{fee}";
        }

        private async void OnCreateButtonClicked()
        {
            var timer = testing ? overrideTimer : _selectedTime * 60;

            var message = CanCreatNewSession(_selectedFee, GameManager.UserData.id);
            if (message != string.Empty)
            {
                UiManager.Instance.ShowToast(message);
                
                return;
            }

            // ✅ FIX #1: Check balance BEFORE creating lobby
            GameLogger.LogNetwork($"💰 Checking balance for {_selectedFee} FZ room...");
            var balanceCheck = await CurrencyManager.HasSufficientBalance(_selectedFee);
            
            if (!balanceCheck)
            {
                GameLogger.LogNetwork($"❌ Insufficient balance to create {_selectedFee} FZ room. Current: {CurrencyManager.Freekz} FZ", GameLogger.LogType.Error);
                UiManager.Instance.ShowToast($"Insufficient balance! You need {_selectedFee} FZ to create this room.");
                return;
            }
            
            GameLogger.LogNetwork($"✅ Balance check passed: {CurrencyManager.Freekz} FZ >= {_selectedFee} FZ");
                
            var sessionData = new SessionData()
            {
                ID = Guid.NewGuid().ToString("N")[..6],
                TimeToStart = timer,
                Fee = _selectedFee,
                Players = new List<PlayerInfo>(),
                CreatedBy = GameManager.UserData.id.ToString(),
            };

            var waitingPanel = UiManager.Instance.ShowPanel(UiScreenName.WaitingPanel, null);

            GameLogger.LogNetwork($"🎲 Creating lobby: ID={sessionData.ID}, Fee={sessionData.Fee}, Timer={sessionData.TimeToStart}");
            var created = await SupabaseApiController.CreateNewLobby(sessionData);
            GameLogger.LogNetwork($"📝 Lobby creation result: {created}");
            
            if (!created)
            {
                UiManager.Instance.HidePanel(waitingPanel);
                GameLogger.ShowLog("Failed to create session", GameLogger.LogType.Error);
                UiManager.Instance.ShowToast("Failed to create session");
                return;
            }

            // ✅ FIX #2: Wait for Supabase Realtime to populate the session cards dictionary
            GameLogger.LogNetwork($"⏱️ Waiting 400ms for Supabase Realtime to sync...");
            await Cysharp.Threading.Tasks.UniTask.Delay(400);
            GameLogger.LogNetwork($"✅ Realtime sync delay complete");

            // Auto-join the created session
            GameLogger.LogNetwork($"👤 Auto-joining lobby {sessionData.ID} as {GameManager.UserData.username}");
            var player = new PlayerInfo
            {
                Id = GameManager.UserData.id.ToString(),
                Name = GameManager.UserData.username
            };

            var joined = await SupabaseApiController.JoinLobby(sessionData.ID, player);
            GameLogger.LogNetwork($"✅ Auto-join result: {joined}");
            
            UiManager.Instance.HidePanel(waitingPanel);

            if (!joined)
            {
                GameLogger.ShowLog("❌ Failed to join created session", GameLogger.LogType.Error);
                UiManager.Instance.ShowToast("Created session but failed to join. Try joining manually.");
                // Note: DeleteLobby method doesn't exist yet - session will timeout naturally
            }
            else
            {
                // Set JoinedSession IMMEDIATELY to prevent race condition with realtime updates
                _publicMatchMakingPanel.JoinedSession = sessionData;
                GameManager.JoinedSessionData = sessionData; // Set global session data for GamePlayController
                GameLogger.LogNetwork($"🎮 Set GameManager.JoinedSessionData: ID={sessionData.ID}, Fee={sessionData.Fee}");
                GameLogger.ShowLog($"✅ Session created and auto-joined: {sessionData.ID}");
                UiManager.Instance.ShowToast($"Session created! Waiting for players...");
            }

            UiManager.Instance.HidePanel(this);
        }

        private string CanCreatNewSession(int selectedFee, string userDataID)
        {
            var sessions = _publicMatchMakingPanel.GetGroupedSessionCards(selectedFee);

            if (_publicMatchMakingPanel.JoinedSession != null)
            {
                return $"You're already in a match!";
            }

            if (sessions.Any(session => session.Data.Players.Count < 4))
            {
                return $"An open room already exists for {selectedFee}fz!";
            }

            if (sessions.Any(session => session.Data.CreatedBy == userDataID.ToString()))
            {
                return $"You've already made a room for {selectedFee}fz!";
            }

            return string.Empty;
        }


        private void OnCancelButtonClicked()
        {
            UiManager.Instance.HidePanel(this);
        }

    }
}
