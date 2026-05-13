# Design: Auth session redirect, multi-user login, XS header fix

**Date:** 2026-05-12
**Status:** Approved

## Overview

Three related frontend improvements:
1. Redirect to `/login` automatically when a JWT expires mid-session (any API call returns 401).
2. Show a username field on the login page when the server reports `multiUser: true`.
3. Hide the "IoTSpy" logo text on XS screens so action buttons fit without overflow.

---

## 1. Session expiry redirect

### Problem

`apiFetch` throws `ApiError(401)` on expired tokens, but nothing intercepts this globally. `useAuthInit` only fires once at mount. Mid-session token expiry causes API failures with no redirect — the user stays on a broken page.

### Solution

**`src/api/client.ts`**

Add a module-level registered callback:

```ts
let onUnauthorized: (() => void) | null = null
export function setOnUnauthorized(cb: () => void) { onUnauthorized = cb }
```

In `apiFetch`, after receiving a 401 response, before throwing:
```ts
if (res.status === 401) {
  clearToken()
  onUnauthorized?.()
}
throw new ApiError(res.status, message)
```

**`src/hooks/useAuth.ts` — `useAuthInit`**

After a successful auth init (token verified), register the callback once:
```ts
setOnUnauthorized(() => {
  dispatch({ type: 'LOGOUT' })
  navigate('/login', { replace: true })
})
```

`LOGOUT` already calls `clearToken()` in the reducer, so no double-clear is needed — but `clearToken()` in `apiFetch` runs first (synchronously) to ensure the token is gone even if the React dispatch is delayed.

### Constraints
- The callback is registered only after a successful authenticated init. It is not registered during the initial unauthenticated state, so a 401 on login itself does not trigger a redirect loop.
- No cleanup is required — re-mounting re-registers.

---

## 2. Multi-user login

### Problem

`LoginPage` only shows a password field and hardcodes `username: 'admin'`. The backend already expects a `username` in `LoginRequest` and returns `multiUser?: boolean` in `AuthStatusResponse`.

### Solution

**`src/store/authStore.ts`**

Extend `AuthState`:
```ts
export interface AuthState {
  status: AuthStatus
  token: string | null
  multiUser: boolean       // new
}
```

Initial value: `multiUser: false`.

Add action:
```ts
| { type: 'SET_MULTI_USER'; value: boolean }
```

Reducer handles it: `return { ...state, multiUser: action.value }`.

**`src/hooks/useAuth.ts` — `useAuthInit`**

After `getAuthStatus()` resolves:
```ts
dispatch({ type: 'SET_MULTI_USER', value: multiUser ?? false })
```

**`src/pages/LoginPage.tsx`**

Read `multiUser` from `useAuthState()`. Conditionally render a username field:
- When `multiUser: true`: show username input (autoFocus), then password. Submit with the typed username.
- When `multiUser: false`: username field hidden; submit with `username: 'admin'` as before.

State: add `const [username, setUsername] = useState('')`.

Submit handler uses `username || 'admin'` only when `!multiUser`, or just `username` when `multiUser`.

---

## 3. XS header fix

### Problem

`header.css` has no responsive breakpoints. Budget analysis on a 375px phone (343px usable after padding): even hiding just the logo text, the status badge (~112px) + Start/Stop (~55px) + Root CA (~70px) + 4 icon buttons (~160px) + gaps (~48px) ≈ 473px — still ~130px over budget.

### Solution — layered compaction + overflow menu

**Breakpoint: ≤640px** (covers XS and SM phones/tablets in portrait):

**CSS compaction (`src/styles/header.css`):**
- Hide `.header__logo-text` (saves ~64px; icon stays)
- Hide `.proxy-status-badge span:last-child` (badge text) — dot-only indicator (saves ~92px)
- Hide the "Root CA" text span inside the Root CA button; show only its download SVG (saves ~38px)
- Hide `.header__btn--icon-group` buttons (Admin, Theme, Settings, Logout) — they move into the overflow menu

**Overflow menu (`src/components/layout/Header.tsx`):**
- Add `menuOpen: boolean` state to `Header`
- Add a `⋮` hamburger button (32px), visible only on ≤640px
- Render a `header__menu` dropdown (position: absolute, right-aligned below the button) containing: Admin link (if admin role), Theme toggle, Settings, Logout
- Click-outside closes the menu via a `useEffect` document click listener
- On ≥640px: hamburger hidden, all buttons shown inline as today

**Result on 375px:** Logo icon (28) + dot (22) + spacer + Start/Stop (55) + Root CA icon (32) + `⋮` (32) + gaps (~40) ≈ 209px — comfortably fits.

**`src/components/proxy/ProxyStatusBadge.tsx`:**
- Wrap the label text in `<span className="proxy-status-badge__label">` so CSS can target it independently.

**`src/components/layout/Header.tsx`:**
- Root CA `<a>` gets an inline download SVG and a `<span className="header__btn-label">Root CA</span>` (hidden on mobile via CSS).
- Admin/Theme/Settings/Logout buttons gain class `header__btn--desktop` (hidden on ≤640px).
- New `header__hamburger` button (hidden on ≥640px) + `header__menu` dropdown element.

---

## Files to change

| File | Change |
|---|---|
| `src/api/client.ts` | Add `setOnUnauthorized`, call callback + `clearToken()` on 401 |
| `src/store/authStore.ts` | Add `multiUser` to `AuthState`, add `SET_MULTI_USER` action |
| `src/hooks/useAuth.ts` | Dispatch `SET_MULTI_USER` in `useAuthInit`; register `setOnUnauthorized` callback after auth |
| `src/pages/LoginPage.tsx` | Conditional username field driven by `useAuthState().multiUser` |
| `src/components/proxy/ProxyStatusBadge.tsx` | Wrap label text in `.proxy-status-badge__label` span |
| `src/components/layout/Header.tsx` | Add overflow menu state + hamburger button + dropdown; label spans on Root CA and desktop-only class on icon buttons |
| `src/styles/header.css` | Media query ≤640px: hide logo text, badge label, Root CA label, desktop-only buttons; add hamburger + dropdown styles |

## Out of scope

- Backend changes (already supports multi-user, `LoginRequest.username` already exists)
- Token refresh / silent re-auth (not in scope — redirect is the correct UX for expired sessions)
