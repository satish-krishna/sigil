---
name: frontend-design
description: Create distinctive, production-grade frontend interfaces for Siora using Angular 21, Spartan-NG, Tailwind CSS, and Font Awesome Pro. Use when building UI components, pages, or applications.
---

# Frontend Design for Siora

## Overview

Creates distinctive, production-grade interfaces using Siora's exact stack: Angular 21 standalone components, Spartan-NG UI primitives (`hlm*` directives), Tailwind CSS v4, and Font Awesome Pro icons.

Avoid generic "AI slop" aesthetics. Every component should feel intentionally designed for Siora — a wedding/events platform that is elegant, trustworthy, and emotionally resonant.

## Design Thinking First

Before coding, commit to a **BOLD aesthetic direction**:

- **Purpose**: What problem does this interface solve? Who uses it?
- **Tone**: Pick deliberately — elegant/luxury, warm/personal, editorial, minimal/clean, playful, etc.
- **Differentiation**: What makes this UNFORGETTABLE?

**CRITICAL**: Bold minimalism and refined elegance both work — the key is intentionality.

## Siora Tech Stack

**Always use:**

- **Angular 21** standalone components with `ChangeDetectionStrategy.OnPush`
- **Signals** for all state (`signal()`, `computed()`) — NO RxJS
- **Spartan-NG** UI primitives with `hlm*` directives (`hlmBtn`, `hlmCard`, etc.)
- **Tailwind CSS v4** for layout, spacing, responsiveness
- **Font Awesome Pro** icons: `<i class="fa-solid fa-icon-name"></i>` or `<i class="fa-light fa-icon-name"></i>`
- **Resource API** for async data (NO Observables)
- `inject()` pattern for DI

**Import from:**

- `@siora/helm/*` for shared UI components
- `@siora/frontend/core` for services/state
- `@siora/frontend/shared` for shared components

## Component Template

```typescript
import { Component, ChangeDetectionStrategy, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HlmButtonDirective } from '@siora/helm/button';
// Import Spartan-NG primitives as needed

@Component({
  selector: 'siora-my-feature',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, HlmButtonDirective],
  template: `
    <div class="...tailwind classes...">
      <!-- Spartan-NG + Tailwind + Font Awesome -->
      <button hlmBtn variant="default">
        <i class="fa-solid fa-check mr-2"></i>
        Action
      </button>
    </div>
  `,
})
export class MyFeatureComponent {
  private service = inject(MyService);

  // Signal state
  isLoading = signal(false);
  data = computed(() => this.service.data());
}
```

## Aesthetic Guidelines

### Typography

- Choose fonts that are **beautiful, unique, and contextually appropriate**
- Avoid generic: Arial, Inter, Roboto, system fonts
- Pair distinctive display fonts with refined body fonts
- Wedding/events context: consider serif elegance, luxury sans-serif, editorial options

### Color & Theme

- Commit to a cohesive aesthetic with CSS variables
- Dominant colors + sharp accents > evenly distributed palettes
- Consider Siora brand identity: warm, trustworthy, celebratory
- Support dark mode via `.dark` class (applied by ThemeService)

### Motion (CSS-only for Angular templates)

- High-impact moments: orchestrated page load with staggered reveals
- Micro-interactions on hover/focus states
- CSS `animation-delay` for stagger effects
- Avoid gratuitous animation — every animation should serve the user

### Spatial Composition

- Unexpected layouts: asymmetry, overlap, diagonal flow
- Grid-breaking elements for visual interest
- Generous negative space OR controlled density (pick one)
- Mobile-first: `sm:`, `md:`, `lg:` breakpoints

### Visual Details

- Backgrounds: gradient meshes, subtle textures, layered transparencies
- Dramatic shadows, decorative borders where appropriate
- Platform-specific styling: `body.ios`, `body.android`, `body.web`

## Icon Usage

```html
<!-- Font Awesome Pro (preferred for standard icons) -->
<i class="fa-solid fa-heart"></i>
<!-- solid style -->
<i class="fa-light fa-calendar"></i>
<!-- light style -->
<i class="fa-duotone fa-ring"></i>
<!-- duotone -->
<i class="fa-brands fa-google"></i>
<!-- brand icons -->

<!-- With Tailwind sizing -->
<i class="fa-solid fa-ring text-xl text-primary"></i>
```

## Spartan-NG Quick Reference

```html
<!-- Buttons -->
<button hlmBtn>Default</button>
<button hlmBtn variant="outline">Outline</button>
<button hlmBtn variant="ghost">Ghost</button>
<button hlmBtn variant="destructive">Destructive</button>

<!-- Cards -->
<div hlmCard>
  <div hlmCardHeader>
    <h3 hlmCardTitle>Title</h3>
    <p hlmCardDescription>Description</p>
  </div>
  <div hlmCardContent>Content</div>
</div>

<!-- Form Inputs -->
<input hlmInput type="text" placeholder="Enter value" />
<label hlmLabel>Label text</label>

<!-- Badges -->
<span hlmBadge>Badge</span>
<span hlmBadge variant="secondary">Secondary</span>
```

## Siora Platform Styling

```scss
/* Platform-specific overrides */
body.ios .my-component {
  padding-top: var(--ion-safe-area-top);
}
body.android .my-component {
  /* Android-specific */
}
body.web .my-component {
  /* Web-specific */
}

/* Dark mode */
.dark .my-component {
  /* Dark theme overrides */
}
```

## NEVER Use

- Generic AI aesthetics (purple gradients on white, predictable layouts)
- Common overused fonts (Inter as primary, Space Grotesk default)
- Scattered micro-interactions without purpose
- RxJS or Observables in components
- `NgModule` (standalone only)
- Direct DOM manipulation
- No `any` TypeScript types ever. Use `unknown` if the type is not used. Otherwise you have no excuse for `any`.

## Quality Check Before Done

- [ ] Component is standalone with OnPush
- [ ] All state uses signals
- [ ] Spartan-NG directives for UI primitives
- [ ] Font Awesome Pro icons (correct style variant)
- [ ] Tailwind CSS for layout/spacing (no inline styles)
- [ ] Mobile-first responsive design
- [ ] Dark mode works
- [ ] Platform-specific styles where needed
- [ ] Accessible (aria labels, keyboard navigation)
- [ ] No RxJS, no NgModule
