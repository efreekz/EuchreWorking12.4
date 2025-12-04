<?php
/**
 * Homepage Shortcode with Supabase Authentication
 */
class SAF_Homepage {

    public static function init() {
        add_shortcode('saf_homepage', [__CLASS__, 'homepage_display']);
        add_shortcode('saf_login', [__CLASS__, 'login_form_display']);
    }

    public static function homepage_display() {
        // Get the plugin URL for logo
        $logo_url = SAF_PLUGIN_URL . 'assets/EFlogoV2.png';
        
        ob_start();
        ?>
        <style>
        @import url('https://fonts.googleapis.com/css2?family=Fredoka:wght@400;600;700&family=Bubblegum+Sans&display=swap');

        /* ==================================== GLOBAL STYLES ==================================== */
        .ef-homepage-container {
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

        .ef-homepage-container * {
            box-sizing: border-box;
        }

        /* ==================================== HERO SECTION ==================================== */
        .ef-hero {
            max-width: 1200px;
            width: 100%;
            margin: 0 auto;
            padding: 40px;
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 60px;
            align-items: center;
        }

        .ef-hero-left {
            text-align: center;
        }

        .ef-hero-title {
            font-family: 'Impact', 'Arial Black', sans-serif;
            font-size: 56px;
            color: #FFFFFF;
            text-shadow: 4px 4px 0 rgba(0,0,0,0.4);
            letter-spacing: 3px;
            margin-bottom: 20px;
            animation: ef-slideDown 0.8s ease-out;
            line-height: 1.2;
            font-style: italic;
            text-transform: uppercase;
        }

        @keyframes ef-slideDown {
            from {
                opacity: 0;
                transform: translateY(-50px);
            }
            to {
                opacity: 1;
                transform: translateY(0);
            }
        }

        .ef-hero-logo {
            width: 100%;
            max-width: 300px;
            height: auto;
            margin: 25px auto;
            display: block;
            animation: ef-logoFloat 3s ease-in-out infinite;
        }

        @keyframes ef-logoFloat {
            0%, 100% { transform: translateY(0px) rotate(0deg); }
            50% { transform: translateY(-10px) rotate(2deg); }
        }

        .ef-hero-subtitle {
            font-size: 24px;
            color: #E0E7FF;
            margin-top: 25px;
            font-weight: 600;
            animation: ef-slideDown 0.8s ease-out 0.2s both;
        }

        .ef-hero-tagline {
            font-size: 20px;
            color: #FFD700;
            margin-top: 15px;
            font-weight: 600;
            font-family: 'Brush Script MT', cursive;
            font-style: italic;
            animation: ef-slideDown 0.8s ease-out 0.25s both;
            text-shadow: 2px 2px 4px rgba(0,0,0,0.3);
        }

        .ef-hero-cards {
            font-size: 70px;
            margin: 25px 0 20px 0;
            display: flex;
            gap: 15px;
            justify-content: center;
            animation: ef-slideDown 0.8s ease-out 0.4s both;
        }

        .ef-card-icon {
            animation: ef-float 3s ease-in-out infinite;
            display: inline-block;
        }

        .ef-card-icon:nth-child(1) { animation-delay: 0s; }
        .ef-card-icon:nth-child(2) { animation-delay: 0.3s; }
        .ef-card-icon:nth-child(3) { animation-delay: 0.6s; }
        .ef-card-icon:nth-child(4) { animation-delay: 0.9s; }

        @keyframes ef-float {
            0%, 100% { transform: translateY(0px) rotate(0deg); }
            50% { transform: translateY(-15px) rotate(5deg); }
        }

        /* ==================================== BUTTONS ==================================== */
        .ef-hero-buttons {
            display: flex;
            gap: 15px;
            justify-content: center;
            margin-top: 20px;
            animation: ef-slideDown 0.8s ease-out 0.6s both;
        }

        .ef-hero-btn {
            background: linear-gradient(135deg, #5DADE2 0%, #3498DB 100%);
            color: white;
            border: 3px solid white;
            padding: 12px 25px;
            font-size: 16px;
            font-weight: 700;
            font-family: 'Bubblegum Sans', cursive;
            border-radius: 15px;
            cursor: pointer;
            text-transform: uppercase;
            letter-spacing: 1px;
            transition: all 0.2s;
            box-shadow: 0 4px 0 #2874A6, 0 6px 15px rgba(0,0,0,0.3);
            text-decoration: none;
            display: inline-block;
        }

        .ef-hero-btn:hover {
            transform: translateY(-3px);
            box-shadow: 0 7px 0 #2874A6, 0 9px 20px rgba(0,0,0,0.4);
        }

        .ef-hero-btn:active {
            transform: translateY(2px);
            box-shadow: 0 2px 0 #2874A6, 0 4px 10px rgba(0,0,0,0.3);
        }

        /* ==================================== REGISTRATION BOX ==================================== */
        .ef-registration-container {
            animation: ef-slideUp 0.8s ease-out;
        }

        @keyframes ef-slideUp {
            from {
                opacity: 0;
                transform: translateY(50px);
            }
            to {
                opacity: 1;
                transform: translateY(0);
            }
        }

        .ef-registration-box {
            background: rgba(255, 255, 255, 0.95);
            border: 6px solid #FFFFFF;
            border-radius: 30px;
            padding: 35px;
            box-shadow: 0 20px 60px rgba(0,0,0,0.5);
            max-width: 500px;
        }

        .ef-registration-title {
            font-family: 'Bubblegum Sans', cursive;
            font-size: 32px;
            color: #4151E2;
            text-align: center;
            margin-bottom: 8px;
            text-shadow: 2px 2px 0 rgba(65,81,226,0.2);
        }

        .ef-registration-subtitle {
            text-align: center;
            font-size: 15px;
            color: #2C3E50;
            margin-bottom: 25px;
            font-weight: 600;
        }

        .ef-registration-bonus {
            background: linear-gradient(135deg, #5DADE2 0%, #3498DB 100%);
            border: 3px solid white;
            border-radius: 15px;
            padding: 15px;
            text-align: center;
            margin-bottom: 25px;
            box-shadow: 0 4px 12px rgba(52,152,219,0.4);
        }

        .ef-bonus-text {
            font-family: 'Bubblegum Sans', cursive;
            font-size: 20px;
            color: white;
            text-shadow: 2px 2px 0 rgba(0,0,0,0.2);
            margin: 0;
        }

        .ef-bonus-amount {
            font-size: 36px;
            display: block;
            margin: 5px 0;
        }

        .ef-form-group {
            margin-bottom: 18px;
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
        }

        .ef-form-input:focus {
            outline: none;
            border-color: #00D97E;
            box-shadow: 0 0 0 3px rgba(0,217,126,0.2);
        }

        .ef-form-input::placeholder {
            color: #95A5A6;
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

        .ef-login-link-container {
            text-align: center;
            margin-top: 18px;
            padding-top: 18px;
            border-top: 2px solid #E0E7FF;
        }

        .ef-login-text {
            font-size: 14px;
            color: #2C3E50;
            font-weight: 500;
        }

        .ef-login-link {
            color: #4151E2;
            font-weight: 700;
            text-decoration: none;
            transition: all 0.2s;
        }

        .ef-login-link:hover {
            color: #00D97E;
            text-decoration: underline;
        }

        .ef-checkbox-group {
            margin: 18px 0;
            display: flex;
            align-items: center;
            gap: 10px;
        }

        .ef-checkbox {
            width: 20px;
            height: 20px;
            cursor: pointer;
        }

        .ef-checkbox-label {
            font-size: 14px;
            color: #2C3E50;
            font-weight: 600;
            cursor: pointer;
            user-select: none;
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

        /* ==================================== RESPONSIVE DESIGN ==================================== */
        @media (max-width: 968px) {
            .ef-hero {
                grid-template-columns: 1fr;
                gap: 40px;
                text-align: center;
                padding: 30px 20px;
            }

            .ef-hero-left {
                text-align: center;
            }

            .ef-hero-title {
                font-size: 40px;
            }

            .ef-hero-logo {
                max-width: 300px;
                margin: 20px auto;
            }

            .ef-hero-subtitle {
                font-size: 20px;
            }

            .ef-hero-cards {
                justify-content: center;
                font-size: 50px;
            }

            .ef-registration-box {
                max-width: 100%;
            }
        }

        @media (max-width: 768px) {
            .ef-hero-title {
                font-size: 32px;
            }

            .ef-hero-subtitle {
                font-size: 18px;
            }

            .ef-hero-cards {
                font-size: 40px;
                gap: 10px;
            }

            .ef-registration-box {
                padding: 25px 20px;
            }

            .ef-registration-title {
                font-size: 26px;
            }
        }
        </style>

        <div class="ef-homepage-container">
            <section class="ef-hero">
                <div class="ef-hero-left">
                    <h1 class="ef-hero-title">Welcome To EuchreFreekz</h1>
                    <img src="<?php echo esc_url($logo_url); ?>" alt="EuchreFreekz Logo" class="ef-hero-logo">
                    <p class="ef-hero-tagline">Win Real Cash & Prizes</p>
                    <p class="ef-hero-subtitle">The Most Exciting Place To Play Euchre</p>
                    <div class="ef-hero-cards">
                        <span class="ef-card-icon">🂡</span>
                        <span class="ef-card-icon">🂱</span>
                        <span class="ef-card-icon">🃁</span>
                        <span class="ef-card-icon">🃑</span>
                    </div>
                    <div class="ef-hero-buttons">
                        <a href="/faq" class="ef-hero-btn">F.A.Q.</a>
                        <a href="/rules" class="ef-hero-btn">Rules</a>
                        <a href="/tutorials" class="ef-hero-btn">Tutorials</a>
                    </div>
                </div>

                <!-- REGISTRATION BOX -->
                <div class="ef-registration-container">
                    <div class="ef-registration-box">
                        <h2 class="ef-registration-title">Join Free Today!</h2>
                        <p class="ef-registration-subtitle">Start playing in seconds</p>
                        
                        <div class="ef-registration-bonus">
                            <p class="ef-bonus-text">
                                <span class="ef-bonus-amount">100 FREE Freekz</span>
                                with Promo Code
                            </p>
                        </div>

                        <div id="ef-message-container"></div>

                        <form class="ef-registration-form" id="ef-signup-form" method="post" onsubmit="return false;">
                            <div class="ef-form-group">
                                <label class="ef-form-label">Username</label>
                                <input
                                    type="text"
                                    id="ef-username"
                                    name="username"
                                    class="ef-form-input"
                                    placeholder="Choose a unique username"
                                    required
                                />
                            </div>

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

                            <div class="ef-form-group">
                                <label class="ef-form-label">Password</label>
                                <input
                                    type="password"
                                    id="ef-password"
                                    name="password"
                                    class="ef-form-input"
                                    placeholder="Create a strong password"
                                    required
                                />
                            </div>

                            <div class="ef-form-group">
                                <label class="ef-form-label">Promo Code</label>
                                <input
                                    type="text"
                                    id="ef-promo-code"
                                    name="promo_code"
                                    class="ef-form-input"
                                    placeholder="Enter promo code"
                                    required
                                />
                            </div>

                            <div class="ef-checkbox-group">
                                <input
                                    type="checkbox"
                                    id="age-checkbox"
                                    name="age_confirm"
                                    class="ef-checkbox"
                                    required
                                />
                                <label for="age-checkbox" class="ef-checkbox-label">
                                    Click here if over 18 years of age
                                </label>
                            </div>

                            <button type="submit" class="ef-submit-btn" id="ef-submit-btn">
                                Sign Up Free!
                            </button>
                        </form>

                        <div class="ef-login-link-container">
                            <p class="ef-login-text">
                                Already have an account?
                                <a href="#" class="ef-login-link" id="ef-show-login">Log In Here</a>
                            </p>
                        </div>
                        
                        <!-- Hidden Login Form -->
                        <div id="ef-login-section" style="display: none; margin-top: 30px;">
                            <h2 class="ef-registration-title">Log In</h2>
                            <div id="ef-login-message-container"></div>
                            <form id="ef-login-form" method="post" onsubmit="return false;">
                                <div class="ef-form-group">
                                    <label class="ef-form-label">Username or Email</label>
                                    <input type="text" id="ef-login-identifier" class="ef-form-input" placeholder="username or your@email.com" required />
                                </div>
                                <div class="ef-form-group">
                                    <label class="ef-form-label">Password</label>
                                    <input type="password" id="ef-login-password" class="ef-form-input" placeholder="Enter your password" required />
                                </div>
                                <button type="submit" class="ef-submit-btn" id="ef-login-submit-btn">Log In</button>
                            </form>
                            <div class="ef-login-link-container" style="margin-top: 15px; padding-top: 15px; border-top: 2px solid #E0E7FF;">
                                <p class="ef-login-text">
                                    <a href="/forgot-password" class="ef-login-link">Forgot Password?</a>
                                    <span style="color: #95A5A6; margin: 0 8px;">|</span>
                                    <a href="#" class="ef-login-link" id="ef-show-register">Back to Registration</a>
                                </p>
                            </div>
                        </div>
                    </div>
                </div>
            </section>
        </div>

        <script>
        (function() {
            const SUPABASE_URL = '<?php echo esc_js(SAF_SUPABASE_URL); ?>';
            const SUPABASE_ANON_KEY = '<?php echo esc_js(SAF_SUPABASE_ANON_KEY); ?>';
            
            const form = document.getElementById('ef-signup-form');
            const submitBtn = document.getElementById('ef-submit-btn');
            const messageContainer = document.getElementById('ef-message-container');
            
            const loginForm = document.getElementById('ef-login-form');
            const loginSubmitBtn = document.getElementById('ef-login-submit-btn');
            const loginMessageContainer = document.getElementById('ef-login-message-container');
            const loginSection = document.getElementById('ef-login-section');
            const registerForm = document.getElementById('ef-signup-form');
            const registerTitle = document.querySelector('.ef-registration-title');
            const registerSubtitle = document.querySelector('.ef-registration-subtitle');
            const registerBonus = document.querySelector('.ef-registration-bonus');
            const loginLinkContainer = document.querySelector('.ef-login-link-container');
            
            // Toggle between login and registration
            const showLoginBtn = document.getElementById('ef-show-login');
            if (showLoginBtn) {
                showLoginBtn.addEventListener('click', function(e) {
                    e.preventDefault();
                    registerForm.style.display = 'none';
                    registerTitle.style.display = 'none';
                    registerSubtitle.style.display = 'none';
                    registerBonus.style.display = 'none';
                    loginLinkContainer.style.display = 'none';
                    loginSection.style.display = 'block';
                });
            }
            
            const showRegisterBtn = document.getElementById('ef-show-register');
            if (showRegisterBtn) {
                showRegisterBtn.addEventListener('click', function(e) {
                    e.preventDefault();
                    registerForm.style.display = 'block';
                    registerTitle.style.display = 'block';
                    registerSubtitle.style.display = 'block';
                    registerBonus.style.display = 'block';
                    loginLinkContainer.style.display = 'block';
                    loginSection.style.display = 'none';
                });
            }

            // Initialize login form with shared handler
            // Wait for login-handler.js to load
            function tryInitLoginHandler() {
                if (typeof initLoginHandler === 'function') {
                    initLoginHandler({
                        formId: 'ef-login-form',
                        identifierFieldId: 'ef-login-identifier',
                        passwordFieldId: 'ef-login-password',
                        submitBtnId: 'ef-login-submit-btn',
                        messageContainerId: 'ef-login-message-container',
                        supabaseUrl: SUPABASE_URL,
                        supabaseAnonKey: SUPABASE_ANON_KEY,
                        redirectUrl: '/game'
                    });
                } else {
                    // Retry after a short delay if handler not loaded yet
                    setTimeout(tryInitLoginHandler, 100);
                }
            }
            tryInitLoginHandler();

            // Initialize signup form with shared handler
            function tryInitSignupHandler() {
                if (typeof initSignupHandler === 'function') {
                    initSignupHandler({
                        formId: 'ef-signup-form',
                        usernameFieldId: 'ef-username',
                        emailFieldId: 'ef-email',
                        passwordFieldId: 'ef-password',
                        promoCodeFieldId: 'ef-promo-code',
                        ageCheckboxId: 'age-checkbox',
                        submitBtnId: 'ef-submit-btn',
                        messageContainerId: 'ef-message-container',
                        supabaseUrl: SUPABASE_URL,
                        supabaseAnonKey: SUPABASE_ANON_KEY,
                        redirectUrl: '/game'
                    });
                } else {
                    // Retry after a short delay if handler not loaded yet
                    setTimeout(tryInitSignupHandler, 100);
                }
            }
            tryInitSignupHandler();
        })();
        </script>

        <?php
        return ob_get_clean();
    }

    /**
     * Login form shortcode - simplified version for embedding
     */
    public static function login_form_display() {
        $logo_url = SAF_PLUGIN_URL . 'assets/EFlogoV2.png';
        
        ob_start();
        ?>
        <style>
        .saf-login-wrapper {
            font-family: 'Fredoka', 'Comic Sans MS', cursive;
            max-width: 500px;
            margin: 40px auto;
            padding: 20px;
        }
        
        .saf-login-box {
            background: rgba(255, 255, 255, 0.95);
            border: 6px solid #4151E2;
            border-radius: 30px;
            padding: 40px;
            box-shadow: 0 10px 30px rgba(0,0,0,0.2);
        }
        
        .saf-login-logo {
            width: 180px;
            height: auto;
            margin: 0 auto 30px;
            display: block;
        }
        
        .saf-login-title {
            font-family: 'Bubblegum Sans', cursive;
            font-size: 36px;
            color: #4151E2;
            text-align: center;
            margin-bottom: 30px;
        }
        
        .saf-form-group {
            margin-bottom: 20px;
        }
        
        .saf-form-label {
            display: block;
            font-size: 15px;
            color: #2A35A0;
            font-weight: 700;
            margin-bottom: 8px;
        }
        
        .saf-form-input {
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
        
        .saf-form-input:focus {
            outline: none;
            border-color: #00D97E;
            box-shadow: 0 0 0 3px rgba(0,217,126,0.2);
        }
        
        .saf-submit-btn {
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
            box-shadow: 0 4px 0 #2874A6;
            margin-top: 10px;
        }
        
        .saf-submit-btn:hover {
            transform: translateY(-2px);
            box-shadow: 0 6px 0 #2874A6;
        }
        
        .saf-submit-btn:disabled {
            opacity: 0.6;
            cursor: not-allowed;
        }
        
        .saf-links {
            text-align: center;
            margin-top: 20px;
            padding-top: 20px;
            border-top: 2px solid #E0E7FF;
        }
        
        .saf-link {
            color: #4151E2;
            font-weight: 700;
            text-decoration: none;
            margin: 0 10px;
        }
        
        .saf-error-message {
            background: #e74c3c;
            color: white;
            padding: 12px;
            border-radius: 10px;
            margin-bottom: 15px;
            text-align: center;
        }
        
        .saf-success-message {
            background: #00D97E;
            color: white;
            padding: 12px;
            border-radius: 10px;
            margin-bottom: 15px;
            text-align: center;
        }
        </style>
        
        <div class="saf-login-wrapper">
            <div class="saf-login-box">
                <img src="<?php echo esc_url($logo_url); ?>" alt="EuchreFreekz Logo" class="saf-login-logo">
                <h2 class="saf-login-title">Welcome Back!</h2>
                
                <div id="saf-message-container"></div>
                
                <form id="saf-login-form">
                    <div class="saf-form-group">
                        <label class="saf-form-label">Username or Email</label>
                        <input type="text" id="saf-identifier" class="saf-form-input" placeholder="username or your@email.com" required />
                    </div>
                    
                    <div class="saf-form-group">
                        <label class="saf-form-label">Password</label>
                        <input type="password" id="saf-password" class="saf-form-input" placeholder="Enter your password" required />
                    </div>
                    
                    <button type="submit" class="saf-submit-btn" id="saf-submit-btn">Log In</button>
                </form>
                
                <div class="saf-links">
                    <a href="/forgot-password" class="saf-link">Forgot Password?</a>
                    <span style="color: #95A5A6;">|</span>
                    <a href="/" class="saf-link">Create Account</a>
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
                    formId: 'saf-login-form',
                    identifierFieldId: 'saf-identifier',
                    passwordFieldId: 'saf-password',
                    submitBtnId: 'saf-submit-btn',
                    messageContainerId: 'saf-message-container',
                    supabaseUrl: SUPABASE_URL,
                    supabaseAnonKey: SUPABASE_ANON_KEY,
                    redirectUrl: '/game'
                });
            } else {
                console.error('initLoginHandler not found. Make sure login-handler.js is loaded.');
            }
        })();
        </script>
        <?php
        return ob_get_clean();
    }
}
