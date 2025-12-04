<?php
/**
 * Site Protection Handler
 * Protects all pages except whitelisted URLs
 */
class SAF_Site_Protection {

    /**
     * Whitelisted URLs that don't require authentication
     */
    private static $whitelist = [
        '/',
        '/login',
        '/forgot-password',
        '/update-password',
        '/faq',
        '/rules',
        '/tutorials',
    ];

    public static function init() {
        add_action('template_redirect', [__CLASS__, 'check_access'], 999);
    }

    /**
     * Check if user has access to current page
     */
    public static function check_access() {
        // Allow access to WordPress admin area first
        if (is_admin()) {
            return;
        }

        // Allow access to REST API endpoints (needed for WordPress/Elementor)
        $current_path = parse_url($_SERVER['REQUEST_URI'], PHP_URL_PATH);
        if (strpos($current_path, '/wp-json/') === 0) {
            return;
        }

        // Allow access to WordPress core files (assets, ajax, etc.)
        if (strpos($current_path, '/wp-') === 0) {
            return;
        }

        // Only allow WordPress ADMIN users on frontend pages
        if (current_user_can('manage_options')) {
            return;
        }

        // Allow Elementor editor and preview modes
        if (isset($_GET['elementor-preview']) || (isset($_GET['action']) && $_GET['action'] === 'elementor')) {
            return;
        }

        // Trim and normalize path
        $current_path = rtrim($current_path, '/');
        if (empty($current_path)) {
            $current_path = '/';
        }

        // Check if current path is whitelisted
        if (self::is_whitelisted($current_path)) {
            return; // Allow access
        }

        // Check if user is authenticated in Supabase
        if (!self::is_user_authenticated()) {
            // Redirect to login page
            wp_redirect('/login?redirect=' . urlencode($_SERVER['REQUEST_URI']));
            exit;
        }
    }

    /**
     * Check if current path is whitelisted
     */
    private static function is_whitelisted($path) {
        foreach (self::$whitelist as $whitelist_path) {
            if ($path === $whitelist_path || $path === rtrim($whitelist_path, '/')) {
                return true;
            }
        }
        return false;
    }

    /**
     * Check if user is authenticated
     * Checks for Supabase access token in cookies (set by JavaScript)
     */
    private static function is_user_authenticated() {
        // Check for Supabase access token in cookies (set by JavaScript)
        if (isset($_COOKIE['ff_supabase_access_token']) && !empty($_COOKIE['ff_supabase_access_token'])) {
            return true;
        }

        // No valid token found
        return false;
    }

    /**
     * Add a URL to the whitelist
     */
    public static function add_to_whitelist($url) {
        if (!in_array($url, self::$whitelist)) {
            self::$whitelist[] = $url;
        }
    }

    /**
     * Remove a URL from the whitelist
     */
    public static function remove_from_whitelist($url) {
        $key = array_search($url, self::$whitelist);
        if ($key !== false) {
            unset(self::$whitelist[$key]);
        }
    }

    /**
     * Get current whitelist
     */
    public static function get_whitelist() {
        return self::$whitelist;
    }
}
