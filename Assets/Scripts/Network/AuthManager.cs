using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Data;
using Managers;
using Newtonsoft.Json;
using UnityEngine.Networking;
using UnityEngine.Serialization;


namespace Network
{
    public class AuthManager : MonoBehaviour
    {
        
        [SerializeField] private string supabaseApiKey = "sb_publishable_Hy7pxKobWznbLtBXrpklUA_-z-uPVdN";


        [SerializeField] private string baseUrl = $"https://evbrcrmyvxqeuomaocvz.supabase.co";
        [SerializeField] private string endPointLogin = "/functions/v1/login";
        [SerializeField] private string endPointSignUp = "/functions/v1/signup";
        
        private string LoginURL => $"{baseUrl}{endPointLogin}";
        private string SignUpURL => $"{baseUrl}{endPointSignUp}";

        public static AuthManager Instance { get; private set; }

        private static string JwtTokenKey
        {
            get
            {
#if UNITY_EDITOR
                return $"jwtToken_{Process.GetCurrentProcess().Id}";
#else
                return "jwtToken";
#endif
            }
        } 
        private string _jwtToken;
    
        private void Awake()
        {
            // Singleton Pattern
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        
        
        // Store JWT token after login/registration
        private void SetToken(string token)
        {
            _jwtToken = token;
            PlayerPrefs.SetString(JwtTokenKey, token);
            PlayerPrefs.Save();
        }

        // Load stored token (call at app startup)
        private void LoadToken()
        {
            if (PlayerPrefs.HasKey(JwtTokenKey))
            {
                _jwtToken = PlayerPrefs.GetString(JwtTokenKey);
            }
        }

        // Check if user is logged in
        public bool IsLoggedIn()
        {
            LoadToken();
            return !string.IsNullOrEmpty(_jwtToken);
        }


        public void LogOut()
        {
            // Clear Supabase authentication
            SupabaseTokenManager.Instance?.ClearTokens();
            
            // Clear game state
            GameManager.UserData = null;
            GameManager.PromoCode = null;
            CurrencyManager.Freekz = 0;
            
            _jwtToken = string.Empty;
            PlayerPrefs.DeleteKey(JwtTokenKey);
            PlayerPrefs.Save();
            
            GameLogger.LogNetwork("Logged out successfully - all tokens and data cleared");
        }

        #region Helper Funtions

        private const int RequestTimeoutSeconds = 10;
        
        // ✅ Shared helper: Safe JSON parse for error messages
        private bool TryExtractError(string json, out string errorMessage)
        {
            errorMessage = null;
            if (string.IsNullOrEmpty(json)) return false;

            try
            {
                var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                if (dict != null && dict.TryGetValue("error", out var err))
                {
                    errorMessage = err.ToString();
                    return true;
                }
            }
            catch (Exception ex)
            {
                GameLogger.LogNetwork($"[TryExtractError] JSON parse failed: {ex.Message}", GameLogger.LogType.Error);
            }

            return false;
        }

        /// <summary>
        /// Sends a UnityWebRequest and enforces a timeout by polling request.isDone.
        /// Returns true if the request completed (success or HTTP error), false if timed out (request aborted).
        /// </summary>
        private async UniTask<bool> SendWebRequestWithTimeout(UnityWebRequest request, float timeoutSeconds = 10f)
        {
            // start the request
            var operation = request.SendWebRequest();

            // track elapsed time (use realtimeSinceStartup so it works regardless of timescale)
            float start = Time.realtimeSinceStartup;

            // poll until request completes or timeout occurs
            while (!operation.isDone)
            {
                // if timed out, abort and return false
                if (Time.realtimeSinceStartup - start >= timeoutSeconds)
                {
                    try { request.Abort(); } catch { /* ignore */ }
                    return false;
                }

                // yield for a small interval to avoid tight loop
                await UniTask.DelayFrame(1);
            }

            // request finished (could be success or HTTP error)
            return true;
        }


        #endregion

        public async UniTask Login(string identifier, string password, Action<LoginResponse> onSuccess, Action<string> onError)
        {
            try
            {
                var requestData = new { identifier, password };
                var json = JsonConvert.SerializeObject(requestData);

                using var request = new UnityWebRequest(LoginURL, "POST");
                var bodyRaw = Encoding.UTF8.GetBytes(json);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                await request.SendWebRequest();

                var responseText = request.downloadHandler.text;

                if (request.result is UnityWebRequest.Result.ConnectionError or UnityWebRequest.Result.ProtocolError)
                {
                    GameLogger.LogNetwork($"[LOGIN ERROR RAW] {responseText}", GameLogger.LogType.Error);

                    try
                    {
                        var errorObj = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseText);
                        if (errorObj != null && errorObj.TryGetValue("error", out var userMessage))
                        {
                            onError?.Invoke(userMessage.ToString());
                            return;
                        }
                    }
                    catch (Exception jsonEx)
                    {
                        GameLogger.LogNetwork($"JSON parse failed: {jsonEx.Message}", GameLogger.LogType.Error);
                    }

                    onError?.Invoke("Unable to login. Please try again.");
                    return;
                }

                GameLogger.LogNetwork($"[LOGIN RESPONSE] {responseText}");
                
                // Parse Supabase response
                var supabaseResponse = JsonConvert.DeserializeObject<SupabaseLoginResponse>(responseText);
                
                if (supabaseResponse == null || string.IsNullOrEmpty(supabaseResponse.access_token))
                {
                    onError?.Invoke("Login failed. Please check your credentials.");
                    return;
                }
                
                // Save tokens to SupabaseTokenManager
                if (SupabaseTokenManager.Instance != null)
                {
                    SupabaseTokenManager.Instance.SaveTokensToStorage(
                        supabaseResponse.access_token,
                        supabaseResponse.refresh_token,
                        supabaseResponse.user.id,
                        supabaseResponse.user.email,
                        supabaseResponse.user.username,
                        supabaseResponse.user.balance,
                        supabaseResponse.user.promo_code
                    );
                }
                
                // Convert to LoginResponse format for compatibility
                var response = new LoginResponse
                {
                    message = supabaseResponse.message,
                    access_token = supabaseResponse.access_token,
                    token = supabaseResponse.access_token,
                    user = new UserData
                    {
                        id = supabaseResponse.user.id,
                        email = supabaseResponse.user.email,
                        username = supabaseResponse.user.username,
                        promo_code = supabaseResponse.user.promo_code,
                        balance = (int)supabaseResponse.user.balance
                    },
                    balance = supabaseResponse.user.balance,
                    promo_code = supabaseResponse.user.promo_code
                };

                SetToken(response.access_token);
                onSuccess?.Invoke(response);
            }
            catch (Exception ex)
            {
                GameLogger.LogNetwork($"[EXCEPTION] {ex.Message}", GameLogger.LogType.Error);
                var readableError = "Something went wrong. Please try again.";

                try
                {
                    var jsonStart = ex.Message.IndexOf('{');
                    if (jsonStart >= 0)
                    {
                        var jsonPart = ex.Message.Substring(jsonStart);
                        var errorObj = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonPart);
                        if (errorObj != null && errorObj.TryGetValue("error", out var err))
                            readableError = err.ToString();
                    }
                }
                catch (Exception parseEx)
                {
                    GameLogger.LogNetwork($"JSON parse failed in exception: {parseEx.Message}",
                        GameLogger.LogType.Error);
                }

                onError?.Invoke(readableError);
            }
        }

