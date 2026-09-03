(() => {
  const input = document.getElementById('explore-search-input');
  const resultsEl = document.getElementById('search-results');
  const form = document.getElementById('explore-search-form');
  if (!input || !resultsEl) return;

  let debounceTimer = null;
  let activeController = null;
  let requestSeq = 0;

  function pick(obj, ...keys) {
    for (const key of keys) {
      if (obj?.[key] != null) return obj[key];
    }
    return null;
  }

  function escapeHtml(value = '') {
    const node = document.createElement('div');
    node.textContent = String(value);
    return node.innerHTML;
  }

  function renderAvatar(name, photoUrl) {
    if (photoUrl) {
      return `<img src="${escapeHtml(photoUrl)}" alt="" />`;
    }
    return escapeHtml((name || 'U').charAt(0).toUpperCase());
  }

  function actionButton(status, userId) {
    if (status === 'PendingReceived') {
      return `<a href="/Friendships/Pending" class="btn btn-light btn-sm">Respond</a>`;
    }

    let btnClass = 'btn btn-primary btn-sm add-friend-btn';
    let btnText = 'Add Friend';
    let disabled = false;

    if (status === 'Friends') {
      btnText = 'Friends';
      disabled = true;
      btnClass = 'btn btn-light btn-sm';
    } else if (status === 'PendingSent') {
      btnText = 'Request sent';
      disabled = true;
      btnClass = 'btn btn-light btn-sm';
    } else if (status === 'Blocked') {
      btnText = 'Unavailable';
      disabled = true;
      btnClass = 'btn btn-light btn-sm';
    }

    const disabledAttr = disabled ? ' disabled' : '';
    return `<button type="button" class="${btnClass}" data-user-id="${escapeHtml(userId)}"${disabledAttr}>${btnText}</button>`;
  }

  function renderUserCard(user) {
    const id = pick(user, 'id', 'Id');
    const displayName = pick(user, 'displayName', 'DisplayName') || 'User';
    const username = pick(user, 'username', 'Username') || displayName;
    const bio = pick(user, 'bio', 'Bio');
    const photo = pick(user, 'profilePictureUrl', 'ProfilePictureUrl');
    const status = pick(user, 'friendshipStatus', 'FriendshipStatus') || 'None';
    const bioText = bio?.trim() ? escapeHtml(bio) : 'Connectly member';

    return `
      <div class="user-discover-card" data-user-id="${escapeHtml(id)}">
        <a href="/Profile?userId=${encodeURIComponent(id)}" class="user-discover-main">
          <span class="avatar avatar-lg">${renderAvatar(displayName, photo)}</span>
          <span class="user-discover-info">
            <strong>${escapeHtml(displayName)}</strong>
            <span class="text-muted" style="font-size:0.82rem">${escapeHtml(username)}</span>
            <span class="text-muted">${bioText}</span>
          </span>
        </a>
        ${actionButton(status, id)}
      </div>`;
  }

  function renderLoading() {
    resultsEl.innerHTML = `
      <div class="card empty-state">
        <p class="mb-0 text-muted">Searching…</p>
      </div>`;
  }

  function renderDiscover(users) {
    if (!users.length) {
      resultsEl.innerHTML = `
        <section>
          <h2 style="font-size:1.05rem;margin:0 0 0.85rem">People you can connect with</h2>
          <div class="card empty-state">
            <i class="fa-solid fa-user-group"></i>
            <p class="mb-0">No new people to suggest right now. Try searching above.</p>
          </div>
        </section>`;
      return;
    }

    resultsEl.innerHTML = `
      <section>
        <h2 style="font-size:1.05rem;margin:0 0 0.85rem">People you can connect with</h2>
        <div class="discover-grid">${users.map(renderUserCard).join('')}</div>
      </section>`;
    bindAddFriendButtons();
  }

  function renderSearchResults(data, query) {
    const users = pick(data, 'users', 'Users') || (Array.isArray(data) ? data : []);

    if (!users.length) {
      resultsEl.innerHTML = `
        <div class="card empty-state">
          <p class="mb-0">No people found for "<strong>${escapeHtml(query)}</strong>".</p>
        </div>`;
      return;
    }

    resultsEl.innerHTML = `
      <section class="search-section">
        <h2>People</h2>
        <div class="discover-grid">${users.map(renderUserCard).join('')}</div>
      </section>`;
    bindAddFriendButtons();
  }

  function bindAddFriendButtons() {
    resultsEl.querySelectorAll('.add-friend-btn:not([disabled])').forEach(btn => {
      if (btn.dataset.bound === 'true') return;
      btn.dataset.bound = 'true';
      btn.addEventListener('click', async () => {
        try {
          await Connectly.api('api/friendships/request', {
            method: 'POST',
            body: JSON.stringify({ receiverId: btn.dataset.userId })
          });
          btn.disabled = true;
          btn.textContent = 'Request sent';
          btn.className = 'btn btn-light btn-sm';
          Connectly.showToast('Friend request sent!', 'success');
        } catch (err) {
          Connectly.showToast(err.message, 'error');
        }
      });
    });
  }

  function updateUrl(query) {
    const url = new URL(window.location.href);
    if (query) {
      url.searchParams.set('q', query);
    } else {
      url.searchParams.delete('q');
    }
    window.history.replaceState({}, '', url);
  }

  async function runSearch(query) {
    const trimmed = query.trim();
    updateUrl(trimmed);

    if (activeController) {
      activeController.abort();
    }

    const controller = new AbortController();
    activeController = controller;
    const seq = ++requestSeq;

    renderLoading();

    try {
      if (!trimmed) {
        const users = await Connectly.api('api/search/discover?page=1&pageSize=20', {
          signal: controller.signal
        });
        if (seq !== requestSeq) return;
        const list = Array.isArray(users) ? users : (pick(users, 'users', 'Users') || []);
        renderDiscover(list);
        return;
      }

      const data = await Connectly.api(`api/search?q=${encodeURIComponent(trimmed)}`, {
        signal: controller.signal
      });
      if (seq !== requestSeq) return;
      renderSearchResults(data, trimmed);
    } catch (err) {
      if (err.name === 'AbortError') return;
      if (seq !== requestSeq) return;
      resultsEl.innerHTML = `
        <div class="card empty-state">
          <p class="mb-0 text-danger">${escapeHtml(err.message || 'Search failed.')}</p>
        </div>`;
    } finally {
      if (activeController === controller) {
        activeController = null;
      }
    }
  }

  function scheduleSearch() {
    clearTimeout(debounceTimer);
    debounceTimer = setTimeout(() => runSearch(input.value), 300);
  }

  input.addEventListener('input', scheduleSearch);

  form?.addEventListener('submit', (event) => {
    event.preventDefault();
    clearTimeout(debounceTimer);
    runSearch(input.value);
  });

  bindAddFriendButtons();
})();
