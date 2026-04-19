# Angular Tech Debt Checklist

Reference checklist for detecting common tech debt patterns in Angular codebases (v17+).

---

## Subscription Leaks

### Observable.subscribe() without cleanup

- **Pattern to check:** `.subscribe()` calls in components without `takeUntilDestroyed()`, `DestroyRef`, manual `unsubscribe()` in `ngOnDestroy`, or use of `async` pipe.
- **Why it matters:** Subscriptions outlive the component, causing memory leaks, stale callbacks, and phantom HTTP requests.
- **Fix:** Use `takeUntilDestroyed()` from `@angular/core/rxjs-interop`, or prefer the `async` pipe in templates. For imperative subscriptions, inject `DestroyRef` and register cleanup.

### Subscriptions stored without unsubscribe

- **Pattern to check:** `this.subscription = obs$.subscribe(...)` without corresponding `this.subscription.unsubscribe()` in `ngOnDestroy`.
- **Why it matters:** The subscription persists after navigation, executing callbacks on a destroyed component.
- **Fix:** Use `takeUntilDestroyed()` or collect subscriptions in a `Subscription` bag and unsubscribe in `ngOnDestroy`.

### Manual subscribe for template data

- **Pattern to check:** `.subscribe(data => this.items = data)` to populate template-bound properties.
- **Why it matters:** Requires manual lifecycle management and bypasses Angular's built-in cleanup with `async` pipe.
- **Fix:** Use `async` pipe in the template, or migrate to signals with `toSignal()` from `@angular/core/rxjs-interop`.

---

## Signal Patterns

### Writable signal where computed suffices

- **Pattern to check:** A `signal()` whose value is always derived from other signals, manually kept in sync via `effect()`.
- **Why it matters:** Creates redundant state that can drift out of sync. Increases cognitive load.
- **Fix:** Replace with `computed(() => ...)` which is automatically derived and always consistent.

### Effect used for state synchronization

- **Pattern to check:** `effect()` that reads signals and writes to other signals.
- **Why it matters:** Creates implicit data flow that is hard to trace. Can trigger infinite update loops.
- **Fix:** Use `computed()` for derived state. Reserve `effect()` for side effects (logging, DOM manipulation, external API calls).

### Signal not used where beneficial

- **Pattern to check:** Mutable class fields used for component state instead of `signal()`, especially in OnPush components.
- **Why it matters:** OnPush components will not detect changes to plain fields without manual `markForCheck()` calls.
- **Fix:** Use `signal()` for reactive state. Angular's change detection automatically picks up signal reads in templates.

---

## OnPush Change Detection

### Mutable state in OnPush components

- **Pattern to check:** OnPush components that mutate objects/arrays in place (e.g., `this.items.push(...)`) instead of creating new references.
- **Why it matters:** OnPush only checks reference identity. In-place mutations are invisible to change detection.
- **Fix:** Always create new references: `this.items = [...this.items, newItem]`. Or migrate to signals.

### Missing markForCheck after async operations

- **Pattern to check:** OnPush components that update state in `setTimeout`, `setInterval`, or non-Angular async callbacks without calling `markForCheck()`.
- **Why it matters:** The view does not update because Angular skips change detection for OnPush components unless inputs change or an event fires.
- **Fix:** Call `this.cdr.markForCheck()` after async state changes, or use signals which notify Angular automatically.

### Component not using OnPush

- **Pattern to check:** Components using the default `ChangeDetectionStrategy.Default`.
- **Why it matters:** Default strategy checks every component on every change detection cycle, degrading performance in large component trees.
- **Fix:** Add `changeDetection: ChangeDetectionStrategy.OnPush` and ensure all state changes are immutable or signal-based.

---

## Change Detection Performance

### Complex expressions in templates

- **Pattern to check:** Method calls or complex computations in template bindings (e.g., `{{ calculateTotal(items) }}`).
- **Why it matters:** Template expressions are re-evaluated on every change detection cycle, potentially hundreds of times per second.
- **Fix:** Move computations to `computed()` signals or component properties updated explicitly. Use pure pipes for formatting.

### Excessive zone.js triggers

- **Pattern to check:** Third-party libraries (charts, maps, animations) running inside NgZone, triggering change detection on every mouse move or timer tick.
- **Why it matters:** Floods change detection with unnecessary cycles, causing jank and high CPU usage.
- **Fix:** Run non-Angular async work outside the zone with `NgZone.runOutsideAngular()`. Re-enter only when Angular state needs updating.

---

## Lazy Loading

### Eagerly loaded feature modules

- **Pattern to check:** Feature routes imported directly in the root `AppModule` or root routing config instead of using `loadChildren` / `loadComponent`.
- **Why it matters:** The entire feature bundle is included in the initial payload, increasing time-to-interactive.
- **Fix:** Use `loadComponent` for standalone components or `loadChildren` for route groups. Move feature code to lazy-loaded boundaries.

### Missing preload strategy

- **Pattern to check:** Lazy-loaded routes with no `PreloadingStrategy` configured, or using `PreloadAllModules` when selective preloading is more appropriate.
- **Why it matters:** Without preloading, lazy routes show a loading delay on first navigation. `PreloadAllModules` negates bundle splitting benefits on metered connections.
- **Fix:** Use `QuicklinkStrategy` or a custom strategy that preloads routes visible in the viewport.

### Barrel file re-exports pulling in entire libraries

- **Pattern to check:** `index.ts` barrel files that re-export everything from a library, causing tree-shaking failures.
- **Why it matters:** Importing one symbol from the barrel pulls in the entire module graph, inflating bundle size.
- **Fix:** Import directly from the specific file path, or restructure barrels to export only public API symbols.

