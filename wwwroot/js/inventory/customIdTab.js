function initializeCustomIdTab(inventoryId, csrfToken) {
    const customIdTabEl = document.getElementById('customid-tab');
    const currentFormatList = document.getElementById('current-format-list');
    const availableSegmentsList = document.getElementById('available-segments-list');
    const previewSpan = document.getElementById('idPreview');
    const saveIdFormatBtn = document.getElementById('saveIdFormatBtn');
    const editSegmentModal = new bootstrap.Modal(document.getElementById('editSegmentModal'));
    const editSegmentModalLabel = document.getElementById('editSegmentModalLabel');
    const editSegmentModalBody = document.getElementById('editSegmentModalBody');
    const editSegmentForm = document.getElementById('editSegmentForm');
    const editSelectedSegmentBtn = document.getElementById('editSelectedSegmentBtn');
    const removeSelectedSegmentsBtn = document.getElementById('removeSelectedSegmentsBtn');
    const clearFormatBtn = document.getElementById('clearFormatBtn');
    const segmentTrash = document.getElementById('segment-trash');
    let currentIdFormat = [];
    let datePreviewDebounceTimer;
    let originalSegmentForCancel = null;
    const datePreviewCache = new Map();

    function debounce(func, delay) {
        return function (...args) {
            clearTimeout(datePreviewDebounceTimer);
            datePreviewDebounceTimer = setTimeout(() => {
                func.apply(this, args);
            }, delay);
        };
    }

    const updateDatePreviews = debounce(async (formatsToPreview) => {
        if (formatsToPreview.length === 0) return;
        const formatsToFetch = [...new Set(formatsToPreview.map(p => p.format))].filter(f => !datePreviewCache.has(f));
        if (formatsToFetch.length > 0) {
            try {
                const response = await fetch('/api/utils/date-previews', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(formatsToFetch)
                });
                if (!response.ok) throw new Error('API Error');
                const previews = await response.json();
                previews.forEach(p => datePreviewCache.set(p.format, p));
            } catch (error) {
                formatsToFetch.forEach(f => datePreviewCache.set(f, { preview: '[API Error]', isValid: false }));
            }
        }
        formatsToPreview.forEach(item => {
            const previewSpan = document.getElementById(item.spanId);
            if (previewSpan) {
                const result = datePreviewCache.get(item.format);
                if (result) {
                    previewSpan.textContent = result.preview;
                    previewSpan.classList.toggle('text-danger', !result.isValid);
                }
            }
        });
    }, 300);

    function generatePreview() {
        if (currentIdFormat.length === 0) {
            previewSpan.textContent = 'No format defined.';
            return;
        }
        let previewHtml = '';
        let formatsToFetch = [];
        const sequenceSegment = currentIdFormat.find(s => s.type === 'Sequence');
        const sequenceValue = sequenceSegment ? sequenceSegment.startValue : 0;
        currentIdFormat.forEach(segment => {
            const spanId = `preview-segment-${segment.id}`;
            switch (segment.type) {
                case 'FixedText':
                    previewHtml += `<span>${escapeHtml(segment.value)}</span>`;
                    break;
                case 'Sequence':
                    previewHtml += `<span>${escapeHtml(sequenceValue.toString().padStart(segment.padding, '0'))}</span>`;
                    break;
                case 'Date':
                    if (datePreviewCache.has(segment.format)) {
                        const result = datePreviewCache.get(segment.format);
                        const dangerClass = result.isValid ? '' : 'text-danger';
                        previewHtml += `<span id="${spanId}" class="${dangerClass}">${escapeHtml(result.preview)}</span>`;
                    } else {
                        previewHtml += `<span id="${spanId}">[Loading date...]</span>`;
                        formatsToFetch.push({ format: segment.format, spanId: spanId });
                    }
                    break;
                case 'RandomNumbers':
                    previewHtml += `<span>[${escapeHtml(segment.format)}]</span>`;
                    break;
                case 'Guid':
                    previewHtml += `<span>[GUID-${escapeHtml(segment.format)}]</span>`;
                    break;
            }
        });
        previewSpan.innerHTML = previewHtml;
        if (formatsToFetch.length > 0) {
            updateDatePreviews(formatsToFetch);
        }
    }

    function renderCurrentFormat() {
        currentFormatList.innerHTML = '';
        currentIdFormat.forEach(segment => {
            const el = document.createElement('div');
            el.className = 'list-group-item d-flex align-items-center';
            el.dataset.id = segment.id;
            let details = Object.entries(segment).filter(([k]) => k !== 'id' && k !== 'type').map(([k, v]) => `${k}: ${v}`).join(', ');
            el.innerHTML = `
                <input class="form-check-input me-3 segment-checkbox" type="checkbox" value="${segment.id}">
                <div class="flex-grow-1">
                    <strong class="d-block">${segment.type}</strong>
                    <small class="text-muted">${details}</small>
                </div>`;
            currentFormatList.appendChild(el);
        });
        generatePreview();
        updateSegmentToolbar();
    }

    async function fetchIdFormat() {
        try {
            const response = await fetch(`/api/inventory/${inventoryId}/id-format`);
            if (!response.ok) {
                showToast('Failed to load ID format.', true);
                return;
            }
            currentIdFormat = await response.json();
            renderCurrentFormat();
        } catch (error) {
            showToast('An error occurred while fetching the ID format.', true);
        }
    }

    async function saveIdFormat() {
        try {
            const response = await fetch(`/api/inventory/${inventoryId}/id-format`, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': csrfToken },
                body: JSON.stringify(currentIdFormat)
            });
            if (response.ok) {
                const result = await response.json();
                updateInventoryVersion(result.newVersion);
                const h2 = document.querySelector('h2[data-inventory-version]');
                if (h2) {
                    h2.dataset.inventoryFormatHash = result.newHash || '';
                }
                showToast('ID format saved successfully.');
            } else if (response.status === 409) {
                window.handleConcurrencyError();
            } else {
                showToast('Failed to save ID format.', true);
            }
        } catch (error) {
            showToast('An error occurred while saving the ID format.', true);
        }
    }

    function openEditSegmentModal(segmentId) {
        const segment = currentIdFormat.find(s => s.id === segmentId);
        if (!segment) return;

        originalSegmentForCancel = JSON.parse(JSON.stringify(segment));
        document.getElementById('segmentId').value = segmentId;
        editSegmentModalLabel.textContent = `Edit ${segment.type} Segment`;
        let formHtml = '';

        switch (segment.type) {
            case 'FixedText':
                formHtml = `<div class="mb-3"><label for="prop-value" class="form-label">Text Value</label><input type="text" class="form-control" id="prop-value" name="value" value="${escapeHtml(String(segment.value))}" required></div>`;
                break;
            case 'Sequence':
                formHtml = `<div class="mb-3"><label for="prop-startValue" class="form-label">Start Value</label><input type="number" class="form-control" id="prop-startValue" name="startValue" value="${segment.startValue}" min="0" required></div>
                            <div class="mb-3"><label for="prop-step" class="form-label">Step</label><input type="number" class="form-control" id="prop-step" name="step" value="${segment.step}" min="1" required></div>
                            <div class="mb-3"><label for="prop-padding" class="form-label">Padding (Minimum digits)</label><input type="number" class="form-control" id="prop-padding" name="padding" value="${segment.padding}" min="1" max="20" required></div>`;
                break;
            case 'Date':
                formHtml = `<div class="mb-3"><label for="prop-format" class="form-label">Date Format</label><input type="text" class="form-control" id="prop-format" name="format" value="${escapeHtml(String(segment.format))}" required><small class="form-text text-muted">e.g., yyyy-MM-dd, MMddyy, HH:mm</small></div>`;
                break;
            case 'RandomNumbers':
                const randomFormats = ['9-digit', '6-digit', '32-bit', '20-bit'];
                let randomOptions = randomFormats.map(f => `<option value="${f}" ${segment.format === f ? 'selected' : ''}>${f}</option>`).join('');
                formHtml = `<div class="mb-3"><label for="prop-format" class="form-label">Format</label><select class="form-select" id="prop-format" name="format">${randomOptions}</select></div>`;
                break;
            case 'Guid':
                const formats = ['N', 'D', 'B', 'P'];
                let options = formats.map(f => `<option value="${f}" ${segment.format === f ? 'selected' : ''}>${f}</option>`).join('');
                formHtml = `<div class="mb-3"><label for="prop-format" class="form-label">Format</label><select class="form-select" id="prop-format" name="format">${options}</select></div>`;
                break;
        }
        editSegmentModalBody.innerHTML = formHtml;
        editSegmentModal.show();
    }

    function createDefaultSegment(type) {
        const base = { id: `client-${Math.random().toString(36).slice(2)}`, type: type };
        switch (type) {
            case 'FixedText': return { ...base, value: '-' };
            case 'Sequence': return { ...base, startValue: 1, step: 1, padding: 4 };
            case 'Date': return { ...base, format: 'yyyy-MM-dd' };
            case 'RandomNumbers': return { ...base, format: '9-digit' };
            case 'Guid': return { ...base, format: 'N' };
            default: return null;
        }
    }

    function updateSegmentToolbar() {
        const selectedCount = document.querySelectorAll('.segment-checkbox:checked').length;
        editSelectedSegmentBtn.disabled = selectedCount !== 1;
        removeSelectedSegmentsBtn.disabled = selectedCount === 0;
    }

    saveIdFormatBtn.addEventListener('click', saveIdFormat);
    clearFormatBtn.addEventListener('click', () => { currentIdFormat = []; renderCurrentFormat(); });
    removeSelectedSegmentsBtn.addEventListener('click', () => {
        const selectedIds = Array.from(document.querySelectorAll('.segment-checkbox:checked')).map(cb => cb.value);
        currentIdFormat = currentIdFormat.filter(s => !selectedIds.includes(s.id));
        renderCurrentFormat();
    });
    editSelectedSegmentBtn.addEventListener('click', () => {
        const selectedId = document.querySelector('.segment-checkbox:checked').value;
        openEditSegmentModal(selectedId);
    });
    currentFormatList.addEventListener('change', e => { if (e.target.classList.contains('segment-checkbox')) updateSegmentToolbar(); });

    new Sortable(availableSegmentsList, {
        group: { name: 'segments', pull: 'clone', put: false }, sort: false, animation: 150
    });
    new Sortable(currentFormatList, {
        group: 'segments', animation: 150,
        onAdd: evt => {
            if (evt.from.id === 'segment-trash') { evt.item.remove(); return; }
            const newSegment = createDefaultSegment(evt.item.dataset.segmentType);
            evt.item.remove();
            if (newSegment) {
                currentIdFormat.splice(evt.newIndex, 0, newSegment);
                renderCurrentFormat();
                openEditSegmentModal(newSegment.id);
            }
        },
        onEnd: evt => {
            if (evt.to.id === 'current-format-list') {
                const [movedItem] = currentIdFormat.splice(evt.oldIndex, 1);
                currentIdFormat.splice(evt.newIndex, 0, movedItem);
                renderCurrentFormat();
            }
        }
    });
    new Sortable(segmentTrash, {
        group: 'segments', animation: 150,
        onAdd: evt => {
            const segmentId = evt.item.dataset.id;
            evt.item.remove();
            currentIdFormat = currentIdFormat.filter(s => s.id !== segmentId);
            renderCurrentFormat();
        }
    });

    editSegmentForm.addEventListener('submit', function (e) {
        e.preventDefault();
        const segmentId = document.getElementById('segmentId').value;
        const segmentIndex = currentIdFormat.findIndex(s => s.id === segmentId);
        if (segmentIndex === -1) return;
        const segment = currentIdFormat[segmentIndex];
        const formData = new FormData(editSegmentForm);
        switch (segment.type) {
            case 'FixedText': segment.value = formData.get('value'); break;
            case 'Sequence':
                segment.startValue = parseInt(formData.get('startValue'), 10);
                segment.step = parseInt(formData.get('step'), 10);
                segment.padding = parseInt(formData.get('padding'), 10);
                break;
            case 'Date': segment.format = formData.get('format'); break;
            case 'RandomNumbers': segment.format = formData.get('format'); break;
            case 'Guid': segment.format = formData.get('format'); break;
        }
        originalSegmentForCancel = null;
        editSegmentModal.hide();
        renderCurrentFormat();
    });

    document.getElementById('editSegmentModal').addEventListener('hide.bs.modal', () => {
        if (originalSegmentForCancel) {
            const segmentIndex = currentIdFormat.findIndex(s => s.id === originalSegmentForCancel.id);
            if (segmentIndex !== -1) {
                currentIdFormat[segmentIndex] = originalSegmentForCancel;
            }
            originalSegmentForCancel = null;
            renderCurrentFormat();
        }
    });

    customIdTabEl.addEventListener('show.bs.tab', fetchIdFormat, { once: true });
}