# Component Patterns

Copy-pasteable HTML/CSS patterns that match the Siora design system.
All colours use `var(--siora-*)` tokens defined in the prototype's `:root` block.

---

## Button

```css
.btn {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 8px 16px;
  border-radius: 8px;
  border: none;
  font-family: var(--siora-font-sans);
  font-size: 14px;
  font-weight: 600;
  cursor: pointer;
  transition: background 0.15s;
}
.btn-primary {
  background: var(--siora-brand-primary);
  color: var(--siora-neutral-0);
}
.btn-primary:hover {
  background: var(--siora-brand-primary-hover);
}
.btn-outline {
  background: transparent;
  border: 1px solid var(--siora-neutral-200);
  color: var(--siora-neutral-700);
}
.btn-outline:hover {
  background: var(--siora-neutral-100);
}
.btn-ghost {
  background: transparent;
  color: var(--siora-neutral-700);
}
.btn-ghost:hover {
  background: var(--siora-neutral-100);
}
.btn-danger {
  background: var(--siora-brand-danger);
  color: var(--siora-neutral-0);
}
.btn-sm {
  padding: 5px 10px;
  font-size: 12px;
}

.icon-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 36px;
  height: 36px;
  border-radius: 8px;
  background: transparent;
  border: none;
  cursor: pointer;
  color: var(--siora-neutral-500);
  transition: background 0.15s;
}
.icon-btn:hover {
  background: var(--siora-neutral-100);
}
```

```html
<button class="btn btn-primary">Save changes</button>
<button class="btn btn-outline">
  <i data-lucide="plus" style="width:14px;height:14px;"></i> Add
</button>
```

---

## Badge

Uses semantic colour vars so no hex appears in the CSS rules.

```css
.badge {
  display: inline-flex;
  align-items: center;
  padding: 2px 8px;
  border-radius: 999px;
  font-size: 11px;
  font-weight: 600;
  white-space: nowrap;
}
.badge-success {
  background: var(--siora-badge-success-bg);
  color: var(--siora-badge-success-fg);
}
.badge-warning {
  background: var(--siora-badge-warning-bg);
  color: var(--siora-badge-warning-fg);
}
.badge-danger {
  background: var(--siora-badge-danger-bg);
  color: var(--siora-badge-danger-fg);
}
.badge-info {
  background: var(--siora-badge-info-bg);
  color: var(--siora-badge-info-fg);
}
```

```html
<span class="badge badge-success">Confirmed</span>
<span class="badge badge-warning">Pending</span>
<span class="badge badge-danger">Declined</span>
```

---

## Card

```css
.card {
  background: var(--siora-neutral-0);
  border: 1px solid var(--siora-neutral-200);
  border-radius: 12px;
  overflow: hidden;
}
.card-header {
  padding: 16px 20px;
  border-bottom: 1px solid var(--siora-neutral-100);
  display: flex;
  align-items: center;
  justify-content: space-between;
}
.card-title {
  font-size: 15px;
  font-weight: 600;
  color: var(--siora-neutral-900);
}
.card-body {
  padding: 20px;
}
.card-footer {
  padding: 12px 20px;
  border-top: 1px solid var(--siora-neutral-100);
  display: flex;
  gap: 8px;
  justify-content: flex-end;
}
```

---

## Data Table

```css
.data-table {
  width: 100%;
  border-collapse: collapse;
}
.data-table th {
  text-align: left;
  padding: 10px 16px;
  font-size: 11px;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  color: var(--siora-neutral-500);
  border-bottom: 1px solid var(--siora-neutral-200);
  background: var(--siora-neutral-50);
}
.data-table td {
  padding: 12px 16px;
  font-size: 14px;
  border-bottom: 1px solid var(--siora-neutral-100);
  color: var(--siora-neutral-700);
}
.data-table tbody tr:hover {
  background: var(--siora-neutral-50);
}
```

---

## Form Fields

