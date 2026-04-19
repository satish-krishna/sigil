# esbuild + Vitest Migration Plan

**Branch:** `feat/spartan-ng-migration` (current)
**NX version:** 22.5.2
**Angular version:** 21.1.x
**Status:** Ready to implement

Do **both phases in a single fresh context**. Phase 1 (esbuild) unblocks Phase 2 (Vitest with `vitest-analog`).

---

## Why Both Together

- `ts-jest` 29 forcibly overwrites `moduleResolution` to `Node10`, breaking Angular 21 subpath exports. We patched with `tsconfig.base.json` paths — fragile.
- `@angular/build:unit-test` (Vitest Angular) requires a `buildTarget` pointing to `@angular/build:application` — only works after esbuild migration, and only for the app. Lib projects still have no build target, so `vitest-analog` remains the right choice for all 17 projects regardless.
- NX 22+ defaults new Angular 21+ projects to Vitest + esbuild. We're bringing the existing workspace up to that standard.

---

## Phase 1: esbuild Migration (apps/frontend only)

### What changes

**`apps/frontend/project.json`** — replace three targets:

#### `build` target (before)

```json
{
  "executor": "@nx/angular:webpack-browser",
  "outputs": ["{options.outputPath}"],
  "defaultConfiguration": "production",
  "options": {
    "outputPath": "dist/apps/frontend",
    "index": "apps/frontend/src/index.html",
    "main": "apps/frontend/src/main.ts",
    "polyfills": ["zone.js"],
    "tsConfig": "apps/frontend/tsconfig.app.json",
    "assets": ["apps/frontend/src/favicon.ico", "apps/frontend/src/assets"],
    "styles": ["apps/frontend/src/styles.scss"],
    "scripts": []
  },
  "configurations": {
    "production": { "optimization": true },
    "development": { "optimization": false }
  }
}
```

#### `build` target (after)

```json
{
  "executor": "@angular/build:application",
  "outputs": ["{options.outputPath}"],
  "defaultConfiguration": "production",
  "options": {
    "outputPath": "dist/apps/frontend",
    "index": "apps/frontend/src/index.html",
    "browser": "apps/frontend/src/main.ts",
    "polyfills": ["zone.js"],
    "tsConfig": "apps/frontend/tsconfig.app.json",
    "assets": ["apps/frontend/src/favicon.ico", "apps/frontend/src/assets"],
    "styles": ["apps/frontend/src/styles.scss"],
    "scripts": []
  },
  "configurations": {
    "production": { "optimization": true },
    "development": { "optimization": false }
  }
}
```

> **Key change:** `executor` + rename `main` → `browser`.

#### `serve` target (before)

```json
{
  "executor": "@nx/angular:webpack-server",
  "defaultConfiguration": "development",
  "options": { "browserTarget": "frontend:build" },
  "configurations": {
    "production": { "browserTarget": "frontend:build:production" },
    "development": { "browserTarget": "frontend:build:development" }
  }
}
```

#### `serve` target (after)

```json
{
  "executor": "@angular/build:dev-server",
  "defaultConfiguration": "development",
  "options": { "buildTarget": "frontend:build" },
  "configurations": {
    "production": { "buildTarget": "frontend:build:production" },
    "development": { "buildTarget": "frontend:build:development" }
  }
}
```

> **Key change:** `executor` + rename `browserTarget` → `buildTarget`.

#### `extract-i18n` target (before)

```json
{
  "executor": "@nx/angular:extract-i18n",
  "options": { "browserTarget": "frontend:build" }
}
```

#### `extract-i18n` target (after)

```json
{
  "executor": "@angular/build:extract-i18n",
  "options": { "buildTarget": "frontend:build" }
}
```

### No other files need changing

- `apps/frontend/tsconfig.app.json` — clean, no webpack-specific settings, no changes needed.
- No custom `webpack.config.js` exists — vanilla migration.
- `@angular-devkit/build-angular` already in `devDependencies`; `@angular/build` ships as part of Angular CLI 21 (`@angular/cli: ^21.1.0`) — already available.

