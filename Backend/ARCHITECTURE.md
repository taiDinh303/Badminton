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

API → Services + Repositories; Services → contracts/Core/ModelViews; Repositories → repository contracts/Core. Contract.Services must not reference Repositories. UniqueConstraintException crosses the persistence boundary instead of DbUpdateException/SqlException. SQL Server remains the only implemented provider; raw SQL in business operations is a documented migration limitation.

ICurrentUser exposes a framework-neutral ClaimsPrincipal. API provides HttpCurrentUser; tests use a mutable identity fixture. Shared staff guards verify the active database account and assigned branch for manager reads/lifecycle/check-in. Existing service constructors are composed in DI, not controllers.

Projects target net8.0; global.json selects an installed-compatible SDK for .slnx. dotnet tool restore restores pinned EF tooling. Production migration/demo seed is forbidden at startup; Development/isolated Testing uses explicit flags. --check-config validates without building a host or contacting services.
