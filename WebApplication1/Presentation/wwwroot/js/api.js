const API_ROOT = window.CONNECTLY_API_ROOT || '';

function isAccessTokenExpired(token) {
  if (!token) return true;
  try {
    const payload = JSON.parse(atob(token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')));
    if (!payload.exp) return false;
    return payload.exp * 1000 <= Date.now();
  } catch {
    return true;
  }
}

export class ApiError extends Error {
  constructor(message, fieldErrors = {}, status = 0) {
    super(message);
    this.name = 'ApiError';
    this.fieldErrors = fieldErrors;
    this.status = status;
  }
}

export const auth = {
  get accessToken() { return localStorage.getItem('accessToken'); },
  get refreshToken() { return localStorage.getItem('refreshToken'); },
  isAuthenticated() {
    const token = this.accessToken;
    if (!token || isAccessTokenExpired(token)) {
      if (token) this.clear();
      return false;
    }
    return true;
  },
  save(session) {
    const accessToken = session.accessToken ?? session.AccessToken;
    const refreshToken = session.refreshToken ?? session.RefreshToken;
    if (!accessToken || !refreshToken) throw new Error('The server did not return authentication tokens.');
    localStorage.setItem('accessToken', accessToken);
    localStorage.setItem('refreshToken', refreshToken);
    localStorage.setItem('connectlyUser', JSON.stringify({
      id: session.userId ?? session.UserId,
      displayName: session.displayName ?? session.DisplayName,
      email: session.email ?? session.Email,
      profilePictureUrl: session.profilePictureUrl ?? session.ProfilePictureUrl
    }));
  },
  clear() { ['accessToken', 'refreshToken', 'connectlyUser'].forEach((key) => localStorage.removeItem(key)); },
  user() { try { return JSON.parse(localStorage.getItem('connectlyUser')) || {}; } catch { return {}; } },
  require() { if (!this.isAuthenticated()) window.location.replace('/Auth/Login'); },
  async ensureMvcSession() {
    if (!this.isAuthenticated()) return false;
    try {
      const response = await fetch(`${API_ROOT}/api/auth/mvc-session`, {
        method: 'POST',
        headers: { Authorization: `Bearer ${this.accessToken}` },
        credentials: 'same-origin'
      });
      if (!response.ok) {
        if (response.status === 401) this.clear();
        return false;
      }
      return true;
    } catch {
      return false;
    }
  },
  async signOutMvcSession() {
    try { await fetch(`${API_ROOT}/api/auth/mvc-signout`, { method: 'POST', credentials: 'same-origin' }); } catch { /* local tokens are still cleared */ }
  }
};

export function showToast(message, type = 'info') {
  let host = document.getElementById('toast-host');
  if (!host) {
    host = document.createElement('div');
    host.id = 'toast-host';
    host.className = 'toast-host';
    document.body.append(host);
  }
  const toast = document.createElement('div');
  toast.className = `toast ${type === 'success' || type === 'error' ? type : ''}`;
  toast.textContent = message || 'Something went wrong.';
  host.append(toast);
  setTimeout(() => toast.remove(), 5000);
}

async function parseBody(response) {
  const text = await response.text();
  if (!text) return null;
  try { return JSON.parse(text); } catch { return { message: text }; }
}

function normalizeFieldErrors(body) {
  const raw = body?.fieldErrors ?? body?.FieldErrors;
  if (!raw || typeof raw !== 'object' || Array.isArray(raw)) return {};

  return Object.fromEntries(
    Object.entries(raw).map(([key, value]) => [
      key.charAt(0).toLowerCase() + key.slice(1),
      Array.isArray(value) ? value : [String(value)]
    ])
  );
}

function errorMessage(body, fallback) {
  if (!body) return fallback;
  const message = body.message ?? body.Message ?? body.title ?? body.Title;
  const errors = body.errors ?? body.Errors;

  if (typeof errors === 'object' && !Array.isArray(errors)) {
    const first = Object.values(errors).flat()[0];
    if (first) return String(first);
  }

  if (Array.isArray(errors) && errors.length) {
    return message ? `${message} ${errors.join(' ')}` : errors.join(' ');
  }

  return message || fallback;
}

export async function api(path, options = {}) {
  const headers = new Headers(options.headers || {});
  const token = auth.accessToken;
  if (token) headers.set('Authorization', `Bearer ${token}`);
  if (options.body && !headers.has('Content-Type') && !(options.body instanceof FormData)) headers.set('Content-Type', 'application/json');

  let response;
  try {
    response = await fetch(`${API_ROOT}/${path.replace(/^\//, '')}`, {
      credentials: 'same-origin',
      ...options,
      headers
    });
  } catch {
    throw new ApiError('Unable to reach Connectly. Check your connection and try again.');
  }

  const body = await parseBody(response);
  if (!response.ok || body?.success === false || body?.Success === false) {
    const fieldErrors = normalizeFieldErrors(body);
    const message = errorMessage(body, `Request failed (${response.status}).`);

    if (response.status === 401 && !path.startsWith('api/auth/') && !window.location.pathname.toLowerCase().startsWith('/auth/')) {
      auth.clear();
      window.location.replace('/Auth/Login');
    }

    throw new ApiError(message, fieldErrors, response.status);
  }

  return body?.data ?? body?.Data ?? body;
}

export function escapeHtml(value = '') {
  const node = document.createElement('div'); node.textContent = String(value); return node.innerHTML;
}

export function formatDate(value) {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '';
  const seconds = Math.round((Date.now() - date.getTime()) / 1000);
  if (seconds < 60) return 'just now'; if (seconds < 3600) return `${Math.floor(seconds / 60)}m ago`;
  if (seconds < 86400) return `${Math.floor(seconds / 3600)}h ago`;
  return new Intl.DateTimeFormat(undefined, { month: 'short', day: 'numeric', year: date.getFullYear() === new Date().getFullYear() ? undefined : 'numeric' }).format(date);
}
