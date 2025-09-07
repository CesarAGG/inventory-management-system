function initializeAccessTab(inventoryId, csrfToken) {
    // --- CONSTANT DECLARATIONS ---
    const accessTabEl = document.getElementById('access-tab');
    const isPublicSwitch = document.getElementById('isPublicSwitch');
    const userSearchInput = document.getElementById('userSearchInput');
    const userSearchBtn = document.getElementById('userSearchBtn');
    const userSearchResults = document.getElementById('userSearchResults');
    const grantedUsersList = document.getElementById('grantedUsersList');
    const revokeSelectedBtn = document.getElementById('revokeSelectedBtn');
    const selectAllPermissionsCheckbox = document.getElementById('selectAllPermissionsCheckbox');
    let searchDebounceTimer;
    let searchAbortController = new AbortController();

    // --- FUNCTION DEFINITIONS ---
    async function fetchAccessSettings() {
        try {
            const response = await fetch(`/api/inventory/${inventoryId}/access`);
            if (!response.ok) throw new Error('Failed to fetch settings.');
            const settings = await response.json();

            isPublicSwitch.checked = settings.isPublic;
            renderGrantedUsers(settings.permissions);
        } catch (error) {
            showToast(error.message, true);
        }
    }

    function renderGrantedUsers(permissions) {
        grantedUsersList.innerHTML = '';
        if (permissions.length === 0) {
            grantedUsersList.innerHTML = '<li class="list-group-item text-muted">No users have been granted specific access.</li>';
        } else {
            permissions.forEach(p => {
                const li = document.createElement('li');
                li.className = 'list-group-item';
                li.dataset.userId = p.userId;
                li.innerHTML = `
                <input class="form-check-input me-2 permission-checkbox" type="checkbox" value="${escapeHtml(p.userId)}">
                <span>${escapeHtml(p.userEmail)}</span>
            `;
                grantedUsersList.appendChild(li);
            });
        }
        updateRevokeButtonState();
    }

    async function updatePublicAccess() {
        const isPublic = isPublicSwitch.checked;
        try {
            const response = await fetch(`/api/inventory/${inventoryId}/access/public`, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': csrfToken },
                body: JSON.stringify(isPublic)
            });
            if (!response.ok) throw new Error('Failed to update setting.');
            showToast('Public access updated successfully.');
        } catch (error) {
            showToast(error.message, true);
            isPublicSwitch.checked = !isPublic;
        }
    }

    async function searchUsers() {
        const query = userSearchInput.value.trim();
        userSearchResults.innerHTML = '';
        if (!query) return;

        searchAbortController.abort();
        searchAbortController = new AbortController();
        const signal = searchAbortController.signal;

        try {
            const response = await fetch(`/api/inventory/${inventoryId}/access/search-users?query=${encodeURIComponent(query)}`, { signal });
            if (!response.ok) throw new Error('Search failed.');
            const users = await response.json();

            userSearchResults.innerHTML = '';
            if (users.length === 0) {
                userSearchResults.innerHTML = '<div class="list-group-item text-muted fst-italic">No matching user found.</div>';
            } else {
                users.forEach(user => {
                    const item = document.createElement('a');
                    item.href = '#';
                    item.className = 'list-group-item list-group-item-action';
                    item.dataset.userId = user.id;
                    item.textContent = escapeHtml(user.email);
                    userSearchResults.appendChild(item);
                });
            }
        } catch (error) {
            if (error.name !== 'AbortError') {
                showToast(error.message, true);
            }
        }
    }

    async function grantAccess(userId) {
        try {
            const response = await fetch(`/api/inventory/${inventoryId}/access/grant`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': csrfToken },
                body: JSON.stringify(userId)
            });
            if (!response.ok) throw new Error('Failed to grant access.');

            fetchAccessSettings();
            userSearchInput.value = '';
            userSearchResults.innerHTML = '';
            showToast('Access granted successfully.');
        } catch (error) {
            showToast(error.message, true);
        }
    }

    function updateRevokeButtonState() {
        const selectedCount = grantedUsersList.querySelectorAll('.permission-checkbox:checked').length;
        revokeSelectedBtn.disabled = selectedCount === 0;
    }

    async function revokeSelectedPermissions() {
        const selectedIds = Array.from(grantedUsersList.querySelectorAll('.permission-checkbox:checked')).map(cb => cb.value);
        if (selectedIds.length === 0) return;

        try {
            const response = await fetch(`/api/inventory/${inventoryId}/access/revoke`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': csrfToken },
                body: JSON.stringify(selectedIds)
            });
            if (!response.ok) throw new Error('Failed to revoke access.');

            fetchAccessSettings();
            showToast('Access revoked successfully.');
        } catch (error) {
            showToast(error.message, true);
        }
    }

    // --- EVENT LISTENERS ---
    accessTabEl.addEventListener('show.bs.tab', fetchAccessSettings);

    isPublicSwitch.addEventListener('change', updatePublicAccess);

    userSearchBtn.addEventListener('click', searchUsers);

    userSearchInput.addEventListener('keypress', function (e) {
        if (e.key === 'Enter') {
            e.preventDefault(); // Prevent form submission if it's in a form
            searchUsers();
        }
    });

    userSearchResults.addEventListener('click', e => {
        e.preventDefault();
        const target = e.target.closest('.list-group-item-action');
        if (target && target.dataset.userId) {
            grantAccess(target.dataset.userId);
        }
    });

    grantedUsersList.addEventListener('change', e => {
        if (e.target.classList.contains('permission-checkbox')) {
            updateRevokeButtonState();
        }
    });

    selectAllPermissionsCheckbox.addEventListener('change', e => {
        grantedUsersList.querySelectorAll('.permission-checkbox').forEach(cb => {
            cb.checked = e.target.checked;
        });
        updateRevokeButtonState();
    });

    revokeSelectedBtn.addEventListener('click', revokeSelectedPermissions);

}