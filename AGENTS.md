# Badminton working agreement

Read docs/00-README.md, docs/08-plan.md, docs/13-configuration.md and the latest worklog before edits. The active human task defines scope.

- Keep booking/payment/points/branch scope requirements specific to Badminton. Reference repos supply patterns, never accounts, credentials, domains or unrelated business policy.
- Preserve user changes, existing HTTP contracts, table names and historical migrations. Label CURRENT/TARGET/NOT VERIFIED separately.
- Core/contracts do not depend on persistence or ASP.NET HTTP. Repository owns EF registration and provider-error mapping. API composes adapters, auth, initialization and jobs. Services consume ICurrentUser and repository contracts.
- Production never auto-migrates or seeds demo data. --check-config must be side-effect free and sanitized. Never use tests, load helpers or migration commands against live data without authorization.
- Frontend modules own feature pages; shared app/providers/navigation, API transport/contracts and theme remain separate. Clear cached user data on identity changes and enforce permissions on the server.
- Run meaningful configuration, architecture, SQL/HTTP and browser regressions for affected boundaries; record real commands, counts and limits in worklog. Build is not production acceptance.
- Keep secrets, generated artifacts and local overrides outside Git. Backlog alone does not authorize deploying or purchasing services.
