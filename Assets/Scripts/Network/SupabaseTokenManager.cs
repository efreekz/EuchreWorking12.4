using System;
using System.Collections;
using System.Runtime.InteropServices;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json.Linq;

namespace Network
{
    /// <summary>
    /// Manages Supabase authentication tokens across platforms.
    /// - WebGL: Uses localStorage via jslib bridge
    /// - Android: Uses PlayerPrefs (future: upgrade to Android Keystore)
    /// - iOS: Uses PlayerPrefs (future: upgrade to iOS Keychain)
    /// </summary>
    public class SupabaseTokenManager : MonoBehaviour
    {
        private static SupabaseTokenManager _instance;
        
        public static SupabaseTokenManager Instance 
        { 
            get 
            {
                if (_instance == null)
                {
                    // Try to find existing instance with the modern API to avoid obsolete warnings
                    _instance = FindFirstObjectByType<SupabaseTokenManager>();
                    
                    // If still null, auto-create
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("SupabaseTokenManager");
                        _instance = go.AddComponent<SupabaseTokenManager>();
                        DontDestroyOnLoad(go);
                        Debug.Log("[SupabaseTokenManager] Auto-created singleton instance");
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

        // Token storage keys
        private const string KEY_ACCESS_TOKEN = "supabase_access_token";
        private const string KEY_REFRESH_TOKEN = "supabase_refresh_token";
        private const string KEY_USER_ID = "supabase_user_id";
        private const string KEY_USER_EMAIL = "supabase_user_email";
        private const string KEY_USER_USERNAME = "supabase_user_username";
        private const string KEY_USER_BALANCE = "supabase_user_balance";
        private const string KEY_USER_PROMO_CODE = "supabase_user_promo_code";
        private const string KEY_TOKEN_EXPIRY = "supabase_token_expiry";

        // Supabase configuration
        private const string SUPABASE_URL = "https://evbrcrmyvxqeuomaocvz.supabase.co";
        private const string SUPABASE_ANON_KEY = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6ImV2YnJjcm15dnhxZXVvbWFvY3Z6Iiwicm9sZSI6ImFub24iLCJpYXQiOjE3NTk2NzkzMjgsImV4cCI6MjA3NTI1NTMyOH0.X16_eXjdOEmZdRVJPjYg3gAnl1y5TBrLHXuoJkjjFQ0";

        // Current session data
        private string _accessToken;
        private string _refreshToken;
        private DateTime _tokenExpiry;
        private bool _isRefreshing = false;
        private Coroutine _autoRefreshCoroutine;
        
        public string UserId { get; private set; }
        public string UserEmail { get; private set; }
        public string Username { get; private set; }
        public float Balance { get; private set; }
        public string PromoCode { get; private set; }

        // Events
        public event Action OnTokenRefreshed;
        public event Action OnSessionExpired;
        public event Action<float> OnBalanceUpdated;

#if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL jslib imports
        [DllImport("__Internal")]
        private static extern string GetFromLocalStorage(string key);
        
        [DllImport("__Internal")]
        private static extern void SaveToLocalStorage(string key, string value);
        
        [DllImport("__Internal")]
        private static extern void RemoveFromLocalStorage(string key);
        
        [DllImport("__Internal")]
        private static extern void ClearAllSupabaseTokens();
        
        [DllImport("__Internal")]
        private static extern int IsLocalStorageAvailable();
#endif

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

        private void Start()
        {
            LoadTokensFromStorage();
            
            // Start auto-refresh if we have a valid session
            if (HasValidSession())
            {
                StartAutoRefresh();
            }
        }

        private void OnDestroy()
        {
            StopAutoRefresh();
        }

        /// <summary>
        /// Check if a valid session exists
        /// </summary>
        public bool HasValidSession()
        {
            if (string.IsNullOrEmpty(_accessToken) || string.IsNullOrEmpty(_refreshToken))
            {
                return false;
            }

            // If token expires in less than 5 minutes, consider it invalid
            // (will trigger refresh before use)
            return _tokenExpiry > DateTime.UtcNow.AddMinutes(5);
        }

        /// <summary>
        /// Get the current access token (for API calls)
        /// </summary>
        public string GetAccessToken()
        {
            // If token is empty in memory, try loading from storage
            if (string.IsNullOrEmpty(_accessToken))
            {
                Debug.Log("[SupabaseTokenManager] ⚠️ Access token empty in memory, reloading from storage...");
                LoadTokensFromStorage();
                
                if (string.IsNullOrEmpty(_accessToken))
                {
                    Debug.LogError("[SupabaseTokenManager] ❌ CRITICAL: Token still empty after LoadTokensFromStorage!");
                    Debug.LogError($"[SupabaseTokenManager] PlayerPrefs check: {PlayerPrefs.GetString(KEY_ACCESS_TOKEN, "NULL")}");
                }
                else
                {
                    Debug.Log($"[SupabaseTokenManager] ✅ Token loaded successfully (length: {_accessToken.Length})");
                }
            }
            return _accessToken;
        }

        /// <summary>
        /// Get the current refresh token
        /// </summary>
        public string GetRefreshToken()
        {
            return _refreshToken;
        }

        /// <summary>
        /// Load tokens from platform-specific storage
        /// </summary>
        public void LoadTokensFromStorage()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGL: Read from localStorage
            if (IsLocalStorageAvailable() == 1)
            {
                _accessToken = GetFromLocalStorage(KEY_ACCESS_TOKEN);
                _refreshToken = GetFromLocalStorage(KEY_REFRESH_TOKEN);
                UserId = GetFromLocalStorage(KEY_USER_ID);
                UserEmail = GetFromLocalStorage(KEY_USER_EMAIL);
                Username = GetFromLocalStorage(KEY_USER_USERNAME);
                PromoCode = GetFromLocalStorage(KEY_USER_PROMO_CODE);
                
                string balanceStr = GetFromLocalStorage(KEY_USER_BALANCE);
                if (!string.IsNullOrEmpty(balanceStr) && float.TryParse(balanceStr, out float balance))
                {
                    Balance = balance;
                }

                string expiryStr = GetFromLocalStorage(KEY_TOKEN_EXPIRY);
                if (!string.IsNullOrEmpty(expiryStr) && long.TryParse(expiryStr, out long expiryTicks))
                {
                    _tokenExpiry = new DateTime(expiryTicks, DateTimeKind.Utc);
                }
                else if (!string.IsNullOrEmpty(_accessToken))
                {
                    // Parse expiry from JWT token
                    _tokenExpiry = ParseTokenExpiry(_accessToken);
                }

                Debug.Log($"[SupabaseTokenManager] Loaded tokens from localStorage. Valid session: {HasValidSession()}");
            }
            else
            {
                Debug.LogError("[SupabaseTokenManager] localStorage not available in WebGL!");
            }
#else
            // Fallback: PlayerPrefs (for Editor, Android, iOS)
            _accessToken = PlayerPrefs.GetString(KEY_ACCESS_TOKEN, null);
            _refreshToken = PlayerPrefs.GetString(KEY_REFRESH_TOKEN, null);
            UserId = PlayerPrefs.GetString(KEY_USER_ID, null);
            UserEmail = PlayerPrefs.GetString(KEY_USER_EMAIL, null);
            Username = PlayerPrefs.GetString(KEY_USER_USERNAME, null);
            Balance = PlayerPrefs.GetFloat(KEY_USER_BALANCE, 0f);
            PromoCode = PlayerPrefs.GetString(KEY_USER_PROMO_CODE, "");

            long expiryTicks = long.Parse(PlayerPrefs.GetString(KEY_TOKEN_EXPIRY, "0"));
            if (expiryTicks > 0)
            {
                _tokenExpiry = new DateTime(expiryTicks, DateTimeKind.Utc);
            }
            else if (!string.IsNullOrEmpty(_accessToken))
            {
                _tokenExpiry = ParseTokenExpiry(_accessToken);
            }

            Debug.Log($"[SupabaseTokenManager] Loaded tokens from PlayerPrefs. Valid session: {HasValidSession()}");
#endif
        }

        /// <summary>
        /// Save tokens to platform-specific storage
        /// </summary>
        public void SaveTokensToStorage(string accessToken, string refreshToken, string userId, string email, string username, float balance, string promoCode = "")
        {
            Debug.Log($"[SupabaseTokenManager] 💾 SaveTokensToStorage called - Token length: {accessToken?.Length ?? 0}");
            
            _accessToken = accessToken;
            _refreshToken = refreshToken;
            UserId = userId;
            UserEmail = email;
            Username = username;
            Balance = balance;
            PromoCode = promoCode;
            _tokenExpiry = ParseTokenExpiry(accessToken);
            
            Debug.Log($"[SupabaseTokenManager] 📝 Set _accessToken in memory: {!string.IsNullOrEmpty(_accessToken)}");

#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGL: Write to localStorage
            SaveToLocalStorage(KEY_ACCESS_TOKEN, accessToken);
            SaveToLocalStorage(KEY_REFRESH_TOKEN, refreshToken);
            SaveToLocalStorage(KEY_USER_ID, userId);
            SaveToLocalStorage(KEY_USER_EMAIL, email);
            SaveToLocalStorage(KEY_USER_USERNAME, username);
            SaveToLocalStorage(KEY_USER_BALANCE, balance.ToString());
            SaveToLocalStorage(KEY_USER_PROMO_CODE, promoCode ?? "");
            SaveToLocalStorage(KEY_TOKEN_EXPIRY, _tokenExpiry.Ticks.ToString());
            
            Debug.Log($"[SupabaseTokenManager] Saved tokens to localStorage. User: {username}, Balance: {balance}");
#else
            // Fallback: PlayerPrefs
            PlayerPrefs.SetString(KEY_ACCESS_TOKEN, accessToken);
            PlayerPrefs.SetString(KEY_REFRESH_TOKEN, refreshToken);
            PlayerPrefs.SetString(KEY_USER_ID, userId);
            PlayerPrefs.SetString(KEY_USER_EMAIL, email);
            PlayerPrefs.SetString(KEY_USER_USERNAME, username);
            PlayerPrefs.SetFloat(KEY_USER_BALANCE, balance);
            PlayerPrefs.SetString(KEY_USER_PROMO_CODE, promoCode ?? "");
            PlayerPrefs.SetString(KEY_TOKEN_EXPIRY, _tokenExpiry.Ticks.ToString());
            PlayerPrefs.Save();
            
            Debug.Log($"[SupabaseTokenManager] Saved tokens to PlayerPrefs. User: {username}, Balance: {balance}");
#endif

            OnBalanceUpdated?.Invoke(balance);
        }

        /// <summary>
        /// Update only the balance (after transactions)
        /// </summary>
        public void UpdateBalance(float newBalance)
        {
            Balance = newBalance;

#if UNITY_WEBGL && !UNITY_EDITOR
            SaveToLocalStorage(KEY_USER_BALANCE, newBalance.ToString());
#else
            PlayerPrefs.SetFloat(KEY_USER_BALANCE, newBalance);
            PlayerPrefs.Save();
#endif

            OnBalanceUpdated?.Invoke(newBalance);
            Debug.Log($"[SupabaseTokenManager] Balance updated: {newBalance} FZ");
        }

        /// <summary>
        /// Clear all tokens (logout)
        /// </summary>
        public void ClearTokens()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            ClearAllSupabaseTokens();
#else
            PlayerPrefs.DeleteKey(KEY_ACCESS_TOKEN);
            PlayerPrefs.DeleteKey(KEY_REFRESH_TOKEN);
            PlayerPrefs.DeleteKey(KEY_USER_ID);
            PlayerPrefs.DeleteKey(KEY_USER_EMAIL);
            PlayerPrefs.DeleteKey(KEY_USER_USERNAME);
            PlayerPrefs.DeleteKey(KEY_USER_BALANCE);
            PlayerPrefs.DeleteKey(KEY_TOKEN_EXPIRY);
            PlayerPrefs.Save();
#endif

            _accessToken = null;
            _refreshToken = null;
            UserId = null;
            UserEmail = null;
            Username = null;
            Balance = 0f;
            _tokenExpiry = DateTime.MinValue;

            Debug.Log("[SupabaseTokenManager] All tokens cleared (logout)");
        }

        /// <summary>
        /// Parse JWT token to extract expiry time
        /// </summary>
        private DateTime ParseTokenExpiry(string jwtToken)
        {
            try
            {
                // JWT format: header.payload.signature
                string[] parts = jwtToken.Split('.');
                if (parts.Length != 3)
                {
                    Debug.LogWarning("[SupabaseTokenManager] Invalid JWT format");
                    return DateTime.UtcNow.AddHours(1); // Default: 1 hour
                }

                // Decode payload (Base64)
                string payload = parts[1];
                
                // Add padding if needed
                int mod4 = payload.Length % 4;
                if (mod4 > 0)
                {
                    payload += new string('=', 4 - mod4);
                }

                byte[] jsonBytes = Convert.FromBase64String(payload);
                string json = System.Text.Encoding.UTF8.GetString(jsonBytes);
                
                // Parse JSON to get 'exp' claim
                JObject jsonObj = JObject.Parse(json);
                if (jsonObj.TryGetValue("exp", out JToken expToken))
                {
                    long expUnix = expToken.Value<long>();
                    DateTime expiry = DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime;
                    Debug.Log($"[SupabaseTokenManager] Token expires at: {expiry:yyyy-MM-dd HH:mm:ss} UTC");
                    return expiry;
                }
                else
                {
                    Debug.LogWarning("[SupabaseTokenManager] No 'exp' claim in JWT token");
                    return DateTime.UtcNow.AddHours(1); // Default: 1 hour
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SupabaseTokenManager] Failed to parse JWT expiry: {ex.Message}");
                return DateTime.UtcNow.AddHours(1); // Default: 1 hour
            }
        }

        /// <summary>
        /// Refresh the access token using the refresh token
        /// </summary>
        public async UniTask<bool> RefreshAccessTokenAsync()
        {
            if (_isRefreshing)
            {
                Debug.Log("[SupabaseTokenManager] Token refresh already in progress...");
                return false;
            }

            if (string.IsNullOrEmpty(_refreshToken))
            {
                Debug.LogError("[SupabaseTokenManager] No refresh token available!");
                OnSessionExpired?.Invoke();
                return false;
            }

            _isRefreshing = true;

            try
            {
                string url = $"{SUPABASE_URL}/auth/v1/token?grant_type=refresh_token";
                
                var requestData = new
                {
                    refresh_token = _refreshToken
                };
                
                string jsonBody = JsonUtility.ToJson(requestData);
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);

                using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
                {
                    request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.SetRequestHeader("apikey", SUPABASE_ANON_KEY);

                    await request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        string responseText = request.downloadHandler.text;
                        JObject response = JObject.Parse(responseText);

                        // Extract new tokens
                        string newAccessToken = response["access_token"]?.ToString();
                        string newRefreshToken = response["refresh_token"]?.ToString();

                        if (!string.IsNullOrEmpty(newAccessToken) && !string.IsNullOrEmpty(newRefreshToken))
                        {
                            // Update tokens in memory and storage
                            _accessToken = newAccessToken;
                            _refreshToken = newRefreshToken;
                            _tokenExpiry = ParseTokenExpiry(newAccessToken);

                            // Save to storage
#if UNITY_WEBGL && !UNITY_EDITOR
                            SaveToLocalStorage(KEY_ACCESS_TOKEN, newAccessToken);
                            SaveToLocalStorage(KEY_REFRESH_TOKEN, newRefreshToken);
                            SaveToLocalStorage(KEY_TOKEN_EXPIRY, _tokenExpiry.Ticks.ToString());
#else
                            PlayerPrefs.SetString(KEY_ACCESS_TOKEN, newAccessToken);
                            PlayerPrefs.SetString(KEY_REFRESH_TOKEN, newRefreshToken);
                            PlayerPrefs.SetString(KEY_TOKEN_EXPIRY, _tokenExpiry.Ticks.ToString());
                            PlayerPrefs.Save();
#endif

                            Debug.Log($"[SupabaseTokenManager] Token refreshed successfully. New expiry: {_tokenExpiry:yyyy-MM-dd HH:mm:ss} UTC");
                            
                            OnTokenRefreshed?.Invoke();
                            _isRefreshing = false;
                            return true;
                        }
                        else
                        {
                            Debug.LogError("[SupabaseTokenManager] Token refresh response missing tokens");
                            _isRefreshing = false;
                            OnSessionExpired?.Invoke();
                            return false;
                        }
                    }
                    else
                    {
                        Debug.LogError($"[SupabaseTokenManager] Token refresh failed: {request.error}\nResponse: {request.downloadHandler.text}");
                        _isRefreshing = false;
                        OnSessionExpired?.Invoke();
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SupabaseTokenManager] Token refresh exception: {ex.Message}");
                _isRefreshing = false;
                OnSessionExpired?.Invoke();
                return false;
            }
        }

