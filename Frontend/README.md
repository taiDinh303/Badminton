# Badminton React SPA

```text
src/app/                 Router, permissions, providers, navigation, query client
src/modules/<feature>/   Auth, customer, booking, account, groups, reviews, waitlist,
                         check-in and admin pages
src/services/http.ts     Fetch, envelope and session expiry
src/services/api/        Typed endpoint contracts
src/components/          Shared layout and UI primitives
src/store/               Auth identity
src/utils/               Vietnam date/currency formatting
src/theme.css            Semantic design tokens
config/environment.mjs   API/proxy validation shared with Vite and Node tests
```

From Frontend: npm ci; npm test; npm run lint; npm run build. npm run dev starts port 5173 with /api proxy to localhost:5131. API_PROXY_TARGET overrides the dev proxy origin. VITE_API_URL accepts /api/v1 or an absolute HTTPS base ending in /api/v1 (HTTP only for loopback).

The white/gray application shell follows the reference style while retaining Badminton's green/yellow sport brand. Desktop sidebar and mobile menu use the same navigation registry; admin-only pages are guarded. Guards aid navigation and never replace API authorization.

npx playwright install chromium then npm run test:e2e exercises the built SPA with mocked API. Backend HTTP tests separately exercise JWT/controllers/SQL on an isolated local DB. Mocked browser tests do not prove payment providers or cloud operation.

See [configuration](../docs/13-configuration.md), [UI](../docs/07-ui-design-system.md) and [worklog](../docs/worklog.md).
