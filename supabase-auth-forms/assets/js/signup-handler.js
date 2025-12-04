/**
 * Shared Signup/Registration Handler
 * Extracted to avoid code duplication across registration forms
 */
(function() {
    'use strict';

    /**
     * Initialize registration form handler
     * @param {object} config - Configuration object
     */
    window.initSignupHandler = function(config) {
        const {
            formId,
            usernameFieldId,
            emailFieldId,
            passwordFieldId,
            confirmPasswordFieldId,
            promoCodeFieldId,
            ageCheckboxId,
            submitBtnId,
            messageContainerId,
            supabaseUrl,
            supabaseAnonKey,
            redirectUrl = '/game'
        } = config;

        const form = document.getElementById(formId);
        const usernameField = document.getElementById(usernameFieldId);
        const emailField = document.getElementById(emailFieldId);
        const passwordField = document.getElementById(passwordFieldId);
        const confirmPasswordField = confirmPasswordFieldId ? document.getElementById(confirmPasswordFieldId) : null;
        const promoCodeField = promoCodeFieldId ? document.getElementById(promoCodeFieldId) : null;
        const ageCheckbox = ageCheckboxId ? document.getElementById(ageCheckboxId) : null;
        const submitBtn = document.getElementById(submitBtnId);
        const messageContainer = document.getElementById(messageContainerId);

        if (!form || !usernameField || !emailField || !passwordField || !submitBtn || !messageContainer) {
            console.error('Signup handler: Missing required elements', config);
            return;
        }

        function showMessage(message, isError = false) {
            // Try different message class patterns
            const className = messageContainer.className.includes('saf-')
                ? `saf-${isError ? 'error' : 'success'}-message`
                : `ef-${isError ? 'error' : 'success'}-message`;
            
            messageContainer.innerHTML = `<div class="${className}">${message}</div>`;
        }

        form.addEventListener('submit', async (e) => {
            e.preventDefault();

            const username = usernameField.value.trim();
            const email = emailField.value.trim();
            const password = passwordField.value;
            const confirmPassword = confirmPasswordField ? confirmPasswordField.value : password;
            const promoCode = promoCodeField ? promoCodeField.value.trim().toUpperCase() : '';

            // Validation
            if (ageCheckbox && !ageCheckbox.checked) {
                showMessage('You must confirm that you are 18 years or older', true);
                return;
            }

            if (!username || !email || !password) {
                showMessage('Please fill in all required fields', true);
                return;
            }

            if (confirmPasswordField && password !== confirmPassword) {
                showMessage('Passwords do not match', true);
                return;
            }

            if (password.length < 6) {
                showMessage('Password must be at least 6 characters', true);
                return;
            }

            // Disable submit button
            submitBtn.disabled = true;
            const originalText = submitBtn.textContent;
            submitBtn.textContent = 'Creating Account...';

            try {
                const response = await fetch(`${supabaseUrl}/functions/v1/signup`, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'Authorization': `Bearer ${supabaseAnonKey}`
                    },
                    body: JSON.stringify({
                        username,
                        email,
                        password,
                        promoCode: promoCode || undefined
                    })
                });

                const data = await response.json();

                if (!response.ok) {
                    throw new Error(data.error || 'Registration failed');
                }

                // Store tokens in localStorage
                localStorage.setItem('ff_supabase_access_token', data.access_token);
                localStorage.setItem('ff_supabase_refresh_token', data.refresh_token);
                localStorage.setItem('ff_supabase_user', JSON.stringify(data.user));

                // Show success message
                showMessage(`Welcome ${username}! Redirecting...`);

                // Redirect after short delay
                setTimeout(() => {
                    window.location.href = redirectUrl;
                }, 1500);

            } catch (error) {
                console.error('Signup error:', error);
                showMessage(error.message || 'Registration failed. Please try again.', true);
                
                // Re-enable submit button
                submitBtn.disabled = false;
                submitBtn.textContent = originalText;
            }
        });
    };

})();