        /// <summary>
        /// Start automatic token refresh background task
        /// Refreshes token 5 minutes before expiry
        /// </summary>
        private void StartAutoRefresh()
        {
            StopAutoRefresh();
            _autoRefreshCoroutine = StartCoroutine(AutoRefreshCoroutine());
            Debug.Log("[SupabaseTokenManager] Auto-refresh started");
        }

        /// <summary>
        /// Stop automatic token refresh
        /// </summary>
        private void StopAutoRefresh()
        {
            if (_autoRefreshCoroutine != null)
            {
                StopCoroutine(_autoRefreshCoroutine);
                _autoRefreshCoroutine = null;
                Debug.Log("[SupabaseTokenManager] Auto-refresh stopped");
            }
        }

        /// <summary>
        /// Background coroutine that automatically refreshes tokens
        /// </summary>
        private IEnumerator AutoRefreshCoroutine()
        {
            while (true)
            {
                // Wait 1 minute between checks
                yield return new WaitForSeconds(60f);

                // Check if token expires in less than 5 minutes
                TimeSpan timeUntilExpiry = _tokenExpiry - DateTime.UtcNow;
                
                if (timeUntilExpiry.TotalMinutes < 5 && timeUntilExpiry.TotalMinutes > 0)
                {
                    Debug.Log($"[SupabaseTokenManager] Token expires in {timeUntilExpiry.TotalMinutes:F1} minutes. Refreshing...");
                    
                    // Convert UniTask to coroutine-compatible
                    var refreshTask = RefreshAccessTokenAsync();
                    yield return refreshTask.ToCoroutine();
                    bool success = refreshTask.GetAwaiter().GetResult();
                    
                    if (!success)
                    {
                        Debug.LogWarning("[SupabaseTokenManager] Auto-refresh failed. Stopping auto-refresh.");
                        yield break; // Exit coroutine
                    }
                }
                else if (timeUntilExpiry.TotalMinutes <= 0)
                {
                    Debug.LogWarning("[SupabaseTokenManager] Token already expired!");
                    OnSessionExpired?.Invoke();
                    yield break; // Exit coroutine
                }
            }
        }

        /// <summary>
        /// Get Supabase URL
        /// </summary>
        public string GetSupabaseUrl()
        {
            return SUPABASE_URL;
        }

        /// <summary>
        /// Get Supabase anon key (for unauthenticated requests)
        /// </summary>
        public string GetSupabaseAnonKey()
        {
            return SUPABASE_ANON_KEY;
        }
    }
}