```css
.form-group {
  display: flex;
  flex-direction: column;
  gap: 4px;
}
.form-label {
  font-size: 13px;
  font-weight: 600;
  color: var(--siora-neutral-700);
}
.form-input,
.form-select,
.form-textarea {
  padding: 8px 12px;
  border-radius: 8px;
  border: 1px solid var(--siora-neutral-200);
  font-family: var(--siora-font-sans);
  font-size: 14px;
  color: var(--siora-neutral-900);
  background: var(--siora-neutral-0);
  transition: border-color 0.15s;
}
.form-input:focus,
.form-select:focus,
.form-textarea:focus {
  outline: none;
  border-color: var(--siora-brand-primary);
  box-shadow: 0 0 0 3px var(--siora-brand-primary-light);
}
```

---

## Tabs

```css
.tabs {
  display: flex;
  gap: 0;
  border-bottom: 1px solid var(--siora-neutral-200);
}
.tab {
  padding: 10px 16px;
  font-size: 14px;
  font-weight: 500;
  color: var(--siora-neutral-500);
  cursor: pointer;
  border-bottom: 2px solid transparent;
  background: none;
  border-top: none;
  border-left: none;
  border-right: none;
  font-family: var(--siora-font-sans);
  transition:
    color 0.15s,
    border-color 0.15s;
}
.tab:hover {
  color: var(--siora-neutral-700);
}
.tab.active {
  color: var(--siora-brand-primary);
  border-bottom-color: var(--siora-brand-primary);
}
.tab-panel {
  display: none;
  padding: 16px 0;
}
.tab-panel.active {
  display: block;
}
```

```js
function switchTab(el, panelId) {
  document.querySelectorAll('.tab').forEach((t) => t.classList.remove('active'));
  document.querySelectorAll('.tab-panel').forEach((p) => p.classList.remove('active'));
  el.classList.add('active');
  document.getElementById(panelId).classList.add('active');
}
```

---

## Dialog / Sheet

```css
.dialog-backdrop {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.4);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 50;
  opacity: 0;
  pointer-events: none;
  transition: opacity 0.2s;
}
.dialog-backdrop.open {
  opacity: 1;
  pointer-events: auto;
}
.dialog {
  background: var(--siora-neutral-0);
  border-radius: 12px;
  width: 480px;
  max-width: 90vw;
  max-height: 80vh;
  overflow-y: auto;
  box-shadow: 0 20px 60px rgba(0, 0, 0, 0.15);
}
.dialog-header {
  padding: 16px 20px;
  border-bottom: 1px solid var(--siora-neutral-100);
  display: flex;
  align-items: center;
  justify-content: space-between;
}
.dialog-body {
  padding: 20px;
}
.dialog-footer {
  padding: 12px 20px;
  border-top: 1px solid var(--siora-neutral-100);
  display: flex;
  gap: 8px;
  justify-content: flex-end;
}
```

---

## Empty State

Use when a list or table has no data yet.

```css
.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 48px 24px;
  text-align: center;
}
.empty-state-icon {
  width: 56px;
  height: 56px;
  border-radius: 50%;
  background: var(--siora-neutral-100);
  display: flex;
  align-items: center;
  justify-content: center;
  margin-bottom: 16px;
}
.empty-state-icon i {
  color: var(--siora-neutral-500);
}
.empty-state-title {
  font-size: 16px;
  font-weight: 600;
  color: var(--siora-neutral-900);
  margin-bottom: 4px;
}
.empty-state-body {
  font-size: 14px;
  color: var(--siora-neutral-500);
  max-width: 320px;
  margin-bottom: 16px;
}
```

```html
<div class="empty-state">
  <div class="empty-state-icon">
    <i data-lucide="calendar-x" style="width:24px;height:24px;"></i>
  </div>
  <div class="empty-state-title">No bookings yet</div>
  <div class="empty-state-body">When hosts book your venue, their requests will appear here.</div>
  <button class="btn btn-primary">Share your listing</button>
</div>
```

---

## Stat Tile

