using System;
using System.Collections.Generic;
using System.Threading;
using Controllers;
using Cysharp.Threading.Tasks;
using Data;
using GamePlay.Cards;
using GamePlay.Player;
using JetBrains.Annotations;
using Network;
using UIArchitecture;
using UnityEngine.SceneManagement;
using UnityEngine;

namespace Managers
{
    public static class GameManager
    {
        private const float RewardFee = 0.10f;

        public static SessionData JoinedSessionData;
        [CanBeNull] public static GameResult GamesResult;

        public static UserData UserData { get; set; }
        public static string PromoCode { get; set; }

        public static void LoadScene(SceneName sceneName)
        {
            if (Enum.TryParse<SceneName>(SceneManager.GetActiveScene().name, out var scene))
            {
                if (scene == sceneName)
                    return;
            }
            SceneManager.LoadScene(sceneName.ToString());
        }
        
        public static void EndGameResult(NetworkTeamData playerManagerTeamA, NetworkTeamData playerManagerTeamB, int localPlayerIndex, List<PlayerBase> allPlayers)
        {
            if (playerManagerTeamA.score == playerManagerTeamB.score)
            {
                GameLogger.ShowLog("This Match is a Draw", GameLogger.LogType.Error);
                UiManager.Instance.ShowToast("This Match is a Draw");
                return;
            }

            if (JoinedSessionData == null)
            {
                GameLogger.ShowLog("There is no session Data here", GameLogger.LogType.Error);
                return;
            }
            
            var teamA = new GameResult.TeamData(playerManagerTeamA, localPlayerIndex, allPlayers);
            var teamB = new GameResult.TeamData(playerManagerTeamB, localPlayerIndex, allPlayers);
            
            bool isLocalWinner = (teamA.score > teamB.score && teamA.isMyTeam) || (teamB.score > teamA.score && teamB.isMyTeam);

            var reward = 0;
            var fee = JoinedSessionData.Fee;

            if (isLocalWinner)
            {
                // Fixed payout table (correct formula from Nov 28)
                reward = fee switch
                {
                    10 => 18,
                    20 => 36,
                    40 => 70,
                    _ => 0
                };
            }
            
            GamesResult = new GameResult
            {
                teamA = teamA,
                teamB = teamB,
                isLocalPlayerWinner = isLocalWinner,
                reward = reward
            };
        }
        

        public static void CheckForWinScreen()
        {
            if (GamesResult == null)
                return;

            UiManager.Instance.ShowPanel(UiScreenName.ResultPanel, GamesResult);

            GamesResult = null;
            JoinedSessionData = null;
        }

        public static async UniTask RefreshPlayerData()
        {
            // Refresh player data from Supabase API (fetch fresh balance)
            if (SupabaseTokenManager.Instance != null && SupabaseTokenManager.Instance.HasValidSession())
            {
                GameLogger.LogNetwork("Refreshing player data from Supabase...");
                
                // Update local cache from token manager
                if (UserData != null)
                {
                    UserData.username = SupabaseTokenManager.Instance.Username;
                    UserData.email = SupabaseTokenManager.Instance.UserEmail;
                }
                
                // Fetch fresh balance from Supabase API instead of using cached value
                var balanceCheck = await SupabaseCurrencyController.CheckBalance(0);
                if (balanceCheck.Success)
                {
                    CurrencyManager.Freekz = balanceCheck.CurrentBalance;
                    SupabaseTokenManager.Instance.UpdateBalance(balanceCheck.CurrentBalance);
                    GameLogger.LogNetwork($"Player data refreshed: {SupabaseTokenManager.Instance.Username}, Balance: {CurrencyManager.Freekz} FZ");
                }
                else
                {
                    GameLogger.LogNetwork($"Failed to fetch fresh balance: {balanceCheck.Error}", GameLogger.LogType.Warning);
                }
            }
            else
            {
                GameLogger.LogNetwork("No valid Supabase session to refresh", GameLogger.LogType.Warning);
            }
        }
        
        public static async UniTask CheckForAutomaticLogin()
        {
            // Initialize GameManager event handlers
            // (Add event subscription logic here if needed)
            
            // PRIORITY 1: Check Supabase authentication (new system)
            if (SupabaseTokenManager.Instance != null && SupabaseTokenManager.Instance.HasValidSession())
            {
                GameLogger.LogNetwork("Supabase session found, logging in...");
                
                // Load user data from SupabaseTokenManager
                UserData = new UserData
                {
                    id = SupabaseTokenManager.Instance.UserId,
                    username = SupabaseTokenManager.Instance.Username,
                    email = SupabaseTokenManager.Instance.UserEmail,
                    promo_code = SupabaseTokenManager.Instance.PromoCode,
                    balance = (int)SupabaseTokenManager.Instance.Balance
                };
                
                CurrencyManager.Freekz = (int)SupabaseTokenManager.Instance.Balance;
                
                GameLogger.LogNetwork($"Supabase login successful: {UserData.username}, Balance: {CurrencyManager.Freekz} FZ");
                LoadScene(SceneName.MainMenu);
                return;
            }
            
            // No valid session found - go to login screen
            GameLogger.LogNetwork("No valid Supabase session found");
            LoadScene(SceneName.Login);
        }

        
        public static void OnSucessfullLogin(LoginResponse response)
        {
            GameLogger.LogNetwork("Login successful!");
            
            UserData = response.user;
            PromoCode = response.promo_code;
            CurrencyManager.Freekz = (int)response.balance;
            
            // Tokens are already saved by AuthManager.Login() to SupabaseTokenManager
            // Just log confirmation
            if (SupabaseTokenManager.Instance != null)
            {
                GameLogger.LogNetwork($"SupabaseTokenManager ready. User: {SupabaseTokenManager.Instance.Username}, Balance: {SupabaseTokenManager.Instance.Balance} FZ");
            }
            
            LoadScene(SceneName.MainMenu);
        }

        public static void OnLoginFailed(string error)
        {
            LoadScene(SceneName.Login);
            UiManager.Instance.ShowToast(error);
        }
    }
}