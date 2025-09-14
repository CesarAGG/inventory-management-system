function showToast(message, isError = false) {
    const toastContainer = document.querySelector('.toast-container');
    const TOAST_ID_LENGTH = 9;
    const toastId = 'toast-' + Math.random().toString(36).slice(2, 2 + TOAST_ID_LENGTH);
    const toastHtml = `
                                                <div id="${toastId}" class="toast" role="alert" aria-live="assertive" aria-atomic="true">
                                                    <div class="toast-header ${isError ? 'bg-danger text-white' : 'bg-success text-white'}">
                                                    <strong class="me-auto">${isError ? 'Error' : 'Success'}</strong>
                                                    <button type="button" class="btn-close btn-close-white" data-bs-dismiss="toast" aria-label="Close"></button>
                                                </div>
                                                <div class="toast-body">${escapeHtml(message)}</div>
                                            </div>`;
    toastContainer.insertAdjacentHTML('beforeend', toastHtml);
    const toastElement = document.getElementById(toastId);
    const toast = new bootstrap.Toast(toastElement, { delay: 3000 });
    toast.show();
    toastElement.addEventListener('hidden.bs.toast', () => toastElement.remove());
}

function escapeHtml(unsafe) {
    return unsafe.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;").replace(/'/g, "&#039;");
}

function initializePopovers() {
    const popoverTriggerList = document.querySelectorAll('[data-bs-toggle="popover"]');
    const popoverList = [...popoverTriggerList].map(popoverTriggerEl => new bootstrap.Popover(popoverTriggerEl));
}

function updateInventoryVersion(newVersion) {
    if (newVersion) {
        const versionElement = document.querySelector('h2[data-inventory-version]');
        if (versionElement) {
            versionElement.dataset.inventoryVersion = newVersion;
        }
    }
}

function initializeDataTable(selector, options) {
    const commonOptions = {
        "processing": true,
        "serverSide": true,
        "ajax": {
            "type": "POST",
            "dataType": "json",
            "error": function (xhr, error, thrown) {
                // Check if the table still exists before showing a toast
                if ($.fn.DataTable.isDataTable(selector)) {
                    showToast('An error occurred while loading data. Please try again.', true);
                }
            }
        }
    };

    // Deep merge of options, with user options taking precedence.
    // This allows overriding parts of the ajax config, like the url.
    const finalOptions = $.extend(true, {}, commonOptions, options);

    return $(selector).DataTable(finalOptions);
}