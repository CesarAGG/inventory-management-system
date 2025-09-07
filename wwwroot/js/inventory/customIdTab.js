function initializeCustomIdTab(inventoryId, csrfToken) {
    // --- CONSTANT DECLARATIONS ---
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
    let currentIdFormat = [];
    let datePreviewDebounceTimer;
    let originalSegmentForCancel = null;
    const datePreviewCache = new Map();

    // --- FUNCTION DEFINITIONS ---
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

        const formatsToFetch = [...new Set(formatsToPreview.map(p => p.format))]
            .filter(f => !datePreviewCache.has(f));

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
        if (currentIdFormat.length === 0) { previewSpan.innerHTML = 'No format defined.'; return; }
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
                    previewHtml += `<span>${escapeHtml('1'.repeat(segment.length))}</span>`;
                    break;
                case 'Guid':
                    previewHtml += `<span>a1b2c3d4e5f6...</span>`;
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
        initializePopovers();
    }

    async function fetchIdFormat() {
        const response = await fetch(`/api/inventory/${inventoryId}/id-format`);
        if (!response.ok) {
            showToast('Failed to load ID format.', true);
            return;
        }
        currentIdFormat = await response.json();
        renderCurrentFormat();
    }

    async function saveIdFormat() {
        const response = await fetch(`/api/inventory/${inventoryId}/id-format`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': csrfToken },
            body: JSON.stringify(currentIdFormat)
        });
        if (response.ok) { showToast('ID format saved successfully.'); }
        else { showToast('Failed to save ID format.', true); }
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
                                    <div class="mb-3"><label for="prop-padding" class="form-label">Padding (Number of digits)</label><input type="number" class="form-control" id="prop-padding" name="padding" value="${segment.padding}" min="1" max="20" required></div>`;
                break;
            case 'Date':
                formHtml = `<div class="mb-3"><label for="prop-format" class="form-label">Date Format</label><input type="text" class="form-control" id="prop-format" name="format" value="${escapeHtml(String(segment.format))}" required><small class="form-text text-muted">e.g., yyyy-MM-dd, MMddyy, HH:mm</small></div>`;
                break;
            case 'RandomNumbers':
                formHtml = `<div class="mb-3"><label for="prop-length" class="form-label">Length</label><input type="number" class="form-control" id="prop-length" name="length" value="${segment.length}" min="1" max="20" required></div>`;
                break;
            case 'Guid':
                const formats = ['N', 'D', 'B', 'P'];
                let options = formats.map(f => `<option value="${f}" ${segment.format === f ? 'selected' : ''}>${f}</option>`).join('');
                formHtml = `<div class="mb-3"><label for="prop-format" class="form-label">Format</label><select class="form-select" id="prop-format" name="format">${options}</select></div>`;
                break;
        }

        editSegmentModalBody.innerHTML = formHtml;

        if (segment.type === 'Date') {
            const formatInput = editSegmentModalBody.querySelector('#prop-format');
            formatInput.addEventListener('input', () => {
                const segmentToUpdate = currentIdFormat.find(s => s.id === segmentId);
                if (segmentToUpdate) {
                    segmentToUpdate.format = formatInput.value;
                    generatePreview();
                }
            });
        }

        editSegmentModal.show();
    }

    function createDefaultSegment(type) {
        const base = { id: `client-${Math.random().toString(36).slice(2)}`, type: type };
        switch (type) {
            case 'FixedText': return { ...base, value: '-' };
            case 'Sequence': return { ...base, startValue: 1, step: 1, padding: 4 };
            case 'Date': return { ...base, format: 'yyyy-MM-dd' };
            case 'RandomNumbers': return { ...base, length: 4 };
            case 'Guid': return { ...base, format: 'N' };
            default: return null;
        }
    }

    function updateSegmentToolbar() {
        const selectedCount = document.querySelectorAll('.segment-checkbox:checked').length;
        editSelectedSegmentBtn.disabled = selectedCount !== 1;
        removeSelectedSegmentsBtn.disabled = selectedCount === 0;
    }

    // --- EVENT LISTENERS ---
    customIdTabEl.addEventListener('show.bs.tab', () => {
        if (currentFormatList.innerHTML.trim() === '') {
            fetchIdFormat();
        }
    });

    saveIdFormatBtn.addEventListener('click', saveIdFormat);

    clearFormatBtn.addEventListener('click', () => {
        currentIdFormat = [];
        renderCurrentFormat();
    });

    removeSelectedSegmentsBtn.addEventListener('click', () => {
        const selectedIds = Array.from(document.querySelectorAll('.segment-checkbox:checked')).map(cb => cb.value);
        currentIdFormat = currentIdFormat.filter(s => !selectedIds.includes(s.id));
        renderCurrentFormat();
    });

    editSelectedSegmentBtn.addEventListener('click', () => {
        const selectedId = document.querySelector('.segment-checkbox:checked').value;
        openEditSegmentModal(selectedId);
    });

    currentFormatList.addEventListener('change', e => {
        if (e.target.classList.contains('segment-checkbox')) {
            updateSegmentToolbar();
        }
    });

    currentFormatList.addEventListener('click', e => {
        const segmentId = e.target.closest('.list-group-item')?.dataset.id;
        if (!segmentId) return;
        if (e.target.classList.contains('remove-segment-btn')) {
            currentIdFormat = currentIdFormat.filter(s => s.id !== segmentId);
            renderCurrentFormat();
        }
        if (e.target.classList.contains('edit-segment-btn')) openEditSegmentModal(segmentId);
    });

    new Sortable(availableSegmentsList, { group: { name: 'segments', pull: 'clone', put: false }, sort: false, animation: 150 });

    new Sortable(currentFormatList, {
        group: 'segments', animation: 150,
        onAdd: evt => {
            const newSegment = createDefaultSegment(evt.item.dataset.segmentType);
            evt.item.remove(); // Remove the placeholder clone immediately
            if (newSegment) {
                currentIdFormat.splice(evt.newIndex, 0, newSegment);
                renderCurrentFormat();
                openEditSegmentModal(newSegment.id); // Automatically open the edit modal
            }
        },
        onEnd: evt => {
            const [movedItem] = currentIdFormat.splice(evt.oldIndex, 1);
            currentIdFormat.splice(evt.newIndex, 0, movedItem);
            renderCurrentFormat();
        }
    });

    editSegmentForm.addEventListener('submit', function (e) {
        e.preventDefault();
        clearTimeout(datePreviewDebounceTimer); // Prevent unnecessary API call
        const segmentId = document.getElementById('segmentId').value;
        const segmentIndex = currentIdFormat.findIndex(s => s.id === segmentId);
        if (segmentIndex === -1) return;

        const segment = currentIdFormat[segmentIndex];
        const formData = new FormData(editSegmentForm);

        // Explicitly update properties based on segment type
        switch (segment.type) {
            case 'FixedText':
                segment.value = formData.get('value');
                break;
            case 'Sequence':
                segment.startValue = parseInt(formData.get('startValue'), 10);
                segment.step = parseInt(formData.get('step'), 10);
                segment.padding = parseInt(formData.get('padding'), 10);
                break;
            case 'Date':
                segment.format = formData.get('format');
                break;
            case 'RandomNumbers':
                segment.length = parseInt(formData.get('length'), 10);
                break;
            case 'Guid':
                segment.format = formData.get('format');
                break;
        }

        originalSegmentForCancel = null; // Signal that the save was successful
        editSegmentModal.hide();
        renderCurrentFormat();
    });

    document.getElementById('editSegmentModal').addEventListener('hide.bs.modal', () => {
        if (originalSegmentForCancel) {
            // If this object still exists, it means save was not clicked.
            const segmentIndex = currentIdFormat.findIndex(s => s.id === originalSegmentForCancel.id);
            if (segmentIndex !== -1) {
                currentIdFormat[segmentIndex] = originalSegmentForCancel; // Restore original state
            }
            originalSegmentForCancel = null;
            renderCurrentFormat(); // Re-render to show the restored state
        }
    });

    fetchIdFormat();
}