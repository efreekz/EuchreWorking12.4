mergeInto(LibraryManager.library, {
  
  // Get a value from localStorage
  GetFromLocalStorage: function(key) {
    var keyStr = UTF8ToString(key);
    var value = localStorage.getItem(keyStr);
    
    if (value === null || value === undefined) {
      return null;
    }
    
    var bufferSize = lengthBytesUTF8(value) + 1;
    var buffer = _malloc(bufferSize);
    stringToUTF8(value, buffer, bufferSize);
    return buffer;
  },
  
  // Save a value to localStorage
  SaveToLocalStorage: function(key, value) {
    var keyStr = UTF8ToString(key);
    var valueStr = UTF8ToString(value);
    localStorage.setItem(keyStr, valueStr);
  },
  
  // Remove a specific key from localStorage
  RemoveFromLocalStorage: function(key) {
    var keyStr = UTF8ToString(key);
    localStorage.removeItem(keyStr);
  },
  
  // Clear all Supabase-related keys from localStorage
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
  
  // Check if localStorage is available (safety check)
  IsLocalStorageAvailable: function() {
    try {
      var test = '__localStorage_test__';
      localStorage.setItem(test, test);
      localStorage.removeItem(test);
      return 1; // true
    } catch(e) {
      return 0; // false
    }
  }
  
});
