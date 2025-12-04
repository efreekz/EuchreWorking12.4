# 🧪 Supabase Authentication System - Complete Test Plan

**Date:** December 1, 2025  
**Build:** EucherFreekz-dev-huzafa  
**Status:** ✅ All files integrated and compiling

---

## 📋 System Overview

### **Components Implemented:**
1. ✅ **SupabaseTokenManager.cs** (540 lines) - Token storage, auto-refresh, session management
2. ✅ **SupabaseCurrencyController.cs** (200 lines) - Balance operations via Edge Functions
3. ✅ **AuthManager.cs** - Login/Signup with Supabase integration
4. ✅ **GameManager.cs** - Session management using SupabaseTokenManager
5. ✅ **CurrencyManager.cs** - Supabase currency methods (DeductEntryFee, CreditGameReward)
6. ✅ **DataManager.cs** - LoginResponse with all required fields
7. ✅ **ResultScreen.cs** - Proper CreditGameReward integration

---

## 🔄 Authentication Flow Verification

### **1. Login Flow**
```
User enters credentials → AuthManager.Login()
  ↓
Parse SupabaseLoginResponse
  ↓
SupabaseTokenManager.SaveTokensToStorage()
  - Saves: access_token, refresh_token, user_id, email, username, balance, promo_code
  - Storage: WebGL (localStorage) or Native (PlayerPrefs)
  ↓
Convert to LoginResponse
  ↓
GameManager.OnSuccessfulLogin()
  - Sets UserData from response
  - Sets CurrencyManager.Freekz from response.balance
  - Loads MainMenu scene
```

**Test Steps:**
- [ ] Enter valid email/username and password
- [ ] Verify no errors in console
- [ ] Check Unity Console for: `[SupabaseTokenManager] Saved tokens to PlayerPrefs`
- [ ] Verify transition to MainMenu scene
- [ ] Check Account screen shows: User ID, Username, Email
- [ ] Verify balance displays correctly (should match Supabase database)

**Expected Console Logs:**
```
[SupabaseTokenManager] 💾 SaveTokensToStorage called - Token length: XXX
[SupabaseTokenManager] 📝 Set _accessToken in memory: True
[SupabaseTokenManager] Saved tokens to PlayerPrefs. User: {username}, Balance: {balance}
[GameManager] SupabaseTokenManager ready. User: {username}, Balance: {balance} FZ
Login successful!
```

---

### **2. Auto-Login Flow**
```
App Start → GameManager.CheckForAutomaticLogin()
  ↓
SupabaseTokenManager.Instance.HasValidSession()
  - Checks: token exists + expiry > now + 5 minutes
  ↓
If valid: Load UserData from SupabaseTokenManager
  - UserData.id = SupabaseTokenManager.UserId
  - UserData.username = SupabaseTokenManager.Username
  - UserData.email = SupabaseTokenManager.UserEmail
  - CurrencyManager.Freekz = SupabaseTokenManager.Balance
  ↓
Load MainMenu scene
```

**Test Steps:**
- [ ] Login successfully
- [ ] Close Unity editor completely
- [ ] Reopen Unity and enter Play mode
- [ ] Verify auto-login to MainMenu (no Login screen)
- [ ] Check Account screen still shows correct data
- [ ] Verify balance persisted

**Expected Console Logs:**
```
[SupabaseTokenManager] Loaded tokens from PlayerPrefs. Valid session: True
Supabase session found, logging in...
[GameManager] Supabase login successful: {username}, Balance: {balance} FZ
```

---

### **3. Token Refresh Flow**
```
Background Coroutine (every 60 seconds)
  ↓
Check: _tokenExpiry < DateTime.UtcNow + 5 minutes
  ↓
If true: SupabaseTokenManager.RefreshAccessTokenAsync()
  - POST to /auth/v1/token?grant_type=refresh_token
  - Body: { "refresh_token": _refreshToken }
  ↓
Parse new tokens
  ↓
Update _accessToken, _refreshToken, _tokenExpiry
  ↓
Save to storage
  ↓
Trigger OnTokenRefreshed event
```

**Test Steps:**
- [ ] Login successfully
- [ ] Wait in MainMenu for 10-15 minutes (or modify refresh threshold to 1 minute for testing)
- [ ] Watch console for refresh logs
- [ ] Verify no session expiry errors
- [ ] Verify balance API calls still work after refresh

**Expected Console Logs:**
```
[SupabaseTokenManager] Token expires in X.X minutes. Refreshing...
[SupabaseTokenManager] Token refreshed successfully. New expiry: YYYY-MM-DD HH:MM:SS UTC
```

