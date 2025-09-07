function initializeItemsTab(inventoryId, csrfToken, canWrite) {
    if (document.getElementById('itemsDataTable')?.getAttribute('data-initialized')) return;
    document.getElementById('itemsDataTable')?.setAttribute('data-initialized', 'true');

    const itemsTable = document.getElementById('itemsDataTable');
    const itemsTableHead = document.getElementById('items-table-head');
    const itemsTableBody = document.getElementById('items-table-body');
    const viewContentModal = new bootstrap.Modal(document.getElementById('viewContentModal'));
    const viewContentModalLabel = document.getElementById('viewContentModalLabel');
    const viewContentModalBody = document.getElementById('viewContentModalBody');
    let itemsDataTable = null;
    let localItemSchema = [];
    let dtColumnSchema = []; 

    function destroyDataTable() {
        if (itemsDataTable) {
            itemsDataTable.destroy();
            itemsDataTable = null;
        }
        itemsTableHead.innerHTML = '';
        itemsTableBody.innerHTML = '';
    }

    async function initializePage() {
        try {
            const schemaResponse = await fetch(`/api/inventory/${inventoryId}/schema`);
            if (!schemaResponse.ok) throw new Error('Failed to load inventory schema.');

            const schemaData = await schemaResponse.json();
            dtColumnSchema = schemaData.columns;

            if (canWrite) {
                localItemSchema = dtColumnSchema
                    .filter(c => !c.fieldId.startsWith('system_'))
                    .map(c => ({
                        id: c.fieldId,
                        name: c.title,
                        type: c.type,
                        dataKey: c.data.toLowerCase()
                    }));
                buildItemModalForm(localItemSchema);
            }
            initializeDataTable(dtColumnSchema);

        } catch (error) {
            showToast(error.message, true);
            itemsTableHead.innerHTML = '<tr><th>Error</th></tr>';
        }
    }

    function initializeDataTable(columnsSchema) {
        destroyDataTable();

        const headersHtml = `<tr>` + columnsSchema.map(col => {
            const fieldName = escapeHtml(col.title);
            if (fieldName.length > 35) {
                return `<th title="${fieldName}">${fieldName.substring(0, 32)}<span class="unselectable">...</span></th>`;
            }
            return `<th>${fieldName}</th>`;
        }).join('') + `</tr>`;

        itemsTableHead.innerHTML = headersHtml.replace('<th></th>', '<th style="width: 50px;"><input class="form-check-input" type="checkbox" id="selectAllItemsCheckbox" /></th>');

        itemsDataTable = new DataTable('#itemsDataTable', {
            "processing": true,
            "serverSide": true,
            "ajax": {
                "url": `/api/inventory/${inventoryId}/items-data`,
                "type": "POST",
                "dataType": "json",
                "error": function () {
                    if ($.fn.DataTable.isDataTable('#itemsDataTable')) {
                        showToast('An error occurred while loading item data.', true);
                    }
                }
            },
            "columns": columnsSchema.map(c => ({ "data": c.data })),
            "order": [[2, 'desc']],
            "columnDefs": [
                {
                    targets: 0,
                    orderable: false,
                    render: (data) => `<input class="form-check-input item-checkbox" type="checkbox" name="selectedItemIds" value="${escapeHtml(data)}" />`
                },
                {
                    targets: 1,
                    render: (data, type, row) => {
                        const fullId = data || `System ID: ${row.id}`;
                        const displayId = data ? (data.length > 12 ? `${data.substring(0, 12)}...` : data) : `${row.id.substring(0, 8)}...`;
                        const classes = data ? 'font-monospace' : 'font-monospace text-muted';
                        return `<div class="expandable-cell" data-field-name="Item ID" data-content="${escapeHtml(fullId)}"><span class="truncated-text ${classes}" title="${escapeHtml(fullId)}">${escapeHtml(displayId)}</span></div>`;
                    }
                },
                {
                    targets: 2,
                    render: (data) => new Date(data).toLocaleString()
                },
                ...columnsSchema.slice(3).map((col, i) => ({
                    targets: i + 3,
                    render: function (data) {
                        const stringValue = String(data ?? '');
                        if (col.type === 'Bool') {
                            return data === true ? '✔️' : '❌';
                        }
                        if (stringValue.length > 35) {
                            return `<div class="expandable-cell" data-field-name="${escapeHtml(col.title)}" data-content="${escapeHtml(stringValue)}"><span class="truncated-text">${escapeHtml(stringValue.substring(0, 32))}<span class="unselectable">...</span></span></div>`;
                        }
                        return escapeHtml(stringValue);
                    }
                }))
            ]
        });
    }

    function buildItemModalForm(fields) {
        if (!canWrite) return;
        const itemModalBody = document.querySelector('#itemModal .modal-body');
        if (!itemModalBody) return;
        let formHtml = '';
        fields.forEach(field => {
            formHtml += `<div class="mb-3"><label for="item-field-${field.id}" class="form-label">${escapeHtml(field.name)}</label>`;
            switch (field.type) {
                case 'Text': formHtml += `<textarea class="form-control" id="item-field-${field.id}" name="${field.id}"></textarea>`; break;
                case 'Bool': formHtml += `<div class="form-check form-switch"><input class="form-check-input" type="checkbox" id="item-field-${field.id}" name="${field.id}"></div>`; break;
                case 'Numeric': formHtml += `<input type="number" step="any" class="form-control" id="item-field-${field.id}" name="${field.id}">`; break;
                default: formHtml += `<input type="text" class="form-control" id="item-field-${field.id}" name="${field.id}">`;
            }
            formHtml += `</div>`;
        });
        itemModalBody.innerHTML = formHtml;
    }

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
        const itemIdInput = document.getElementById('itemId');
        const editSelectedItemBtn = document.getElementById('editSelectedItemBtn');
        const deleteSelectedItemsBtn = document.getElementById('deleteSelectedItemsBtn');
        const deleteItemsModal = new bootstrap.Modal(document.getElementById('deleteItemsConfirmModal'));
        const confirmDeleteItemsBtn = document.getElementById('confirmDeleteItemsBtn');

        function openItemModal(itemData = null) {
            itemForm.reset();
            itemForm.querySelectorAll('.is-invalid').forEach(el => el.classList.remove('is-invalid'));
            itemForm.querySelectorAll('.invalid-feedback').forEach(el => el.remove());

            if (itemData) {
                itemModalLabel.textContent = 'Edit Item';
                itemIdInput.value = itemData.id;
                localItemSchema.forEach(field => {
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
            } else {
                itemModalLabel.textContent = 'Add New Item';
                itemIdInput.value = '';
            }
            itemModal.show();
        }

        async function saveItem() {
            const itemId = itemIdInput.value;
            const isEditing = !!itemId;
            const formData = new FormData(itemForm);
            const fieldValues = {};
            localItemSchema.forEach(field => {
                if (field.type === 'Bool') {
                    fieldValues[field.id] = formData.has(field.id);
                } else {
                    fieldValues[field.id] = formData.get(field.id);
                }
            });

            const apiUrl = isEditing ? `/api/inventory/items/${itemId}` : `/api/inventory/${inventoryId}/items`;

            const response = await fetch(apiUrl, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': csrfToken },
                body: JSON.stringify({ fieldValues: fieldValues })
            });

            if (response.ok) {
                showToast(`Item ${isEditing ? 'updated' : 'added'} successfully.`);
                itemModal.hide();

                const result = await response.json();
                updateInventoryVersion(result.newInventoryVersion);

                itemsDataTable.ajax.reload(null, false);
            }
            else if (response.status === 400) {
                const errors = await response.json();
                itemForm.querySelectorAll('.is-invalid').forEach(el => el.classList.remove('is-invalid'));
                itemForm.querySelectorAll('.invalid-feedback').forEach(el => el.remove());
                for (const fieldId in errors) {
                    const errorMessage = errors[fieldId];
                    const input = itemForm.querySelector(`[name="${fieldId}"]`);
                    if (input) {
                        input.classList.add('is-invalid');
                        const errorDiv = document.createElement('div');
                        errorDiv.className = 'invalid-feedback';
                        errorDiv.textContent = errorMessage;
                        input.parentElement.appendChild(errorDiv);
                    }
                }
                showToast('Please correct the highlighted errors.', true);
            } else if (response.status === 403) {
                showToast('Your permissions may have changed. Please reload the page.', true);
                itemModal.hide();
            }
             else {
                showToast(`Failed to ${isEditing ? 'update' : 'add'} item.`, true);
            }
        }

        async function deleteSelectedItems() {
            const selectedCheckboxes = document.querySelectorAll('.item-checkbox:checked');
            const itemIds = Array.from(selectedCheckboxes).map(cb => cb.value);
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
            }
            else if (response.status === 403) {
                showToast('Your permissions may have changed. Please reload the page.', true);
            }
            else { showToast('Failed to delete items.', true); }
        }

        const selectionChangeHandler = () => {
            const selectedCount = document.querySelectorAll('.item-checkbox:checked').length;
            editSelectedItemBtn.disabled = selectedCount !== 1;
            if (selectedCount === 1) {
                editSelectedItemBtn.classList.replace('btn-outline-secondary', 'btn-secondary');
            } else {
                editSelectedItemBtn.classList.replace('btn-secondary', 'btn-outline-secondary');
            }
        };

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