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

`header.css` has no responsive breakpoints. On narrow screens (≤480px), the logo text ("IoTSpy") plus the ProxyStatusBadge plus 5–6 action buttons overflow horizontally.

### Solution

**`src/styles/header.css`** — append:

```css
@media (max-width: 480px) {
  .header__logo-text { display: none; }
}
```

The 28×28px icon remains visible. The freed ~60px is enough to accommodate the action buttons. No JS changes needed.

---

## Files to change

| File | Change |
|---|---|
| `src/api/client.ts` | Add `setOnUnauthorized`, call callback + `clearToken()` on 401 |
| `src/store/authStore.ts` | Add `multiUser` to `AuthState`, add `SET_MULTI_USER` action |
| `src/hooks/useAuth.ts` | Dispatch `SET_MULTI_USER` in `useAuthInit`; register `setOnUnauthorized` callback after auth |
| `src/pages/LoginPage.tsx` | Conditional username field driven by `useAuthState().multiUser` |
| `src/styles/header.css` | Add XS media query hiding `.header__logo-text` |

## Out of scope

- Backend changes (already supports multi-user, `LoginRequest.username` already exists)
- Hamburger/overflow menu for action buttons (deferred)
- Token refresh / silent re-auth (not in scope — redirect is the correct UX for expired sessions)
