---
name: siora-prototype
description: >
  Generates self-contained single-file HTML prototypes for Siora screens.
  Use this skill whenever the user asks to prototype, mock up, or visualise any
  Siora UI screen — even if they say "show me", "what does X look like", or
  "design the Y page". Covers host, vendor, and venue personas.
---

# Siora Prototype

Build a **single self-contained HTML file** that looks and feels like a real Siora
screen — correct persona shell, brand tokens, Lucide icons, realistic Indian-market
event-planning data, and working micro-interactions.

---

## Step 0 — Pick the persona

Siora has three personas. If the prompt doesn't make it obvious which one, **ask
one short question** ("Which persona — host, vendor, or venue?") and wait.

| Persona    | Who they are                            | Sidebar nav items                                             |
| ---------- | --------------------------------------- | ------------------------------------------------------------- |
| **Host**   | Event planner booking venues & vendors  | My Events, Explore Venues, Find Vendors, Agent Chat, Settings |
| **Vendor** | Caterer / photographer / DJ / decorator | Dashboard, My Packages, Availability, Inquiries, Settings     |
| **Venue**  | Banquet hall / resort / farmhouse       | Dashboard, My Spaces, Calendar, Booking Requests, Settings    |

Once the persona is clear, proceed.

---

## Step 1 — Read the design tokens

Read `apps/frontend/src/styles.scss` to get the live HSL values for `--primary`,
`--background`, `--foreground`, etc. The brand primary is **Siora orange
hsl(19 88% 60%) ≈ #f2703f**. Map them to prototype CSS vars:

```css
:root {
  /* Brand */
  --siora-brand-primary: #f2703f;
  --siora-brand-primary-hover: #d95e2e;
  --siora-brand-primary-light: #fde8e0;
  --siora-brand-accent: #f59e0b;
  --siora-brand-success: #10b981;
  --siora-brand-danger: #ef4444;

  /* Neutrals */
  --siora-neutral-0: #ffffff;
  --siora-neutral-50: #fafafa;
  --siora-neutral-100: #f4f4f5;
  --siora-neutral-200: #e4e4e7;
  --siora-neutral-500: #6b7280;
  --siora-neutral-700: #374151;
  --siora-neutral-900: #111827;

  /* Badge semantic vars */
  --siora-badge-success-bg: #d1fae5;
  --siora-badge-success-fg: #065f46;
  --siora-badge-warning-bg: #fef3c7;
  --siora-badge-warning-fg: #92400e;
  --siora-badge-danger-bg: #fee2e2;
  --siora-badge-danger-fg: #991b1b;
  --siora-badge-info-bg: #dbeafe;
  --siora-badge-info-fg: #1e40af;

  /* Typography */
  --siora-font-sans: 'Manrope', 'DM Sans', system-ui, sans-serif;
}
```

Use **only** `var(--siora-*)` references in all CSS. No raw hex values anywhere
in the stylesheet — the token block above is the single source of truth.

---

## Step 2 — Build the HTML

Read `references/persona-shells.md` for the HTML/CSS of the correct app shell.
Read `references/component-patterns.md` for copy-pasteable component patterns.

### Structure

```
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <title>Siora — [Screen Name]</title>
  <script src="https://unpkg.com/lucide@latest/dist/umd/lucide.min.js"></script>
  <style>
    /* Design tokens */
    /* Shell CSS (from persona-shells.md) */
    /* Screen-specific CSS */
  </style>
</head>
<body>
  <!-- App shell with sidebar + topbar -->
  <!-- Screen content in .main-content -->
  <script>
    lucide.createIcons();
    // Micro-interactions
  </script>
  <!-- Handoff note (HTML comment) -->
</body>
</html>
```

### Rules

1. **No hex outside `:root`** — every colour reference in CSS rules and inline
   styles must use `var(--siora-*)`. Badge colours use the semantic vars
   (`--siora-badge-success-bg` etc.), not raw hex.

2. **Lucide icons only** — load via unpkg CDN, call `lucide.createIcons()` after
   DOM ready and after any dynamic DOM update. Use `data-lucide="icon-name"` on
   `<i>` elements.

3. **Self-contained** — one `.html` file, no external CSS/JS beyond the Lucide CDN.
   All styles inline in `<style>`, all JS inline in `<script>`.

4. **Interactions work** — buttons, tabs, filters, accept/decline actions, form
   toggling should all function via vanilla JS. Re-call `lucide.createIcons()`
   after DOM mutations.

5. **Realistic Siora data** — use Indian-market names and context:
   - Events: "Priya & Arjun's Wedding", "TechConf 2026", "Johnson Gala",
     "Mehra Anniversary", "Kapoor Engagement"
   - Venues: "The Grand Pavilion", "Riverside Meadows", "Horizon Banquets"
   - Vendors: "Golden Fork Catering", "Lens & Light Photography",
     "BeatDrop DJ Co.", "Bloom & Petal Decor"
   - Prices in INR (₹)

6. **Correct persona shell** — sidebar items, logo text, and user avatar must
   match the chosen persona exactly.

### Agent confirmation flow

When the prototype involves the AI agent proposing something (venue, vendor,
package), use the four-state agent flow:

| State        | What's visible                                                                           |
| ------------ | ---------------------------------------------------------------------------------------- |
| `proposal`   | Card with "✦ Agent Proposal" header, venue/vendor details, "Looks good" + "Edit" buttons |
| `form`       | Editable confirmation form (venue, date, guests, total)                                  |
| `confirming` | Loading spinner, "Confirming..."                                                         |
| `success`    | Green check, booking reference, next-steps list                                          |

Use a `data-state` attribute on the card container and toggle visibility with JS.

---

## Handoff note

End the file with an HTML comment block:

```html
<!--
  ═══ Siora prototype notes ═══
  Persona : host | vendor | venue
  Screen  : [Screen name]
  Agent flow shown: yes | no

  What's interactive:
  - [list of working interactions]

  What's static:
  - [hardcoded items that would be dynamic in Angular]

  Suggested Angular components:
  - [mapped to @siora/helm/* imports]

  Open questions:
  - [anything unclear for production implementation]
-->
```

---

## Component reference

See `references/component-patterns.md` for ready-to-use patterns:
Button, Badge, Card, Data Table, Form Fields, Tabs, Dialog/Sheet,
Empty State, Stat Tile, Agent Confirmation Card, Availability Grid,
Package Card, Event Timeline.

See also `.bob/guides/spartan/_index.md` for the full Spartan-NG component
catalog (60+ components with `@siora/helm/*` imports).
