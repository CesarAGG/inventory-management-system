function initializeSettingsTab(inventoryId, inventoryName, csrfToken, currentUserId, canManageSettings) {
    // --- RENAME LOGIC ---
    const renameInput = document.getElementById('renameInventoryInput');
    const renameBtn = document.getElementById('renameInventoryBtn');
    const deleteConfirmLabel = document.getElementById('deleteConfirmLabel');

    async function renameInventory() {
        const newName = renameInput.value.trim();
        if (newName === '' || newName === inventoryName) {
            showToast('Please enter a new name.', true);
            return;
        }

        try {
            const response = await fetch(`/api/inventory/${inventoryId}/rename`, {
                method: 'PUT',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': csrfToken
                },
                body: JSON.stringify({ newName: newName })
            });

            if (!response.ok) {
                if (response.status === 403) {
                    throw new Error('Your permissions may have changed. Please reload the page.');
                }
                const errorData = await response.json();
                const errorMessage = errorData.errors?.NewName?.[0] || 'Failed to rename inventory.';
                throw new Error(errorMessage);
            }

            const data = await response.json();
            inventoryName = data.newName;
            document.querySelector('h2').textContent = `Manage Inventory: ${escapeHtml(data.newName)}`;
            if (deleteConfirmLabel) {
                deleteConfirmLabel.innerHTML = `To confirm, type the name of the inventory: <strong class="text-danger">${escapeHtml(inventoryName)}</strong>`;
            }
            deleteConfirmInput.value = '';
            deleteConfirmInput.dispatchEvent(new Event('input'));
            updateInventoryVersion(data.newVersion);
            showToast('Inventory renamed successfully.');
        } catch (error) {
            showToast(error.message, true);
        }
    }


    renameBtn.addEventListener('click', renameInventory);

    // --- TRANSFER OWNERSHIP LOGIC ---
    const transferInput = document.getElementById('transferOwnerInput');
    const transferBtn = document.getElementById('transferOwnerBtn');
    const transferConfirmModal = new bootstrap.Modal(document.getElementById('transferConfirmModal'));
    const confirmTransferBtn = document.getElementById('confirmTransferBtn');
    const transferTargetUserEmailSpan = document.getElementById('transferTargetUserEmail');
    let targetUserEmail = '';

    async function transferOwnership() {
        try {
            const response = await fetch(`/api/inventory/${inventoryId}/transfer`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': csrfToken },
                body: JSON.stringify({ newOwnerEmail: targetUserEmail })
            });

            if (!response.ok) {
                if (response.status === 403) {
                    throw new Error('Your permissions may have changed. Please reload the page.');
                }
                let errorMessage = 'Failed to transfer ownership.';
                try {
                    const errorData = await response.json();
                    if (errorData.message) {
                        errorMessage = errorData.message;
                    } else {
                        const errorKeys = Object.keys(errorData);
                        if (errorKeys.length > 0 && Array.isArray(errorData[errorKeys[0]])) {
                            errorMessage = errorData[errorKeys[0]][0];
                        }
                    }
                } catch (e) {
                    console.error("Could not parse error response as JSON.", e);
                }
                throw new Error(errorMessage);
            }

            const data = await response.json();
            showToast(data.message, false);
            updateInventoryVersion(data.newVersion);

            if (data.redirectUrl) {
                setTimeout(() => { window.location.href = data.redirectUrl; }, 2000);
            }
        } catch (error) {
            showToast(error.message, true);
        } finally {
            transferConfirmModal.hide();
        }
    }

    transferBtn.addEventListener('click', () => {
        targetUserEmail = transferInput.value.trim();
        if (targetUserEmail === '') {
            showToast('Please enter the new owner\'s email address.', true);
            return;
        }
        transferTargetUserEmailSpan.textContent = targetUserEmail;
        transferConfirmModal.show();
    });
    confirmTransferBtn.addEventListener('click', transferOwnership);

    // --- DELETE INVENTORY LOGIC ---
    const deleteConfirmInput = document.getElementById('deleteConfirmInput');
    const deleteBtn = document.getElementById('deleteInventoryBtn');
    const deleteInventoryConfirmModal = new bootstrap.Modal(document.getElementById('deleteInventoryConfirmModal'));
    const confirmDeleteInventoryBtn = document.getElementById('confirmDeleteInventoryBtn');

    async function deleteInventory() {
        try {
            const response = await fetch(`/api/inventory/${inventoryId}/delete`, {
                method: 'DELETE',
                headers: { 'RequestVerificationToken': csrfToken }
            });
            if (!response.ok) {
                if (response.status === 403) {
                    throw new Error('Your permissions may have changed. Please reload the page.');
                }
                throw new Error('Failed to delete inventory. You must be the owner or an admin.');
            }
            const data = await response.json();
            showToast(data.message, false);
            setTimeout(() => { window.location.href = '/'; }, 2000);
        } catch (error) {
            showToast(error.message, true);
        } finally {
            deleteInventoryConfirmModal.hide();
        }
    }

    deleteConfirmInput.addEventListener('input', () => {
        deleteBtn.disabled = deleteConfirmInput.value !== inventoryName;
    });

    deleteBtn.addEventListener('click', () => {
        deleteInventoryConfirmModal.show();
    });
    confirmDeleteInventoryBtn.addEventListener('click', deleteInventory);
}