```css
.stat-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  gap: 16px;
}
.stat-tile {
  background: var(--siora-neutral-0);
  border: 1px solid var(--siora-neutral-200);
  border-radius: 12px;
  padding: 16px;
}
.stat-label {
  font-size: 12px;
  font-weight: 500;
  color: var(--siora-neutral-500);
  margin-bottom: 4px;
}
.stat-value {
  font-size: 24px;
  font-weight: 700;
  color: var(--siora-neutral-900);
}
.stat-sub {
  font-size: 12px;
  color: var(--siora-neutral-500);
  margin-top: 2px;
}
```

---

## Agent Confirmation Card

Four-state card for the AI orchestrator proposing venues/vendors. Switch states by
updating the `data-state` attribute on `.agent-card`.

```css
.agent-card {
  position: relative;
}
.agent-card [data-step] {
  display: none;
}
.agent-card[data-state='proposal'] [data-step='proposal'],
.agent-card[data-state='form'] [data-step='form'],
.agent-card[data-state='confirming'] [data-step='confirming'],
.agent-card[data-state='success'] [data-step='success'] {
  display: block;
}

.agent-header {
  display: flex;
  align-items: center;
  gap: 8px;
  font-weight: 600;
  font-size: 15px;
  color: var(--siora-brand-primary);
  margin-bottom: 12px;
}
.proposal-detail {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 0;
  font-size: 14px;
}
.proposal-detail i {
  color: var(--siora-neutral-500);
}
```

```html
<div class="card agent-card" data-state="proposal">
  <!-- State 1: Proposal -->
  <div data-step="proposal" class="card-body">
    <div class="agent-header">✦ Agent Proposal</div>
    <div class="proposal-detail">
      <i data-lucide="building-2" style="width:16px;height:16px;"></i> The Grand Pavilion
    </div>
    <div class="proposal-detail">
      <i data-lucide="utensils" style="width:16px;height:16px;"></i> Golden Fork Catering — ₹1,200 /
      plate
    </div>
    <div class="proposal-detail">
      <i data-lucide="calendar" style="width:16px;height:16px;"></i> 15 Mar 2026
    </div>
    <div style="margin-top:16px;display:flex;gap:8px;">
      <button class="btn btn-primary" onclick="agentState('form')">Looks good</button>
      <button class="btn btn-outline" onclick="agentState('form')">Edit details</button>
    </div>
  </div>

  <!-- State 2: Confirmation form -->
  <div data-step="form" class="card-body">
    <div class="agent-header">Confirm booking</div>
    <!-- Editable fields -->
    <div style="display:flex;flex-direction:column;gap:12px;">
      <div class="form-group">
        <label class="form-label">Venue</label
        ><input class="form-input" value="The Grand Pavilion" />
      </div>
      <div class="form-group">
        <label class="form-label">Date</label
        ><input class="form-input" type="date" value="2026-03-15" />
      </div>
      <div class="form-group">
        <label class="form-label">Guests</label
        ><input class="form-input" type="number" value="200" />
      </div>
      <div class="form-group">
        <label class="form-label">Estimated total</label
        ><input class="form-input" value="₹3,40,000" />
      </div>
    </div>
    <div style="margin-top:16px;display:flex;gap:8px;">
      <button class="btn btn-primary" onclick="agentState('confirming')">Confirm</button>
      <button class="btn btn-outline" onclick="agentState('proposal')">Back</button>
    </div>
  </div>

  <!-- State 3: Confirming -->
  <div data-step="confirming" class="card-body" style="text-align:center;padding:32px;">
    <div style="font-size:14px;color:var(--siora-neutral-500);">Confirming...</div>
  </div>

  <!-- State 4: Success -->
  <div data-step="success" class="card-body" style="text-align:center;padding:32px;">
    <div
      style="width:48px;height:48px;border-radius:50%;background:var(--siora-badge-success-bg);display:inline-flex;align-items:center;justify-content:center;margin-bottom:12px;"
    >
      <i
        data-lucide="check"
        style="width:24px;height:24px;color:var(--siora-badge-success-fg);"
      ></i>
    </div>
    <div style="font-size:16px;font-weight:600;margin-bottom:4px;">Booking confirmed!</div>
    <div style="font-size:13px;color:var(--siora-neutral-500);">Reference: SIO-2026-0842</div>
  </div>
</div>
```

