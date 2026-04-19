// bisram.js — CLIENT-SIDE JAVASCRIPT for BisRAM
// Handles: add-to-cart AJAX, image preview, message polling, UI interactions.
// Loaded on every page via _Layout.cshtml.

// ── CART ──
function addToCart(productId, btn) {
    if (btn) btn.disabled = true;
    fetch('/Cart/AddToCart', { method:'POST', headers:{'Content-Type':'application/x-www-form-urlencoded'}, body:'productId='+productId+'&quantity=1' })
    .then(r=>r.json()).then(d=>{
        if (d.success) {
            var el = document.getElementById('cartCount'); if (el) el.textContent = d.cartCount;
            if (btn) { var orig=btn.innerHTML; btn.innerHTML='<i class="fas fa-check"></i>'; btn.classList.add('added'); setTimeout(()=>{btn.innerHTML=orig;btn.classList.remove('added');btn.disabled=false;},1500); }
        } else { alert(d.message||'Please log in first.'); if(btn) btn.disabled=false; }
    }).catch(()=>{if(btn)btn.disabled=false;});
}

// ── SCROLL REVEAL ──
document.addEventListener('DOMContentLoaded', function() {
    var els = document.querySelectorAll('.product-card, .stat-card, .promo-card, .order-card, .convo-card');
    els.forEach(function(el) { el.classList.add('scroll-reveal'); });
    var observer = new IntersectionObserver(function(entries) {
        entries.forEach(function(e) { if (e.isIntersecting) { e.target.classList.add('visible'); } });
    }, { threshold: 0.1 });
    els.forEach(function(el) { observer.observe(el); });
});

// ── RIPPLE EFFECT on buttons ──
document.addEventListener('click', function(e) {
    var btn = e.target.closest('.btn-primary, .btn-outline');
    if (!btn) return;
    var rect = btn.getBoundingClientRect();
    var ripple = document.createElement('span');
    ripple.style.cssText = 'position:absolute;border-radius:50%;background:rgba(255,255,255,.4);transform:scale(0);animation:ripple .5s linear;left:'+(e.clientX-rect.left-20)+'px;top:'+(e.clientY-rect.top-20)+'px;width:40px;height:40px;pointer-events:none';
    btn.style.position = 'relative'; btn.style.overflow = 'hidden';
    btn.appendChild(ripple);
    setTimeout(()=>ripple.remove(), 600);
});

// ── NAVBAR SCROLL ──
window.addEventListener('scroll', function() {
    var nav = document.querySelector('.navbar');
    if (nav) nav.style.boxShadow = window.scrollY > 10 ? '0 4px 20px rgba(0,0,0,.15)' : '';
});
