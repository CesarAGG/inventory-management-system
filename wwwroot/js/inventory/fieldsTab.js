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
    let fieldsToDeleteIds = [];

    // --- FUNCTION DEFINITIONS ---
    async function fetchFields() {
        const response = await fetch(`/api/inventory/${inventoryId}/fields`);
        if (!response.ok) { showToast('Failed to load fields.', true); return; }
        const fields = await response.json();
        renderFields(fields);
    }

    async function addField() {
        const name = newFieldNameInput.value.trim();
        const type = newFieldTypeSelect.value;
        if (!name) { showToast('Field name cannot be empty.', true); return; }
        const response = await fetch(`/api/inventory/${inventoryId}/fields`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': csrfToken },
            body: JSON.stringify({ name: name, type: type })
        });
        if (response.ok) {
            document.dispatchEvent(new Event('refreshItemsData'));

            newFieldNameInput.value = '';
            showToast('Field added successfully.');
            fetchFields();
        } else {
            const errorText = await response.text();
            showToast(`Failed to add field: ${errorText}`, true);
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

        const response = await fetch('/api/inventory/fields/delete', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': csrfToken },
            body: JSON.stringify({ fieldIds: fieldsToDeleteIds, inventoryVersion: parseInt(inventoryVersion, 10) })
        });

        deleteFieldModal.hide();

        if (!response.ok) {
            // NOTE: The real `handleConcurrencyError` will be added in Part 3.
            // For now, we just handle the error with a standard toast.
            const error = await response.json().catch(() => ({ message: 'Failed to delete selected fields.' }));
            showToast(error.message, true);
            fieldsToDeleteIds = [];
            return;
        }

        const result = await response.json();
        document.dispatchEvent(new Event('refreshItemsData'));
        showToast('Selected fields deleted successfully.');

        // Correctly parse the response: the controller returns the Data object directly.
        if (result.newVersion && versionElement) {
            versionElement.dataset.inventoryVersion = result.newVersion;
        }
        fetchFields();

        fieldsToDeleteIds = [];
    }

    async function updateFieldName(fieldId, newName) {
        const versionElement = document.querySelector('h2[data-inventory-version]');
        const inventoryVersion = versionElement?.dataset.inventoryVersion;

        if (!inventoryVersion) {
            showToast('Could not find inventory version. Cannot save.', true);
            return;
        }

        const response = await fetch(`/api/inventory/fields/${fieldId}`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': csrfToken },
            body: JSON.stringify({ name: newName, inventoryVersion: parseInt(inventoryVersion, 10) })
        });

        const result = await response.json();
        if (response.ok) {
            showToast('Field renamed successfully.');
            if (result.newVersion && versionElement) {
                versionElement.dataset.inventoryVersion = result.newVersion;
            }
            fetchFields();
        } else if (response.status === 409) {
            showToast(result.message, true);
        } else {
            showToast('Failed to rename field.', true);
        }
    }

    async function reorderFields(orderedIds) {
        const versionElement = document.querySelector('h2[data-inventory-version]');
        const inventoryVersion = versionElement?.dataset.inventoryVersion;

        if (!inventoryVersion) {
            showToast('Could not find inventory version. Cannot save order.', true);
            fetchFields(); // Revert visual drag-and-drop on error
            return;
        }

        const response = await fetch(`/api/inventory/${inventoryId}/fields/reorder`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': csrfToken },
            body: JSON.stringify({ orderedFieldIds: orderedIds, inventoryVersion: parseInt(inventoryVersion, 10) })
        });

        const result = await response.json();
        if (response.ok) {
            if (result.newVersion && versionElement) {
                versionElement.dataset.inventoryVersion = result.newVersion;
            }
        } else {
            showToast(result.message || 'Failed to save new order.', true);
            fetchFields(); // Revert the UI to match the server state on failure
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
            const fieldName = escapeHtml(field.name);
            let nameHtml = fieldName;
            if (fieldName.length > 35) { nameHtml = `<span class="truncated-text" title="${fieldName}">${fieldName.substring(0, 32)}<span class="unselectable">...</span></span>`; }
            li.innerHTML = `
                                    <input class="form-check-input me-2" type="checkbox" value="${field.id}" name="selectedFieldIds">
                                    <span class="drag-handle" style="cursor: move; margin-right: 10px;">&#9776;</span>
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
        const fieldId = editFieldIdInput.value;
        const newName = editFieldNameInput.value;
        updateFieldName(fieldId, newName);
        editFieldModal.hide();
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

        const fieldNameSpan = selectedCheckbox.closest('li').querySelector('.field-name');
        const currentName = fieldNameSpan.textContent.trim().replace('...', '');

        editFieldIdInput.value = selectedCheckbox.value;
        editFieldNameInput.value = currentName;
        editFieldModal.show();
    });

    const fieldSelectionChangeHandler = () => {
        const selectedCount = document.querySelectorAll('input[name="selectedFieldIds"]:checked').length;
        if (selectedCount === 1) {
            editSelectedFieldBtn.disabled = false;
            editSelectedFieldBtn.classList.replace('btn-outline-secondary', 'btn-secondary');
        } else {
            editSelectedFieldBtn.disabled = true;
            editSelectedFieldBtn.classList.replace('btn-secondary', 'btn-outline-secondary');
        }
    };

    fieldsList.addEventListener('change', fieldSelectionChangeHandler);

    document.getElementById('fields-tab')?.addEventListener('show.bs.tab', fetchFields, { once: true });
}