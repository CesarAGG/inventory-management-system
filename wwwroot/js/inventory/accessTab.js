function initializeAccessTab(inventoryId, csrfToken) {
    const accessTabEl = document.getElementById('access-tab');
    const isPublicSwitch = document.getElementById('isPublicSwitch');
    const revokeSelectedBtn = document.getElementById('revokeSelectedBtn');
    let grantedUsersTable = null;

    function getInventoryVersion() {
        return parseInt(document.querySelector('h2[data-inventory-version]').dataset.inventoryVersion, 10);
    }

    async function fetchIsPublic() {
        try {
            const response = await fetch(`/api/inventory/${inventoryId}/access`);
            if (response.ok) {
                const settings = await response.json();
                isPublicSwitch.checked = settings.isPublic;
            } else {
                showToast("Could not load public access setting.", true);
            }
        } catch (error) {
            console.error('Failed to fetch public status.', error);
            showToast("An error occurred while loading settings.", true);
        }
    }

    function initializeGrantedUsersTable() {
        grantedUsersTable = new DataTable('#grantedUsersTable', {
            processing: true,
            serverSide: true,
            ajax: {
                url: `/api/inventory/${inventoryId}/granted-users`,
                type: 'POST',
                data: function (d) {
                    d.__RequestVerificationToken = csrfToken;
                },
                error: function () {
                    if ($.fn.DataTable.isDataTable('#grantedUsersTable')) {
                        showToast("Could not load user list.", true);
                    }
                }
            },
            columns: [
                {
                    data: 'userId', orderable: false, render: (data) =>
                        `<input class="form-check-input permission-checkbox" type="checkbox" value="${escapeHtml(data)}">`
                },
                { data: 'userName' },
                { data: 'userEmail' }
            ],
            order: [[1, 'asc']],
            dom: 'rt<"d-flex justify-content-between align-items-center"ip>',
        });

        $('#grantedUsersTable').on('draw.dt', function () {
            $('#selectAllPermissionsCheckbox').prop('checked', false);
            updateRevokeButtonState();
        });

        $('#grantedUsersTable').on('change', '.permission-checkbox, #selectAllPermissionsCheckbox', function () {
            if (this.id === 'selectAllPermissionsCheckbox') {
                $('#grantedUsersTable .permission-checkbox').prop('checked', this.checked);
            }
            updateRevokeButtonState();
        });
    }

    function updateRevokeButtonState() {
        const selectedCount = $('#grantedUsersTable .permission-checkbox:checked').length;
        revokeSelectedBtn.disabled = selectedCount === 0;
    }

    async function updatePublicAccess() {
        const payload = { isPublic: isPublicSwitch.checked, inventoryVersion: getInventoryVersion() };
        try {
            const response = await fetch(`/api/inventory/${inventoryId}/access/public`, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': csrfToken },
                body: JSON.stringify(payload)
            });
            if (response.ok) {
                const result = await response.json();
                updateInventoryVersion(result.newVersion);
                showToast('Public access updated successfully.');
            } else if (response.status === 409) {
                window.handleConcurrencyError();
                fetchIsPublic();
            } else {
                showToast('Failed to update public access.', true);
                isPublicSwitch.checked = !isPublicSwitch.checked;
            }
        } catch (error) {
            showToast('An error occurred.', true);
            isPublicSwitch.checked = !isPublicSwitch.checked;
        }
    }

    async function grantAccess(userId) {
        const payload = { userId: userId, inventoryVersion: getInventoryVersion() };
        try {
            const response = await fetch(`/api/inventory/${inventoryId}/access/grant`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': csrfToken },
                body: JSON.stringify(payload)
            });

            if (response.ok) {
                const result = await response.json();
                updateInventoryVersion(result.newInventoryVersion);
                showToast(`Access granted to ${escapeHtml(result.userName)}.`);
                grantedUsersTable.ajax.reload();
            } else if (response.status === 409) {
                window.handleConcurrencyError();
            } else {
                showToast('Failed to grant access.', true);
            }
        } catch (error) {
            showToast('An error occurred while granting access.', true);
        }
    }

    async function revokeSelectedPermissions() {
        const selectedIds = $('#grantedUsersTable .permission-checkbox:checked').map(function () {
            return $(this).val();
        }).get();

        if (selectedIds.length === 0) return;

        const payload = { userIds: selectedIds, inventoryVersion: getInventoryVersion() };
        try {
            const response = await fetch(`/api/inventory/${inventoryId}/access/revoke`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': csrfToken },
                body: JSON.stringify(payload)
            });

            if (response.ok) {
                const result = await response.json();
                updateInventoryVersion(result.newVersion);
                showToast('Access revoked for selected users.');
                grantedUsersTable.ajax.reload();
            } else if (response.status === 409) {
                window.handleConcurrencyError();
            } else {
                showToast('Failed to revoke access.', true);
            }
        } catch (error) {
            showToast('An error occurred while revoking access.', true);
        }
    }

    const autoCompleteJS = new autoComplete({
        selector: "#userSearchInput",
        placeHolder: "Search by username or email...",
        data: {
            src: async (query) => {
                try {
                    const source = await fetch(`/api/inventory/${inventoryId}/access/search-users?query=${encodeURIComponent(query)}`);
                    if (!source.ok) return [];
                    const data = await source.json();
                    return data;
                } catch (error) { return []; }
            },
            keys: ["userName", "email"],
            cache: false
        },
        resultItem: {
            highlight: true,
            element: (item, data) => {
                item.innerHTML = `
                <span style="display: flex; justify-content: space-between;">
                  <span>${data.match}</span>
                  <small style="color: #999;">${data.key === 'userName' ? data.value.email : data.value.userName}</small>
                </span>`;
            },
        },
        threshold: 2,
        debounce: 300,
        events: {
            input: {
                selection: (event) => {
                    const selection = event.detail.selection.value;
                    autoCompleteJS.input.value = '';
                    grantAccess(selection.id);
                }
            }
        }
    });

    isPublicSwitch.addEventListener('change', updatePublicAccess);
    revokeSelectedBtn.addEventListener('click', revokeSelectedPermissions);

    accessTabEl.addEventListener('show.bs.tab', () => {
        fetchIsPublic();
        if (!grantedUsersTable) {
            initializeGrantedUsersTable();
        }
    }, { once: true });
}