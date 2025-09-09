function initializeFieldsTab(inventoryId, csrfToken) {
    // --- CONSTANT DECLARATIONS ---
    const fieldsList = document.getElementById('fields-list');
    const addFieldBtn = document.getElementById('addFieldBtn');
    const newFieldNameInput = document.getElementById('newFieldName');
    const newFieldTypeSelect = document.getElementById('newFieldType');
    const deleteFieldModal = new bootstrap.Modal(document.getElementById('deleteConfirmModal'));
    const confirmDeleteBtn = document.getElementById('confirmDeleteBtn');
    const deleteSelectedFieldsBtn = document.getElementById('deleteSelectedFieldsBtn');
    const editSelectedFieldBtn = document.getElementById('editSelectedFieldBtn');
    const editFieldModal = new bootstrap.Modal(document.getElementById('editFieldModal'));
    const editFieldForm = document.getElementById('editFieldForm');
    const editFieldIdInput = document.getElementById('editFieldId');
    const editFieldNameInput = document.getElementById('editFieldNameInput');
    const editFieldDescriptionInput = document.getElementById('editFieldDescriptionInput');
    const editFieldIsVisibleInput = document.getElementById('editFieldIsVisibleInput');
    let fieldsToDeleteIds = [];

    // --- FUNCTION DEFINITIONS ---
    async function fetchFields() {
        try {
            const response = await fetch(`/api/inventory/${inventoryId}/fields`);
            if (!response.ok) { showToast('Failed to load fields.', true); return; }
            const fields = await response.json();
            renderFields(fields);
        } catch (error) {
            showToast('An error occurred while fetching fields.', true);
        }
    }

    async function addField() {
        const name = newFieldNameInput.value.trim();
        const type = newFieldTypeSelect.value;
        if (!name) { showToast('Field name cannot be empty.', true); return; }

        const payload = {
            name: name,
            type: type,
            description: '',
            isVisibleInTable: true
        };

        try {
            const response = await fetch(`/api/inventory/${inventoryId}/fields`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': csrfToken },
                body: JSON.stringify(payload)
            });

            if (response.ok) {
                const result = await response.json();
                updateInventoryVersion(result.newInventoryVersion);
                newFieldNameInput.value = '';
                showToast('Field added successfully.');
                fetchFields();
                document.dispatchEvent(new Event('refreshItemsData'));
            } else if (response.status === 409) {
                window.handleConcurrencyError();
            } else if (response.status === 403) {
                showToast('Your permissions may have changed. Please reload the page.', true);
            } else {
                const error = await response.json().catch(() => ({ message: 'Failed to add field.' }));
                showToast(error.message, true);
            }
        } catch (error) {
            showToast('An error occurred while adding the field.', true);
        }
    }

    async function deleteSelectedFields() {
        if (fieldsToDeleteIds.length === 0) return;
        const versionElement = document.querySelector('h2[data-inventory-version]');
        const inventoryVersion = versionElement?.dataset.inventoryVersion;
        if (!inventoryVersion) {
            showToast('Could not find inventory version. Cannot delete.', true);
            return;
        }

        try {
            const response = await fetch('/api/inventory/fields/delete', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': csrfToken },
                body: JSON.stringify({ fieldIds: fieldsToDeleteIds, inventoryVersion: parseInt(inventoryVersion, 10) })
            });
            deleteFieldModal.hide();

            if (response.ok) {
                const result = await response.json();
                showToast('Selected fields deleted successfully.');
                updateInventoryVersion(result.newVersion);
                fetchFields();
                document.dispatchEvent(new Event('refreshItemsData'));
            } else if (response.status === 409) {
                window.handleConcurrencyError();
            } else if (response.status === 403) {
                showToast('Your permissions may have changed. Please reload the page.', true);
            } else {
                const error = await response.json().catch(() => ({ message: 'Failed to delete selected fields.' }));
                showToast(error.message, true);
            }
        } catch (error) {
            showToast('An error occurred while deleting fields.', true);
        } finally {
            fieldsToDeleteIds = [];
        }
    }

    async function updateField() {
        const fieldId = editFieldIdInput.value;
        const newName = editFieldNameInput.value.trim();
        const newDescription = editFieldDescriptionInput.value.trim();
        const newIsVisible = editFieldIsVisibleInput.checked;

        if (!newName) {
            showToast('Field name cannot be empty.', true);
            return;
        }

        const versionElement = document.querySelector('h2[data-inventory-version]');
        const inventoryVersion = versionElement?.dataset.inventoryVersion;

        if (!inventoryVersion) {
            showToast('Could not find inventory version. Cannot save.', true);
            return;
        }

        const payload = {
            name: newName,
            description: newDescription,
            isVisibleInTable: newIsVisible,
            inventoryVersion: parseInt(inventoryVersion, 10)
        };

        try {
            const response = await fetch(`/api/inventory/fields/${fieldId}`, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': csrfToken },
                body: JSON.stringify(payload)
            });

            editFieldModal.hide();

            if (response.ok) {
                const result = await response.json();
                showToast('Field updated successfully.');
                updateInventoryVersion(result.newVersion);
                fetchFields();
                document.dispatchEvent(new Event('refreshItemsData'));
            } else if (response.status === 409) {
                window.handleConcurrencyError();
            } else if (response.status === 403) {
                showToast('Your permissions may have changed. Please reload the page.', true);
            } else {
                const result = await response.json();
                showToast(result.message || 'Failed to update field.', true);
            }
        } catch (error) {
            showToast('An error occurred while updating the field.', true);
        }
    }

    async function reorderFields(orderedIds) {
        const versionElement = document.querySelector('h2[data-inventory-version]');
        const inventoryVersion = versionElement?.dataset.inventoryVersion;

        if (!inventoryVersion) {
            showToast('Could not find inventory version. Cannot save order.', true);
            fetchFields();
            return;
        }

        try {
            const response = await fetch(`/api/inventory/${inventoryId}/fields/reorder`, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': csrfToken },
                body: JSON.stringify({ orderedFieldIds: orderedIds, inventoryVersion: parseInt(inventoryVersion, 10) })
            });

            if (response.ok) {
                const result = await response.json();
                updateInventoryVersion(result.newVersion);
                document.dispatchEvent(new Event('refreshItemsData'));
            } else if (response.status === 409) {
                window.handleConcurrencyError();
                fetchFields();
            } else if (response.status === 403) {
                showToast('Your permissions may have changed. Please reload the page.', true);
                fetchFields();
            } else {
                const result = await response.json();
                showToast(result.message || 'Failed to save new order.', true);
                fetchFields();
            }
        } catch (error) {
            showToast('An error occurred while reordering fields.', true);
            fetchFields();
        }
    }

    function renderFields(fields) {
        fieldsList.innerHTML = '';
        const systemFields = [{ name: 'Item ID', type: 'System ID (Read-Only)' }, { name: 'Created At', type: 'System Date (Read-Only)' }];
        systemFields.forEach(sf => {
            const li = document.createElement('li');
            li.className = 'list-group-item d-flex align-items-center disabled bg-light';
            li.innerHTML = `<div class="flex-grow-1"><strong>${escapeHtml(sf.name)}</strong><small class="text-muted d-block">${escapeHtml(sf.type)}</small></div>`;
            fieldsList.appendChild(li);
        });
        fields.forEach(field => {
            const li = document.createElement('li');
            li.className = 'list-group-item d-flex align-items-center';
            li.dataset.id = field.id;
            li.dataset.description = field.description || '';
            li.dataset.isVisible = field.isVisibleInTable;

            const fieldName = escapeHtml(field.name);
            let nameHtml = fieldName;
            if (fieldName.length > 35) { nameHtml = `<span class="truncated-text" title="${fieldName}">${fieldName.substring(0, 32)}<span class="unselectable">...</span></span>`; }

            const visibilityIcon = field.isVisibleInTable ? '<i class="bi bi-eye-fill text-success me-2" title="Visible in table"></i>' : '<i class="bi bi-eye-slash-fill text-muted me-2" title="Hidden in table"></i>';

            li.innerHTML = `
                <input class="form-check-input me-2" type="checkbox" value="${field.id}" name="selectedFieldIds">
                <span class="drag-handle" style="cursor: move; margin-right: 10px;">&#9776;</span>
                ${visibilityIcon}
                <div class="flex-grow-1" style="min-width: 0;">
                    <strong class="d-block text-truncate field-name" data-field-id="${field.id}">${nameHtml}</strong>
                    <small class="text-muted d-block">${escapeHtml(field.type)}</small>
                </div>`;
            fieldsList.appendChild(li);
        });
    }

    // --- EVENT LISTENERS ---
    editFieldForm.addEventListener('submit', function (e) {
        e.preventDefault();
        updateField();
    });

    new Sortable(fieldsList, { animation: 150, handle: '.drag-handle', onEnd: () => reorderFields(Array.from(fieldsList.querySelectorAll('li[data-id]')).map(item => item.dataset.id)) });
    addFieldBtn.addEventListener('click', addField);
    confirmDeleteBtn.addEventListener('click', deleteSelectedFields);
    deleteSelectedFieldsBtn.addEventListener('click', () => {
        fieldsToDeleteIds = Array.from(document.querySelectorAll('input[name="selectedFieldIds"]:checked')).map(cb => cb.value);
        if (fieldsToDeleteIds.length === 0) {
            showToast('Please select fields to delete.', true);
            return;
        }
        deleteFieldModal.show();
    });
    editSelectedFieldBtn.addEventListener('click', () => {
        const selectedCheckbox = document.querySelector('input[name="selectedFieldIds"]:checked');
        if (!selectedCheckbox) return;

        const li = selectedCheckbox.closest('li');
        const fieldNameSpan = li.querySelector('.field-name');

        editFieldIdInput.value = selectedCheckbox.value;
        editFieldNameInput.value = fieldNameSpan.textContent.trim().replace('...', '');
        editFieldDescriptionInput.value = li.dataset.description;
        editFieldIsVisibleInput.checked = li.dataset.isVisible === 'true';

        editFieldModal.show();
    });

    const fieldSelectionChangeHandler = () => {
        const selectedCount = document.querySelectorAll('input[name="selectedFieldIds"]:checked').length;
        editSelectedFieldBtn.disabled = selectedCount !== 1;
        editSelectedFieldBtn.classList.toggle('btn-secondary', selectedCount === 1);
        editSelectedFieldBtn.classList.toggle('btn-outline-secondary', selectedCount !== 1);
    };
    fieldsList.addEventListener('change', fieldSelectionChangeHandler);
    document.getElementById('fields-tab')?.addEventListener('show.bs.tab', fetchFields, { once: true });
}