### Verification (Phase 1)

```bash
npx nx run frontend:build --configuration=development --skip-nx-cache
npx nx run frontend:serve --configuration=development --skip-nx-cache
# Verify app loads at http://localhost:4200
```

---

## Phase 2: Vitest Migration (all 17 projects)

### Approach: `vitest-analog` (`@nx/vitest` + `@analogjs/vite-plugin-angular`)

**Why NOT `vitest-angular` (`@angular/build:unit-test`):**

- Even after esbuild migration, `@angular/build:unit-test` only works for the `frontend` app (needs a `buildTarget`).
- All 11 library projects have **no build target** in their `project.json` — incompatible.
- It is marked `[EXPERIMENTAL]` in Angular 21.
- `vitest-analog` works for both app and lib projects regardless of build target. Consistent approach across the whole workspace.

### Packages

#### Add

```bash
npm install -D @nx/vitest vitest @analogjs/vite-plugin-angular @vitest/coverage-v8
```

> **Verify peer deps first:** `@nx/vitest@22.x` requires `vitest >= 1.3.0`. Check `@nx/vitest@22.5.x` peer requirements against Angular 21.

#### Remove (after all projects migrated and green)

```bash
npm uninstall @nx/jest jest jest-environment-jsdom jest-preset-angular ts-jest @types/jest
```

---

## tsconfig.base.json Cleanup (after Vitest migration)

**Remove** the ts-jest workaround paths — Vitest uses native ESM with package `exports` and resolves these correctly without overrides:

```json
// DELETE these three entries from compilerOptions.paths:
"@angular/common/http": ["node_modules/@angular/common/types/http.d.ts"],
"@angular/common/http/testing": ["node_modules/@angular/common/types/http-testing.d.ts"],
"@angular/router/testing": ["node_modules/@angular/router/types/testing.d.ts"]
```

---

## nx.json Update

Change the generator default from jest to vitest-analog:

```json
// nx.json — generators section
{
  "generators": {
    "@nx/angular:library": {
      "linter": "eslint",
      "unitTestRunner": "vitest" // was: "jest"
    },
    "@nx/angular:component": {
      "style": "css"
    }
  }
}
```

---

## Shared Vitest Config

Create a shared base config at workspace root:

**`vitest.shared.ts`**:

```typescript
import { defineConfig } from 'vitest/config';
import angular from '@analogjs/vite-plugin-angular';

export default defineConfig({
  plugins: [angular()],
  test: {
    globals: true, // jest-compatible globals (describe/it/expect/vi)
    environment: 'jsdom',
    setupFiles: [],
    reporters: ['default'],
    coverage: {
      provider: 'v8',
    },
  },
});
```

---

## Projects Inventory

### Projects with spec files (6) — full migration

| Project                         | Path                               | Spec files        |
| ------------------------------- | ---------------------------------- | ----------------- |
| `frontend`                      | `apps/frontend`                    | 4 specs (layouts) |
| `@siora/frontend/core`          | `libs/frontend/core`               | 10 specs          |
| `@siora/frontend/features/auth` | `libs/frontend/features/auth`      | 3 specs           |
| `@siora/frontend/shared`        | `libs/frontend/shared`             | 1 spec            |
| `frontend-features-dashboard`   | `libs/frontend/features/dashboard` | 4 specs           |
| `frontend-ui-ui-formly-helm`    | `libs/frontend/ui/ui-formly-helm`  | 10 specs          |

### Projects with no spec files (8) — config changes only (`passWithNoTests: true`)

| Project                             | Path                                     |
| ----------------------------------- | ---------------------------------------- |
| `frontend-ui-ui-button-helm`        | `libs/frontend/ui/ui-button-helm`        |
| `frontend-ui-ui-alert-helm`         | `libs/frontend/ui/ui-alert-helm`         |
| `frontend-ui-ui-card-helm`          | `libs/frontend/ui/ui-card-helm`          |
| `frontend-ui-ui-sheet-helm`         | `libs/frontend/ui/ui-sheet-helm`         |
| `frontend-ui-ui-avatar-helm`        | `libs/frontend/ui/ui-avatar-helm`        |
| `frontend-ui-ui-separator-helm`     | `libs/frontend/ui/ui-separator-helm`     |
| `frontend-ui-ui-badge-helm`         | `libs/frontend/ui/ui-badge-helm`         |
| `frontend-ui-ui-dropdown-menu-helm` | `libs/frontend/ui/ui-dropdown-menu-helm` |

