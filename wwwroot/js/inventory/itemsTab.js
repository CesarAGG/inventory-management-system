function initializeItemsTab(inventoryId, csrfToken, canWrite) {
    if (document.body.dataset.itemsTabInitialized === 'true') return;
    document.body.dataset.itemsTabInitialized = 'true';

    const itemsDataTableContainer = document.getElementById('itemsDataTableContainer');
    const viewContentModal = new bootstrap.Modal(document.getElementById('viewContentModal'));
    const viewContentModalLabel = document.getElementById('viewContentModalLabel');
    const viewContentModalBody = document.getElementById('viewContentModalBody');

    let itemsDataTable = null;
    let fullItemSchema = [];

    async function initializePage() {
        tearDown();

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
                initializeWriteAccessLogic();
            }

            const dtColumnSchema = schemaData.columns.filter(c => c.isVisibleInTable);
            initializeDataTable(dtColumnSchema);

        } catch (error) {
            showToast(error.message, true);
            const itemsTableHead = document.getElementById('items-table-head');
            if (itemsTableHead) itemsTableHead.innerHTML = '<tr><th>Error loading schema</th></tr>';
        }
    }

    function tearDown() {
        if (itemsDataTable) {
            itemsDataTable.destroy();
            itemsDataTable = null;
        }
        itemsDataTableContainer.innerHTML = `
            <table class="table table-hover mb-0" id="itemsDataTable">
                <thead id="items-table-head"></thead>
                <tbody></tbody>
            </table>`;
    }

    function initializeDataTable(columnsSchema) {
        const itemsTableEl = document.getElementById('itemsDataTable');
        const itemsTableHeadEl = document.getElementById('items-table-head');
        const headersHtml = `<tr>${columnsSchema.map(col => {
            const fieldName = escapeHtml(col.title);
            if (fieldName.length > 35) {
                return `<th title="${fieldName}">${fieldName.substring(0, 32)}<span class="unselectable">...</span></th>`;
            }
            return `<th>${fieldName}</th>`;
        }).join('')}</tr>`;

        itemsTableHeadEl.innerHTML = headersHtml.replace('<th></th>', '<th style="width: 50px;"><input class="form-check-input" type="checkbox" id="selectAllItemsCheckbox" /></th>');

        itemsDataTable = new DataTable('#itemsDataTable', {
            processing: true,
            serverSide: true,
            ajax: {
                url: `/api/inventory/${inventoryId}/items-data`,
                type: 'POST',
                data: function (d) { d.__RequestVerificationToken = csrfToken; },
                error: function () { if ($.fn.DataTable.isDataTable('#itemsDataTable')) { showToast('An error occurred while loading item data.', true); } }
            },
            columns: columnsSchema.map(c => ({ "data": c.data })),
            order: [[2, 'desc']],
            columnDefs: [
                {
                    targets: 0, orderable: false,
                    render: (data, type, row) => `<input class="form-check-input item-checkbox" type="checkbox" value="${escapeHtml(row.id)}" />`
                },
                {
                    targets: 1,
                    render: (data, type, row) => {
                        const idToUse = row.customId || `System ID: ${row.id}`;
                        const displayId = row.customId
                            ? (row.customId.length > 12 ? `${row.customId.substring(0, 12)}...` : row.customId)
                            : `${row.id.substring(0, 8)}...`;
                        const classes = row.customId ? 'font-monospace' : 'font-monospace text-muted';
                        return `<div class="expandable-cell" data-field-name="Item ID" data-content="${escapeHtml(idToUse)}"><span class="truncated-text ${classes}" title="${escapeHtml(idToUse)}">${escapeHtml(displayId)}</span></div>`;
                    }
                },
                { targets: 2, render: (data) => new Date(data).toLocaleString() },
                ...columnsSchema.slice(3).map((col, i) => ({
                    targets: i + 3,
                    render: function (data) {
                        const stringValue = String(data ?? '');
                        if (col.type === 'Bool') return data === true ? '✔️' : '❌';
                        if (stringValue.length > 35) return `<div class="expandable-cell" data-field-name="${escapeHtml(col.title)}" data-content="${escapeHtml(stringValue)}"><span class="truncated-text">${escapeHtml(stringValue.substring(0, 32))}<span class="unselectable">...</span></span></div>`;
                        return escapeHtml(stringValue);
                    }
                }))
            ],
            rowCallback: function (row, data) { $(row).data('full-data', data); }
        });
    }

    $(itemsDataTableContainer).on('click', '.expandable-cell', function () {
        viewContentModalLabel.textContent = this.dataset.fieldName;
        viewContentModalBody.textContent = this.dataset.content;
        viewContentModal.show();
    });

    if (canWrite) {
        $(itemsDataTableContainer).on('change', '.item-checkbox, #selectAllItemsCheckbox', function () {
            const editSelectedItemBtn = document.getElementById('editSelectedItemBtn');
            if (this.id === 'selectAllItemsCheckbox') {
                $('.item-checkbox', itemsDataTableContainer).prop('checked', this.checked);
            }
            const selectedCount = $('.item-checkbox:checked', itemsDataTableContainer).length;
            editSelectedItemBtn.disabled = selectedCount !== 1;
            editSelectedItemBtn.classList.toggle('btn-secondary', selectedCount === 1);
            editSelectedItemBtn.classList.toggle('btn-outline-secondary', selectedCount !== 1);
        });
    }

    function buildItemModalForm(fields) {
        const itemModalBody = document.querySelector('#itemModal .modal-body');
        if (!itemModalBody) return;
        let formHtml = `
            <div class="mb-3">
                <label for="item-field-customId" class="form-label">Custom Item ID</label>
                <div class="input-group">
                    <input type="text" class="form-control" id="item-field-customId" name="customId" readonly>
                    <button class="btn btn-outline-secondary" type="button" id="editSegmentBtn">Edit</button>
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

    function initializeWriteAccessLogic() {
        const itemForm = document.getElementById('itemForm');
        if (!itemForm || itemForm.dataset.listenersAttached === 'true') return;

        const itemModalEl = document.getElementById('itemModal');
        const itemModal = new bootstrap.Modal(itemModalEl);
        const itemModalLabel = document.getElementById('itemModalLabel');
        const editSelectedItemBtn = document.getElementById('editSelectedItemBtn');
        const deleteSelectedItemsBtn = document.getElementById('deleteSelectedItemsBtn');
        const deleteItemsModal = new bootstrap.Modal(document.getElementById('deleteItemsConfirmModal'));
        const confirmDeleteItemsBtn = document.getElementById('confirmDeleteItemsBtn');
        const segmentationModalEl = document.getElementById('segmentationModal');
        const segmentationModal = new bootstrap.Modal(segmentationModalEl);
        const segmentationEditorEl = document.getElementById('segmentationEditor');
        const confirmSegmentationBtn = document.getElementById('confirmSegmentationBtn');

        let currentItemState = { id: '', customId: '', boundaries: '', formatHash: '', nextSequenceValue: null };

        function buildSegmentationEditor() {
            segmentationEditorEl.innerHTML = '';

            const segmentsResponse = JSON.parse(document.querySelector('h2[data-inventory-version]').dataset.inventoryFormatSegments || '[]');

            if (!currentItemState.boundaries) {
                const singleInputRow = `<div class="d-flex align-items-center w-100"><span class="badge bg-secondary me-2" style="width: 120px;" title="This ID has no defined structure.">Unstructured</span><input type="text" class="form-control font-monospace" value="${escapeHtml(currentItemState.customId)}"></div>`;
                segmentationEditorEl.innerHTML = singleInputRow;
                return;
            };

            const boundaries = currentItemState.boundaries.split(',').map(Number);
            let currentIndex = 0;
            boundaries.forEach((len, i) => {
                const segmentText = currentItemState.customId.substring(currentIndex, currentIndex + len);
                const segmentInfo = segmentsResponse[i] || { type: 'Unknown' };
                const segmentType = segmentInfo.type.replace('Segment', '');

                const row = document.createElement('div');
                row.className = 'd-flex align-items-center w-100 mb-2';

                const badge = document.createElement('span');
                badge.className = 'badge bg-info me-2 text-truncate';
                badge.style.width = '120px';
                badge.textContent = segmentType;
                badge.title = `Segment Type: ${segmentType}`;

                const inputGroup = document.createElement('div');
                inputGroup.className = 'input-group input-group-sm';

                const segmentInput = document.createElement('input');
                segmentInput.type = 'text';
                segmentInput.className = 'form-control font-monospace';
                segmentInput.value = segmentText;
                segmentInput.dataset.index = i;

                let rulesText = '';
                if (segmentType === 'FixedText') {
                    segmentInput.readOnly = true;
                    rulesText = `Value: "${escapeHtml(String(segmentInfo.value))}"`;
                } else if (segmentType === 'Sequence') {
                    rulesText = `Start: ${segmentInfo.startValue}, Step: ${segmentInfo.step}, Pad: ${segmentInfo.padding}`;
                } else if (segmentType === 'Date') {
                    rulesText = `Format: "${escapeHtml(String(segmentInfo.format))}"`;
                } else if (segmentType === 'RandomNumbers' || segmentType === 'Guid') {
                    rulesText = `Format: "${escapeHtml(String(segmentInfo.format))}"`;
                }

                inputGroup.appendChild(segmentInput);

                if (rulesText) {
                    const rulesSpan = document.createElement('span');
                    rulesSpan.className = 'input-group-text bg-light';
                    rulesSpan.textContent = rulesText;
                    inputGroup.appendChild(rulesSpan);
                }

                row.appendChild(badge);
                row.appendChild(inputGroup);
                segmentationEditorEl.appendChild(row);
                currentIndex += len;
            });
        }

        async function fetchAndSetInitialId() {
            const version = parseInt(document.querySelector('h2[data-inventory-version]').dataset.inventoryVersion, 10);
            try {
                const response = await fetch(`/api/inventory/${inventoryId}/regenerate-id`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': csrfToken },
                    body: JSON.stringify({ inventoryVersion: version, lastKnownSequenceValue: null })
                });
                if (response.ok) {
                    const result = await response.json();
                    currentItemState.customId = result.id;
                    currentItemState.boundaries = result.boundaries;
                    currentItemState.nextSequenceValue = result.newSequenceValue;
                    document.getElementById('item-field-customId').value = result.id;
                } else {
                    showToast('Could not generate initial Item ID.', true);
                }
            } catch (error) {
                showToast('An error occurred while generating the Item ID.', true);
            }
        }

        async function openItemModal(itemData = null) {
            const customIdInput = document.getElementById('item-field-customId');
            const editSegmentBtn = document.getElementById('editSegmentBtn');
            const regenerateIdBtn = document.getElementById('regenerateIdBtn');
            const inventoryHash = document.querySelector('h2[data-inventory-version]').dataset.inventoryFormatHash;

            itemForm.reset();
            const validationErrorDivs = itemForm.querySelectorAll('.invalid-feedback, #customId-validation-error');
            validationErrorDivs.forEach(el => { el.textContent = ''; el.classList.add('d-none'); });
            itemForm.querySelectorAll('.is-invalid').forEach(el => el.classList.remove('is-invalid'));

            if (!inventoryHash) {
                customIdInput.value = '(Not configured)';
                editSegmentBtn.disabled = true;
                regenerateIdBtn.disabled = true;
            } else {
                editSegmentBtn.disabled = false;
                regenerateIdBtn.disabled = false;
            }

            if (itemData) {
                itemModalLabel.textContent = 'Edit Item';
                currentItemState.id = itemData.id;
                currentItemState.customId = itemData.customId;
                currentItemState.boundaries = itemData.customidsegmentboundaries;
                currentItemState.formatHash = itemData.customidformathashapplied;
                currentItemState.nextSequenceValue = null;

                customIdInput.value = currentItemState.customId;
                fullItemSchema.forEach(field => {
                    const input = itemForm.querySelector(`[name="${field.id}"]`);
                    if (input) {
                        const value = itemData[field.dataKey];
                        if (field.type === 'Bool') input.checked = value === true;
                        else input.value = value ?? '';
                    }
                });

                if (inventoryHash && currentItemState.formatHash !== inventoryHash) {
                    showToast('ID format has changed. Regenerating ID.', false);
                    await fetchAndSetInitialId();
                }
            } else {
                itemModalLabel.textContent = 'Add New Item';
                currentItemState = { id: '', customId: '', boundaries: '', formatHash: '', nextSequenceValue: null };
                if (inventoryHash) {
                    await fetchAndSetInitialId();
                }
            }
            itemModal.show();
        }

        async function saveItem() {
            const saveButton = itemForm.querySelector('button[type="submit"]');
            saveButton.disabled = true;

            const isEditing = !!currentItemState.id;
            const formData = new FormData(itemForm);
            const fieldValues = {};
            fullItemSchema.forEach(field => {
                if (field.type === 'Bool') fieldValues[field.id] = formData.has(field.id);
                else fieldValues[field.id] = formData.get(field.id);
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
                    const result = await response.json();
                    updateInventoryVersion(result.newInventoryVersion);
                    showToast(`Item ${isEditing ? 'updated' : 'added'} successfully.`);
                    itemModal.hide();
                    if (itemsDataTable) itemsDataTable.ajax.reload(null, false);
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
            } finally {
                saveButton.disabled = false;
            }
        }

        async function deleteSelectedItems() {
            const selectedCheckboxes = document.querySelectorAll('.item-checkbox:checked');
            const itemIds = Array.from(selectedCheckboxes).map(cb => cb.value);
            if (itemIds.length === 0) {
                showToast('Please select items to delete.', true);
                return;
            }
            try {
                const response = await fetch('/api/inventory/items/delete', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': csrfToken },
                    body: JSON.stringify(itemIds)
                });
                deleteItemsModal.hide();
                if (response.ok) {
                    const result = await response.json();
                    updateInventoryVersion(result.newVersion);
                    showToast('Selected items deleted.', false);
                    if (itemsDataTable) itemsDataTable.ajax.reload(null, false);
                } else if (response.status === 409) {
                    window.handleConcurrencyError();
                } else if (response.status === 403) {
                    showToast('Your permissions may have changed. Please reload the page.', true);
                } else { showToast('Failed to delete items.', true); }
            } catch (error) {
                showToast(`An error occurred while deleting items.`, true);
            }
        }

        async function handleRegenerateId() {
            const customIdInput = document.getElementById('item-field-customId');
            const version = parseInt(document.querySelector('h2[data-inventory-version]').dataset.inventoryVersion, 10);
            try {
                const response = await fetch(`/api/inventory/${inventoryId}/regenerate-id`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': csrfToken },
                    body: JSON.stringify({ inventoryVersion: version, lastKnownSequenceValue: currentItemState.nextSequenceValue })
                });
                if (response.ok) {
                    const result = await response.json();
                    currentItemState.customId = result.id;
                    currentItemState.boundaries = result.boundaries;
                    currentItemState.nextSequenceValue = result.newSequenceValue;
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
            const selectedCheckbox = document.querySelector('#itemsDataTable .item-checkbox:checked');
            if (!selectedCheckbox) return;
            const rowNode = selectedCheckbox.closest('tr');
            const rowData = $(rowNode).data('full-data');
            if (!rowData) { showToast('Could not find data for the selected item.', true); return; }
            openItemModal(rowData);
        });
        deleteSelectedItemsBtn.addEventListener('click', () => {
            if (document.querySelectorAll('#itemsDataTable .item-checkbox:checked').length === 0) {
                showToast('Please select items to delete.', true);
                return;
            }
            deleteItemsModal.show();
        });

        confirmDeleteItemsBtn.addEventListener('click', deleteSelectedItems);

        itemModalEl.addEventListener('click', function (event) {
            const regenerateBtn = event.target.closest('#regenerateIdBtn');
            const editSegmentBtn = event.target.closest('#editSegmentBtn');
            if (regenerateBtn) {
                handleRegenerateId();
            } else if (editSegmentBtn) {
                const h2 = document.querySelector('h2[data-inventory-version]');
                fetch(`/api/inventory/${inventoryId}/id-format`)
                    .then(res => res.json())
                    .then(segments => {
                        h2.dataset.inventoryFormatSegments = JSON.stringify(segments);
                        const errorDiv = segmentationModalEl.querySelector('.modal-body .text-danger');
                        if (errorDiv) errorDiv.remove();
                        buildSegmentationEditor();
                        segmentationModal.show();
                    })
                    .catch(() => showToast('Could not load ID format details.', true));
            }
        });

        async function validateAndApplySegmentation() {
            const inputs = Array.from(segmentationEditorEl.querySelectorAll('input[type="text"]'));
            const newIdParts = inputs.map(input => input.value);
            const newCustomId = newIdParts.join('');
            const newBoundaries = newIdParts.map(part => part.length).join(',');

            const payload = { customId: newCustomId, boundaries: newBoundaries };
            const confirmBtn = document.getElementById('confirmSegmentationBtn');
            confirmBtn.disabled = true;

            try {
                const response = await fetch(`/api/inventory/${inventoryId}/validate-id`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': csrfToken },
                    body: JSON.stringify(payload)
                });

                const errorDiv = segmentationModalEl.querySelector('.modal-body .text-danger');
                if (errorDiv) errorDiv.remove();

                if (response.ok) {
                    currentItemState.customId = newCustomId;
                    currentItemState.boundaries = newBoundaries;
                    currentItemState.nextSequenceValue = null;
                    document.getElementById('item-field-customId').value = newCustomId;
                    showToast('New ID is valid and has been applied.', false);
                    segmentationModal.hide();
                } else {
                    const errorResult = await response.json();
                    const msg = errorResult.message || 'Invalid ID structure.';
                    const errorHtml = `<div class="text-danger mt-2">${escapeHtml(msg)}</div>`;
                    segmentationEditorEl.insertAdjacentHTML('afterend', errorHtml);
                }
            } catch (e) {
                showToast('An error occurred during validation.', true);
            } finally {
                confirmBtn.disabled = false;
            }
        }

        confirmSegmentationBtn.addEventListener('click', validateAndApplySegmentation);

        itemForm.dataset.listenersAttached = 'true';
    }

    document.addEventListener('refreshItemsData', initializePage);
    initializePage();
}