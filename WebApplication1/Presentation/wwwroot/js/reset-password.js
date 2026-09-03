(function () {
  'use strict';

  const form = document.getElementById('reset-form');
  if (!form) return;

  function showToast(message, type = 'info') {
    const host = document.getElementById('toast-host');
    if (!host) return;
    const toast = document.createElement('div');
    toast.className = `toast ${type === 'success' || type === 'error' ? type : ''}`;
    toast.textContent = message;
    host.append(toast);
    setTimeout(() => toast.remove(), 5000);
  }

  function showFormAlert(message) {
    const alert = document.getElementById('reset-form-alert');
    if (!alert) return;
    alert.textContent = message;
    alert.classList.remove('d-none');
  }

  function clearFormErrors() {
    form.querySelectorAll('.field-error').forEach(el => { el.textContent = ''; el.classList.add('d-none'); });
    form.querySelectorAll('.form-control.is-invalid').forEach(el => el.classList.remove('is-invalid'));
    document.getElementById('reset-form-alert')?.classList.add('d-none');
  }

  function showFieldErrors(fieldErrors) {
    Object.entries(fieldErrors || {}).forEach(([field, messages]) => {
      const text = Array.isArray(messages) ? messages.join(' ') : String(messages);
      const errorEl = form.querySelector(`[data-error-for="${field}"]`);
      const input = form.querySelector(`[name="${field}"]`);
      if (errorEl) { errorEl.textContent = text; errorEl.classList.remove('d-none'); }
      if (input) input.classList.add('is-invalid');
    });
  }

  form.addEventListener('submit', async e => {
    e.preventDefault();
    clearFormErrors();

    const fd = new FormData(form);
    const login = String(fd.get('login') || '').trim();
    const code = String(fd.get('code') || '').trim();
    const newPassword = String(fd.get('newPassword') || '');
    const confirmPassword = String(fd.get('confirmPassword') || '');

    const fieldErrors = {};
    if (!login) fieldErrors.login = ['Username or email is required.'];
    if (!/^\d{6}$/.test(code)) fieldErrors.code = ['Enter the 6-digit verification code.'];
    if (!newPassword || newPassword.length < 6) fieldErrors.newPassword = ['Password must be at least 6 characters.'];
    if (newPassword !== confirmPassword) fieldErrors.confirmPassword = ['Passwords do not match.'];
    if (Object.keys(fieldErrors).length) {
      showFieldErrors(fieldErrors);
      showFormAlert('Please fix the highlighted fields.');
      return;
    }

    const button = form.querySelector('button[type="submit"]');
    button.disabled = true;
    const oldText = button.textContent;
    button.textContent = 'Updating…';

    try {
      const response = await fetch('/api/auth/reset-password', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ login, code, newPassword })
      });
      const body = await response.json().catch(() => ({}));
      if (!response.ok || body.success === false) {
        const fieldErrors = body.fieldErrors || {};
        if (Object.keys(fieldErrors).length) {
          showFieldErrors(fieldErrors);
          showFormAlert(body.message || 'Could not reset password.');
        } else {
          showFormAlert(body.message || 'Could not reset password.');
        }
        return;
      }

      showToast('Password updated! Redirecting to sign in…', 'success');
      setTimeout(() => window.location.replace('/Auth/Login'), 1200);
    } catch {
      showFormAlert('Could not reach the server. Try again.');
    } finally {
      button.disabled = false;
      button.textContent = oldText;
    }
  });
})();
