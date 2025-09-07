function initializeAdminTab(inventoryId, inventoryName, csrfToken) {
    // --- RENAME LOGIC ---
    const renameInput = document.getElementById('renameInventoryInput');
    const renameBtn = document.getElementById('renameInventoryBtn');

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
                const errorData = await response.json();
                const errorMessage = errorData.errors?.NewName?.[0] || 'Failed to rename inventory.';
                throw new Error(errorMessage);
            }

            const data = await response.json();
            inventoryName = data.newName; // Update the local variable for future comparisons
            document.querySelector('h2').textContent = `Manage Inventory: ${escapeHtml(data.newName)}`;
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

    // In wwwroot/js/inventory/adminTab.js

    async function transferOwnership() {
        try {
            const response = await fetch(`/api/inventory/${inventoryId}/transfer`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': csrfToken },
                body: JSON.stringify({ newOwnerEmail: targetUserEmail })
            });

            if (!response.ok) {
                let errorMessage = 'Failed to transfer ownership.'; 
                try {
                    const errorData = await response.json();
                    const errorKeys = Object.keys(errorData);
                    if (errorData.message) {
                        errorMessage = errorData.message;
                    } else if (errorKeys.length > 0 && Array.isArray(errorData[errorKeys[0]])) {
                        errorMessage = errorData[errorKeys[0]][0];
                    }
                } catch (e) {
                    console.error("Could not parse error response as JSON.", e);
                }
                throw new Error(errorMessage);
            }

            const data = await response.json();
            showToast(data.message, false);
            setTimeout(() => { window.location.href = '/User/Index'; }, 2000);
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