function initializeConcurrencyHandler() {
    const concurrencyModalElement = document.getElementById('concurrencyModal');
    if (!concurrencyModalElement) {
        console.error('Concurrency modal element not found.');
        return;
    }

    const concurrencyModal = new bootstrap.Modal(concurrencyModalElement);

    window.handleConcurrencyError = function () {
        concurrencyModal.show();
    };

    const reloadPageBtn = document.getElementById('reloadPageBtn');
    if (reloadPageBtn) {
        reloadPageBtn.addEventListener('click', () => {
            location.reload();
        });
    }
}