        public async UniTask SignUp(string email, string username, string password, string promoCode, Action<LoginResponse> onSuccess, Action<string> onError)
        {
            try
            {
                var requestData = new { email, username, password, promoCode };
                var json = JsonConvert.SerializeObject(requestData);

                using var request = new UnityWebRequest(SignUpURL, "POST");
                var bodyRaw = Encoding.UTF8.GetBytes(json);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                await request.SendWebRequest();
                var responseText = request.downloadHandler.text;
                if (request.result is UnityWebRequest.Result.ConnectionError or UnityWebRequest.Result.ProtocolError)
                {
                    GameLogger.LogNetwork($"[SIGNUP ERROR RAW] {responseText}", GameLogger.LogType.Error);

                    try
                    {
                        var errorObj = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseText);
                        if (errorObj != null && errorObj.TryGetValue("error", out var userMessage))
                        {
                            onError?.Invoke(userMessage.ToString());
                            return;
                        }
                    }
                    catch (Exception jsonEx)
                    {
                        GameLogger.LogNetwork($"JSON parse failed: {jsonEx.Message}", GameLogger.LogType.Error);
                    }

                    onError?.Invoke("Unable to sign up. Please try again.");
                    return;
                }

                GameLogger.LogNetwork($"[SIGNUP RESPONSE] {responseText}");
                var response = JsonConvert.DeserializeObject<LoginResponse>(responseText);

                if (response == null || string.IsNullOrEmpty(response.access_token))
                {
                    onError?.Invoke("Sign up failed. Please check your details.");
                    return;
                }

                SetToken(response.access_token);
                onSuccess?.Invoke(response);
            }
            catch (Exception ex)
            {
                GameLogger.LogNetwork($"[EXCEPTION] {ex.Message}", GameLogger.LogType.Error);
                var readableError = "Something went wrong. Please try again.";

                try
                {
                    var jsonStart = ex.Message.IndexOf('{');
                    if (jsonStart >= 0)
                    {
                        var jsonPart = ex.Message.Substring(jsonStart);
                        var errorObj = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonPart);
                        if (errorObj != null && errorObj.TryGetValue("error", out var err))
                            readableError = err.ToString();
                    }
                }
                catch (Exception parseEx)
                {
                    GameLogger.LogNetwork($"JSON parse failed in exception: {parseEx.Message}",
                        GameLogger.LogType.Error);
                }

                onError?.Invoke(readableError);
            }
        }



        
    }
    
    // Supabase response classes
    [Serializable]
    public class SupabaseLoginResponse
    {
        public string message;
        public string access_token;
        public string refresh_token;
        public SupabaseUser user;
    }
    
    [Serializable]
    public class SupabaseSignupResponse
    {
        public bool success;
        public string access_token;
        public string refresh_token;
        public SupabaseUser user;
        public int promo_reward;
        public string message;
    }
    
    [Serializable]
    public class SupabaseUser
    {
        public string id;
        public string email;
        public string username;
        public float balance;
        public string promo_code;
        public bool email_verified;
    }
    
    [Serializable]
    public class SupabaseErrorResponse
    {
        public string error;
    }
}