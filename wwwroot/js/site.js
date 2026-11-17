(function () {
  'use strict';

  function normalizeId(v) { return (v || '').replace(/[\s\-_.]/g, '').toUpperCase(); }
  document.querySelectorAll('input[data-id-input]').forEach(function (input) {
    input.addEventListener('blur', function () { input.value = normalizeId(input.value); });
    input.closest('form') && input.closest('form').addEventListener('submit', function () { input.value = normalizeId(input.value); });
  });

  var find = document.getElementById('side-find');
  if (find) {
    find.addEventListener('submit', function (e) {
      var box = find.querySelector('input');
      box.value = normalizeId(box.value);
      if (!box.value) { e.preventDefault(); box.focus(); }
    });
    document.addEventListener('keydown', function (e) {
      var t = e.target.tagName;
      if (e.key === '/' && t !== 'INPUT' && t !== 'TEXTAREA' && t !== 'SELECT') {
        e.preventDefault(); find.querySelector('input').focus();
      }
    });
  }

  document.querySelectorAll('form').forEach(function (form) {
    form.addEventListener('submit', function (e) {
      if (form.dataset.noGuard !== undefined) return;
      if (form.dataset.submitting === '1') { e.preventDefault(); return; }
      var btn = e.submitter || form.querySelector('button[type=submit]');
      if (!btn || btn.dataset.noGuard !== undefined) return;
      form.dataset.submitting = '1';
      setTimeout(function () {
        btn.classList.add('is-busy');
        btn.setAttribute('aria-busy', 'true');
        if (btn.dataset.busy) { btn.dataset.label = btn.textContent; btn.textContent = btn.dataset.busy; }
      }, 0);
      setTimeout(function () { form.dataset.submitting = '0'; btn.classList.remove('is-busy'); }, 8000);
    });
  });

  var firstBad = document.querySelector('.input-validation-error');
  if (firstBad) {
    firstBad.classList.add('shake');
    firstBad.focus({ preventScroll: false });
    firstBad.addEventListener('animationend', function () { firstBad.classList.remove('shake'); });
  }
  var loginErr = document.querySelector('.auth-form .validation-summary-errors');
  if (loginErr) loginErr.classList.add('shake');

  document.querySelectorAll('.flash').forEach(function (el) {
    function close() { el.classList.add('is-leaving'); setTimeout(function () { el.remove(); }, 220); }
    var b = el.querySelector('button'); if (b) b.addEventListener('click', close);
    if (!el.classList.contains('is-error')) setTimeout(close, 5500);
  });

  var toggle = document.getElementById('nav-toggle');
  var backdrop = document.querySelector('.nav-backdrop');
  function setNav(open) { document.body.classList.toggle('nav-open', open); if (toggle) toggle.setAttribute('aria-expanded', open ? 'true' : 'false'); }
  if (toggle) toggle.addEventListener('click', function () { setNav(!document.body.classList.contains('nav-open')); });
  if (backdrop) backdrop.addEventListener('click', function () { setNav(false); });
  document.addEventListener('keydown', function (e) { if (e.key === 'Escape') setNav(false); });

  var modalEl = document.getElementById('confirm-modal');
  if (modalEl && window.bootstrap) {
    var modal = new window.bootstrap.Modal(modalEl);
    var pendingForm = null;
    document.querySelectorAll('[data-confirm]').forEach(function (btn) {
      btn.addEventListener('click', function (e) {
        e.preventDefault();
        pendingForm = btn.closest('form');
        modalEl.querySelector('.modal-title').textContent = btn.dataset.confirmTitle || 'Are you sure?';
        modalEl.querySelector('.modal-text').textContent = btn.dataset.confirm;
        var go = modalEl.querySelector('[data-go]');
        go.textContent = btn.dataset.confirmVerb || 'Delete';
        modal.show();
      });
    });
    modalEl.addEventListener('shown.bs.modal', function () { modalEl.querySelector('[data-bs-dismiss]').focus(); });
    modalEl.querySelector('[data-go]').addEventListener('click', function () { if (pendingForm) { pendingForm.dataset.noGuard = ''; pendingForm.submit(); } });
  }

  var orderForm = document.getElementById('order-form');
  if (orderForm) {
    var typeRadios = orderForm.querySelectorAll('input[name=ConnectionType]');
    var planSel = orderForm.querySelector('#PlanId');
    var prodSel = orderForm.querySelector('#ProductId');
    var landPlanSel = orderForm.querySelector('#LandlinePlanId');
    var dialBlock = orderForm.querySelector('[data-dial-only]');
    var existing = orderForm.querySelector('#HasExistingLandline');
    var existingBlock = orderForm.querySelector('[data-existing-landline]');
    var newBlock = orderForm.querySelector('[data-new-landline]');
    var qty = orderForm.querySelector('#Quantity');
    var discountNote = orderForm.querySelector('[data-discount-note]');
    var depositNote = orderForm.querySelector('[data-deposit-note]');
    var schemes = JSON.parse(orderForm.dataset.schemes || '[]');
    var deposits = JSON.parse(orderForm.dataset.deposits || '{}');

    function currentType() { var r = orderForm.querySelector('input[name=ConnectionType]:checked'); return r ? r.value : '2'; }
    function filter(select, attr, ok) {
      var changed = false;
      Array.prototype.forEach.call(select.options, function (o) {
        if (!o.value) return;
        var show = ok(o.getAttribute(attr));
        o.hidden = !show; o.disabled = !show;
        if (!show && o.selected) changed = true;
      });
      if (changed) select.value = '';
    }
    function refresh() {
      var t = currentType();
      filter(planSel, 'data-type', function (v) { return v === t; });
      var cats = { '1': ['1'], '2': ['1', '2'], '3': ['3'] }[t];
      filter(prodSel, 'data-cat', function (v) { return cats.indexOf(v) >= 0; });
      dialBlock.classList.toggle('is-hidden', t !== '1');
      var has = existing.checked && t === '1';
      existingBlock.classList.toggle('is-hidden', !has);
      newBlock.classList.toggle('is-hidden', has);
      if (has) { qty.value = 1; }
      qty.readOnly = has;
      var n = parseInt(qty.value, 10) || 1;
      var pct = 0;
      schemes.forEach(function (s) { if (n >= s.min && (s.max === null || n <= s.max)) pct = Math.max(pct, s.pct); });
      discountNote.textContent = pct > 0
        ? pct + '% off the first rental and the security deposit for ' + n + ' connections'
        : 'Orders of 10 or more connections get a discount on the first rental and the deposit';
      var dep = deposits[t];
      depositNote.textContent = dep !== undefined
        ? 'Refundable security deposit: $' + Number(dep).toFixed(2) + ' per connection' + (pct > 0 ? ', less ' + pct + '%' : '')
        : '';
    }
    typeRadios.forEach(function (r) { r.addEventListener('change', refresh); });
    existing.addEventListener('change', refresh);
    qty.addEventListener('input', refresh);
    refresh();
  }

  var lines = document.getElementById('po-lines');
  if (lines) {
    var tpl = document.getElementById('po-row-template');
    var addBtn = document.getElementById('po-add');
    function reindex() {
      lines.querySelectorAll('.line-row').forEach(function (row, i) {
        row.querySelectorAll('[data-name]').forEach(function (el) { el.name = 'Lines[' + i + '].' + el.dataset.name; });
      });
    }
    addBtn.addEventListener('click', function () {
      var node = tpl.content.firstElementChild.cloneNode(true);
      lines.appendChild(node); reindex();
      node.querySelector('select').focus();
    });
    lines.addEventListener('click', function (e) {
      var rm = e.target.closest('[data-remove]');
      if (!rm) return;
      if (lines.querySelectorAll('.line-row').length > 1) { rm.closest('.line-row').remove(); reindex(); }
    });
    reindex();
  }

  document.querySelectorAll('[data-fill]').forEach(function (btn) {
    btn.addEventListener('click', function () {
      var target = document.querySelector(btn.dataset.fill);
      if (target) { target.value = btn.dataset.value; target.focus(); }
    });
  });

  document.querySelectorAll('[data-print]').forEach(function (b) { b.addEventListener('click', function () { window.print(); }); });
})();
