/**
 * Supabase Authentication Helper
 * Sets cookie from localStorage for PHP-side authentication checking
 */
(function() {
    'use strict';

    // Function to set cookie
    function setCookie(name, value, days) {
        const expires = new Date();
        expires.setTime(expires.getTime() + (days * 24 * 60 * 60 * 1000));
        document.cookie = `${name}=${value};expires=${expires.toUTCString()};path=/;SameSite=Lax`;
    }

    // Function to delete cookie
    function deleteCookie(name) {
        document.cookie = `${name}=;expires=Thu, 01 Jan 1970 00:00:00 UTC;path=/;`;
    }

    // Check localStorage for Supabase token and sync to cookie
    function syncAuthCookie() {
        const token = localStorage.getItem('ff_supabase_access_token');
        
        if (token) {
            // Set cookie for 7 days
            setCookie('ff_supabase_access_token', token, 7);
        } else {
            // Remove cookie if no token
            deleteCookie('ff_supabase_access_token');
        }
    }

    // Sync on page load
    syncAuthCookie();

    // Also sync whenever localStorage changes (login/logout)
    window.addEventListener('storage', function(e) {
        if (e.key === 'ff_supabase_access_token') {
            syncAuthCookie();
        }
    });

    // Expose logout function globally
    window.supabaseLogout = function() {
        localStorage.removeItem('ff_supabase_access_token');
        localStorage.removeItem('ff_supabase_refresh_token');
        localStorage.removeItem('ff_supabase_user');
        deleteCookie('ff_supabase_access_token');
        window.location.href = '/login';
    };

    // Check if user is authenticated
    window.isSupabaseAuthenticated = function() {
        return !!localStorage.getItem('ff_supabase_access_token');
    };

    console.log('Supabase Auth Helper loaded. Token synced:', !!localStorage.getItem('ff_supabase_access_token'));
})();
