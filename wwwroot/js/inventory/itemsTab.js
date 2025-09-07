function initializeItemsTab(inventoryId, csrfToken, canWrite) {
    if (document.getElementById('itemsDataTable')?.getAttribute('data-initialized')) return;
    document.getElementById('itemsDataTable')?.setAttribute('data-initialized', 'true');

    // --- CONSTANTS ---
    const itemsTableHead = document.getElementById('items-table-head');
    const itemsTableBody = document.getElementById('items-table-body');
    const viewContentModal = new bootstrap.Modal(document.getElementById('viewContentModal'));
    const viewContentModalLabel = document.getElementById('viewContentModalLabel');
    const viewContentModalBody = document.getElementById('viewContentModalBody');
    let itemSchema = [];
    let itemsDataTable = null;

    // --- FUNCTION DEFINITIONS ---
    async function fetchItemsAndSchema() {
        const response = await fetch(`/api/inventory/${inventoryId}/items-data`);
        if (!response.ok) {
            showToast('Failed to load items.', true);
            return;
        }
        const data = await response.json();
        itemSchema = data.fields;

        buildItemModalForm(itemSchema);
        renderItemsUI(itemSchema, data.items);
    }

    function convertItemToRowDataArray(item, fields) {
        // This object will be passed to the 'Created At' column's render function.
        const createdAtData = {
            display: new Date(item.createdAt).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' }),
            sort: new Date(item.createdAt).getTime()
        };

        // This object will be passed to the 'Item ID' column's render function.
        const idCellData = {
            id: item.id,
            display: item.customId ? (item.customId.length > 12 ? `${item.customId.substring(0, 12)}...` : item.customId) : `${item.id.substring(0, 8)}...`,
            full: item.customId || `System ID: ${item.id}`,
            isCustom: !!item.customId
        };

        const rowData = [
            idCellData, // Checkbox column will use the ID from this object
            idCellData,
            createdAtData
        ];

        fields.forEach(f => {
            rowData.push(item.fields[f.targetColumn] ?? '');
        });

        return rowData;
    }

    function renderItemsUI(fields, items) {
        if (itemsDataTable) {
            itemsDataTable.destroy();
        }

        let headersHtml = `<tr><th style="width: 50px;"><input class="form-check-input" type="checkbox" id="selectAllItemsCheckbox" /></th>
                               <th>Item ID</th>
                               <th style="width: 180px;">Created At</th>`;

        fields.forEach(f => {
            const fieldName = escapeHtml(f.name);
            const headerText = fieldName.length > 35 ? `${fieldName.substring(0, 32)}<span class="unselectable">...</span>` : fieldName;
            headersHtml += `<th title="${fieldName}">${headerText}</th>`;
        });
        itemsTableHead.innerHTML = headersHtml + `</tr>`;
        itemsTableBody.innerHTML = '';

        const tableData = items.map(item => convertItemToRowDataArray(item, fields));

        itemsDataTable = new DataTable('#itemsDataTable', {
            data: tableData,
            order: [[2, 'desc']],
            columnDefs: [
                { // Checkbox
                    targets: 0, orderable: false,
                    render: (data) => `<input class="form-check-input item-checkbox" type="checkbox" name="selectedItemIds" value="${escapeHtml(data.id)}" />`
                },
                { // Item ID
                    targets: 1,
                    render: (data) => {
                        const classes = data.isCustom ? 'font-monospace' : 'font-monospace text-muted';
                        return `<div class="expandable-cell" data-field-name="Item ID" data-content="${escapeHtml(data.full)}"><span class="truncated-text ${classes}" title="${escapeHtml(data.full)}">${escapeHtml(data.display)}</span></div>`;
                    }
                },
                { // Created At
                    targets: 2,
                    render: (data, type) => type === 'sort' ? data.sort : data.display
                },
                ...fields.map((f, i) => ({
                    targets: i + 3,
                    render: function (data, type, row) {
                        let display = '';
                        const stringValue = String(data ?? '');
                        let cellClass = '';

                        if (f.type === 'Bool') {
                            display = data === true ? '✔️' : '❌';
                        } else if (stringValue.length > 35) {
                            display = `<span class="truncated-text">${escapeHtml(stringValue.substring(0, 32))}<span class="unselectable">...</span></span>`;
                            // This class enables the modal popup for long text.
                            cellClass = 'expandable-cell';
                        } else {
                            display = escapeHtml(stringValue);
                        }

                        // This wrapper div with data attributes is essential.
                        return `<div class="${cellClass}" data-field-name="${escapeHtml(f.name)}" data-content="${escapeHtml(stringValue)}">${display}</div>`;
                    }
                }))
            ]
        });
    }

    // --- EVENT LISTENERS ---
    const dtElement = document.getElementById('itemsDataTable');

    if (dtElement) {
        dtElement.addEventListener('click', e => {
            if (e.target.closest('.expandable-cell')) {
                const cell = e.target.closest('.expandable-cell');
                viewContentModalLabel.textContent = cell.dataset.fieldName;
                viewContentModalBody.textContent = cell.dataset.content;
                viewContentModal.show();
            }
        });
    }

    if (canWrite) {
        // --- CONSTANTS (Write Permissions Only) ---)
        const itemForm = document.getElementById('itemForm');
        const itemModalEl = document.getElementById('itemModal');
        const itemModal = new bootstrap.Modal(itemModalEl);
        const itemModalLabel = document.getElementById('itemModalLabel');
        const itemIdInput = document.getElementById('itemId');
        const editSelectedItemBtn = document.getElementById('editSelectedItemBtn');
        const deleteSelectedItemsBtn = document.getElementById('deleteSelectedItemsBtn');
        const deleteItemsModal = new bootstrap.Modal(document.getElementById('deleteItemsConfirmModal'));
        const confirmDeleteItemsBtn = document.getElementById('confirmDeleteItemsBtn');

        // --- FUNCTIONS (Write Permissions Only) ---
        function buildItemModalForm(fields) {
            const itemModalBody = document.querySelector('#itemModal .modal-body');
            let formHtml = '';
            fields.forEach(field => {
                formHtml += `<div class="mb-3"><label for="item-field-${field.id}" class="form-label">${escapeHtml(field.name)}</label>`;
                switch (field.type) {
                    case 'Text':
                        formHtml += `<textarea class="form-control" id="item-field-${field.id}" name="${field.id}"></textarea>`;
                        break;
                    case 'Bool':
                        formHtml += `<div class="form-check form-switch"><input class="form-check-input" type="checkbox" id="item-field-${field.id}" name="${field.id}"></div>`;
                        break;
                    case 'Numeric':
                        formHtml += `<input type="number" step="any" class="form-control" id="item-field-${field.id}" name="${field.id}">`;
                        break;
                    default: // String, FileUrl
                        formHtml += `<input type="text" class="form-control" id="item-field-${field.id}" name="${field.id}">`;
                }
                formHtml += `</div>`;
            });
            itemModalBody.innerHTML = formHtml;
        }

        function openItemModal(itemData = null) {
            itemForm.reset();
            itemForm.querySelectorAll('.is-invalid').forEach(el => el.classList.remove('is-invalid'));
            itemForm.querySelectorAll('.invalid-feedback').forEach(el => el.remove());

            if (itemData) { // Edit Mode
                itemModalLabel.textContent = 'Edit Item';
                itemIdInput.value = itemData.id;

                itemSchema.forEach(field => {
                    const input = itemForm.querySelector(`[name="${field.id}"]`);
                    if (input) {
                        const value = itemData.fields[field.targetColumn];
                        if (field.type === 'Bool') {
                            input.checked = value === true;
                        } else {
                            input.value = value ?? '';
                        }
                    }
                });
            } else { // Add Mode
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
            itemSchema.forEach(field => {
                if (field.type === 'Bool') {
                    fieldValues[field.id] = formData.has(field.id);
                } else {
                    fieldValues[field.id] = formData.get(field.id);
                }
            });

            const apiUrl = isEditing ? `/api/inventory/items/${itemId}` : `/api/inventory/${inventoryId}/items`;

            const response = await fetch(apiUrl, {
                method: isEditing ? 'PUT' : 'POST',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': csrfToken },
                body: JSON.stringify({ fieldValues: fieldValues })
            });

            if (response.ok) {
                showToast(`Item ${isEditing ? 'updated' : 'added'} successfully.`);
                itemModal.hide();

                const contentLength = response.headers.get('content-length');
                if (contentLength && parseInt(contentLength, 10) > 0) {
                    const createdItem = await response.json();
                    if (itemsDataTable) {
                        const newRowData = convertItemToRowDataArray(createdItem, itemSchema);
                        itemsDataTable.row.add(newRowData).draw();
                    }
                } else {
                    fetchItemsAndSchema();
                }
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
            } else {
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
            if (response.ok) { showToast('Selected items deleted.', true); fetchItemsAndSchema(); }
            else { showToast('Failed to delete items.', true); }
        }

        const selectionChangeHandler = () => {
            const selectedCount = document.querySelectorAll('.item-checkbox:checked').length;
            if (selectedCount === 1) {
                editSelectedItemBtn.disabled = false;
                editSelectedItemBtn.classList.remove('btn-outline-secondary');
                editSelectedItemBtn.classList.add('btn-secondary'); // A more vibrant, solid color
            } else {
                editSelectedItemBtn.disabled = true;
                editSelectedItemBtn.classList.remove('btn-secondary');
                editSelectedItemBtn.classList.add('btn-outline-secondary');
            }
        };

        // --- EVENT LISTENERS (Write Permissions Only) ---
        document.querySelector('button[data-bs-target="#itemModal"]').addEventListener('click', () => openItemModal());

        itemForm.addEventListener('submit', (e) => { e.preventDefault(); saveItem(); });

        editSelectedItemBtn.addEventListener('click', async () => {
            const selectedCheckbox = document.querySelector('.item-checkbox:checked');
            if (!selectedCheckbox) return;
            try {
                const response = await fetch(`/api/inventory/items/${selectedCheckbox.value}`);
                if (!response.ok) throw new Error('Error fetching item details.');
                const itemData = await response.json();
                openItemModal(itemData);
            } catch (error) { showToast(error.message, true); }
        });

        deleteSelectedItemsBtn.addEventListener('click', () => {
            if (document.querySelectorAll('.item-checkbox:checked').length === 0) {
                showToast('Please select items to delete.', true);
                return;
            }
            deleteItemsModal.show();
        });

        confirmDeleteItemsBtn.addEventListener('click', deleteSelectedItems);

        if (dtElement) {
            dtElement.addEventListener('change', (e) => {
                if (e.target.id === 'selectAllItemsCheckbox') {
                    dtElement.querySelectorAll('.item-checkbox').forEach(cb => cb.checked = e.target.checked);
                }
                if (e.target.matches('.item-checkbox, #selectAllItemsCheckbox')) {
                    selectionChangeHandler();
                }
            });
        }
    }

    document.addEventListener('refreshItemsData', function () {
        fetchItemsAndSchema();
    });

    // --- INITIALIZATION ---
    fetchItemsAndSchema();
}