---

## Forms

### Untyped FormGroup

- **Pattern to check:** `new FormGroup({})` or `this.fb.group({})` without generic type parameters (Angular 14+ typed forms).
- **Why it matters:** Form control values are typed as `any`, removing type safety and enabling runtime errors.
- **Fix:** Use `this.fb.group<MyFormType>({...})` or `new FormGroup<MyFormControls>({...})` with explicit type parameters.

### Missing form validation

- **Pattern to check:** Form controls without `Validators` or with validation only on the backend.
- **Why it matters:** Users receive no immediate feedback, causing frustration and unnecessary server round-trips.
- **Fix:** Add synchronous validators (`Validators.required`, `Validators.email`, custom validators). Use async validators for server-side checks.

### No error message display

- **Pattern to check:** Forms with validators but no corresponding error messages shown in the template.
- **Why it matters:** Validation silently prevents submission without telling the user what to fix.
- **Fix:** Add conditional error messages that check `control.errors` and `control.touched` or `control.dirty`.

---

## HTTP Handling

### Missing error handling on HTTP calls

- **Pattern to check:** `httpClient.get(...)` or `fetch()` calls without `.catch()`, `catchError`, or error callback.
- **Why it matters:** Network failures, 4xx, and 5xx responses are silently swallowed, leaving the user with a frozen UI.
- **Fix:** Handle errors explicitly. Show user-facing error messages. Log errors for debugging.

### No retry logic for transient failures

- **Pattern to check:** HTTP calls that fail permanently on the first network hiccup with no retry.
- **Why it matters:** Transient failures (timeouts, 503s) are common in production. A single retry often resolves the issue.
- **Fix:** Use RxJS `retry({ count: 2, delay: 1000 })` or implement retry with exponential backoff for critical calls.

### No HTTP caching strategy

- **Pattern to check:** Repeated identical GET requests on every component mount or navigation.
- **Why it matters:** Wastes bandwidth, increases latency, and puts unnecessary load on the API.
- **Fix:** Use HTTP cache headers, service-level caching, or Angular's `resource()` API with built-in caching.

---

## SOLID Violations

### God services (>300 lines or >7 dependencies)

- **Pattern to check:** Services with many injected dependencies or that exceed 300 lines.
- **Why it matters:** Indicates too many responsibilities. Hard to test, hard to reason about, high change risk.
- **Fix:** Split by domain responsibility. Extract focused services. Use facade pattern if multiple services must be coordinated.

### Components doing business logic

- **Pattern to check:** Components that contain data transformation, validation rules, API orchestration, or complex calculations.
- **Why it matters:** Business logic in components cannot be reused or tested independently. Violates single responsibility.
- **Fix:** Move business logic to services. Components should only handle presentation concerns (template, user events, navigation).

### Missing abstraction layer

- **Pattern to check:** Components directly calling `HttpClient` or accessing global state instead of going through a service.
- **Why it matters:** Tight coupling to HTTP layer makes components untestable without HTTP mocking. Changes to API require updating every component.
- **Fix:** Create data services that encapsulate API calls. Components inject the service, not HttpClient.

---

## Performance

### Missing trackBy on ngFor / @for

- **Pattern to check:** `*ngFor` without `trackBy` function, or `@for` without `track` expression.
- **Why it matters:** Without identity tracking, Angular destroys and recreates all DOM nodes on every change, causing layout thrashing and poor performance on large lists.
- **Fix:** Add `trackBy` returning a unique identifier (e.g., `item.id`), or use `track item.id` with the new `@for` syntax.

### Large bundles from unused imports

- **Pattern to check:** Imports of entire libraries when only a single function is needed (e.g., `import * as _ from 'lodash'`).
- **Why it matters:** Tree-shaking cannot remove unused code from namespace imports, inflating the bundle.
- **Fix:** Use specific imports: `import { debounce } from 'lodash-es/debounce'`. Audit with `source-map-explorer` or `webpack-bundle-analyzer`.

### Images without lazy loading

- **Pattern to check:** `<img>` tags without `loading="lazy"` or Angular's `NgOptimizedImage` directive for below-the-fold images.
- **Why it matters:** All images load eagerly, blocking initial render and wasting bandwidth on content not yet visible.
- **Fix:** Add `loading="lazy"` to below-the-fold images. Use `NgOptimizedImage` for automatic optimization.

---

## Lifecycle and Constructor Misuse

### Side effects in constructors

- **Pattern to check:** HTTP calls, DOM manipulation, subscriptions, or timer setup inside component constructors.
- **Why it matters:** The component is not fully initialized (no inputs, no view) when the constructor runs. Side effects may fail or behave unexpectedly.
- **Fix:** Move side effects to `ngOnInit` for initialization logic, or `ngAfterViewInit` for DOM-dependent work. Use `inject()` for DI only.

### Missing cleanup in ngOnDestroy

- **Pattern to check:** Components or services that create timers (`setInterval`), event listeners (`addEventListener`), or WebSocket connections without cleaning up.
- **Why it matters:** Resources leak on every navigation, accumulating until the tab crashes or behaves erratically.
- **Fix:** Clear timers, remove listeners, and close connections in `ngOnDestroy`. Use `DestroyRef.onDestroy()` for declarative cleanup.

### afterRender / afterNextRender misuse

- **Pattern to check:** Using `ngAfterViewInit` for DOM reads/writes that should use `afterRender` or `afterNextRender` (Angular 17+).
- **Why it matters:** `ngAfterViewInit` runs inside change detection, and DOM reads/writes can trigger layout thrashing.
- **Fix:** Use `afterRender()` for recurring DOM work or `afterNextRender()` for one-time initialization. Both run outside change detection.