---

### **4. Logout Flow**
```
User clicks Logout → AuthManager.LogOut()
  ↓
SupabaseTokenManager.Instance.ClearTokens()
  - WebGL: ClearAllSupabaseTokens() jslib call
  - Native: PlayerPrefs.DeleteKey() for all keys
  ↓
Clear GameManager.UserData
Clear GameManager.PromoCode
CurrencyManager.Freekz = 0
  ↓
Load Login scene
```

**Test Steps:**
- [ ] While logged in, click Account → Logout
- [ ] Verify transition to Login scene
- [ ] Check console for: `All tokens cleared (logout)`
- [ ] Close and reopen Unity
- [ ] Verify app starts at Login screen (no auto-login)

**Expected Console Logs:**
```
[SupabaseTokenManager] All tokens cleared (logout)
Logged out successfully - all tokens and data cleared
```

---

## 💰 Currency System Verification

### **5. Balance Check (Soft Check)**
```
User tries to join lobby → CurrencyManager.HasSufficientBalance(fee)
  ↓
SupabaseCurrencyController.CheckBalance(requiredAmount)
  - GET /functions/v1/check-balance
  - Headers: Authorization: Bearer {access_token}
  - Body: { "amount": requiredAmount }
  ↓
Returns: { success, has_sufficient_balance, current_balance }
```

**Test Steps:**
- [ ] Login with account that has 100 FZ
- [ ] Try to join 50 FZ lobby
- [ ] Verify balance check passes (no error)
- [ ] Try to join 200 FZ lobby
- [ ] Verify error message: "Insufficient balance"

**Expected Console Logs:**
```
[GameLogger] CheckBalance request: 50 FZ required
[GameLogger] CheckBalance response: Success=true, HasSufficient=true, Balance=100
```

---

### **6. Entry Fee Deduction (Hard Check)**
```
Game starts → CurrencyManager.DeductEntryFee(lobbyId, fee)
  ↓
SupabaseCurrencyController.DeductEntryFee(lobbyId, lobbyFee)
  - POST /functions/v1/deduct-entry-fee
  - Headers: Authorization: Bearer {access_token}
  - Body: { "lobby_id": lobbyId, "lobby_fee": lobbyFee }
  ↓
Supabase Edge Function:
  1. Verifies user has sufficient balance
  2. Deducts fee from balance
  3. Records transaction in currency_transactions table
  ↓
Returns: { success, balance, transaction, message }
  ↓
Update CurrencyManager.Freekz = result.Balance
```

**Test Steps:**
- [ ] Login with 100 FZ balance
- [ ] Join 50 FZ lobby
- [ ] Wait for game to start
- [ ] Check console for deduction log
- [ ] Verify balance updates to 50 FZ in UI
- [ ] Check Supabase database:
  - [ ] users table: balance = 50
  - [ ] currency_transactions table: new row with amount=-50, type='deduction'

**Expected Console Logs:**
```
🔑 Checking token - SupabaseTokenManager.Instance: True
🔑 Token from SupabaseTokenManager: True (length: XXX)
DeductEntryFee Request: URL=https://evbrcrmyvxqeuomaocvz.supabase.co/functions/v1/deduct-entry-fee, Body={"lobby_id":"XXX","lobby_fee":50}
DeductEntryFee Success: Balance updated to 50 FZ
```

---

### **7. Game Reward Credit**
```
Game ends → ResultScreen.Initialize()
  ↓
CurrencyManager.CreditGameReward(lobbyId, fee, reward, won)
  ↓
SupabaseCurrencyController.CreditGameReward(...)
  - POST /functions/v1/credit-game-reward
  - Body: { "lobby_id", "lobby_fee", "reward_amount", "won_game" }
  ↓
Supabase Edge Function:
  - If won_game=true: Credits balance + reward
  - If won_game=false: Logs loss transaction
  - Records transaction in currency_transactions table
  ↓
Returns: { success, balance, transaction, message }
  ↓
Update CurrencyManager.Freekz = result.Balance
```

**Test Steps:**
- [ ] Start game with 100 FZ (50 FZ entry fee)
- [ ] Win the game (reward should be ~90 FZ after 10% fee)
- [ ] Check ResultScreen shows: "You Won! +90 FZ"
- [ ] Verify balance updates to 140 FZ (50 deducted + 90 reward)
- [ ] Click "Close" on result screen
- [ ] Verify MainMenu balance shows 140 FZ
- [ ] Check Supabase database:
  - [ ] users table: balance = 140
  - [ ] currency_transactions table: new row with amount=+90, type='reward'

