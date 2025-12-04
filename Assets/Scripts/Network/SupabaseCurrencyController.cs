using System;
using Cysharp.Threading.Tasks;
using Managers;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace Network
{
    [Serializable]
    public class BalanceCheckResult
    {
        [JsonProperty("success")] public bool Success;
        [JsonProperty("has_sufficient_balance")] public bool HasSufficientBalance;
        [JsonProperty("current_balance")] public int CurrentBalance;
        [JsonProperty("required_amount")] public int RequiredAmount;
        [JsonProperty("error")] public string Error;
    }

    [Serializable]
    public class CurrencyTransaction
    {
        [JsonProperty("id")] public int Id;
        [JsonProperty("user_id")] public string UserId;
        [JsonProperty("amount")] public int Amount;
        [JsonProperty("transaction_type")] public string TransactionType;
        [JsonProperty("reason")] public string Reason;
        [JsonProperty("game_type")] public string GameType;
        [JsonProperty("lobby_id")] public string LobbyId;
        [JsonProperty("created_at")] public DateTime CreatedAt;
    }

    [Serializable]
    public class CurrencyResult
    {
        [JsonProperty("success")] public bool Success;
        [JsonProperty("balance")] public int Balance;
        [JsonProperty("transaction")] public CurrencyTransaction Transaction;
        [JsonProperty("message")] public string Message;
        [JsonProperty("error")] public string Error;
    }

    public static class SupabaseCurrencyController
    {
        private const string SupabaseUrl = "https://evbrcrmyvxqeuomaocvz.supabase.co";
        private const string SUPABASE_ANON_KEY = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6ImV2YnJjcm15dnhxZXVvbWFvY3Z6Iiwicm9sZSI6ImFub24iLCJpYXQiOjE3NTk2NzkzMjgsImV4cCI6MjA3NTI1NTMyOH0.X16_eXjdOEmZdRVJPjYg3gAnl1y5TBrLHXuoJkjjFQ0";

        /// <summary>
        /// Checks if user has sufficient balance (soft check - no deduction)
        /// </summary>
        public static async UniTask<BalanceCheckResult> CheckBalance(int requiredAmount)
        {
            try
            {
                var accessToken = SupabaseTokenManager.Instance?.GetAccessToken();
                if (string.IsNullOrEmpty(accessToken))
                {
                    GameLogger.LogNetwork("No access token available", GameLogger.LogType.Error);
                    return new BalanceCheckResult { Success = false, Error = "Not authenticated" };
                }

                var requestBody = new { amount = requiredAmount };
                var jsonBody = JsonConvert.SerializeObject(requestBody);
                var bodyBytes = System.Text.Encoding.UTF8.GetBytes(jsonBody);

                var url = $"{SupabaseUrl}/functions/v1/smooth-endpoint";
                using var request = new UnityWebRequest(url, "POST");
                request.uploadHandler = new UploadHandlerRaw(bodyBytes);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", $"Bearer {accessToken}");
                request.SetRequestHeader("apikey", SUPABASE_ANON_KEY);

                await request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    GameLogger.LogNetwork($"CheckBalance failed: {request.error}", GameLogger.LogType.Error);
                    return new BalanceCheckResult { Success = false, Error = request.error };
                }

                var response = JsonConvert.DeserializeObject<BalanceCheckResult>(request.downloadHandler.text);
                return response ?? new BalanceCheckResult { Success = false, Error = "Invalid response" };
            }
            catch (Exception ex)
            {
                GameLogger.LogNetwork($"CheckBalance exception: {ex.Message}", GameLogger.LogType.Error);
                return new BalanceCheckResult { Success = false, Error = ex.Message };
            }
        }

        /// <summary>
        /// Deducts entry fee when game starts (hard check + deduction)
        /// </summary>
        public static async UniTask<CurrencyResult> DeductEntryFee(string lobbyId, int lobbyFee)
        {
            try
            {
                GameLogger.LogNetwork($"🔑 Checking token - SupabaseTokenManager.Instance: {SupabaseTokenManager.Instance != null}");
                
                string accessToken = null;
                
                // Try to get token from SupabaseTokenManager if it exists
                if (SupabaseTokenManager.Instance != null)
                {
                    accessToken = SupabaseTokenManager.Instance.GetAccessToken();
                    GameLogger.LogNetwork($"🔑 Token from SupabaseTokenManager: {!string.IsNullOrEmpty(accessToken)} (length: {accessToken?.Length ?? 0})");
                }
                else
                {
                    // Fallback: Read directly from PlayerPrefs if SupabaseTokenManager doesn't exist
                    GameLogger.LogNetwork("⚠️ SupabaseTokenManager.Instance is NULL - reading token directly from PlayerPrefs");
                    accessToken = PlayerPrefs.GetString("supabase_access_token", null);
                    GameLogger.LogNetwork($"🔑 Token from PlayerPrefs: {!string.IsNullOrEmpty(accessToken)} (length: {accessToken?.Length ?? 0})");
                }
                
                if (string.IsNullOrEmpty(accessToken))
                {
                    GameLogger.LogNetwork("❌ No access token available - User needs to re-login", GameLogger.LogType.Error);
                    return new CurrencyResult { Success = false, Error = "Not authenticated - please log in again" };
                }

                var requestBody = new { lobby_id = lobbyId, lobby_fee = lobbyFee };
                var jsonBody = JsonConvert.SerializeObject(requestBody);
                var bodyBytes = System.Text.Encoding.UTF8.GetBytes(jsonBody);

                var url = $"{SupabaseUrl}/functions/v1/hyper-action";
                GameLogger.LogNetwork($"DeductEntryFee Request: URL={url}, Body={jsonBody}");
                
                using var request = new UnityWebRequest(url, "POST");
                request.uploadHandler = new UploadHandlerRaw(bodyBytes);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", $"Bearer {accessToken}");
                request.SetRequestHeader("apikey", SUPABASE_ANON_KEY);

                await request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    GameLogger.LogNetwork($"DeductEntryFee HTTP Error: {request.error}", GameLogger.LogType.Error);
                    GameLogger.LogNetwork($"Response Code: {request.responseCode}", GameLogger.LogType.Error);
                    GameLogger.LogNetwork($"Response Body: {request.downloadHandler?.text}", GameLogger.LogType.Error);
                    
                    // Try to parse error response
                    try
                    {
                        if (!string.IsNullOrEmpty(request.downloadHandler?.text))
                        {
                            var errorResponse = JsonConvert.DeserializeObject<CurrencyResult>(request.downloadHandler.text);
                            if (errorResponse != null)
                            {
                                GameLogger.LogNetwork($"Parsed Error: {errorResponse.Error}", GameLogger.LogType.Error);
                                return errorResponse;
                            }
                        }
                    }
                    catch (Exception parseEx)
                    {
                        GameLogger.LogNetwork($"Failed to parse error response: {parseEx.Message}", GameLogger.LogType.Error);
                    }
                    
                    return new CurrencyResult { Success = false, Error = $"{request.error} (Code: {request.responseCode})" };
                }

                var response = JsonConvert.DeserializeObject<CurrencyResult>(request.downloadHandler.text);
                return response ?? new CurrencyResult { Success = false, Error = "Invalid response" };
            }
            catch (Exception ex)
            {
                GameLogger.LogNetwork($"DeductEntryFee exception: {ex.Message}", GameLogger.LogType.Error);
                return new CurrencyResult { Success = false, Error = ex.Message };
            }
        }

        /// <summary>
        /// Credits winner reward or logs loser at game end
        /// </summary>
        public static async UniTask<CurrencyResult> CreditGameReward(string lobbyId, int lobbyFee, int rewardAmount, bool wonGame)
        {
            try
            {
                var accessToken = SupabaseTokenManager.Instance?.GetAccessToken();
                if (string.IsNullOrEmpty(accessToken))
                {
                    GameLogger.LogNetwork("No access token available", GameLogger.LogType.Error);
                    return new CurrencyResult { Success = false, Error = "Not authenticated" };
                }

                var requestBody = new
                {
                    lobby_id = lobbyId,
                    lobby_fee = lobbyFee,
                    reward_amount = rewardAmount,
                    won_game = wonGame
                };
                var jsonBody = JsonConvert.SerializeObject(requestBody);
                var bodyBytes = System.Text.Encoding.UTF8.GetBytes(jsonBody);

                var url = $"{SupabaseUrl}/functions/v1/smart-action";
                using var request = new UnityWebRequest(url, "POST");
                request.uploadHandler = new UploadHandlerRaw(bodyBytes);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", $"Bearer {accessToken}");
                request.SetRequestHeader("apikey", SUPABASE_ANON_KEY);

                await request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    GameLogger.LogNetwork($"CreditGameReward failed: {request.error}", GameLogger.LogType.Error);
                    return new CurrencyResult { Success = false, Error = request.error };
                }

                var response = JsonConvert.DeserializeObject<CurrencyResult>(request.downloadHandler.text);
                return response ?? new CurrencyResult { Success = false, Error = "Invalid response" };
            }
            catch (Exception ex)
            {
                GameLogger.LogNetwork($"CreditGameReward exception: {ex.Message}", GameLogger.LogType.Error);
                return new CurrencyResult { Success = false, Error = ex.Message };
            }
        }
    }
}
