# WebGL WordPress Authentication Integration Guide

This guide provides step-by-step instructions on how to integrate a WordPress-based login system with your Unity WebGL build. This allows users to authenticate through your WordPress site (`euchrefreekz.com/login`) and then be redirected to your WebGL game, maintaining their session across visits.

This guide assumes your game is hosted on the same domain as your WordPress site (e.g., `euchrefreekz.com/game`).

---

## Architecture Overview

1.  **User Starts on WordPress:** A user navigates directly to your WordPress site (`euchrefreekz.com`), where they log in or sign up using a WordPress form.
2.  **WordPress Authenticates with Supabase:** Your WordPress backend communicates with Supabase to authenticate the user and receives a JWT (JSON Web Token) access token and a refresh token.
3.  **Redirect to Game Page:** Upon successful authentication, WordPress redirects the user's browser to your Unity WebGL game's URL (`euchrefreekz.com/game`).
    *   **Crucially:** The `access_token` and `refresh_token` must be appended to this redirect URL as query parameters (e.g., `https://euchrefreekz.com/game?access_token=...&refresh_token=...`).
4.  **WebGL Game Loads:** The browser loads your Unity WebGL game. Before the Unity engine fully initializes, a JavaScript "gatekeeper" script within the game's `index.html` takes over:
    *   It reads the `access_token` and `refresh_token` from the URL.
    *   It saves these tokens into the browser's `localStorage` for session persistence.
    *   It then triggers a method within your Unity game to pick up these tokens and initiate the in-game session.
5.  **Session Persistence:** If a user returns to `euchrefreekz.com/game` later, the JavaScript gatekeeper will check `localStorage` for existing tokens. If found, it will automatically log the user into the game, bypassing the WordPress login. If no tokens are found (neither in the URL nor `localStorage`), the user is redirected back to `euchrefreekz.com/login`.

---

## Phase 1: Configure Unity for Different Build Types

We'll use Unity's Scripting Define Symbols to easily switch between a development (in-editor) login flow and a production (WordPress-based) login flow.

1.  **Open Project Settings:** In the Unity Editor, navigate to **Edit > Project Settings**.
2.  **Select Player Settings:** In the left-hand menu, select the **Player** tab.
3.  **Expand Other Settings:** In the right-hand pane, scroll down and expand the **Other Settings** section.
4.  **Add Scripting Define Symbol:** Locate the **Scripting Define Symbols** field.
    *   Add `USE_WP_AUTH` to the end of the line. If there are existing symbols, separate them with a semicolon (e.g., `EXISTING_SYMBOL;USE_WP_AUTH`).
    *   Click outside the text box and wait for Unity to recompile your scripts.

This symbol (`USE_WP_AUTH`) will be used in your C# code to conditionally compile different login logic.

---

## Phase 2: Modify C# Scripts

These changes will adapt your existing authentication logic to work with the external WordPress system.

### Step 1: Modify `GameManager.cs`

This modification ensures that your game does not try to load its internal `Login` scene when built for WebGL production, and instead waits for the web-based authentication.

1.  **Open `Assets/Scripts/Managers/GameManager.cs`**.
2.  Locate the `CheckForAutomaticLogin()` method.
3.  Find the lines that load the `SceneName.Login` (usually towards the end of the method after checking for a valid session).
4.  **Replace** the existing login scene loading logic with the following:

    ```csharp
    // No valid session found - go to login screen
    #if USE_WP_AUTH
        // In production, if there's no session, we do nothing.
        // The web page's JavaScript is responsible for either providing a token
        // or redirecting the user to the WordPress login page.
        GameLogger.LogNetwork("No valid session. Waiting for web login authentication...");
    #else
        // In development, load the built-in login scene as before.
        GameLogger.LogNetwork("No valid Supabase session found");
        LoadScene(SceneName.Login);
    #endif
    ```

### Step 2: Modify `AuthManager.cs`

We'll add a new public method to `AuthManager` that the JavaScript in your WebGL template will call after it has successfully stored the authentication tokens.

1.  **Open `Assets/Scripts/Network/AuthManager.cs`**.
2.  Add the following new public method inside the `AuthManager` class (e.g., at the end of the class, before the closing brace `}`).

    ```csharp
        /// <summary>
        /// This method is called from the WebGL template's JavaScript after it
        /// has saved the auth tokens from the website to localStorage.
        /// </summary>
        public void TriggerAutomaticLogin()
        {
            GameLogger.LogNetwork("TriggerAutomaticLogin() called from web page.");
        
            // First, force the token manager to load from storage where JS saved the tokens.
            SupabaseTokenManager.Instance.LoadTokensFromStorage();
        
            // Now, run the standard check. It will find the session and log the user in.
            GameManager.CheckForAutomaticLogin().Forget(); // Using .Forget() for UniTask in void method
        }
    ```

