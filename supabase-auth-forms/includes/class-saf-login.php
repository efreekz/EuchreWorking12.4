<?php
/**
 * Login Page Handler with Supabase Authentication
 */
class SAF_Login {

    public static function init() {
        add_action('init', [__CLASS__, 'add_rewrite_rules']);
        add_action('template_redirect', [__CLASS__, 'handle_login_page']);
    }

    /**
     * Add rewrite rule for /login
     */
    public static function add_rewrite_rules() {
        add_rewrite_rule('^login/?$', 'index.php?saf_login=1', 'top');
        add_rewrite_tag('%saf_login%', '([^&]+)');
    }

    /**
     * Handle login page display
     */
    public static function handle_login_page() {
        if (get_query_var('saf_login')) {
            self::render_login_page();
            exit;
        }
    }

    /**
     * Render login page
     */
    private static function render_login_page() {
        $logo_url = SAF_PLUGIN_URL . 'assets/EFlogoV2.png';
        ?>
        <!DOCTYPE html>
        <html <?php language_attributes(); ?>>
        <head>
            <meta charset="<?php bloginfo('charset'); ?>">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>Login - <?php bloginfo('name'); ?></title>
            <link rel="preconnect" href="https://fonts.googleapis.com">
            <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
            <link href="https://fonts.googleapis.com/css2?family=Fredoka:wght@400;600;700&family=Bubblegum+Sans&display=swap" rel="stylesheet">
            <?php wp_head(); ?>
            <style>
                body {
                    font-family: 'Fredoka', 'Comic Sans MS', cursive;
                    background: linear-gradient(180deg, #5563F7 0%, #4151E2 50%, #3641C0 100%);
                    color: #2C3E50;
                    min-height: 100vh;
                    margin: 0;
                    padding: 0;
                    display: flex;
                    align-items: center;
                    justify-content: center;
                }

                .ef-login-container {
                    max-width: 500px;
                    width: 100%;
                    padding: 40px 20px;
                }

                .ef-login-box {
                    background: rgba(255, 255, 255, 0.95);
                    border: 6px solid #FFFFFF;
                    border-radius: 30px;
                    padding: 40px;
                    box-shadow: 0 20px 60px rgba(0,0,0,0.5);
                }

                .ef-login-logo {
                    width: 180px;
                    height: auto;
                    margin: 0 auto 30px;
                    display: block;
                }

                .ef-login-title {
                    font-family: 'Bubblegum Sans', cursive;
                    font-size: 36px;
                    color: #4151E2;
                    text-align: center;
                    margin-bottom: 10px;
                    text-shadow: 2px 2px 0 rgba(65,81,226,0.2);
                }

                .ef-login-subtitle {
                    text-align: center;
                    font-size: 16px;
                    color: #2C3E50;
                    margin-bottom: 30px;
                    font-weight: 600;
                }

                .ef-form-group {
                    margin-bottom: 20px;
                }

                .ef-form-label {
                    display: block;
                    font-size: 15px;
                    color: #2A35A0;
                    font-weight: 700;
                    margin-bottom: 8px;
                }

                .ef-form-input {
                    width: 100%;
                    padding: 14px;
                    font-size: 15px;
                    border: 3px solid #4151E2;
                    border-radius: 12px;
                    font-family: 'Fredoka', cursive;
                    font-weight: 500;
                    transition: all 0.2s;
                    box-sizing: border-box;
                }

                .ef-form-input:focus {
                    outline: none;
                    border-color: #00D97E;
                    box-shadow: 0 0 0 3px rgba(0,217,126,0.2);
                }

                .ef-submit-btn {
                    width: 100%;
                    background: linear-gradient(135deg, #5DADE2 0%, #3498DB 100%);
                    color: white;
                    border: 3px solid white;
                    padding: 16px 35px;
                    font-size: 22px;
                    font-weight: 700;
                    font-family: 'Bubblegum Sans', cursive;
                    border-radius: 15px;
                    cursor: pointer;
                    text-transform: uppercase;
                    letter-spacing: 1px;
                    transition: all 0.2s;
                    box-shadow: 0 4px 0 #2874A6, 0 6px 15px rgba(0,0,0,0.3);
                    margin-top: 10px;
                }

                .ef-submit-btn:hover {
                    transform: translateY(-3px);
                    box-shadow: 0 7px 0 #2874A6, 0 9px 20px rgba(0,0,0,0.4);
                }

                .ef-submit-btn:active {
                    transform: translateY(2px);
                    box-shadow: 0 2px 0 #2874A6, 0 4px 10px rgba(0,0,0,0.3);
                }

                .ef-submit-btn:disabled {
                    opacity: 0.6;
                    cursor: not-allowed;
                }

                .ef-links-container {
                    text-align: center;
                    margin-top: 20px;
                    padding-top: 20px;
                    border-top: 2px solid #E0E7FF;
                }

                .ef-link {
                    color: #4151E2;
                    font-weight: 700;
                    text-decoration: none;
                    transition: all 0.2s;
                    font-size: 14px;
                    margin: 0 10px;
                }

                .ef-link:hover {
                    color: #00D97E;
                    text-decoration: underline;
                }

                .ef-error-message {
                    background: #e74c3c;
                    color: white;
                    padding: 12px;
                    border-radius: 10px;
                    margin-bottom: 15px;
                    text-align: center;
                    font-weight: 600;
                }

                .ef-success-message {
                    background: #00D97E;
                    color: white;
                    padding: 12px;
                    border-radius: 10px;
                    margin-bottom: 15px;
                    text-align: center;
                    font-weight: 600;
                }
            </style>
        </head>
        <body>
            <div class="ef-login-container">
                <div class="ef-login-box">
                    <img src="<?php echo esc_url($logo_url); ?>" alt="EuchreFreekz Logo" class="ef-login-logo">
                    <h1 class="ef-login-title">Welcome Back!</h1>
                    <p class="ef-login-subtitle">Log in to continue playing</p>

                    <div id="ef-message-container"></div>

                    <form id="ef-login-form">
                        <div class="ef-form-group">
                            <label class="ef-form-label">Username or Email</label>
                            <input
                                type="text"
                                id="ef-login-identifier"
                                name="identifier"
                                class="ef-form-input"
                                placeholder="username or your@email.com"
                                required
                            />
                        </div>

                        <div class="ef-form-group">
                            <label class="ef-form-label">Password</label>
                            <input
                                type="password"
                                id="ef-password"
                                name="password"
                                class="ef-form-input"
                                placeholder="Enter your password"
                                required
                            />
                        </div>

                        <button type="submit" class="ef-submit-btn" id="ef-submit-btn">
                            Log In
                        </button>
                    </form>

                    <div class="ef-links-container">
                        <a href="/forgot-password" class="ef-link">Forgot Password?</a>
                        <span style="color: #95A5A6;">|</span>
                        <a href="/" class="ef-link">Create Account</a>
                    </div>
                </div>
            </div>

            <script>
            (function() {
                const SUPABASE_URL = '<?php echo esc_js(SAF_SUPABASE_URL); ?>';
                const SUPABASE_ANON_KEY = '<?php echo esc_js(SAF_SUPABASE_ANON_KEY); ?>';
                
                // Initialize login form with shared handler
                if (typeof initLoginHandler === 'function') {
                    initLoginHandler({
                        formId: 'ef-login-form',
                        identifierFieldId: 'ef-login-identifier',
                        passwordFieldId: 'ef-password',
                        submitBtnId: 'ef-submit-btn',
                        messageContainerId: 'ef-message-container',
                        supabaseUrl: SUPABASE_URL,
                        supabaseAnonKey: SUPABASE_ANON_KEY,
                        redirectUrl: '/game'
                    });
                } else {
                    console.error('initLoginHandler not found. Make sure login-handler.js is loaded.');
                }
            })();
            </script>

            <?php wp_footer(); ?>
        </body>
        </html>
        <?php
    }
}
