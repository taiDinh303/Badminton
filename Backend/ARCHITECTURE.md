# Badminton backend architecture

[Source of maintained specification](../docs/04-system-architecture.md) · [Configuration](../docs/13-configuration.md) · [Local setup](../docs/10-setup-local.md)

```text
API/Configuration/       Composition, pure startup validation, health endpoints
API/Bootstrap/           Guarded database initialization
API/Security/            HTTP adapter for Core ICurrentUser
API/Controllers/         Feature HTTP boundary and role attributes
API/Jobs/                Five in-process workers and SQL application locks
Services/                Booking/payment/points/etc. business services and adapters
Contract.Services/       Business interfaces; no persistence dependency
Repositories/            EF context/migrations/UnitOfWork/provider error mapping/DI
Contract.Repositories/   Entity and repository contracts; no EF
Core/                    Shared domain, errors, settings and ICurrentUser
ModelViews/              Request/response DTOs
Tests/                   Config/architecture, service/SQL and real HTTP tests
```

API → Services + Repositories; Services → contracts/Core/ModelViews; Repositories → repository contracts/Core. Contract.Services must not reference Repositories. UniqueConstraintException crosses the persistence boundary instead of DbUpdateException/SqlException. SQL Server and PostgreSQL have separate migration histories. Typed IBookingCommands/IPaymentCommands/IVoucherCommands own conditional writes in Repositories; business services and workers do not submit SQL strings.

ICurrentUser exposes a framework-neutral ClaimsPrincipal. API provides HttpCurrentUser; tests use a mutable identity fixture. Shared staff guards verify the active database account and assigned branch for manager reads/lifecycle/check-in. Existing service constructors are composed in DI, not controllers.

Projects target net8.0; global.json selects an installed-compatible SDK for .slnx. dotnet tool restore restores pinned EF tooling. Production migration/demo seed is forbidden at startup; Development/isolated Testing uses explicit flags. --check-config validates without building a host or contacting services.

## Booking consistency (CURRENT — 24/10/2026)

BookingService preserves the existing HTTP-facing service contract and delegates creation, availability and lifecycle work to focused collaborators. BookingCompletionService is shared by manual completion and AutoCompleteJob; BookingExpiryService owns expiry, voucher release and point restoration. State changes and point ledger entries commit together. Voucher release rechecks after account/voucher locks and conditionally deletes the reservation before decrementing its counter. No-show counters share the account lock. Nested unit-of-work callbacks join the outer transaction; exceptions must propagate to its owner. Root rollback clears tracked entities.

Point mutations acquire a transaction-owned account lock on both providers before checking the ledger/balance. Booking creation also checks daily limits under that lock. Idempotent creation locks the request key and compares a versioned SHA-256 fingerprint of the full normalized intent. RequestFingerprint is a nullable additive column: legacy payloads cannot be reconstructed, so old keys without a fingerprint fail closed rather than silently accepting a changed intent.

Availability publishes date-effective unit prices, including weekend surcharge, through the existing fields; historical booking totals remain snapshots. Membership lookup failures roll back completion. Earned points are persisted from the actual ledger and full refunds claw back that snapshot proportionally.

LoginAttemptTracker uses synchronized, bounded per-process state and TimeProvider; it enforces the configured 5-failure / 10-minute window and 15-minute lockout. This is not distributed lockout. Payment gateway side effects still require provider idempotency/reconciliation: a local transaction cannot roll back a successful external refund after a process failure. Sandbox certification and PostgreSQL runtime regression must be recorded separately from SQL Server evidence.
