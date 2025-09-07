function showToast(message, isError = false) {
    const toastContainer = document.querySelector('.toast-container');
    const toastId = 'toast-' + Math.random().toString(36).substr(2, 9);
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