### Projects with no test target (3) — add test target only

| Project                       | Path                               |
| ----------------------------- | ---------------------------------- |
| `frontend-features-events`    | `libs/frontend/features/events`    |
| `frontend-features-messaging` | `libs/frontend/features/messaging` |
| `frontend-features-vendors`   | `libs/frontend/features/vendors`   |

> `ui-helm` (`libs/ui`) — no test target, leave as-is (not in scope).

---

## Per-Project Migration Steps (Phase 2)

For **each** of the 14 frontend projects (6 with specs + 8 no-spec helm projects):

### Step 1: Delete jest config

```bash
rm libs/<project-path>/jest.config.ts
```

### Step 2: Create `vite.config.ts`

```typescript
// libs/<project-path>/vite.config.ts
import { defineConfig } from 'vitest/config';
import angular from '@analogjs/vite-plugin-angular';

export default defineConfig({
  plugins: [angular()],
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: ['src/test-setup.ts'],
    include: ['src/**/*.spec.ts'],
    reporters: ['default'],
    coverage: {
      reportsDirectory: '../../coverage/<project-name>',
      provider: 'v8',
    },
    passWithNoTests: true, // for helm projects with no specs
  },
});
```

### Step 3: Update `tsconfig.spec.json`

Remove jest-preset-angular specific options. New minimal spec tsconfig:

```json
{
  "extends": "./tsconfig.json",
  "compilerOptions": {
    "outDir": "../../dist/out-tsc",
    "types": ["vitest/globals", "node"]
  },
  "files": ["src/test-setup.ts"],
  "include": ["src/**/*.spec.ts", "src/**/*.test.ts", "src/**/*.d.ts"]
}
```

> **Remove from compilerOptions:** `"module": "commonjs"`, `"customConditions"`, any jest-specific overrides.

### Step 4: Update `src/test-setup.ts`

```typescript
// New (vitest + zone.js):
import '@angular/core/testing';
import { getTestBed } from '@angular/core/testing';
import {
  BrowserDynamicTestingModule,
  platformBrowserDynamicTesting,
} from '@angular/platform-browser-dynamic/testing';

getTestBed().initTestEnvironment(BrowserDynamicTestingModule, platformBrowserDynamicTesting());
```

> Check each project's current `test-setup.ts` — some may already use this pattern (dashboard, events, messaging, vendors have fresh `test-setup.ts` stubs), others import `jest-preset-angular/setup-jest`.

### Step 5: Update `project.json` test target

```json
{
  "targets": {
    "test": {
      "executor": "@nx/vitest:test",
      "outputs": ["{workspaceRoot}/coverage/{projectRoot}"],
      "options": {
        "config": "{projectRoot}/vite.config.ts"
      }
    }
  }
}
```

> Remove `"jestConfig"` and `"passWithNoTests"` options (passWithNoTests moves to `vite.config.ts`).

### For the 3 projects with no test target (events, messaging, vendors)

Same steps 1-4 above, but Step 5 is **adding** the test target (they currently have none):

```json
{
  "targets": {
    "test": {
      "executor": "@nx/vitest:test",
      "outputs": ["{workspaceRoot}/coverage/{projectRoot}"],
      "options": {
        "config": "{projectRoot}/vite.config.ts"
      }
    }
  }
}
```

---

## Spec File Changes

### Minimal changes needed (globals mode = true)

With `globals: true` in vitest config, `describe`, `it`, `expect`, `beforeEach`, `afterEach` all work without imports — identical to jest.

**The only required changes:**

