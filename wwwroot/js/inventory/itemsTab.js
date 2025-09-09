function initializeItemsTab(inventoryId, csrfToken, canWrite) {
    if (document.getElementById('itemsDataTable')?.getAttribute('data-initialized')) return;
    document.getElementById('itemsDataTable')?.setAttribute('data-initialized', 'true');

    const itemsTable = document.getElementById('itemsDataTable');
    const itemsTableHead = document.getElementById('items-table-head');
    const viewContentModal = new bootstrap.Modal(document.getElementById('viewContentModal'));
    const viewContentModalLabel = document.getElementById('viewContentModalLabel');
    const viewContentModalBody = document.getElementById('viewContentModalBody');
    let itemsDataTable = null;
    let fullItemSchema = [];
    let dtColumnSchema = [];

    let currentItemState = {
        id: '',
        customId: '',
        boundaries: '',
        formatHash: '',
        fields: {}
    };

    function destroyDataTable() {
        if (itemsDataTable) {
            itemsDataTable.destroy();
            itemsDataTable = null;
        }
        itemsTableHead.innerHTML = '';
    }

    async function initializePage() {
        try {
            const schemaResponse = await fetch(`/api/inventory/${inventoryId}/schema`);
            if (!schemaResponse.ok) throw new Error('Failed to load inventory schema.');
            const schemaData = await schemaResponse.json();

            fullItemSchema = schemaData.columns
                .filter(c => !c.fieldId.startsWith('system_'))
                .map(c => ({
                    id: c.fieldId, name: c.title, description: c.description,
                    type: c.type, dataKey: c.data.toLowerCase()
                }));

            if (canWrite) {
                buildItemModalForm(fullItemSchema);
            }

            dtColumnSchema = schemaData.columns.filter(c => c.isVisibleInTable);
            initializeDataTable(dtColumnSchema);

        } catch (error) {
            showToast(error.message, true);
            itemsTableHead.innerHTML = '<tr><th>Error</th></tr>';
        }
    }

    function initializeDataTable(columnsSchema) { /* ... unchanged logic from last correct version ... */ }

    if (itemsTable) {
        itemsTable.addEventListener('click', e => {
            if (e.target.closest('.expandable-cell')) {
                const cell = e.target.closest('.expandable-cell');
                viewContentModalLabel.textContent = cell.dataset.fieldName;
                viewContentModalBody.textContent = cell.dataset.content;
                viewContentModal.show();
            }
        });
    }

    if (canWrite) {
        const itemForm = document.getElementById('itemForm');
        const itemModalEl = document.getElementById('itemModal');
        const itemModal = new bootstrap.Modal(itemModalEl);
        const itemModalLabel = document.getElementById('itemModalLabel');
        const customIdInput = document.getElementById('item-field-customId');
        const segmentBtn = document.getElementById('segmentBtn');
        const regenerateIdBtn = document.getElementById('regenerateIdBtn');
        const deleteItemsModal = new bootstrap.Modal(document.getElementById('deleteItemsConfirmModal'));
        const confirmDeleteItemsBtn = document.getElementById('confirmDeleteItemsBtn');
        const segmentationModal = new bootstrap.Modal(document.getElementById('segmentationModal'));
        const segmentationIdStringEl = document.getElementById('segmentationIdString');
        const segmentationEditorEl = document.getElementById('segmentationEditor');
        const confirmSegmentationBtn = document.getElementById('confirmSegmentationBtn');

        function buildItemModalForm(fields) {
            const itemModalBody = document.querySelector('#itemModal .modal-body');
            let formHtml = `
                <div class="mb-3">
                    <label for="item-field-customId" class="form-label">Custom Item ID</label>
                    <div class="input-group">
                        <input type="text" class="form-control" id="item-field-customId" name="customId" readonly>
                        <button class="btn btn-outline-secondary" type="button" id="segmentBtn">Structure</button>
                        <button class="btn btn-outline-secondary" type="button" id="regenerateIdBtn">Regenerate</button>
                    </div>
                    <div id="customId-validation-error" class="text-danger small mt-1 d-none"></div>
                </div>
                <hr/>
            `;
            fields.forEach(field => {
                const hintId = `hint-for-${field.id}`;
                const description = field.description ? `<small id="${hintId}" class="form-text text-muted">${escapeHtml(field.description)}</small>` : '';
                const ariaDescribedBy = field.description ? `aria-describedby="${hintId}"` : '';
                formHtml += `<div class="mb-3"><label for="item-field-${field.id}" class="form-label">${escapeHtml(field.name)}</label>`;
                switch (field.type) {
                    case 'Text': formHtml += `<textarea class="form-control" id="item-field-${field.id}" name="${field.id}" ${ariaDescribedBy}></textarea>`; break;
                    case 'Bool': formHtml += `<div class="form-check form-switch"><input class="form-check-input" type="checkbox" id="item-field-${field.id}" name="${field.id}" ${ariaDescribedBy}></div>`; break;
                    case 'Numeric': formHtml += `<input type="number" step="any" class="form-control" id="item-field-${field.id}" name="${field.id}" ${ariaDescribedBy}>`; break;
                    default: formHtml += `<input type="text" class="form-control" id="item-field-${field.id}" name="${field.id}" ${ariaDescribedBy}>`;
                }
                formHtml += `${description}</div>`;
            });
            itemModalBody.innerHTML = formHtml;
        }

        function buildSegmentationEditor() {
            segmentationIdStringEl.textContent = currentItemState.customId;
            segmentationEditorEl.innerHTML = '';

            const boundaries = currentItemState.boundaries.split(',').map(Number);
            let totalLength = 0;
            boundaries.forEach(len => totalLength += len);

            let currentIndex = 0;
            boundaries.forEach((len, i) => {
                const segmentText = currentItemState.customId.substring(currentIndex, currentIndex + len);
                const isLast = i === boundaries.length - 1;

                const segmentDiv = document.createElement('div');
                segmentDiv.className = 'p-2 border rounded bg-light font-monospace';
                segmentDiv.textContent = segmentText;

                const lenInput = document.createElement('input');
                lenInput.type = 'number';
                lenInput.className = 'form-control form-control-sm';
                lenInput.style.width = '60px';
                lenInput.value = len;
                lenInput.min = 0;
                lenInput.dataset.index = i;

                if (isLast) lenInput.readOnly = true;

                segmentationEditorEl.appendChild(segmentDiv);
                if (!isLast) segmentationEditorEl.appendChild(lenInput);

                currentIndex += len;
            });

            segmentationEditorEl.addEventListener('change', (e) => {
                if (e.target.matches('input[type="number"]')) {
                    const changedIndex = parseInt(e.target.dataset.index, 10);
                    const newLen = parseInt(e.target.value, 10);
                    let newBoundaries = boundaries.slice();
                    newBoundaries[changedIndex] = newLen;

                    let currentSum = 0;
                    for (let i = 0; i <= changedIndex; i++) {
                        currentSum += newBoundaries[i];
                    }

                    const remainingLength = totalLength - currentSum;
                    const inputs = segmentationEditorEl.querySelectorAll('input[type="number"]');

                    // Adjust the next input field if it exists
                    if (changedIndex + 1 < inputs.length) {
                        const nextInput = inputs[changedIndex + 1];
                        let nextLen = parseInt(nextInput.value, 10);

                        let sumOfRest = 0;
                        for (let i = changedIndex + 2; i < newBoundaries.length; i++) {
                            sumOfRest += newBoundaries[i];
                        }

                        nextInput.value = totalLength - currentSum - sumOfRest;
                    }
                    // The last boundary is implicitly defined by the remainder
                }
            });
        }

        async function openItemModal(itemData = null) {
            itemForm.reset();
            itemForm.querySelectorAll('.is-invalid').forEach(el => el.classList.remove('is-invalid'));
            itemForm.querySelectorAll('.invalid-feedback').forEach(el => el.remove());

            if (itemData) {
                itemModalLabel.textContent = 'Edit Item';
                currentItemState.id = itemData.id;
                currentItemState.customId = itemData.customId;
                currentItemState.boundaries = itemData.customIdSegmentBoundaries;
                currentItemState.formatHash = itemData.customIdFormatHashApplied;

                customIdInput.value = currentItemState.customId;

                fullItemSchema.forEach(field => {
                    const input = itemForm.querySelector(`[name="${field.id}"]`);
                    if (input) {
                        const value = itemData[field.dataKey];
                        if (field.type === 'Bool') {
                            input.checked = value === true;
                        } else {
                            input.value = value ?? '';
                        }
                    }
                });

                const inventoryHash = document.querySelector('h2[data-inventory-version]').dataset.inventoryFormatHash;
                if (inventoryHash && currentItemState.formatHash !== inventoryHash) {
                    showToast('ID format has changed. Regenerating ID.', false);
                    await handleRegenerateId();
                }
            } else {
                itemModalLabel.textContent = 'Add New Item';
                currentItemState = { id: '', customId: '', boundaries: '', formatHash: '', fields: {} };
                await handleRegenerateId();
            }
            itemModal.show();
        }

        async function saveItem() {
            const isEditing = !!currentItemState.id;
            const itemForm = document.getElementById('itemForm');
            const formData = new FormData(itemForm);
            const fieldValues = {};
            fullItemSchema.forEach(field => {
                if (field.type === 'Bool') {
                    fieldValues[field.id] = formData.has(field.id);
                } else {
                    fieldValues[field.id] = formData.get(field.id);
                }
            });

            const payload = {
                customId: currentItemState.customId,
                customIdSegmentBoundaries: currentItemState.boundaries,
                fieldValues: fieldValues
            };

            const apiUrl = isEditing ? `/api/inventory/items/${currentItemState.id}` : `/api/inventory/${inventoryId}/items`;

            try {
                const response = await fetch(apiUrl, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': csrfToken },
                    body: JSON.stringify(payload)
                });

                const customIdErrorDiv = document.getElementById('customId-validation-error');
                customIdErrorDiv.classList.add('d-none');
                itemForm.querySelectorAll('.is-invalid').forEach(el => el.classList.remove('is-invalid'));
                itemForm.querySelectorAll('.invalid-feedback').forEach(el => el.remove());

                if (response.ok) {
                    showToast(`Item ${isEditing ? 'updated' : 'added'} successfully.`);
                    itemModal.hide();
                    itemsDataTable.ajax.reload(null, false); // Reload table data
                } else if (response.status === 409) {
                    window.handleConcurrencyError();
                    itemModal.hide();
                } else if (response.status === 400) {
                    const errors = await response.json();
                    for (const fieldId in errors) {
                        const errorMessage = errors[fieldId];
                        if (fieldId === 'customId') {
                            customIdErrorDiv.textContent = errorMessage;
                            customIdErrorDiv.classList.remove('d-none');
                        } else {
                            const input = itemForm.querySelector(`[name="${fieldId}"]`);
                            if (input) {
                                input.classList.add('is-invalid');
                                const errorDiv = document.createElement('div');
                                errorDiv.className = 'invalid-feedback';
                                errorDiv.textContent = errorMessage;
                                input.parentElement.appendChild(errorDiv);
                            }
                        }
                    }
                    showToast('Please correct the highlighted errors.', true);
                } else if (response.status === 403) {
                    showToast('Your permissions may have changed. Please reload the page.', true);
                    itemModal.hide();
                } else {
                    showToast(`Failed to ${isEditing ? 'update' : 'add'} item.`, true);
                }
            } catch (error) {
                showToast(`An error occurred while saving the item.`, true);
            }
        }

        async function deleteSelectedItems() {
            const selectedCheckboxes = document.querySelectorAll('.item-checkbox:checked');
            const itemIds = Array.from(selectedCheckboxes).map(cb => cb.value);

            try {
                const response = await fetch('/api/inventory/items/delete', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': csrfToken },
                    body: JSON.stringify(itemIds)
                });
                deleteItemsModal.hide();

                if (response.ok) {
                    const result = await response.json();
                    updateInventoryVersion(result.newInventoryVersion);
                    showToast('Selected items deleted.', false);
                    itemsDataTable.ajax.reload(null, false);
                } else if (response.status === 409) {
                    window.handleConcurrencyError();
                } else if (response.status === 403) {
                    showToast('Your permissions may have changed. Please reload the page.', true);
                } else { showToast('Failed to delete items.', true); }
            } catch (error) {
                showToast(`An error occurred while deleting items.`, true);
            }
        }

        const selectionChangeHandler = () => {
            const selectedCount = document.querySelectorAll('.item-checkbox:checked').length;
            editSelectedItemBtn.disabled = selectedCount !== 1;
            editSelectedItemBtn.classList.toggle('btn-secondary', selectedCount === 1);
            editSelectedItemBtn.classList.toggle('btn-outline-secondary', selectedCount !== 1);
        };

        regenerateIdBtn.addEventListener('click', handleRegenerateId);
        segmentBtn.addEventListener('click', () => {
            buildSegmentationEditor();
            segmentationModal.show();
        });

        confirmSegmentationBtn.addEventListener('click', () => {
            const inputs = Array.from(segmentationEditorEl.querySelectorAll('input[type="number"]'));
            let newBoundaries = inputs.map(input => parseInt(input.value, 10));

            let totalLength = 0;
            const idStringLength = currentItemState.customId.length;
            newBoundaries.forEach(len => totalLength += len);

            // Calculate the last boundary
            newBoundaries.push(idStringLength - totalLength);

            currentItemState.boundaries = newBoundaries.join(',');
            showToast('New ID structure confirmed.', false);
            segmentationModal.hide();
        });

        async function handleRegenerateId() {
            const version = parseInt(document.querySelector('h2[data-inventory-version]').dataset.inventoryVersion, 10);
            try {
                const response = await fetch(`/api/inventory/${inventoryId}/regenerate-id`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': csrfToken },
                    body: JSON.stringify({ inventoryVersion: version })
                });

                if (response.ok) {
                    const result = await response.json();
                    currentItemState.customId = result.id;
                    currentItemState.boundaries = result.boundaries;
                    customIdInput.value = result.id;
                } else if (response.status === 409) {
                    window.handleConcurrencyError();
                } else {
                    showToast('Could not regenerate ID. The format might be invalid or settings have changed.', true);
                }
            } catch (error) {
                showToast('An error occurred while regenerating the ID.', true);
            }
        }

        document.querySelector('button[data-bs-target="#itemModal"]').addEventListener('click', () => openItemModal());
        itemForm.addEventListener('submit', (e) => { e.preventDefault(); saveItem(); });

        editSelectedItemBtn.addEventListener('click', () => {
            const selectedCheckbox = document.querySelector('.item-checkbox:checked');
            if (!selectedCheckbox) return;

            const rowNode = selectedCheckbox.closest('tr');
            if (!rowNode) return;

            const rowData = itemsDataTable.row(rowNode).data();
            if (!rowData) {
                showToast('Could not find data for the selected item.', true);
                return;
            }
            openItemModal(rowData);
        });

        deleteSelectedItemsBtn.addEventListener('click', () => {
            if (document.querySelectorAll('.item-checkbox:checked').length === 0) {
                showToast('Please select items to delete.', true);
                return;
            }
            deleteItemsModal.show();
        });
        confirmDeleteItemsBtn.addEventListener('click', deleteSelectedItems);

        if (itemsTable) {
            itemsTable.addEventListener('change', (e) => {
                if (e.target.id === 'selectAllItemsCheckbox') {
                    itemsTable.querySelectorAll('.item-checkbox').forEach(cb => cb.checked = e.target.checked);
                }
                if (e.target.matches('.item-checkbox, #selectAllItemsCheckbox')) {
                    selectionChangeHandler();
                }
            });
        }
    }

    document.addEventListener('refreshItemsData', function () {
        destroyDataTable();
        initializePage();
    });

    initializePage();
}