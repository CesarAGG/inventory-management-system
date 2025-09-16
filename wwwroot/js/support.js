function initializeSupportTicket() {
    const supportTicketModalEl = document.getElementById('supportTicketModal');
    if (!supportTicketModalEl) return;

    const supportTicketModal = new bootstrap.Modal(supportTicketModalEl);
    const supportTicketForm = document.getElementById('supportTicketForm');
    const sourceUrlInput = document.getElementById('ticketSourceUrl');
    const inventoryIdInput = document.getElementById('ticketInventoryId');
    const summaryInput = document.getElementById('ticketSummary');
    const validationErrorDiv = document.getElementById('ticket-validation-error');

    supportTicketModalEl.addEventListener('show.bs.modal', function () {
        // Reset form state on open
        supportTicketForm.reset();
        validationErrorDiv.classList.add('d-none');
        validationErrorDiv.textContent = '';
        summaryInput.classList.remove('is-invalid');

        // Populate hidden fields with current context
        sourceUrlInput.value = window.location.href;

        // Robustly find inventory ID using the specific data attribute
        const inventoryHeader = document.querySelector('h2[data-inventory-id]');
        inventoryIdInput.value = inventoryHeader ? inventoryHeader.dataset.inventoryId : '';
    });

    supportTicketForm.addEventListener('submit', async function (event) {
        event.preventDefault();
        const submitButton = supportTicketForm.querySelector('button[type="submit"]');
        submitButton.disabled = true;
        validationErrorDiv.classList.add('d-none');

        const formData = new FormData(supportTicketForm);
        const payload = {
            summary: formData.get('Summary'),
            priority: formData.get('Priority'),
            sourceUrl: formData.get('SourceUrl'),
            inventoryId: formData.get('InventoryId') || null
        };

        const csrfToken = formData.get('__RequestVerificationToken');

        try {
            const response = await fetch('/Support/CreateTicket', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': csrfToken
                },
                body: JSON.stringify(payload)
            });

            if (response.ok) {
                showToast('Support ticket submitted successfully. We will get back to you shortly.');
                supportTicketModal.hide();
            } else {
                const errorData = await response.json().catch(() => ({ message: 'An unknown server error occurred.' }));
                const errorMessage = errorData.message || Object.values(errorData).flat().join(' ');
                validationErrorDiv.textContent = `Submission failed: ${errorMessage}`;
                validationErrorDiv.classList.remove('d-none');
            }
        } catch (error) {
            validationErrorDiv.textContent = 'A network error occurred. Please check your connection and try again.';
            validationErrorDiv.classList.remove('d-none');
        } finally {
            submitButton.disabled = false;
        }
    });
}