---

## Phase 3: Create the WebGL Template and Add the Gatekeeper Script

This is the most critical part, as it dictates the authentication flow at the browser level, before your Unity game even fully starts.

### Step 1: Create a Custom WebGL Template

If you already use a custom WebGL template, skip this step and integrate the script into your existing `index.html`. If not:

1.  In your Unity project's `Assets` folder, create a new folder named `WebGLTemplates`.
2.  Right-click inside the `Assets/WebGLTemplates` folder and select **Create > WebGL Template**.
3.  Name the new template `EuchreFreekzTemplate`.
4.  Go to **Edit > Project Settings > Player**. Under the **Resolution and Presentation** section, select `EuchreFreekzTemplate` from the **WebGL Template** dropdown.

### Step 2: Add the Gatekeeper Script to `index.html`

1.  **Open** the `index.html` file located at `Assets/WebGLTemplates/EuchreFreekzTemplate/index.html`.
2.  **Replace its entire content** with the following code. This provides a basic Unity WebGL template structure with the authentication gatekeeper logic integrated.

    ```html
    <!DOCTYPE html>
    <html lang="en-us">
      <head>
        <meta charset="utf-8">
        <meta http-equiv="Content-Type" content="text/html; charset=utf-8">
        <title>Unity WebGL Player | EuchreFreekz</title>
        <link rel="shortcut icon" href="TemplateData/favicon.ico">
        <link rel="stylesheet" href="TemplateData/style.css">
      </head>
      <body>
        <div id="unity-container" class="unity-desktop">
          <canvas id="unity-canvas" width=960 height=600></canvas>
          <div id="unity-loading-bar">
            <div id="unity-logo"></div>
            <div id="unity-progress-bar-empty">
              <div id="unity-progress-bar-full"></div>
            </div>
          </div>
          <div id="unity-warning"> </div>
          <div id="unity-footer">
            <div id="unity-webgl-logo"></div>
            <div id="unity-fullscreen-button"></div>
            <div id="unity-build-title">EuchreFreekz</div>
          </div>
        </div>

        <!-- *** START OF AUTH GATEKEEPER SCRIPT *** -->
        <script>
          // --- CONFIGURATION ---
          // The URL to your WordPress login page
          const LOGIN_URL = 'https://euchrefreekz.com/login'; 
          // The name of the GameObject in your Unity scene that has the AuthManager.cs script.
          // IMPORTANT: This MUST match the name of your GameObject in Unity.
          const AUTH_MANAGER_GAMEOBJECT_NAME = 'AuthManager'; 

          // Supabase token keys (must match SupabaseTokenManager.cs constant names)
          const KEY_ACCESS_TOKEN = "supabase_access_token";
          const KEY_REFRESH_TOKEN = "supabase_refresh_token";

          // This function is called once Unity has loaded and is ready to receive messages.
          function loadGame(unityInstance) {
            // Now that tokens are confirmed (either new or stored), trigger the login process inside Unity.
            console.log("Auth Gatekeeper: Tokens are in place. Triggering automatic login in Unity...");
            unityInstance.SendMessage(AUTH_MANAGER_GAMEOBJECT_NAME, 'TriggerAutomaticLogin');
          }

          // --- GATEKEEPER LOGIC (Executes BEFORE Unity fully loads) ---
          console.log("Auth Gatekeeper: Starting session check...");
          const urlParams = new URLSearchParams(window.location.search);
          const accessToken = urlParams.get('access_token');
          const refreshToken = urlParams.get('refresh_token');

          // CASE 1: User is arriving from WordPress login (tokens in URL)
          if (accessToken && refreshToken) {
            console.log("Auth Gatekeeper: New tokens found in URL. Saving to localStorage.");
            
            // Save tokens to localStorage so Unity's SupabaseTokenManager can find them
            localStorage.setItem(KEY_ACCESS_TOKEN, accessToken);
            localStorage.setItem(KEY_REFRESH_TOKEN, refreshToken);

            // Clean the tokens from the URL for security and aesthetics
            window.history.replaceState({}, document.title, window.location.pathname);

            // Proceed to load the game (the rest of this script will handle it)
          }

          // CASE 2: Check for an existing session (returning user, tokens in localStorage)
          const storedAccessToken = localStorage.getItem(KEY_ACCESS_TOKEN);

          if (storedAccessToken) {
            console.log("Auth Gatekeeper: Found existing token in localStorage. Proceeding to load game.");
            // If a token exists, we proceed with loading the game.
            // Unity's SupabaseTokenManager.cs will handle validation and refreshing.
            // The game will be loaded by the script block below.
          } 
          // CASE 3: No token in URL, no token in localStorage. User is not logged in.
          else {
            console.log("Auth Gatekeeper: No session found. Redirecting to login page.");
            window.location.href = LOGIN_URL;
          }
        </script>
        <!-- *** END OF AUTH GATEKEEPER SCRIPT *** -->


        <script>
          // --- Unity's Standard WebGL Loader Script (modified) ---
          var container = document.querySelector("#unity-container");
          var canvas = document.querySelector("#unity-canvas");
          var loadingBar = document.querySelector("#unity-loading-bar");
          var progressBarFull = document.querySelector("#unity-progress-bar-full");
          var fullscreenButton = document.querySelector("#unity-fullscreen-button");
          var warningBanner = document.querySelector("#unity-warning");

          function unityShowBanner(msg, type) {
            function updateBannerVisibility() {
              warningBanner.style.display = warningBanner.children.length ? 'block' : 'none';
            }
            var div = document.createElement('div');
            div.innerHTML = msg;
            warningBanner.appendChild(div);
            if (type == 'error') div.style = 'background: red; padding: 10px;';
            else {
              if (type == 'warning') div.style = 'background: yellow; padding: 10px;';
              setTimeout(function() {
                warningBanner.removeChild(div);
                updateBannerVisibility();
              }, 5000);
            }
            updateBannerVisibility();
          }

          var buildUrl = "Build";
          var loaderUrl = buildUrl + "/{{{ LOADER_FILENAME }}}";
          var config = {
            dataUrl: buildUrl + "/{{{ DATA_FILENAME }}}",
            frameworkUrl: buildUrl + "/{{{ FRAMEWORK_FILENAME }}}",
            codeUrl: buildUrl + "/{{{ CODE_FILENAME }}}",
            streamingAssetsUrl: "StreamingAssets",
            companyName: "{{{ COMPANY_NAME }}}",
            productName: "{{{ PRODUCT_NAME }}}",
            productVersion: "{{{ PRODUCT_VERSION }}}",
            showBanner: unityShowBanner,
          };

          if (/iPhone|iPad|iPod|Android/i.test(navigator.userAgent)) {
            container.className = "unity-mobile";
            config.devicePixelRatio = 1;
          } else {
            canvas.style.width = "{{{ WIDTH }}}px";
            canvas.style.height = "{{{ HEIGHT }}}px";
          }
          loadingBar.style.display = "block";

          var script = document.createElement("script");
          script.src = loaderUrl;
          script.onload = () => {
            createUnityInstance(canvas, config, (progress) => {
              progressBarFull.style.width = 100 * progress + "%";
            }).then((unityInstance) => {
              loadingBar.style.display = "none";
              fullscreenButton.onclick = () => {
                unityInstance.SetFullscreen(1);
              };
              
              // *** IMPORTANT: TRIGGER THE LOGIN CHECK AFTER UNITY HAS LOADED ***
              // This calls the 'loadGame' function defined in the Gatekeeper script above.
              loadGame(unityInstance);

            }).catch((message) => {
              alert(message);
            });
          };
          document.body.appendChild(script);
        </script>
      </body>
    </html>
    ```

---

## Final Action Items for Implementation

*   **WordPress Developer Instructions:**
    *   Instruct your WordPress developer that after a successful Supabase login, the site **must redirect** to your game page (e.g., `https://euchrefreekz.com/game`).
    *   This redirect URL **must include** the `access_token` and `refresh_token` from Supabase as query parameters:
        `https://euchrefreekz.com/game?access_token=YOUR_ACCESS_TOKEN&refresh_token=YOUR_REFRESH_TOKEN`
*   **GameObject Name Verification:** In your Unity scene that loads first, ensure the GameObject containing your `AuthManager.cs` script is named exactly `AuthManager`. If not, you'll need to update the `AUTH_MANAGER_GAMEOBJECT_NAME` constant in the `index.html` file to match its actual name.

This guide provides a comprehensive plan for integrating your WordPress-based authentication with your Unity WebGL game. You can now use these instructions when you are ready to proceed with the implementation.