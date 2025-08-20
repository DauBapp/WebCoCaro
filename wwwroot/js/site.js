// ========================================
// GAME CARO - AI SIMPLE VERSION
// ========================================

console.log('=== GAME CARO LOADED ===');

// Note: Game logic is now handled in _Layout.cshtml to avoid variable redeclaration errors
// This file is kept for potential future extensions

console.log('=== GAME CARO SCRIPT LOADED ===');

document.addEventListener('DOMContentLoaded', function () {
    // Hiệu ứng loading khi submit form
    const loginForm = document.querySelector('.login-form');
    const registerForm = document.querySelector('.register-form');
    [loginForm, registerForm].forEach(function(form) {
        if (form) {
            form.addEventListener('submit', function(e) {
                const btn = form.querySelector('button[type="submit"]');
                if (btn) {
                    btn.disabled = true;
                    btn.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span> Đang xử lý...';
                }
            });
        }
    });

    // Hiệu ứng shake khi có lỗi
    const validationSummary = document.querySelector('.validation-summary-errors, .text-danger');
    if (validationSummary && validationSummary.innerText.trim() !== '') {
        const form = validationSummary.closest('form');
        if (form) {
            form.classList.add('animate__animated', 'animate__shakeX');
            setTimeout(() => {
                form.classList.remove('animate__animated', 'animate__shakeX');
            }, 1000);
        }
    }
});