**Test Steps (Loss):**
- [ ] Start game with 100 FZ
- [ ] Lose the game
- [ ] Check ResultScreen shows: "You Lost"
- [ ] Verify balance stays at 50 FZ (only entry fee deducted)
- [ ] Check Supabase database: loss transaction logged

**Expected Console Logs (Win):**
```
CreditGameReward Request: won_game=true, reward=90
CreditGameReward Success: Balance updated to 140 FZ
```

---

## 🔐 Token Storage Verification

### **8. PlayerPrefs Token Storage (Editor/Native)**
**Test Steps:**
- [ ] Login successfully in Unity Editor
- [ ] Open: Edit → Preferences → PlayerPrefs (Windows Registry on Windows)
- [ ] Verify keys exist:
  - [ ] `supabase_access_token`
  - [ ] `supabase_refresh_token`
  - [ ] `supabase_user_id`
  - [ ] `supabase_user_email`
  - [ ] `supabase_user_username`
  - [ ] `supabase_user_balance`
  - [ ] `supabase_user_promo_code`
  - [ ] `supabase_token_expiry`

**Windows Registry Path:**
```
HKEY_CURRENT_USER\SOFTWARE\Unity\UnityEditor\DefaultCompany\EucherFreekz
```

---

### **9. WebGL localStorage Storage (WebGL Build)**
**Prerequisites:**
- WebGL build deployed
- Browser with Developer Tools

**Test Steps:**
- [ ] Login via WebGL build
- [ ] Open Browser DevTools (F12)
- [ ] Go to: Application → Local Storage → {your-domain}
- [ ] Verify keys exist:
  - [ ] `supabase_access_token`
  - [ ] `supabase_refresh_token`
  - [ ] `supabase_user_id`
  - [ ] `supabase_user_email`
  - [ ] `supabase_user_username`
  - [ ] `supabase_user_balance`
  - [ ] `supabase_user_promo_code`
  - [ ] `supabase_token_expiry`

