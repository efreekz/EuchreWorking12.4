<?php
/**
 * Password Reset Page Handler with Supabase Authentication
 */
class SAF_Password_Reset {

    public static function init() {
        add_action('init', [__CLASS__, 'add_rewrite_rules']);
        add_action('template_redirect', [__CLASS__, 'handle_password_reset_page']);
    }

    /**
     * Add rewrite rule for /forgot-password
     */
    public static function add_rewrite_rules() {
        add_rewrite_rule('^forgot-password/?$', 'index.php?saf_password_reset=1', 'top');
        add_rewrite_tag('%saf_password_reset%', '([^&]+)');
    }

    /**
     * Handle password reset page display
     */
    public static function handle_password_reset_page() {
        if (get_query_var('saf_password_reset')) {
            self::render_password_reset_page();
            exit;
        }
    }

    /**
     * Render password reset page
     */
    private static function render_password_reset_page() {
        $logo_url = SAF_PLUGIN_URL . 'assets/EFlogoV2.png';
        ?>
        <!DOCTYPE html>
        <html <?php language_attributes(); ?>>
        <head>
            <meta charset="<?php bloginfo('charset'); ?>">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>Reset Password - <?php bloginfo('name'); ?></title>
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

                .ef-reset-container {
                    max-width: 500px;
                    width: 100%;
                    padding: 40px 20px;
                }

                .ef-reset-box {
                    background: rgba(255, 255, 255, 0.95);
                    border: 6px solid #FFFFFF;
                    border-radius: 30px;
                    padding: 40px;
                    box-shadow: 0 20px 60px rgba(0,0,0,0.5);
                }

                .ef-reset-logo {
                    width: 180px;
                    height: auto;
                    margin: 0 auto 30px;
                    display: block;
                }

                .ef-reset-title {
                    font-family: 'Bubblegum Sans', cursive;
                    font-size: 32px;
                    color: #4151E2;
                    text-align: center;
                    margin-bottom: 10px;
                    text-shadow: 2px 2px 0 rgba(65,81,226,0.2);
                }

                .ef-reset-subtitle {
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

                .ef-info-box {
                    background: #E8F4FD;
                    border: 2px solid #4151E2;
                    border-radius: 12px;
                    padding: 15px;
                    margin-bottom: 20px;
                    font-size: 14px;
                    color: #2C3E50;
                    line-height: 1.6;
                }
            </style>
        </head>
        <body>
            <div class="ef-reset-container">
                <div class="ef-reset-box">
                    <img src="<?php echo esc_url($logo_url); ?>" alt="EuchreFreekz Logo" class="ef-reset-logo">
                    <h1 class="ef-reset-title">Reset Password</h1>
                    <p class="ef-reset-subtitle">Enter your email address and we'll send you a password reset link</p>

                    <div id="ef-message-container"></div>

                    <form id="ef-reset-form">
                        <div class="ef-form-group">
                            <label class="ef-form-label">Email Address</label>
                            <input
                                type="email"
                                id="ef-email"
                                name="email"
                                class="ef-form-input"
                                placeholder="your@email.com"
                                required
                            />
                        </div>

                        <button type="submit" class="ef-submit-btn" id="ef-submit-btn">
                            Send Reset Link
                        </button>
                    </form>

                    <div class="ef-links-container">
                        <a href="/login" class="ef-link">Back to Login</a>
                        <span style="color: #95A5A6;">|</span>
                        <a href="/" class="ef-link">Create Account</a>
                    </div>
                </div>
            </div>

            <script>
            (function() {
                const SUPABASE_URL = '<?php echo esc_js(SAF_SUPABASE_URL); ?>';
                const SUPABASE_ANON_KEY = '<?php echo esc_js(SAF_SUPABASE_ANON_KEY); ?>';
                
                // Debug logging
                console.log('SUPABASE_URL:', SUPABASE_URL);
                console.log('SUPABASE_ANON_KEY:', SUPABASE_ANON_KEY ? 'Present (length: ' + SUPABASE_ANON_KEY.length + ')' : 'MISSING');
                console.log('Full endpoint:', `${SUPABASE_URL}/functions/v1/reset-password`);
                
                const form = document.getElementById('ef-reset-form');
                const submitBtn = document.getElementById('ef-submit-btn');
                const messageContainer = document.getElementById('ef-message-container');

                function showMessage(message, isError = false) {
                    messageContainer.innerHTML = `<div class="ef-${isError ? 'error' : 'success'}-message">${message}</div>`;
                }

                form.addEventListener('submit', async (e) => {
                    e.preventDefault();
                    
                    const email = document.getElementById('ef-email').value;

                    if (!email) {
                        showMessage('Please enter your email address', true);
                        return;
                    }

                    submitBtn.disabled = true;
                    submitBtn.textContent = 'Sending...';

                    try {
                        // Call custom Supabase Edge Function for password reset
                        console.log('Calling reset-password function...');
                        const response = await fetch(`${SUPABASE_URL}/functions/v1/reset-password`, {
                            method: 'POST',
                            headers: {
                                'Content-Type': 'application/json',
                                'Authorization': `Bearer ${SUPABASE_ANON_KEY}`
                            },
                            body: JSON.stringify({
                                email: email
                            })
                        });
                        
                        console.log('Response status:', response.status);
                        console.log('Response headers:', [...response.headers.entries()]);

                        const data = await response.json();

                        if (response.ok && data.success) {
                            showMessage('Password reset link sent! Please check your email inbox.');
                            form.reset();
                            
                            // Show info box after successful submission
                            const infoBox = document.createElement('div');
                            infoBox.className = 'ef-info-box';
                            infoBox.innerHTML = '📧 Check your email for the reset link. The link will expire in 1 hour. Make sure to check your spam folder.';
                            messageContainer.appendChild(infoBox);
                        } else {
                            throw new Error(data.error || 'Failed to send reset email');
                        }

                    } catch (error) {
                        console.error('Password reset error:', error);
                        showMessage(error.message || 'Failed to send reset email. Please try again.', true);
                    } finally {
                        submitBtn.disabled = false;
                        submitBtn.textContent = 'Send Reset Link';
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
