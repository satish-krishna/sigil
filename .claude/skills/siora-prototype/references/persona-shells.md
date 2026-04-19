# Persona Shells

Each Siora persona has a fixed app shell: 240px sidebar + 56px topbar + scrollable main area.
Copy the relevant shell below and fill in `.main-content` with the screen.

---

## Shell CSS (shared by all three)

```css
/* ── Shell ── */
.app-shell {
  display: flex;
  height: 100vh;
  overflow: hidden;
}

.sidebar {
  width: 240px;
  min-width: 240px;
  background: var(--siora-neutral-0);
  border-right: 1px solid var(--siora-neutral-200);
  display: flex;
  flex-direction: column;
}
.sidebar-logo {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 20px 20px 16px;
  font-size: 18px;
  font-weight: 700;
  color: var(--siora-brand-primary);
  border-bottom: 1px solid var(--siora-neutral-100);
}
.sidebar-nav {
  display: flex;
  flex-direction: column;
  padding: 12px;
  flex: 1;
  gap: 2px;
}
.nav-item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 9px 12px;
  border-radius: 8px;
  color: var(--siora-neutral-700);
  text-decoration: none;
  font-size: 14px;
  font-weight: 500;
  transition:
    background 0.15s,
    color 0.15s;
}
.nav-item:hover {
  background: var(--siora-neutral-100);
}
.nav-item.active {
  background: var(--siora-brand-primary-light);
  color: var(--siora-brand-primary);
}
.nav-item i {
  width: 16px;
  height: 16px;
  flex-shrink: 0;
}
.nav-bottom {
  margin-top: auto;
}
.nav-badge {
  margin-left: auto;
  background: var(--siora-brand-primary);
  color: var(--siora-neutral-0);
  font-size: 10px;
  font-weight: 700;
  padding: 2px 6px;
  border-radius: 999px;
  min-width: 18px;
  text-align: center;
}
.sidebar-user {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 16px;
  border-top: 1px solid var(--siora-neutral-100);
}
.user-avatar {
  width: 32px;
  height: 32px;
  border-radius: 50%;
  background: var(--siora-brand-primary-light);
  color: var(--siora-brand-primary);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 13px;
  font-weight: 700;
}
.user-info {
  font-size: 13px;
}
.user-name {
  font-weight: 600;
  color: var(--siora-neutral-900);
}
.user-role {
  color: var(--siora-neutral-500);
  font-size: 11px;
}

/* Right pane */
.right-pane {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.topbar {
  height: 56px;
  min-height: 56px;
  background: var(--siora-neutral-0);
  border-bottom: 1px solid var(--siora-neutral-200);
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 24px;
}
.topbar-title {
  font-size: 16px;
  font-weight: 600;
}
.topbar-actions {
  display: flex;
  gap: 8px;
}

.main-content {
  flex: 1;
  overflow-y: auto;
  padding: 24px;
  background: var(--siora-neutral-50);
}
```

---

## Host Shell

```html
<div class="app-shell">
  <aside class="sidebar">
    <div class="sidebar-logo">
      <i data-lucide="sparkles" style="width:20px;height:20px;"></i> Siora
    </div>
    <nav class="sidebar-nav">
      <a class="nav-item" href="#"><i data-lucide="calendar-days"></i> My Events</a>
      <a class="nav-item" href="#"><i data-lucide="building-2"></i> Explore Venues</a>
      <a class="nav-item" href="#"><i data-lucide="store"></i> Find Vendors</a>
      <a class="nav-item active" href="#"><i data-lucide="message-square"></i> Agent Chat</a>
      <div class="nav-bottom">
        <a class="nav-item" href="#"><i data-lucide="settings"></i> Settings</a>
      </div>
    </nav>
    <div class="sidebar-user">
      <div class="user-avatar">PA</div>
      <div class="user-info">
        <div class="user-name">Priya Anand</div>
        <div class="user-role">Host</div>
      </div>
    </div>
  </aside>

  <div class="right-pane">
    <header class="topbar">
      <span class="topbar-title"><!-- Screen title here --></span>
      <div class="topbar-actions">
        <button class="icon-btn"><i data-lucide="bell" style="width:18px;height:18px;"></i></button>
      </div>
    </header>
    <main class="main-content">
      <!-- Screen content here -->
    </main>
  </div>
</div>
```

---

## Vendor Shell

```html
<div class="app-shell">
  <aside class="sidebar">
    <div class="sidebar-logo">
      <i data-lucide="sparkles" style="width:20px;height:20px;"></i> Siora
    </div>
    <nav class="sidebar-nav">
      <a class="nav-item" href="#"><i data-lucide="layout-dashboard"></i> Dashboard</a>
      <a class="nav-item" href="#"><i data-lucide="package"></i> My Packages</a>
      <a class="nav-item" href="#"><i data-lucide="calendar-check"></i> Availability</a>
      <a class="nav-item active" href="#"><i data-lucide="inbox"></i> Inquiries</a>
      <div class="nav-bottom">
        <a class="nav-item" href="#"><i data-lucide="settings"></i> Settings</a>
      </div>
    </nav>
    <div class="sidebar-user">
      <div class="user-avatar">GF</div>
      <div class="user-info">
        <div class="user-name">Golden Fork Catering</div>
        <div class="user-role">Vendor</div>
      </div>
    </div>
  </aside>

  <div class="right-pane">
    <header class="topbar">
      <span class="topbar-title"><!-- Screen title here --></span>
      <div class="topbar-actions">
        <button class="icon-btn"><i data-lucide="bell" style="width:18px;height:18px;"></i></button>
      </div>
    </header>
    <main class="main-content">
      <!-- Screen content here -->
    </main>
  </div>
</div>
```

---

## Venue Shell

```html
<div class="app-shell">
  <aside class="sidebar">
    <div class="sidebar-logo">
      <i data-lucide="sparkles" style="width:20px;height:20px;"></i> Siora
    </div>
    <nav class="sidebar-nav">
      <a class="nav-item" href="#"><i data-lucide="layout-dashboard"></i> Dashboard</a>
      <a class="nav-item" href="#"><i data-lucide="door-open"></i> My Spaces</a>
      <a class="nav-item" href="#"><i data-lucide="calendar"></i> Calendar</a>
      <a class="nav-item active" href="#"><i data-lucide="clipboard-list"></i> Booking Requests</a>
      <div class="nav-bottom">
        <a class="nav-item" href="#"><i data-lucide="settings"></i> Settings</a>
      </div>
    </nav>
    <div class="sidebar-user">
      <div class="user-avatar">GP</div>
      <div class="user-info">
        <div class="user-name">The Grand Pavilion</div>
        <div class="user-role">Venue</div>
      </div>
    </div>
  </aside>

  <div class="right-pane">
    <header class="topbar">
      <span class="topbar-title"><!-- Screen title here --></span>
      <div class="topbar-actions">
        <button class="icon-btn"><i data-lucide="bell" style="width:18px;height:18px;"></i></button>
      </div>
    </header>
    <main class="main-content">
      <!-- Screen content here -->
    </main>
  </div>
</div>
```
