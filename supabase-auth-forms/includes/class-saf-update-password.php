<?php
/**
 * Update Password Page Handler with Supabase Authentication
 * This page is accessed after clicking the password reset link in email
 */
class SAF_Update_Password {

    public static function init() {
        add_action('init', [__CLASS__, 'add_rewrite_rules']);
        add_action('template_redirect', [__CLASS__, 'handle_update_password_page']);
    }

    /**
     * Add rewrite rule for /update-password
     */
    public static function add_rewrite_rules() {
        add_rewrite_rule('^update-password/?$', 'index.php?saf_update_password=1', 'top');
        add_rewrite_tag('%saf_update_password%', '([^&]+)');
    }

    /**
     * Handle update password page display
     */
    public static function handle_update_password_page() {
        if (get_query_var('saf_update_password')) {
            self::render_update_password_page();
            exit;
        }
    }

    /**
     * Render update password page
     */
    private static function render_update_password_page() {
        $logo_url = SAF_PLUGIN_URL . 'assets/EFlogoV2.png';
        ?>
        <!DOCTYPE html>
        <html <?php language_attributes(); ?>>
        <head>
            <meta charset="<?php bloginfo('charset'); ?>">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>Update Password - <?php bloginfo('name'); ?></title>
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

                .ef-update-container {
                    max-width: 500px;
                    width: 100%;
                    padding: 40px 20px;
                }

                .ef-update-box {
                    background: rgba(255, 255, 255, 0.95);
                    border: 6px solid #FFFFFF;
                    border-radius: 30px;
                    padding: 40px;
                    box-shadow: 0 20px 60px rgba(0,0,0,0.5);
                }

                .ef-update-logo {
                    width: 180px;
                    height: auto;
                    margin: 0 auto 30px;
                    display: block;
                }

                .ef-update-title {
                    font-family: 'Bubblegum Sans', cursive;
                    font-size: 32px;
                    color: #4151E2;
                    text-align: center;
                    margin-bottom: 10px;
                    text-shadow: 2px 2px 0 rgba(65,81,226,0.2);
                }

                .ef-update-subtitle {
                    text-align: center;
                    font-size: 15px;
                    color: #2C3E50;
                    margin-bottom: 30px;
                    font-weight: 600;
                    line-height: 1.5;
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

                .ef-password-requirements {
                    background: #E8F4FD;
                    border: 2px solid #4151E2;
                    border-radius: 12px;
                    padding: 15px;
                    margin-bottom: 20px;
                    font-size: 13px;
                }

                .ef-password-requirements h4 {
                    margin: 0 0 10px 0;
                    color: #2A35A0;
                    font-size: 14px;
                }

                .ef-password-requirements ul {
                    margin: 0;
                    padding-left: 20px;
                }

                .ef-password-requirements li {
                    margin: 5px 0;
                    color: #2C3E50;
                }
            </style>
        </head>
        <body>
            <div class="ef-update-container">
                <div class="ef-update-box">
                    <img src="<?php echo esc_url($logo_url); ?>" alt="EuchreFreekz Logo" class="ef-update-logo">
                    <h1 class="ef-update-title">Set New Password</h1>
                    <p class="ef-update-subtitle">Enter your new password below</p>

                    <div id="ef-message-container"></div>

                    <div class="ef-password-requirements">
                        <h4>Password Requirements:</h4>
                        <ul>
                            <li>At least 8 characters long</li>
                            <li>Must match in both fields</li>
                        </ul>
                    </div>

                    <form id="ef-update-form">
                        <div class="ef-form-group">
                            <label class="ef-form-label">New Password</label>
                            <input
                                type="password"
                                id="ef-password"
                                name="password"
                                class="ef-form-input"
                                placeholder="Enter new password"
                                required
                                minlength="8"
                            />
                        </div>

                        <div class="ef-form-group">
                            <label class="ef-form-label">Confirm Password</label>
                            <input
                                type="password"
                                id="ef-confirm-password"
                                name="confirm_password"
                                class="ef-form-input"
                                placeholder="Confirm new password"
                                required
                                minlength="8"
                            />
                        </div>

                        <button type="submit" class="ef-submit-btn" id="ef-submit-btn">
                            Update Password
                        </button>
                    </form>

                    <div class="ef-links-container">
                        <a href="/login" class="ef-link">Back to Login</a>
                    </div>
                </div>
            </div>

            <script>
            (function() {
                const SUPABASE_URL = '<?php echo esc_js(SAF_SUPABASE_URL); ?>';
                const SUPABASE_ANON_KEY = '<?php echo esc_js(SAF_SUPABASE_ANON_KEY); ?>';
                
                const form = document.getElementById('ef-update-form');
                const submitBtn = document.getElementById('ef-submit-btn');
                const messageContainer = document.getElementById('ef-message-container');

                // Get access token from URL hash (Supabase sends it there)
                const hashParams = new URLSearchParams(window.location.hash.substring(1));
                const accessToken = hashParams.get('access_token');
                const refreshToken = hashParams.get('refresh_token');

                if (!accessToken) {
                    showMessage('Invalid or expired reset link. Please request a new password reset.', true);
                    submitBtn.disabled = true;
                }

                function showMessage(message, isError = false) {
                    messageContainer.innerHTML = `<div class="ef-${isError ? 'error' : 'success'}-message">${message}</div>`;
                }

                form.addEventListener('submit', async (e) => {
                    e.preventDefault();
                    
                    const password = document.getElementById('ef-password').value;
                    const confirmPassword = document.getElementById('ef-confirm-password').value;

                    if (!password || !confirmPassword) {
                        showMessage('Please fill in all fields', true);
                        return;
                    }

                    if (password.length < 8) {
                        showMessage('Password must be at least 8 characters long', true);
                        return;
                    }

                    if (password !== confirmPassword) {
                        showMessage('Passwords do not match', true);
                        return;
                    }

                    submitBtn.disabled = true;
                    submitBtn.textContent = 'Updating...';

                    try {
                        // Update password using Supabase API
                        const response = await fetch(`${SUPABASE_URL}/auth/v1/user`, {
                            method: 'PUT',
                            headers: {
                                'Content-Type': 'application/json',
                                'Authorization': `Bearer ${accessToken}`,
                                'apikey': SUPABASE_ANON_KEY
                            },
                            body: JSON.stringify({
                                password: password
                            })
                        });

                        if (response.ok) {
                            showMessage('Password updated successfully! Redirecting to login...');
                            form.reset();
                            
                            // Redirect to login after 2 seconds
                            setTimeout(() => {
                                window.location.href = '/login';
                            }, 2000);
                        } else {
                            const data = await response.json();
                            throw new Error(data.error_description || data.msg || 'Failed to update password');
                        }

                    } catch (error) {
                        console.error('Password update error:', error);
                        showMessage(error.message || 'Failed to update password. Please try again.', true);
                        submitBtn.disabled = false;
                        submitBtn.textContent = 'Update Password';
                    }
                });
            })();
            </script>

            <?php wp_footer(); ?>
        </body>
        </html>
        <?php
    }
}
