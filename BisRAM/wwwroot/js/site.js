// Global add-to-cart used across pages
function addToCart(productId, btn) {
    if (btn) btn.disabled = true;
    fetch('/Cart/AddToCart', {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
        body: `productId=${productId}&quantity=1`
    })
        .then(r => r.json())
        .then(d => {
            if (d.success) {
                const counter = document.getElementById('cartCount');
                if (counter) counter.textContent = d.cartCount;
                if (btn) {
                    const orig = btn.innerHTML;
                    btn.innerHTML = '<i class="fas fa-check"></i>';
                    btn.classList.add('added');
                    setTimeout(() => {
                        btn.innerHTML = orig;
                        btn.classList.remove('added');
                        btn.disabled = false;
                    }, 1500);
                }
            } else {
                alert(d.message || 'Please log in first.');
                if (btn) btn.disabled = false;
            }
        })
        .catch(() => { if (btn) btn.disabled = false; });
}