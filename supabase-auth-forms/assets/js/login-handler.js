/**
 * Shared Login Handler
 * Extracted to avoid code duplication across login forms
 */
(function() {
    'use strict';

    /**
     * Initialize login form handler
     * @param {string} formId - ID of the form element
     * @param {string} identifierFieldId - ID of the username/email input
     * @param {string} passwordFieldId - ID of the password input
     * @param {string} submitBtnId - ID of the submit button
     * @param {string} messageContainerId - ID of the message container
     * @param {string} supabaseUrl - Supabase project URL
     * @param {string} supabaseAnonKey - Supabase anon key
     * @param {string} redirectUrl - Where to redirect after successful login (default: '/game')
     */
    window.initLoginHandler = function(config) {
        console.log('🔵 initLoginHandler called with config:', config);
        
        const {
            formId,
            identifierFieldId,
            passwordFieldId,
            submitBtnId,
            messageContainerId,
            supabaseUrl,
            supabaseAnonKey,
            redirectUrl = '/game'
        } = config;

        const form = document.getElementById(formId);
        const identifierField = document.getElementById(identifierFieldId);
        const passwordField = document.getElementById(passwordFieldId);
        const submitBtn = document.getElementById(submitBtnId);
        const messageContainer = document.getElementById(messageContainerId);

        console.log('🔵 DOM Elements found:', {
            form: !!form,
            identifierField: !!identifierField,
            passwordField: !!passwordField,
            submitBtn: !!submitBtn,
            messageContainer: !!messageContainer
        });

        if (!form || !identifierField || !passwordField || !submitBtn || !messageContainer) {
            console.error('❌ Login handler: Missing required elements', config);
            console.error('❌ Missing elements:', {
                form: form ? 'found' : 'MISSING',
                identifierField: identifierField ? 'found' : 'MISSING',
                passwordField: passwordField ? 'found' : 'MISSING',
                submitBtn: submitBtn ? 'found' : 'MISSING',
                messageContainer: messageContainer ? 'found' : 'MISSING'
            });
            return;
        }

        console.log('✅ All login form elements found, attaching event listener');

        function showMessage(message, isError = false) {
            // Try different message class patterns
            const className = messageContainer.className.includes('saf-') 
                ? `saf-${isError ? 'error' : 'success'}-message`
                : `ef-${isError ? 'error' : 'success'}-message`;
            
            messageContainer.innerHTML = `<div class="${className}">${message}</div>`;
        }

        form.addEventListener('submit', async (e) => {
            console.log('🟢 Login form submitted!');
            e.preventDefault();

            const identifier = identifierField.value.trim();
            const password = passwordField.value;
            console.log('🟢 Login attempt:', { identifier, passwordLength: password.length });

            if (!identifier || !password) {
                showMessage('Please fill in all fields', true);
                return;
            }

            // Disable submit button
            submitBtn.disabled = true;
            const originalText = submitBtn.textContent;
            submitBtn.textContent = 'Logging In...';

            try {
                const response = await fetch(`${supabaseUrl}/functions/v1/login`, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'Authorization': `Bearer ${supabaseAnonKey}`
                    },
                    body: JSON.stringify({ identifier, password })
                });

                const data = await response.json();

                if (!response.ok) {
                    throw new Error(data.error || 'Login failed');
                }

                // Store tokens in localStorage
                localStorage.setItem('ff_supabase_access_token', data.access_token);
                localStorage.setItem('ff_supabase_refresh_token', data.refresh_token);
                localStorage.setItem('ff_supabase_user', JSON.stringify(data.user));

                // Show success message
                showMessage('Login successful! Redirecting...');

                // Redirect after short delay
                setTimeout(() => {
                    window.location.href = redirectUrl;
                }, 1500);

            } catch (error) {
                console.error('Login error:', error);
                showMessage(error.message || 'Login failed. Please try again.', true);
                
                // Re-enable submit button
                submitBtn.disabled = false;
                submitBtn.textContent = originalText;
            }
        });
    };

})();
