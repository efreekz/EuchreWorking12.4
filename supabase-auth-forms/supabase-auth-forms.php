<?php
/**
 * Plugin Name: Supabase Auth Forms
 * Plugin URI: https://euchrefreekz.com
 * Description: Clean Supabase authentication integration with beautiful homepage, login, and password reset forms
 * Version: 1.0.0
 * Author: EuchreFreekz Team
 * Author URI: https://euchrefreekz.com
 * Text Domain: supabase-auth-forms
 * Domain Path: /languages
 */

// Exit if accessed directly
if (!defined('ABSPATH')) {
    exit;
}

// Define plugin constants
define('SAF_VERSION', '1.0.0');
define('SAF_PLUGIN_DIR', plugin_dir_path(__FILE__));
define('SAF_PLUGIN_URL', plugin_dir_url(__FILE__));
define('SAF_PLUGIN_FILE', __FILE__);

// Supabase configuration
define('SAF_SUPABASE_URL', 'https://evbrcrmyvxqeuomaocvz.supabase.co');
define('SAF_SUPABASE_ANON_KEY', 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6ImV2YnJjcm15dnhxZXVvbWFvY3Z6Iiwicm9sZSI6ImFub24iLCJpYXQiOjE3MzYzNzk4MzAsImV4cCI6MjA1MTk1NTgzMH0.kBrIBZOyBGIwuJa1XZY41XafW5eKA3YbKDv_nFN9ffc');

/**
 * Autoloader for plugin classes
 */
spl_autoload_register(function ($class) {
    // Only autoload classes with SAF_ prefix
    if (strpos($class, 'SAF_') !== 0) {
        return;
    }

    // Convert class name to file name
    $class_file = strtolower(str_replace('_', '-', $class));
    $file = SAF_PLUGIN_DIR . 'includes/class-' . $class_file . '.php';

    if (file_exists($file)) {
        require_once $file;
    }
});

/**
 * Main plugin class
 */
class Supabase_Auth_Forms {
    
    private static $instance = null;

    /**
     * Get singleton instance
     */
    public static function get_instance() {
        if (null === self::$instance) {
            self::$instance = new self();
        }
        return self::$instance;
    }

    /**
     * Constructor
     */
    private function __construct() {
        $this->init_hooks();
    }

    /**
     * Initialize hooks
     */
    private function init_hooks() {
        add_action('plugins_loaded', [$this, 'init_classes']);
        add_action('wp_enqueue_scripts', [$this, 'enqueue_scripts']);
        register_activation_hook(__FILE__, [$this, 'activate']);
        register_deactivation_hook(__FILE__, [$this, 'deactivate']);
    }

    /**
     * Enqueue scripts
     */
    public function enqueue_scripts() {
        // Core auth helper (cookie sync)
        wp_enqueue_script(
            'supabase-auth-helper',
            SAF_PLUGIN_URL . 'assets/js/supabase-auth-helper.js',
            [],
            SAF_VERSION,
            true
        );

        // Shared login handler (depends on auth helper)
        wp_enqueue_script(
            'supabase-login-handler',
            SAF_PLUGIN_URL . 'assets/js/login-handler.js',
            ['supabase-auth-helper'],
            SAF_VERSION,
            true
        );

        // Shared signup handler (depends on auth helper)
        wp_enqueue_script(
            'supabase-signup-handler',
            SAF_PLUGIN_URL . 'assets/js/signup-handler.js',
            ['supabase-auth-helper'],
            SAF_VERSION,
            true
        );
    }

    /**
     * Initialize plugin classes
     */
    public function init_classes() {
        // Initialize homepage with shortcode
        if (class_exists('SAF_Homepage')) {
            SAF_Homepage::init();
        }

        // Initialize login page
        if (class_exists('SAF_Login')) {
            SAF_Login::init();
        }

        // Initialize password reset page
        if (class_exists('SAF_Password_Reset')) {
            SAF_Password_Reset::init();
        }

        // Initialize update password page
        if (class_exists('SAF_Update_Password')) {
            SAF_Update_Password::init();
        }

        // Initialize site protection
        if (class_exists('SAF_Site_Protection')) {
            SAF_Site_Protection::init();
        }
    }

    /**
     * Plugin activation
     */
    public function activate() {
        // Flush rewrite rules
        flush_rewrite_rules();
    }

    /**
     * Plugin deactivation
     */
    public function deactivate() {
        // Flush rewrite rules
        flush_rewrite_rules();
    }
}

// Initialize plugin
Supabase_Auth_Forms::get_instance();