```js
function agentState(state) {
  const card = document.querySelector('.agent-card');
  card.setAttribute('data-state', state);
  if (state === 'confirming') setTimeout(() => agentState('success'), 1500);
  lucide.createIcons();
}
```

---

## Package Card

For vendor service packages (photography, catering, DJ, etc.).

```css
.package-card {
  background: var(--siora-neutral-0);
  border: 1px solid var(--siora-neutral-200);
  border-radius: 12px;
  overflow: hidden;
}
.package-header {
  padding: 20px;
}
.package-name {
  font-size: 16px;
  font-weight: 700;
  color: var(--siora-neutral-900);
}
.package-vendor {
  font-size: 13px;
  color: var(--siora-neutral-500);
  margin-top: 2px;
}
.package-price {
  font-size: 22px;
  font-weight: 700;
  color: var(--siora-brand-primary);
  margin-top: 8px;
}
.package-includes {
  padding: 0 20px 20px;
  display: flex;
  flex-direction: column;
  gap: 8px;
}
.package-includes li {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 14px;
  color: var(--siora-neutral-700);
  list-style: none;
}
.package-includes li i {
  color: var(--siora-brand-success);
  width: 16px;
  height: 16px;
}
.package-footer {
  padding: 12px 20px;
  border-top: 1px solid var(--siora-neutral-100);
}
```

```html
<div class="package-card">
  <div class="package-header">
    <div class="package-name">Premium Coverage</div>
    <div class="package-vendor">Lens & Light Photography</div>
    <div class="package-price">₹1,25,000</div>
  </div>
  <ul class="package-includes">
    <li><i data-lucide="check-circle"></i> 8 hours coverage</li>
    <li><i data-lucide="check-circle"></i> 2 photographers</li>
    <li><i data-lucide="check-circle"></i> Edited gallery (300+ photos)</li>
    <li><i data-lucide="check-circle"></i> Highlight reel (3-5 min)</li>
  </ul>
  <div class="package-footer">
    <button class="btn btn-primary" style="width:100%;">Request quote</button>
  </div>
</div>
```

---

## Event Timeline

```css
.timeline {
  display: flex;
  flex-direction: column;
  gap: 0;
  position: relative;
}
.timeline::before {
  content: '';
  position: absolute;
  left: 15px;
  top: 0;
  bottom: 0;
  width: 2px;
  background: var(--siora-neutral-200);
}
.timeline-item {
  display: flex;
  gap: 16px;
  position: relative;
  padding: 12px 0;
}
.timeline-dot {
  width: 10px;
  height: 10px;
  border-radius: 50%;
  background: var(--siora-brand-primary);
  margin-top: 5px;
  flex-shrink: 0;
  position: relative;
  left: 11px;
  z-index: 1;
}
.timeline-dot.future {
  background: var(--siora-neutral-200);
}
.timeline-content {
  padding-left: 8px;
}
.timeline-title {
  font-size: 14px;
  font-weight: 600;
  color: var(--siora-neutral-900);
}
.timeline-time {
  font-size: 12px;
  color: var(--siora-neutral-500);
}
```

---

## Availability Grid

7-column week grid for venue/vendor availability.

```css
.avail-grid {
  display: grid;
  grid-template-columns: repeat(7, 1fr);
  gap: 4px;
}
.avail-day-header {
  text-align: center;
  font-size: 11px;
  font-weight: 600;
  color: var(--siora-neutral-500);
  padding: 4px;
}
.avail-cell {
  aspect-ratio: 1;
  border-radius: 8px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 13px;
  font-weight: 500;
  cursor: pointer;
  transition: background 0.15s;
}
.avail-cell.available {
  background: var(--siora-badge-success-bg);
  color: var(--siora-badge-success-fg);
}
.avail-cell.booked {
  background: var(--siora-badge-danger-bg);
  color: var(--siora-badge-danger-fg);
}
.avail-cell.tentative {
  background: var(--siora-badge-warning-bg);
  color: var(--siora-badge-warning-fg);
}
.avail-cell.empty {
  background: var(--siora-neutral-100);
  color: var(--siora-neutral-500);
}
```