| Jest             | Vitest         | Files affected   |
| ---------------- | -------------- | ---------------- |
| `jest.fn()`      | `vi.fn()`      | ALL spec files   |
| `jest.mock()`    | `vi.mock()`    | None currently   |
| `jest.spyOn()`   | `vi.spyOn()`   | ALL spec files   |
| `jest.Mocked<T>` | `vi.Mocked<T>` | auth, core specs |

> **Grep to find all occurrences before starting:**
>
> ```bash
> grep -r "jest\." libs/frontend apps/frontend/src --include="*.spec.ts" -l
> ```

---

## Migration Order (recommended)

1. **Phase 1: esbuild**
   - Update `apps/frontend/project.json` (build, serve, extract-i18n targets)
   - Verify: `npx nx run frontend:build --configuration=development`
   - Verify: `npx nx run frontend:serve` loads at localhost:4200

2. **Install Vitest packages**
   - `npm install -D @nx/vitest vitest @analogjs/vite-plugin-angular @vitest/coverage-v8`

3. **Create `vitest.shared.ts`** at workspace root

4. **Migrate `@siora/frontend/shared`** first (1 spec, simplest) — proves the pattern

5. **Migrate `@siora/frontend/core`** (10 specs, most complex — verifies HTTP module resolution works natively without the tsconfig.base.json hacks)

6. **Migrate `@siora/frontend/features/auth`** (3 specs)

7. **Migrate `frontend-features-dashboard`** (4 specs)

8. **Migrate `frontend` app** (4 specs)

9. **Migrate `frontend-ui-ui-formly-helm`** (10 specs)

10. **Batch-migrate the 8 no-spec helm projects** (just config changes — Steps 1, 2, 3, 4, 5)

11. **Add test target to events, messaging, vendors** (no spec files — Steps 2, 3, 4 + add target)

12. **Remove jest packages**

    ```bash
    npm uninstall @nx/jest jest jest-environment-jsdom jest-preset-angular ts-jest @types/jest
    ```

13. **Clean `tsconfig.base.json` paths** (remove the 3 Angular subpath workarounds)

14. **Update `nx.json` generator defaults** (`unitTestRunner: "vitest"`)

15. **Full verification:**
    ```bash
    npx nx run-many --target=test --all --skip-nx-cache
    ```

---

## Verification

After each project migration:

```bash
npx nx run <project>:test --skip-nx-cache
```

Final full verification:

```bash
npx nx run-many --target=test --all --skip-nx-cache
```

Expected: All projects green, zero jest references in any config file.

---

## Gotchas / Watch Out For

1. **`jest.Mocked<T>` type** — used in `auth.service.spec.ts` and `login.page.spec.ts`. Replace with `import type { Mocked } from 'vitest'` and use `Mocked<T>`.

2. **`@angular/common/http/testing` imports** — these will resolve correctly in Vitest without the `tsconfig.base.json` path hacks. Confirm after first migration (step 5 — core lib).

3. **zone.js in test-setup** — `jest-preset-angular/setup-jest` patches zone.js for jest. With Vitest, use the `getTestBed().initTestEnvironment()` pattern above. Check each `test-setup.ts`.

4. **`RouterTestingModule`** — still imported in some dashboard spec files. Works fine with Vitest, but the recommended pattern going forward is `provideRouter([])`.

5. **`passWithNoTests`** — moves from `jest.config.ts` to the `test` section of `vite.config.ts`. Already handled by using `passWithNoTests: true` in the vite config template above.

6. **NX cache** — always use `--skip-nx-cache` when verifying migration changes.

7. **`@nx/angular` peer deps** — check that `@nx/vitest@22.x` is compatible with `@nx/angular@22.3.x` in the workspace before installing.

8. **esbuild + `@angular-devkit/build-angular`** — after migrating to `@angular/build:application`, `@angular-devkit/build-angular` is no longer needed for the build target but may still be required by NX internally. Leave it in `devDependencies` for now; remove only if no errors appear after a clean build.

9. **`apps/frontend/tsconfig.app.json`** — no webpack-specific settings exist; file is clean and needs no changes for esbuild migration.