**⚠️ Note:** WebGL requires jslib bridge in `Assets/Plugins/WebGL/`. If missing, tokens will fall back to PlayerPrefs (which doesn't persist in WebGL builds).

---

## 🐛 Error Handling Tests

### **10. Invalid Credentials**
**Test Steps:**
- [ ] Enter wrong password
- [ ] Verify error toast: "Invalid credentials" or similar
- [ ] Check console for: `[LOGIN ERROR RAW]`
- [ ] Verify stays on Login screen

---

### **11. Network Timeout**
**Test Steps:**
- [ ] Disconnect internet
- [ ] Try to login
- [ ] Verify error: "Unable to login. Please try again."
- [ ] Reconnect internet and retry
- [ ] Verify successful login

---

### **12. Expired Session**
**Test Steps:**
- [ ] Manually delete `supabase_refresh_token` from PlayerPrefs
- [ ] Restart app
- [ ] Verify auto-login fails
- [ ] Verify redirected to Login screen
- [ ] Check console: `No valid Supabase session found`

---

### **13. Insufficient Balance**
**Test Steps:**
- [ ] Login with account that has 10 FZ
- [ ] Try to join 50 FZ lobby
- [ ] Verify error message displayed
- [ ] Verify balance not deducted
- [ ] Check console: `CheckBalance response: has_sufficient_balance=false`

---

## 📊 Database Verification

### **14. Supabase Database Checks**
**After each transaction, verify in Supabase Dashboard:**

**users table:**
```sql
SELECT id, username, email, balance, promo_code
FROM users
WHERE email = '{test_email}';
```
- [ ] balance column updates correctly
- [ ] promo_code persists

**currency_transactions table:**
```sql
SELECT id, user_id, amount, transaction_type, reason, lobby_id, created_at
FROM currency_transactions
WHERE user_id = '{user_id}'
ORDER BY created_at DESC
LIMIT 10;
```
- [ ] Deduction transactions: amount < 0, transaction_type='deduction'
- [ ] Reward transactions: amount > 0, transaction_type='reward'
- [ ] Loss transactions: amount = 0, transaction_type='loss'
- [ ] lobby_id matches actual lobby

---

## ✅ Comprehensive Integration Test

### **15. Full Game Flow Test**
**Starting Balance: 100 FZ**

1. [ ] **Login**
   - Enter credentials
   - Verify MainMenu loads
   - Check Account screen: User ID, Username, Email populated
   - Verify balance shows 100 FZ

2. [ ] **Join Lobby**
   - Select 50 FZ lobby
   - Verify no insufficient balance error
   - Wait for game to start

3. [ ] **Game Start**
   - Check console: `DeductEntryFee Success`
   - Verify balance deducted (should show 50 FZ if you check mid-game)

4. [ ] **Play Game**
   - Complete game (either win or lose)

5. [ ] **Game End (Win Scenario)**
   - ResultScreen shows: "You Won! +90 FZ"
   - Check console: `CreditGameReward Success: Balance updated to 140 FZ`
   - Click Close

6. [ ] **Return to MainMenu**
   - Check RefreshPlayerData called
   - Verify balance shows 140 FZ
   - Check Account screen still populated

7. [ ] **Logout**
   - Click Account → Logout
   - Verify Login screen loads
   - Check console: `All tokens cleared`

8. [ ] **Auto-Login Test**
   - Login again
   - Close Unity completely
   - Reopen and enter Play mode
   - Verify auto-login to MainMenu
   - Verify balance persisted (140 FZ)

---

## 🔧 Known Issues & Workarounds

### **WebGL jslib Missing**
**Issue:** SupabaseTokenManager references WebGL jslib for localStorage that may not exist  
**Location:** Lines 37-48 in SupabaseTokenManager.cs  
**Impact:** WebGL builds will fall back to PlayerPrefs (doesn't persist across browser sessions)  
**Workaround:** Create jslib bridge in `Assets/Plugins/WebGL/SupabaseLocalStorage.jslib`

**Required jslib code:**
```javascript
mergeInto(LibraryManager.library, {
    GetFromLocalStorage: function(key) {
        var keyStr = UTF8ToString(key);
        var value = localStorage.getItem(keyStr);
        if (value === null) return null;
        var bufferSize = lengthBytesUTF8(value) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(value, buffer, bufferSize);
        return buffer;
    },
    SaveToLocalStorage: function(key, value) {
        var keyStr = UTF8ToString(key);
        var valueStr = UTF8ToString(value);
        localStorage.setItem(keyStr, valueStr);
    },
    RemoveFromLocalStorage: function(key) {
        var keyStr = UTF8ToString(key);
        localStorage.removeItem(keyStr);
    },
    ClearAllSupabaseTokens: function() {
        localStorage.removeItem('supabase_access_token');
        localStorage.removeItem('supabase_refresh_token');
        localStorage.removeItem('supabase_user_id');
        localStorage.removeItem('supabase_user_email');
        localStorage.removeItem('supabase_user_username');
        localStorage.removeItem('supabase_user_balance');
        localStorage.removeItem('supabase_user_promo_code');
        localStorage.removeItem('supabase_token_expiry');
    },
    IsLocalStorageAvailable: function() {
        return (typeof(Storage) !== "undefined") ? 1 : 0;
    }
});
```

---

## 📈 Success Criteria

**Authentication System is PASSING if:**
- ✅ Login flow works without errors
- ✅ Account screen populates with User ID, Username, Email
- ✅ Balance displays correctly
- ✅ Auto-login works after restart
- ✅ Token refresh happens automatically
- ✅ Logout clears all data
- ✅ DeductEntryFee deducts correctly
- ✅ CreditGameReward credits winner correctly
- ✅ Supabase database updates correctly
- ✅ No HTTP 401 errors during gameplay

**System is FAILING if:**
- ❌ Account screen blank despite being logged in
- ❌ HTTP 401 Unauthorized errors
- ❌ Balance not updating after transactions
- ❌ Auto-login not working after restart
- ❌ Tokens not persisting

---

## 🎯 Next Steps After Testing

1. **If all tests pass:**
   - Mark build as stable
   - Deploy to production
   - Monitor Supabase Edge Function logs

2. **If WebGL localStorage fails:**
   - Create SupabaseLocalStorage.jslib
   - Test WebGL build again

3. **If token refresh fails:**
   - Check Supabase refresh token expiry (default: 30 days)
   - Verify network connectivity during refresh

4. **If balance operations fail:**
   - Check Supabase Edge Functions logs
   - Verify database policies allow authenticated users
   - Check currency_transactions table for error logs

---

**Test Completed By:** _________________  
**Date:** _________________  
**Build Version:** EucherFreekz-dev-huzafa  
**Test Result:** ☐ PASS  ☐ FAIL  
**Notes:** ___________________________________
