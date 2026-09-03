(function () {
  'use strict';

  const REQUEST_TIMEOUT_MS = 30000;

  class ApiError extends Error {
    constructor(message, fieldErrors = {}, status = 0, data = null) {
      super(message);
      this.name = 'ApiError';
      this.fieldErrors = fieldErrors;
      this.status = status;
      this.data = data;
    }
  }

  async function fetchWithTimeout(url, options = {}, timeoutMs = REQUEST_TIMEOUT_MS) {
    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), timeoutMs);
    try {
      return await fetch(url, { ...options, signal: controller.signal });
    } catch (error) {
      if (error?.name === 'AbortError') {
        throw new ApiError('Request timed out. Is the app running? Restart it from Visual Studio (F5).');
      }
      throw error;
    } finally {
      clearTimeout(timeoutId);
    }
  }

  const auth = {
    get accessToken() { return localStorage.getItem('accessToken'); },
    save(session) {
      const accessToken = session.accessToken ?? session.AccessToken;
      const refreshToken = session.refreshToken ?? session.RefreshToken;
      if (!accessToken || !refreshToken) throw new ApiError('Server did not return login tokens.');
      localStorage.setItem('accessToken', accessToken);
      localStorage.setItem('refreshToken', refreshToken);
      localStorage.setItem('connectlyUser', JSON.stringify({
        id: session.userId ?? session.UserId,
        displayName: session.displayName ?? session.DisplayName,
        email: session.email ?? session.Email,
        profilePictureUrl: session.profilePictureUrl ?? session.ProfilePictureUrl
      }));
    },
    clear() {
      ['accessToken', 'refreshToken', 'connectlyUser'].forEach(k => localStorage.removeItem(k));
    },
    async ensureMvcSession() {
      const token = this.accessToken;
      if (!token) return false;
      const response = await fetchWithTimeout('/api/auth/mvc-session', {
        method: 'POST',
        headers: { Authorization: `Bearer ${token}` },
        credentials: 'same-origin'
      });
      return response.ok;
    }
  };

  function showToast(message, type = 'info') {
    const host = document.getElementById('toast-host');
    if (!host) return;
    const toast = document.createElement('div');
    toast.className = `toast ${type === 'success' || type === 'error' ? type : ''}`;
    toast.textContent = message;
    host.append(toast);
    setTimeout(() => toast.remove(), 5000);
  }

  async function api(path, options = {}) {
    const headers = new Headers(options.headers || {});
    if (auth.accessToken) headers.set('Authorization', `Bearer ${auth.accessToken}`);
    if (options.body && !headers.has('Content-Type')) headers.set('Content-Type', 'application/json');

    let response;
    try {
      response = await fetchWithTimeout(`/${path.replace(/^\//, '')}`, {
        credentials: 'same-origin',
        ...options,
        headers
      });
    } catch (error) {
      if (error instanceof ApiError) throw error;
      throw new ApiError('Cannot reach Connectly. Start the app with F5 in Visual Studio.');
    }

    const text = await response.text();
    let body = null;
    if (text) {
      try { body = JSON.parse(text); } catch { body = { message: text }; }
    }

    if (!response.ok || body?.success === false) {
      const raw = body?.fieldErrors ?? body?.FieldErrors ?? {};
      const fieldErrors = typeof raw === 'object' && !Array.isArray(raw)
        ? Object.fromEntries(Object.entries(raw).map(([k, v]) => [k.charAt(0).toLowerCase() + k.slice(1), Array.isArray(v) ? v : [String(v)]]))
        : {};
      const message = body?.message || body?.Message || `Request failed (${response.status}).`;
      throw new ApiError(message, fieldErrors, response.status, body?.data ?? body?.Data ?? null);
    }

    return body?.data ?? body?.Data ?? body;
  }

  let registerMode = false;
  let forgotMode = false;
  let forgotResetMode = false;
  let verifyMode = false;
  let pendingVerifyEmail = '';
  let pendingForgotLogin = '';

  function $(id) { return document.getElementById(id); }

  function clearFormErrors(form) {
    if (!form) return;
    form.querySelectorAll('.field-error').forEach(el => { el.textContent = ''; el.classList.add('d-none'); });
    form.querySelectorAll('.form-control.is-invalid').forEach(el => el.classList.remove('is-invalid'));
    form.querySelectorAll('.form-alert').forEach(el => { el.textContent = ''; el.classList.add('d-none'); });
  }

  function showFormAlert(form, message) {
    const alert = form?.querySelector('.form-alert');
    if (!alert || !message) return;
    alert.textContent = message;
    alert.classList.remove('d-none');
  }

  function showFieldErrors(form, fieldErrors, { highlightFields = true } = {}) {
    Object.entries(fieldErrors || {}).forEach(([field, messages]) => {
      const text = Array.isArray(messages) ? messages.join(' ') : String(messages);
      const errorEl = form.querySelector(`[data-error-for="${field}"]`);
      const input = form.querySelector(`[name="${field}"]`);
      if (errorEl) { errorEl.textContent = text; errorEl.classList.remove('d-none'); }
      if (highlightFields && input) input.classList.add('is-invalid');
    });
  }

  function showLoginAlert(form, message) {
    showFormAlert(form, message);
  }

  function loginValidationMessage(login, password) {
    const hasLogin = Boolean(login);
    const hasPassword = Boolean(password);
    if (!hasLogin && !hasPassword) return 'Please enter your username or email and password.';
    if (!hasLogin) return 'Please enter your username or email.';
    if (!hasPassword) return 'Please enter your password.';
    return '';
  }

  function sanitizeLogin(value = '') {
    let trimmed = String(value).trim();
    while (trimmed.startsWith('@')) trimmed = trimmed.slice(1).trimStart();
    return trimmed;
  }

  function setMode(mode) {
    registerMode = mode === 'register';
    forgotMode = mode === 'forgot';
    forgotResetMode = mode === 'forgot-reset';
    verifyMode = mode === 'verify';
    $('login-form')?.classList.toggle('d-none', registerMode || forgotMode || forgotResetMode || verifyMode);
    $('register-form')?.classList.toggle('d-none', !registerMode);
    $('forgot-form')?.classList.toggle('d-none', !forgotMode);
    $('forgot-reset-form')?.classList.toggle('d-none', !forgotResetMode);
    $('verify-email-form')?.classList.toggle('d-none', !verifyMode);
    document.querySelector('.auth-tabs')?.classList.toggle('d-none', forgotMode || forgotResetMode || verifyMode);
    const heading = $('auth-heading');
    const copy = $('auth-copy');
    if (heading && copy) {
      if (verifyMode) {
        heading.textContent = 'Verify your email';
        copy.textContent = 'Enter the 6-digit code we sent to your inbox.';
      } else if (forgotResetMode) {
        heading.textContent = 'Reset password';
        copy.textContent = 'Enter the verification code and your new password.';
      } else if (forgotMode) {
        heading.textContent = 'Forgot password?';
        copy.textContent = 'Enter your username or email and we will send a verification code.';
      } else if (registerMode) {
        heading.textContent = 'Create your account';
        copy.textContent = 'Join Connectly and start connecting today.';
      } else {
        heading.textContent = 'Welcome back';
        copy.textContent = 'Enter your details to access your account.';
      }
    }
    document.querySelectorAll('.auth-tab').forEach(t => {
      t.classList.toggle('active', t.dataset.mode === (registerMode ? 'register' : 'login'));
    });
  }

  function showVerifyEmailStep(email) {
    pendingVerifyEmail = email;
    const display = $('verify-email-display');
    const hidden = $('verify-email-hidden');
    if (display) display.textContent = email;
    if (hidden) hidden.value = email;
    $('verify-email-form')?.reset();
    if (hidden) hidden.value = email;
    setMode('verify');
    $('verify-code')?.focus();
  }

  function showForgotResetStep(login) {
    pendingForgotLogin = login;
    $('forgot-reset-login').value = login;
    $('forgot-reset-form')?.reset();
    $('forgot-reset-login').value = login;
    setMode('forgot-reset');
    $('forgot-reset-code')?.focus();
  }

  async function finishSignIn(form, session) {
    auth.save(session);
    for (let attempt = 0; attempt < 3; attempt++) {
      try {
        if (await auth.ensureMvcSession()) {
          showToast('Welcome to Connectly!', 'success');
          window.location.href = '/Feed';
          return;
        }
      } catch (error) {
        if (attempt === 2) throw error;
      }
      await new Promise(r => setTimeout(r, 400));
    }
    throw new ApiError('Signed in but browser session failed. Refresh the page and try again.');
  }

  async function submitLogin(form) {
    clearFormErrors(form);
    const loginInput = form.querySelector('[name="login"]');
    const passwordInput = form.querySelector('[name="password"]');
    const login = sanitizeLogin(loginInput?.value || '');
    const password = passwordInput?.value || '';
    if (loginInput && loginInput.value !== login) loginInput.value = login;
    const validationMessage = loginValidationMessage(login, password);
    if (validationMessage) {
      showLoginAlert(form, validationMessage);
      (login ? passwordInput : loginInput)?.focus();
      return;
    }

    const data = { login, password };

    const button = form.querySelector('button[type="submit"]');
    button.disabled = true;
    const oldText = button.textContent;
    button.textContent = 'Please wait…';

    try {
      const session = await api('api/auth/login', { method: 'POST', body: JSON.stringify(data) });
      await finishSignIn(form, session);
    } catch (error) {
      if (error instanceof ApiError && error.fieldErrors?.email?.some(m => /verify your email/i.test(m))) {
        const email = error.data?.pendingEmail ?? error.data?.PendingEmail ?? (data.login.includes('@') ? data.login : '');
        showLoginAlert(form, error.fieldErrors.email.join(' '));
        if (email) showVerifyEmailStep(String(email).trim().toLowerCase());
        return;
      }
      if (error instanceof ApiError && (error.fieldErrors?.login || error.fieldErrors?.password)) {
        showLoginAlert(form, 'Invalid username or password. Please try again.');
        passwordInput?.focus();
        passwordInput?.select();
        return;
      }
      if (error instanceof ApiError && Object.keys(error.fieldErrors).length) {
        showLoginAlert(form, error.message || 'Sign in failed.');
      } else {
        showLoginAlert(form, error.message || 'Sign in failed.');
      }
    } finally {
      button.disabled = false;
      button.textContent = oldText;
    }
  }

  async function submitRegister(form) {
    clearFormErrors(form);
    const data = Object.fromEntries(new FormData(form));
    data.username = (data.username || '').trim().toLowerCase();
    data.name = (data.name || '').trim();
    data.email = (data.email || '').trim();
    const fieldErrors = {};
    if (!data.name || data.name.length < 2) fieldErrors.name = ['Name must be at least 2 characters.'];
    if (!data.username) fieldErrors.username = ['Username is required.'];
    if (!data.email) fieldErrors.email = ['Email is required.'];
    if (!data.password) fieldErrors.password = ['Password is required.'];
    if (data.password !== data.confirmPassword) fieldErrors.confirmPassword = ['Passwords do not match.'];
    if (!data.dateOfBirth) fieldErrors.dateOfBirth = ['Birthday is required.'];
    if (Object.keys(fieldErrors).length) {
      showFieldErrors(form, fieldErrors);
      showFormAlert(form, 'Please fix the highlighted fields.');
      return;
    }

    const button = form.querySelector('button[type="submit"]');
    button.disabled = true;
    const oldText = button.textContent;
    button.textContent = 'Please wait…';

    try {
      const result = await api('api/auth/register', { method: 'POST', body: JSON.stringify(data) });
      showToast('Account created! Enter the verification code.', 'success');
      showVerifyEmailStep(result?.email ?? result?.Email ?? data.email);
    } catch (error) {
      if (error instanceof ApiError && Object.keys(error.fieldErrors).length) {
        showFieldErrors(form, error.fieldErrors);
        showFormAlert(form, error.message);
      } else {
        showFormAlert(form, error.message || 'Registration failed.');
      }
    } finally {
      button.disabled = false;
      button.textContent = oldText;
    }
  }

  async function submitVerify(form) {
    clearFormErrors(form);
    const email = pendingVerifyEmail || $('verify-email-hidden')?.value;
    const code = $('verify-code')?.value?.trim();
    if (!email || !/^\d{6}$/.test(code || '')) {
      showFieldErrors(form, { code: ['Enter the 6-digit verification code.'] });
      showFormAlert(form, 'Please enter your verification code.');
      return;
    }

    const button = form.querySelector('button[type="submit"]');
    button.disabled = true;
    const oldText = button.textContent;
    button.textContent = 'Verifying…';

    try {
      const session = await api('api/auth/verify-email', { method: 'POST', body: JSON.stringify({ email, code }) });
      await finishSignIn(form, session);
    } catch (error) {
      if (error instanceof ApiError && Object.keys(error.fieldErrors).length) {
        showFieldErrors(form, error.fieldErrors);
        showFormAlert(form, error.message);
      } else {
        showFormAlert(form, error.message || 'Verification failed.');
      }
    } finally {
      button.disabled = false;
      button.textContent = oldText;
    }
  }

  async function submitForgot(form) {
    clearFormErrors(form);
    const login = sanitizeLogin(form.querySelector('[name="login"]')?.value || '');
    if (!login) {
      showFieldErrors(form, { login: ['Username or email is required.'] });
      return;
    }

    const button = form.querySelector('button[type="submit"]');
    button.disabled = true;
    const oldText = button.textContent;
    button.textContent = 'Sending…';

    try {
      await api('api/auth/forgot-password', { method: 'POST', body: JSON.stringify({ login }) });
      showToast('If that account exists, a verification code was sent to your email.', 'success');
      showForgotResetStep(login);
    } catch (error) {
      showFormAlert(form, error.message || 'Could not send verification code.');
    } finally {
      button.disabled = false;
      button.textContent = oldText;
    }
  }

  async function submitForgotReset(form) {
    clearFormErrors(form);
    const login = pendingForgotLogin || $('forgot-reset-login')?.value || '';
    const code = $('forgot-reset-code')?.value?.trim() || '';
    const newPassword = form.querySelector('[name="newPassword"]')?.value || '';
    const confirmPassword = form.querySelector('[name="confirmPassword"]')?.value || '';
    const fieldErrors = {};
    if (!/^\d{6}$/.test(code)) fieldErrors.code = ['Enter the 6-digit verification code.'];
    if (!newPassword || newPassword.length < 6) fieldErrors.newPassword = ['Password must be at least 6 characters.'];
    if (newPassword !== confirmPassword) fieldErrors.confirmPassword = ['Passwords do not match.'];
    if (Object.keys(fieldErrors).length) {
      showFieldErrors(form, fieldErrors);
      showFormAlert(form, 'Please fix the highlighted fields.');
      return;
    }

    const button = form.querySelector('button[type="submit"]');
    button.disabled = true;
    const oldText = button.textContent;
    button.textContent = 'Updating…';

    try {
      await api('api/auth/reset-password', {
        method: 'POST',
        body: JSON.stringify({ login, code, newPassword })
      });
      showToast('Password updated. You can sign in now.', 'success');
      setMode('login');
    } catch (error) {
      if (error instanceof ApiError && Object.keys(error.fieldErrors).length) {
        showFieldErrors(form, error.fieldErrors);
        showFormAlert(form, error.message);
      } else {
        showFormAlert(form, error.message || 'Could not reset password.');
      }
    } finally {
      button.disabled = false;
      button.textContent = oldText;
    }
  }

  function bindForm(formId, handler) {
    const form = $(formId);
    if (!form) return;
    form.addEventListener('submit', (e) => {
      e.preventDefault();
      handler(form);
    });
  }

  function init() {
    const url = new URL(window.location.href);
    ['password', 'Password', 'confirmPassword', 'token'].forEach(k => url.searchParams.delete(k));
    history.replaceState(null, '', url.pathname + (url.search || ''));

    auth.clear();
    const initialMode = document.body?.dataset?.initialMode === 'register' ? 'register' : 'login';
    setMode(initialMode);

    bindForm('login-form', submitLogin);
    $('login-form')?.querySelectorAll('.form-control').forEach(input => {
      input.addEventListener('input', () => {
        $('login-form-alert')?.classList.add('d-none');
      });
    });
    bindForm('register-form', submitRegister);
    bindForm('verify-email-form', submitVerify);
    bindForm('forgot-form', submitForgot);
    bindForm('forgot-reset-form', submitForgotReset);

    document.querySelectorAll('.auth-tab').forEach(tab => {
      tab.addEventListener('click', () => setMode(tab.dataset.mode === 'register' ? 'register' : 'login'));
    });
    $('show-forgot')?.addEventListener('click', () => setMode('forgot'));
    $('back-to-login')?.addEventListener('click', () => setMode('login'));
    $('back-from-verify')?.addEventListener('click', () => setMode('login'));
    $('back-from-forgot-reset')?.addEventListener('click', () => setMode('forgot'));

    $('resend-verification')?.addEventListener('click', async () => {
      if (!pendingVerifyEmail) return;
      const btn = $('resend-verification');
      btn.disabled = true;
      try {
        await api('api/auth/resend-verification', {
          method: 'POST',
          body: JSON.stringify({ email: pendingVerifyEmail })
        });
        showToast('New code sent. Check your inbox.', 'success');
      } catch (error) {
        showFormAlert($('verify-email-form'), error.message);
      } finally {
        btn.disabled = false;
      }
    });
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();
