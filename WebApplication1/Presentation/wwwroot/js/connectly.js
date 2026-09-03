window.Connectly = (() => {

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

  const AUTH_LOGIN_PATH = '/Auth/Login';

  function isAuthPage() {
    const path = window.location.pathname.toLowerCase();
    return path.startsWith('/auth/');
  }

  const auth = {

    get accessToken() { return localStorage.getItem('accessToken'); },

    isAuthenticated() {
      const token = this.accessToken;
      if (!token || isAccessTokenExpired(token)) {
        if (token) this.clear();
        return false;
      }
      return true;
    },

    user() {

      try { return JSON.parse(localStorage.getItem('connectlyUser')) || {}; }

      catch { return {}; }

    },

    clear() { ['accessToken', 'refreshToken', 'connectlyUser'].forEach(k => localStorage.removeItem(k)); },

    async ensureMvcSession() {

      if (!this.isAuthenticated()) return false;

      try {

        const r = await fetch('/api/auth/mvc-session', {

          method: 'POST',

          headers: { Authorization: `Bearer ${this.accessToken}` },

          credentials: 'same-origin'

        });

        if (!r.ok) {
          if (r.status === 401) this.clear();
          return false;
        }

        return true;

      } catch { return false; }

    },

    async signOut() {

      try { await fetch('/api/auth/mvc-signout', { method: 'POST', credentials: 'same-origin' }); } catch { /* ignore */ }

      this.clear();

      window.location.replace(AUTH_LOGIN_PATH);

    },

    require() {

      if (!this.isAuthenticated()) window.location.replace(AUTH_LOGIN_PATH);

    }

  };



  function escapeHtml(value = '') {

    const node = document.createElement('div');

    node.textContent = String(value);

    return node.innerHTML;

  }



  function showToast(message, type = 'info') {

    let host = document.getElementById('toast-host');

    if (!host) {

      host = document.createElement('div');

      host.id = 'toast-host';

      host.className = 'toast-host';

      document.body.append(host);

    }

    const toast = document.createElement('div');

    toast.className = `toast ${type}`;

    toast.textContent = message || 'Something went wrong.';

    host.append(toast);

    setTimeout(() => toast.remove(), 4500);

  }



  async function api(path, options = {}) {

    const headers = new Headers(options.headers || {});

    if (auth.accessToken) headers.set('Authorization', `Bearer ${auth.accessToken}`);

    if (options.body && !headers.has('Content-Type') && !(options.body instanceof FormData))

      headers.set('Content-Type', 'application/json');

    const response = await fetch(`/${path.replace(/^\//, '')}`, { credentials: 'same-origin', ...options, headers });

    const text = await response.text();

    let body = null;

    if (text) { try { body = JSON.parse(text); } catch { body = { message: text }; } }

    if (!response.ok || body?.success === false || body?.Success === false) {

      const detail = Array.isArray(body?.errors) && body.errors.length
        ? body.errors.join(' ')
        : (Array.isArray(body?.Errors) && body.Errors.length ? body.Errors.join(' ') : '');

      const msg = body?.message || body?.Message || body?.title || body?.detail
        || body?.Title || body?.Detail || detail || `Request failed (${response.status}).`;

      if (response.status === 401 && !path.includes('api/auth/') && !isAuthPage()) {

        auth.clear();

        window.location.replace(AUTH_LOGIN_PATH);

      }

      throw new Error(msg);

    }

    return body?.data ?? body?.Data ?? body;

  }



  function formatRelativeTime(dateStr) {

    const d = new Date(dateStr);

    const diffSec = Math.floor((Date.now() - d.getTime()) / 1000);

    if (diffSec < 60) return 'Just now';

    if (diffSec < 3600) return `${Math.floor(diffSec / 60)}m`;

    if (diffSec < 86400) return `${Math.floor(diffSec / 3600)}h`;

    if (diffSec < 604800) return `${Math.floor(diffSec / 86400)}d`;

    return d.toLocaleDateString(undefined, { month: 'short', day: 'numeric' });

  }



  function renderCommentAvatar(name, photoUrl) {

    if (photoUrl) return `<img src="${escapeHtml(photoUrl)}" alt="">`;

    return escapeHtml((name || 'U').charAt(0).toUpperCase());

  }



  function renderComment(c, isReply = false) {

    const item = document.createElement('div');

    item.className = isReply ? 'comment-item comment-reply' : 'comment-item';

    item.dataset.commentId = c.id;

    item.innerHTML = `

      <a href="/Profile?userId=${encodeURIComponent(c.userId)}" class="avatar avatar-sm">${renderCommentAvatar(c.authorName, c.authorProfilePictureUrl)}</a>

      <div class="comment-body">

        <div class="comment-bubble">

          <a href="/Profile?userId=${encodeURIComponent(c.userId)}" class="comment-author">${escapeHtml(c.authorName)}</a>

          <span class="comment-text">${escapeHtml(c.content)}</span>

        </div>

        <div class="comment-meta">
          ${formatRelativeTime(c.createdAt)}
          <button type="button" class="comment-reply-btn">Reply</button>
          ${c.canDelete ? '<button type="button" class="comment-delete">Delete</button>' : ''}
        </div>

        <div class="comment-reply-form-wrap d-none">
          <form class="comment-reply-form" data-post-id="${c.postId}" data-parent-id="${c.id}">
            <input class="form-control comment-input comment-reply-input" name="content" maxlength="1000" required placeholder="Write a reply…" autocomplete="off" />
            <div class="comment-reply-actions">
              <button type="submit" class="btn btn-primary btn-sm">Reply</button>
              <button type="button" class="btn btn-light btn-sm comment-reply-cancel">Cancel</button>
            </div>
          </form>
        </div>

        <div class="comment-replies"></div>

      </div>`;

    const repliesEl = item.querySelector('.comment-replies');

    (c.replies || []).forEach(r => repliesEl.append(renderComment(r, true)));

    return item;

  }



  const MAX_VISIBLE_COMMENTS = 4;



  function renderCommentsList(listEl, comments, expanded = false) {

    listEl.innerHTML = '';

    if (!comments?.length) {

      listEl.innerHTML = '<div class="comments-empty">No comments yet. Be the first to comment.</div>';

      listEl.dataset.commentsExpanded = 'true';

      return;

    }

    const visible = expanded ? comments : comments.slice(0, MAX_VISIBLE_COMMENTS);

    const hiddenCount = comments.length - MAX_VISIBLE_COMMENTS;

    visible.forEach(c => listEl.append(renderComment(c)));

    if (!expanded && hiddenCount > 0) {

      const moreBtn = document.createElement('button');

      moreBtn.type = 'button';

      moreBtn.className = 'comments-view-more';

      moreBtn.textContent = `View ${hiddenCount} more comment${hiddenCount === 1 ? '' : 's'}`;

      listEl.append(moreBtn);

    }

    listEl.dataset.commentsExpanded = expanded ? 'true' : 'false';

  }



  function expandCommentsList(listEl) {

    if (!listEl?._allComments?.length) return;

    renderCommentsList(listEl, listEl._allComments, true);

  }



  function countCommentNodes(item) {

    return item ? item.querySelectorAll('.comment-item').length : 0;

  }



  function appendReplyToTree(comments, parentId, reply) {

    for (const comment of comments) {

      if (comment.id === parentId) {

        comment.replies = comment.replies || [];

        comment.replies.push(reply);

        return true;

      }

      if (comment.replies?.length && appendReplyToTree(comment.replies, parentId, reply)) return true;

    }

    return false;

  }



  function removeCommentFromTree(comments, commentId) {

    for (let i = 0; i < comments.length; i += 1) {

      if (comments[i].id === commentId) {

        comments.splice(i, 1);

        return true;

      }

      if (comments[i].replies?.length && removeCommentFromTree(comments[i].replies, commentId)) return true;

    }

    return false;

  }



  function countCommentsInTree(comments, commentId) {

    for (const comment of comments) {

      if (comment.id === commentId) {

        return 1 + countAllReplies(comment.replies);

      }

      if (comment.replies?.length) {

        const nested = countCommentsInTree(comment.replies, commentId);

        if (nested) return nested;

      }

    }

    return 0;

  }



  function countAllReplies(replies) {

    if (!replies?.length) return 0;

    return replies.reduce((total, reply) => total + 1 + countAllReplies(reply.replies), 0);

  }



  function bumpCommentCount(postId, delta) {

    const countEl = document.querySelector(`#post-${postId} [data-comment-count]`);

    if (countEl) countEl.textContent = String(Math.max(0, Number(countEl.textContent) + delta));

  }



  function updatePostLikeDisplay(postId, liked, delta = null) {

    const card = document.getElementById(`post-${postId}`);

    if (!card) return;

    let count = Number(card.dataset.likesCount || 0);

    if (delta != null) count = Math.max(0, count + delta);

    card.dataset.likesCount = String(count);

    const likeBtn = card.querySelector('.post-like');

    if (likeBtn) {

      likeBtn.classList.toggle('liked', liked);

      likeBtn.dataset.liked = String(liked);

      const icon = likeBtn.querySelector('i');

      if (icon) {

        icon.classList.toggle('fa-solid', liked);

        icon.classList.toggle('fa-regular', !liked);

      }

      const countEl = likeBtn.querySelector('[data-likes-count]');

      if (countEl) {
        countEl.textContent = String(count);
        countEl.classList.toggle('d-none', count <= 0);
      }

      const trigger = card.querySelector('.post-likes-trigger');

      if (trigger && card.dataset.isOwner === 'true') trigger.classList.toggle('d-none', count <= 0);

    }

  }



  async function syncCurrentUser() {

    if (!auth.isAuthenticated()) return;

    try {

      const profile = await api('api/auth/me');

      const stored = auth.user();

      const displayName = profile.displayName ?? profile.DisplayName;

      const profilePictureUrl = profile.profilePictureUrl ?? profile.ProfilePictureUrl;

      if (displayName) stored.displayName = displayName;

      if (profilePictureUrl !== undefined) stored.profilePictureUrl = profilePictureUrl;

      localStorage.setItem('connectlyUser', JSON.stringify(stored));

    } catch { /* sidebar falls back to cached user */ }

  }



  function initSidebarUser() {

    const user = auth.user();

    const nameEl = document.getElementById('sidebar-user-name');

    const avatarEl = document.getElementById('sidebar-user-avatar');

    if (nameEl) nameEl.textContent = user.displayName || 'Your profile';

    if (avatarEl) {

      const pic = user.profilePictureUrl;

      avatarEl.innerHTML = pic ? `<img src="${escapeHtml(pic)}" alt="">` : (user.displayName || 'Y').charAt(0).toUpperCase();

    }

    document.querySelectorAll('.comment-form-avatar').forEach(el => {

      el.innerHTML = user.profilePictureUrl

        ? `<img src="${escapeHtml(user.profilePictureUrl)}" alt="">`

        : (user.displayName || 'Y').charAt(0).toUpperCase();

    });

  }



  function getNotificationHref(n) {
    const type = n.type ?? n.Type;
    const url = (n.targetUrl ?? n.TargetUrl ?? '').trim();
    const typeName = typeof type === 'string' ? type : null;
    const typeNum = typeName
      ? ({ FriendRequest: 1, PostInteraction: 3, MessageRequest: 5, BirthdayReminder: 4, NewMessage: 6 }[typeName] ?? Number(type))
      : type;
    if (typeNum === 1 || /friend request/i.test(n.message || '')) return '/Friendships/Pending';
    if (typeNum === 5 || /message request/i.test(n.message || '')) {
      const userId = n.triggeredById ?? n.TriggeredById;
      if (userId) return `/Chat?id=${encodeURIComponent(userId)}`;
    }
    if (typeNum === 6 || /sent you a message|shared a post with you/i.test(n.message || '')) {
      const userId = n.triggeredById ?? n.TriggeredById;
      if (userId) return `/Chat?id=${encodeURIComponent(userId)}`;
    }
    if (typeNum === 4 || /birthday/i.test(n.message || '')) {
      const userId = n.triggeredById ?? n.TriggeredById;
      if (userId) return `/Profile?userId=${encodeURIComponent(userId)}`;
    }
    if (url.includes('#post-')) return url.startsWith('/') ? url : `/${url}`;
    if (url && url !== '#') {
      const normalized = url.startsWith('/') ? url : `/${url}`;
      const profileMatch = normalized.match(/^\/profile\/([^/?#]+)/i);
      if (profileMatch?.[1]) return `/Profile?userId=${encodeURIComponent(profileMatch[1])}`;
      return normalized;
    }
    return '#';
  }

  function scrollToPostFromHash() {
    const hash = window.location.hash;
    if (!hash?.startsWith('#post-')) return;
    const el = document.querySelector(hash);
    if (!el) return;
    setTimeout(() => {
      el.scrollIntoView({ behavior: 'smooth', block: 'center' });
      el.classList.add('post-highlight');
      setTimeout(() => el.classList.remove('post-highlight'), 2500);
    }, 150);
  }

  async function openPostLikesModal(postId) {
    const modal = document.getElementById('post-likes-modal');
    const list = document.getElementById('post-likes-list');
    if (!modal || !list) return;
    list.innerHTML = '<div class="likes-user-row text-muted" style="justify-content:center">Loading…</div>';
    modal.classList.add('open');
    try {
      const likes = await api(`api/posts/${postId}/likes`);
      list.innerHTML = '';
      if (!likes?.length) {
        list.innerHTML = '<div class="likes-user-row text-muted" style="justify-content:center">No likes yet.</div>';
        return;
      }
      likes.forEach(u => {
        const row = document.createElement('a');
        row.href = `/Profile?userId=${encodeURIComponent(u.userId)}`;
        row.className = 'likes-user-row';
        const avatar = u.profilePictureUrl
          ? `<img src="${escapeHtml(u.profilePictureUrl)}" alt="">`
          : escapeHtml((u.displayName || 'U').charAt(0).toUpperCase());
        row.innerHTML = `<span class="avatar avatar-sm">${avatar}</span><strong>${escapeHtml(u.displayName)}</strong>`;
        list.append(row);
      });
    } catch (err) {
      list.innerHTML = `<div class="likes-user-row text-danger" style="justify-content:center">${escapeHtml(err.message)}</div>`;
    }
  }

  function sortNotificationsNewestFirst(notifications = []) {
    return [...notifications].sort((a, b) => {
      const aTime = new Date(a.createdAt ?? a.CreatedAt ?? 0).getTime();
      const bTime = new Date(b.createdAt ?? b.CreatedAt ?? 0).getTime();
      return bTime - aTime;
    });
  }

  function getNotificationIcon(message = '', type = null) {
    const typeName = typeof type === 'string' ? type : null;
    const typeNum = typeName
      ? ({ FriendRequest: 1, PostInteraction: 3, MessageRequest: 5, BirthdayReminder: 4, NewMessage: 6 }[typeName] ?? Number(type))
      : type;
    if (typeNum === 6 || /sent you a message|shared a post with you/i.test(message)) return 'fa-solid fa-comment-dots';
    if (typeNum === 5 || /message request/i.test(message)) return 'fa-solid fa-envelope';
    if (typeNum === 4 || /birthday/i.test(message)) return 'fa-solid fa-cake-candles';
    if (/liked your post|liked a post you liked/i.test(message)) return 'fa-solid fa-heart';
    if (/commented on your post|commented on a post you liked/i.test(message)) return 'fa-regular fa-comment';
    if (/replied to your comment|replied on a post you liked|replied to a comment on your post/i.test(message)) return 'fa-solid fa-reply';
    if (/shared your post|shared a post/i.test(message)) return 'fa-solid fa-share';
    if (/posted an update/i.test(message)) return 'fa-regular fa-newspaper';
    if (typeNum === 1 || /friend request/i.test(message)) return 'fa-solid fa-user-plus';
    return 'fa-regular fa-bell';
  }

  function buildNotificationElement(n, { showTime = false } = {}) {
    const item = document.createElement('a');
    const href = getNotificationHref(n);
    const id = n.id ?? n.Id;
    const isRead = n.isRead ?? n.IsRead;
    const message = n.message ?? n.Message ?? '';
    const createdAt = n.createdAt ?? n.CreatedAt;
    item.href = href;
    item.className = `notif-item${isRead ? '' : ' unread'}`;
    if (id) item.dataset.notificationId = String(id);
    const photoUrl = n.triggeredByProfilePictureUrl ?? n.TriggeredByProfilePictureUrl;
    const actorName = n.triggeredByName ?? n.TriggeredByName ?? 'C';
    const avatar = photoUrl
      ? `<img src="${escapeHtml(photoUrl)}" alt="">`
      : escapeHtml(actorName.charAt(0));
    const icon = getNotificationIcon(message, n.type ?? n.Type);
    const timeHtml = showTime && createdAt
      ? `<span class="notif-time">${escapeHtml(formatRelativeTime(createdAt))}</span>`
      : '';
    item.innerHTML = `<span class="avatar avatar-sm">${avatar}</span><span class="notif-content"><span class="notif-message"><i class="${icon}"></i> ${escapeHtml(message)}</span>${timeHtml}</span>`;
    item.addEventListener('click', async e => {
      e.preventDefault();

      const notificationId = item.dataset.notificationId;
      if (notificationId && item.classList.contains('unread')) {
        try {
          await api(`api/notifications/${notificationId}/read`, { method: 'PUT' });
          item.classList.remove('unread');
          updateUnreadBadge(-1);
        } catch { /* still navigate */ }
      }

      document.getElementById('notification-menu')?.classList.remove('open');

      if (href && href !== '#') window.location.href = href;
    });
    return item;
  }

  function getNotificationLists() {
    return [
      document.getElementById('notifications-page-list'),
      document.getElementById('notification-list')
    ].filter(Boolean);
  }

  function renderNotificationItem(n, { prepend = true, targetList = null, showTime = false } = {}) {
    const lists = targetList ? [targetList] : getNotificationLists();
    if (!lists.length) return;

    const id = n.id ?? n.Id;
    lists.forEach(list => {
      if (id) list.querySelector(`[data-notification-id="${id}"]`)?.remove();
      list.querySelector('.notif-empty')?.remove();
      const item = buildNotificationElement(n, { showTime: showTime || list.id === 'notifications-page-list' });
      if (prepend) list.prepend(item);
      else list.append(item);
    });
  }

  let unreadNotificationCount = 0;

  function updateUnreadBadge(delta = null) {
    const badges = [
      document.getElementById('notification-badge'),
      document.getElementById('mobile-notification-badge')
    ].filter(Boolean);
    if (!badges.length) return;
    if (delta != null) unreadNotificationCount = Math.max(0, unreadNotificationCount + delta);
    const label = unreadNotificationCount > 99 ? '99+' : String(unreadNotificationCount);
    const show = unreadNotificationCount > 0;
    badges.forEach(badge => {
      badge.textContent = label;
      badge.classList.toggle('d-none', !show);
    });
  }

  function setUnreadCount(count) {
    unreadNotificationCount = Math.max(0, Number(count) || 0);
    updateUnreadBadge();
  }

  async function markAllNotificationsRead() {
    await api('api/notifications/read-all', { method: 'PUT' });
    setUnreadCount(0);
    document.querySelectorAll('.notif-item.unread').forEach(x => x.classList.remove('unread'));
  }

  function initNotifications() {
    const token = auth.accessToken;
    const menu = document.getElementById('notification-menu');
    const toggle = document.getElementById('notification-toggle');

    const closeMenu = () => {
      menu?.classList.remove('open');
      toggle?.setAttribute('aria-expanded', 'false');
    };

    toggle?.addEventListener('click', e => {
      e.preventDefault();
      e.stopPropagation();
      const isOpen = menu?.classList.toggle('open');
      toggle.setAttribute('aria-expanded', isOpen ? 'true' : 'false');
    });

    menu?.addEventListener('click', e => e.stopPropagation());

    document.addEventListener('click', e => {
      if (!menu?.contains(e.target) && !toggle?.contains(e.target)) closeMenu();
    });

    document.addEventListener('keydown', e => {
      if (e.key === 'Escape') closeMenu();
    });

    if (!token) return;

    const loadLists = notifications => {
      const sorted = sortNotificationsNewestFirst(Array.isArray(notifications) ? notifications : []);
      const pageList = document.getElementById('notifications-page-list');
      const menuList = document.getElementById('notification-list');

      if (pageList) {
        pageList.innerHTML = '';
        if (!sorted.length) {
          pageList.innerHTML = '<div class="notif-item notif-empty text-muted" style="justify-content:center">No notifications yet.</div>';
        } else {
          sorted.forEach(n => renderNotificationItem(n, { prepend: false, targetList: pageList, showTime: true }));
        }
      }

      if (menuList) {
        menuList.innerHTML = '';
        const recent = sorted.slice(0, 8);
        if (!recent.length) {
          menuList.innerHTML = '<div class="notif-item notif-empty text-muted" style="justify-content:center">No new notifications.</div>';
        } else {
          recent.forEach(n => renderNotificationItem(n, { prepend: false, targetList: menuList }));
        }
      }

      setUnreadCount(sorted.filter(n => !(n.isRead ?? n.IsRead)).length);
    };

    api('api/notifications').then(loadLists).catch(err => {
      const pageList = document.getElementById('notifications-page-list');
      if (pageList) pageList.innerHTML = `<div class="notif-item text-danger" style="justify-content:center">${escapeHtml(err.message)}</div>`;
      console.error(err);
    });

    document.getElementById('notifications-page-mark-read')?.addEventListener('click', async () => {
      try {
        await markAllNotificationsRead();
        showToast('All notifications marked as read.', 'success');
      } catch (err) { showToast(err.message, 'error'); }
    });

    document.getElementById('mark-notifications-read')?.addEventListener('click', async e => {
      e.preventDefault();
      e.stopPropagation();
      try {
        await markAllNotificationsRead();
        showToast('All notifications marked as read.', 'success');
      } catch (err) { showToast(err.message, 'error'); }
    });

    if (!window.signalR) return;

    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/notificationHub', {
        accessTokenFactory: () => auth.accessToken || ''
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    connection.on('ReceiveNotification', n => {
      renderNotificationItem(n, { prepend: true, showTime: Boolean(document.getElementById('notifications-page-list')) });
      showToast(n.message ?? n.Message ?? 'New notification');
    });

    connection.on('UpdateUnreadCount', setUnreadCount);

    connection.onreconnected(() => api('api/notifications').then(loadLists).catch(console.error));

    connection.start().catch(err => {
      console.error('Notification hub failed:', err);
    });
  }



  async function loadCommentsForPost(postId, listEl, expanded = false) {

    try {

      const comments = await api(`api/posts/${postId}/comments`);

      listEl._allComments = comments || [];

      renderCommentsList(listEl, listEl._allComments, expanded);

    } catch (err) {

      listEl.innerHTML = `<div class="comments-empty text-danger">${escapeHtml(err.message)}</div>`;

    }

  }



  function initPostInteractions() {

    document.querySelectorAll('.post-like').forEach(btn => {

      btn.addEventListener('click', async () => {

        const id = btn.dataset.postId;

        btn.disabled = true;

        try {

          const liked = await api(`api/posts/${id}/like`, { method: 'POST' });

          const wasLiked = btn.dataset.liked === 'true';

          const delta = liked === wasLiked ? 0 : liked ? 1 : -1;

          updatePostLikeDisplay(id, liked, delta);

        } catch (err) { showToast(err.message, 'error'); }

        finally { btn.disabled = false; }

      });

    });



    document.querySelectorAll('.comments-list').forEach(listEl => {

      const postId = listEl.dataset.postId;

      if (postId) loadCommentsForPost(postId, listEl);

      listEl.addEventListener('click', async e => {

        const viewMoreBtn = e.target.closest('.comments-view-more');

        if (viewMoreBtn) {

          expandCommentsList(listEl);

          return;

        }

        const replyBtn = e.target.closest('.comment-reply-btn');

        if (replyBtn) {

          const item = replyBtn.closest('.comment-item');

          const wrap = item?.querySelector('.comment-reply-form-wrap');

          if (!wrap) return;

          listEl.querySelectorAll('.comment-reply-form-wrap').forEach(w => w.classList.add('d-none'));

          wrap.classList.remove('d-none');

          wrap.querySelector('.comment-reply-input')?.focus();

          return;

        }

        const cancelBtn = e.target.closest('.comment-reply-cancel');

        if (cancelBtn) {

          cancelBtn.closest('.comment-reply-form-wrap')?.classList.add('d-none');

          return;

        }

        const btn = e.target.closest('.comment-delete');

        if (!btn) return;

        const item = btn.closest('.comment-item');

        const commentId = item?.dataset.commentId;

        if (!postId || !commentId) return;

        if (!window.confirm('Delete this comment?')) return;

        btn.disabled = true;

        try {

          const removedCount = listEl._allComments

            ? countCommentsInTree(listEl._allComments, Number(commentId))

            : countCommentNodes(item);

          await api(`api/posts/${postId}/comments/${commentId}`, { method: 'DELETE' });

          if (listEl._allComments) {

            removeCommentFromTree(listEl._allComments, Number(commentId));

            renderCommentsList(listEl, listEl._allComments, listEl.dataset.commentsExpanded === 'true');

          } else {

            item.remove();

          }

          bumpCommentCount(postId, -removedCount);

          if (!listEl._allComments?.length) {

            listEl.innerHTML = '<div class="comments-empty">No comments yet. Be the first to comment.</div>';

          }

          showToast('Comment deleted.', 'success');

        } catch (err) {

          showToast(err.message, 'error');

          btn.disabled = false;

        }

      });



      listEl.addEventListener('submit', async e => {

        const form = e.target.closest('.comment-reply-form');

        if (!form) return;

        e.preventDefault();

        const input = form.querySelector('[name="content"]');

        const content = input?.value?.trim();

        if (!content) return;

        const parentId = Number(form.dataset.parentId);

        const submitBtn = form.querySelector('[type="submit"]');

        submitBtn.disabled = true;

        try {

          const reply = await api(`api/posts/${postId}/comments`, {

            method: 'POST',

            body: JSON.stringify({ content, parentCommentId: parentId })

          });

          input.value = '';

          form.closest('.comment-reply-form-wrap')?.classList.add('d-none');

          if (listEl._allComments) {

            appendReplyToTree(listEl._allComments, parentId, reply);

            renderCommentsList(listEl, listEl._allComments, true);

          } else {

            const parentItem = form.closest('.comment-item');

            parentItem?.querySelector('.comment-replies')?.append(renderComment(reply, true));

          }

          bumpCommentCount(postId, 1);

          showToast('Reply posted.', 'success');

        } catch (err) {

          showToast(err.message, 'error');

        } finally {

          submitBtn.disabled = false;

        }

      });

    });



    document.querySelectorAll('.post-comment-toggle').forEach(btn => {

      btn.addEventListener('click', () => {

        const card = btn.closest('.post-card');

        const listEl = card?.querySelector('.comments-list');

        if (listEl && listEl.dataset.commentsExpanded !== 'true') expandCommentsList(listEl);

        const input = card?.querySelector('.comment-input');

        input?.focus();

        card?.querySelector('.comments-section')?.scrollIntoView({ behavior: 'smooth', block: 'nearest' });

      });

    });



    document.querySelectorAll('.comment-form').forEach(form => {

      form.addEventListener('submit', async e => {

        e.preventDefault();

        const postId = form.dataset.postId;

        const input = form.querySelector('[name="content"]');

        const content = input?.value?.trim();

        if (!content) return;

        const submitBtn = form.querySelector('.comment-submit');

        submitBtn.disabled = true;

        try {

          const comment = await api(`api/posts/${postId}/comments`, { method: 'POST', body: JSON.stringify({ content }) });

          input.value = '';

          const listEl = form.closest('.post-card')?.querySelector('.comments-list');

          if (listEl) {

            listEl._allComments = listEl._allComments || [];

            listEl._allComments.push(comment);

            renderCommentsList(listEl, listEl._allComments, true);

            listEl.scrollTop = listEl.scrollHeight;

          }

          bumpCommentCount(postId, 1);

        } catch (err) { showToast(err.message, 'error'); }

        finally { submitBtn.disabled = false; }

      });

    });



    document.querySelectorAll('.post-share').forEach(btn => {

      btn.addEventListener('click', () => openShareModal(btn.dataset.postId, btn.dataset.postAuthor));

    });

    document.querySelectorAll('.post-likes-trigger').forEach(btn => {

      btn.addEventListener('click', e => {

        e.preventDefault();

        e.stopPropagation();

        openPostLikesModal(btn.dataset.postId);

      });

    });

    document.querySelectorAll('.post-delete').forEach(btn => {
      btn.addEventListener('click', async () => {
        const id = btn.dataset.postId;
        if (!id) return;
        if (!window.confirm('Delete this post? This cannot be undone.')) return;

        btn.disabled = true;
        try {
          await api(`api/posts/${id}`, { method: 'DELETE' });
          document.getElementById(`post-${id}`)?.remove();
          showToast('Post deleted.', 'success');
        } catch (err) {
          showToast(err.message, 'error');
          btn.disabled = false;
        }
      });
    });

  }



  let sharePostId = null;

  let friendsLoaded = false;



  async function loadFriendsForShare() {

    const select = document.getElementById('share-friend-select');

    if (!select || friendsLoaded) return;

    const userId = auth.user()?.id;

    if (!userId) return;

    try {

      const friends = await api(`api/friendships/friends/${userId}`);

      select.innerHTML = '<option value="">Choose a friend…</option>';

      (friends || []).forEach(f => {

        const opt = document.createElement('option');

        opt.value = f.userId || f.id;

        opt.textContent = f.displayName || f.name || 'Friend';

        select.append(opt);

      });

      if (!friends?.length) select.innerHTML = '<option value="">No friends yet — add friends first</option>';

      friendsLoaded = true;

    } catch (err) {

      select.innerHTML = `<option value="">${escapeHtml(err.message)}</option>`;

    }

  }



  function openShareModal(postId, authorName) {

    sharePostId = postId;

    const modal = document.getElementById('share-post-modal');

    if (!modal) return;

    document.getElementById('share-caption').value = '';

    document.getElementById('share-chat-message').value = '';

    modal.dataset.postAuthor = authorName || '';

    modal.classList.add('open');

    loadFriendsForShare();

  }



  function initShareModal() {

    const modal = document.getElementById('share-post-modal');

    if (!modal) return;



    modal.addEventListener('click', e => { if (e.target === modal) modal.classList.remove('open'); });



    modal.querySelectorAll('.share-tab').forEach(tab => {

      tab.addEventListener('click', () => {

        modal.querySelectorAll('.share-tab').forEach(t => t.classList.remove('active'));

        tab.classList.add('active');

        const target = tab.dataset.shareTab;

        document.getElementById('share-feed-panel')?.classList.toggle('d-none', target !== 'feed');

        document.getElementById('share-chat-panel')?.classList.toggle('d-none', target !== 'chat');

        if (target === 'chat') loadFriendsForShare();

      });

    });



    document.getElementById('share-to-feed-btn')?.addEventListener('click', async () => {

      if (!sharePostId) return;

      const btn = document.getElementById('share-to-feed-btn');

      btn.disabled = true;

      try {

        const caption = document.getElementById('share-caption')?.value?.trim() || '';

        const privacy = Number(document.getElementById('share-privacy')?.value || 0);

        await api(`api/posts/${sharePostId}/share/feed`, {

          method: 'POST',

          body: JSON.stringify({ caption, privacy })

        });

        modal.classList.remove('open');

        showToast('Post shared to your profile!', 'success');

        setTimeout(() => window.location.reload(), 500);

      } catch (err) { showToast(err.message, 'error'); }

      finally { btn.disabled = false; }

    });



    document.getElementById('share-to-chat-btn')?.addEventListener('click', async () => {

      if (!sharePostId) return;

      const receiverId = document.getElementById('share-friend-select')?.value;

      if (!receiverId) {

        showToast('Choose a friend to send this post to.', 'error');

        return;

      }

      const btn = document.getElementById('share-to-chat-btn');

      btn.disabled = true;

      try {

        const message = document.getElementById('share-chat-message')?.value?.trim() || '';

        await api(`api/posts/${sharePostId}/share/chat`, {

          method: 'POST',

          body: JSON.stringify({ receiverId, message })

        });

        modal.classList.remove('open');

        showToast('Post sent in message!', 'success');

      } catch (err) { showToast(err.message, 'error'); }

      finally { btn.disabled = false; }

    });

  }



  function initCreatePostModal() {

    const overlay = document.getElementById('create-post-modal');

    const form = document.getElementById('create-post-form');

    const mediaInput = document.getElementById('post-media-file');

    const preview = document.getElementById('post-media-preview');

    if (!overlay || !form) return;



    const resetForm = () => {

      form.reset();

      if (preview) {

        preview.innerHTML = '';

        preview.classList.add('d-none');

      }

    };



    document.getElementById('open-create-post')?.addEventListener('click', () => { resetForm(); overlay.classList.add('open'); });

    document.getElementById('sidebar-create-post')?.addEventListener('click', () => { resetForm(); overlay.classList.add('open'); });

    overlay.querySelector('.btn-close')?.addEventListener('click', () => overlay.classList.remove('open'));

    overlay.addEventListener('click', e => { if (e.target === overlay) overlay.classList.remove('open'); });



    mediaInput?.addEventListener('change', () => {

      if (!preview || !mediaInput.files?.length) return;

      const file = mediaInput.files[0];

      preview.innerHTML = '';

      preview.classList.remove('d-none');

      const url = URL.createObjectURL(file);

      if (file.type.startsWith('video/')) {

        preview.innerHTML = `<video src="${url}" controls playsinline style="max-height:220px;width:100%;border-radius:12px"></video>`;

      } else {

        preview.innerHTML = `<img src="${url}" alt="Preview" style="max-height:220px;width:100%;object-fit:cover;border-radius:12px" />`;

      }

    });



    form.addEventListener('submit', async e => {

      e.preventDefault();

      const content = (form.querySelector('[name=content]')?.value || '').toString().trim();

      const privacy = form.querySelector('[name=privacy]')?.value ?? '0';

      const file = mediaInput?.files?.[0] ?? null;

      const hasMedia = Boolean(file);

      if (!content && !hasMedia) {

        showToast('Write something or attach a photo/video.', 'error');

        return;

      }

      if (file) {

        const maxBytes = 1000 * 1024 * 1024;

        if (file.size > maxBytes) {

          showToast('File is too large. Maximum size is 1000 MB.', 'error');

          return;

        }

        const name = file.name.toLowerCase();

        const allowedExt = ['.jpg', '.jpeg', '.png', '.gif', '.webp', '.mp4', '.webm', '.mov', '.m4v', '.3gp'];

        const ext = name.includes('.') ? name.slice(name.lastIndexOf('.')) : '';

        const isVideoType = file.type.startsWith('video/');

        const isImageType = file.type.startsWith('image/');

        const extOk = allowedExt.includes(ext);

        if (!isVideoType && !isImageType && !extOk) {

          showToast('Use JPG, PNG, GIF, WEBP, MP4, WEBM, or MOV.', 'error');

          return;

        }

      }

      const fd = new FormData();

      fd.append('content', content);

      fd.append('privacy', privacy);

      if (file) fd.append('media', file, file.name);

      const btn = form.querySelector('[type="submit"]');

      btn.disabled = true;

      try {

        await api('api/posts/upload', { method: 'POST', body: fd });

        overlay.classList.remove('open');

        resetForm();

        showToast('Post published!', 'success');

        setTimeout(() => window.location.reload(), 400);

      } catch (err) { showToast(err.message, 'error'); }

      finally { btn.disabled = false; }

    });

  }



  function initMobileShell() {
    const sidebar = document.querySelector('.sidebar');
    const backdrop = document.getElementById('sidebar-backdrop');

    const closeDrawer = () => {
      sidebar?.classList.remove('open');
      backdrop?.classList.remove('open');
      document.body.classList.remove('drawer-open');
    };

    const openDrawer = () => {
      sidebar?.classList.add('open');
      backdrop?.classList.add('open');
      document.body.classList.add('drawer-open');
    };

    document.getElementById('mobile-menu-btn')?.addEventListener('click', () => {
      if (sidebar?.classList.contains('open')) closeDrawer();
      else openDrawer();
    });

    backdrop?.addEventListener('click', closeDrawer);

    sidebar?.querySelectorAll('.nav-link, .sidebar-brand, .sidebar-user, .btn-create-post, #logout-btn').forEach(el => {
      el.addEventListener('click', closeDrawer);
    });

    document.getElementById('mobile-header-create-post')?.addEventListener('click', () => {
      document.getElementById('sidebar-create-post')?.click();
    });

    window.addEventListener('resize', () => {
      if (window.innerWidth >= 992) closeDrawer();
    });
  }



  async function init() {

    if (!isAuthPage()) {

      if (!auth.isAuthenticated()) {
        window.location.replace(AUTH_LOGIN_PATH);
        return;
      }

      const sessionReady = await auth.ensureMvcSession();
      if (!sessionReady) {
        auth.clear();
        window.location.replace(AUTH_LOGIN_PATH);
        return;
      }

      await syncCurrentUser();

    }

    initSidebarUser();

    initMobileShell();

    initNotifications();

    initPostInteractions();

    initShareModal();

    initCreatePostModal();
    scrollToPostFromHash();
    document.getElementById('logout-btn')?.addEventListener('click', () => auth.signOut());

  }



  return { auth, api, showToast, escapeHtml, init };

})();



document.addEventListener('DOMContentLoaded', () => Connectly.init());

