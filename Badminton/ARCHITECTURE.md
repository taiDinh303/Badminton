# Badminton Booking API — Architecture (docs 01–05)

This repo implements the V1 booking domain from `docs/01`–`05`.
The design is **4-layer, modular**, with each functional concern isolated in its own folder.

```
Badminton/
├── 1. SERVER/   Badminton/                  API: controllers, middleware, seed, jobs
│              Controllers/<feature>/        One folder per feature:
│                 Shared/ BaseApiController + DatabaseTestController
│                 Auth/ Branch/ Court/ CourtType/ TimeSlot/
│                 Booking/ Payment/ Voucher/ Group/ Review/ Admin/
│              Middleware/ ExceptionMiddleware
│              Jobs/          PendingBookingExpiryJob + VoucherExpiryJob
│              Program.cs     auto-migrate + seed, jobs registered as IHostedService
├── 2. SERVICES/ Services/
│                 Shared/       BaseService (UoW + IHttpContextAccessor + audit helpers)
│                 Auth/         AuthService (phone-first, BCrypt dev fallback) + JwtGenerator
│                 Pricing/      PricingService (SINGLE price function, docs 04 §2)
│                 Branch/       BranchService
│                 Court/        CourtService (peak/low + weekend surcharge)
│                 TimeSlot/     TimeSlotService (is_peak flag)
│                 Booking/      BookingService (1 slot/day, window, limits, idempotency,
│                                              anti-overbooking via partial unique index)
│                 Payment/      PaymentService (init + idempotent webhook + refund NEW record)
│                                 + MockOnlineGateway (VnPay/MoMo/Zalopay)
│                                 + OnSiteGateway (Cash/QR) + PaymentGatewayFactory
│                 Voucher/      VoucherService (percent cap / fixed, min_spend, 1-use)
│                 Points/       PointService (1đ = 1đ point, clawback on refund)
│                 Group/        GroupService (owner-only booking, max 10 members)
│                 Review/       ReviewService (completed-only, rolling avg)
│                 Audit/        AuditService (append-only before/after JSON)
│                 Maintenance/  MaintenanceService (locks slots, never auto-cancels)
│                 DI/           ServiceRegistration (composition root)
│             Contract.Services/  Service interfaces, one folder per feature
│             ModelViews/         DTOs, one folder per feature
├── 3. REPOSITORY/ Repositories/
│                 Context/       BadmintonBookingDbContext (15 tables, partial unique index)
│                                 DesignTimeDbContextFactory
│                 UnitOfWork/    GenericRepository<T> + UnitOfWork
│                 Migrations/    InitialCreate (2026-10-13)
│             Contract.Repositories/
│                 Entity/        15 entity classes
│                 UnitOfWork/    IUnitOfWork + IGenericRepository<T>
├── 4. CORE/   Core/
│              Base/             IntBaseEntity, BaseResponse, BaseException, StatusCodeHelper
│              Domain/           IPaymentGateway (VnPay/MoMo/Zalopay/Cash/QR abstractions)
│              Enums/            Role, BranchStatus, CourtDateStatus, CourtSurface,
│                                 BookingStatus, PaymentMethod, PaymentStatus, VoucherType,
│                                 UserStatus
│              Store/            JwtSettings
│              Utils/            CoreHelper, EnumHelper, TimeHelper
```

## Docs → Code mapping

| Doc rule | Where it lives |
|---|---|
| 01 §2: 1 active admin (DB constraint) | `Users` filtered unique index `ux_active_admin` |
| 01 §3: 1 booking = 1 slot of 1 court in 1 day | `Booking` entity + `ux_court_slot_active` |
| 01 §4: 4 slots/day individual, 8/day group | `BookingService.CountActiveBookings` |
| 01 §4: pending TTL 10 min | `PendingBookingExpiryJob` (1-min poll) |
| 01 §4: refund tiers 2h/1h | `RefundPolicy.Tier` (pure function) |
| 01 §5: 1đ = 1đ point; clawback on refund | `PointService.Award/Clawback` |
| 01 §5: voucher percent cap / fixed, min_spend, 1-use | `VoucherService.PreviewDiscountAsync` + `voucher_redemptions` unique (v_id, u_id) |
| 02 §3: anti-overbooking index | partial unique `(court_id, date, slot_id) WHERE status IN (Pending, Confirmed)` |
| 02 §4: refund = NEW record | `PaymentService.RefundAsync` writes a new row, never mutates the success row |
| 02 §4: webhook is source of truth, idempotent | `PaymentService.HandleWebhookAsync` — duplicate `gateway_ref` no-ops |
| 04 §2: single price function | `PricingService` — UI, payment, dashboard all read from it |
| 04 §2: polymorphic payment | `IPaymentGateway` + `IPaymentGatewayFactory`; add a gateway = 1 class + 1 DI line |
| 04 §4: `idempotency_key` in booking create | `Booking.IdempotencyKey` (unique filtered index) |
| 03 §5: maintenance locks slots, doesn't cancel confirmed | `CourtDateStatus` + `BookingService` check |
| 05: test strategy — unit-testable pricing/refund | `PricingService` + `RefundPolicy` are pure, no DB |

## Decisions (dev vs. production)

- **DB**: LocalDB (SQL Server) for dev/test; **Supabase Postgres** in production (docs 02, 04).
  The EF model is provider-agnostic (only `UseSqlServer` → `UseNpgsql` switch).
- **Auth**: dev = phone + BCrypt password + local JWT.
  Production = Supabase Auth phone OTP (docs 01 §1); the `IAuthService` surface is unchanged.
- **Payment gateways**: `MockOnlineGateway` for VnPay/MoMo/Zalopay; replace with real
  adapter classes when sandbox keys are available (Open/Closed — no caller changes).
- **Jobs**: `PendingBookingExpiryJob` + `VoucherExpiryJob` run as `IHostedService`.
  Production switches to Hangfire on Supabase Postgres (docs 04 §2, 06) — job bodies are identical.
- **CQRS-lite / MediatR** (docs 04 §2): NOT implemented; the current 4-layer + feature
  folder structure is the agreed dev baseline. Add MediatR if request volume warrants it.

## Build + E2E status (2026-10-13)

- `dotnet build Badminton.slnx` → **0 errors / 0 warnings**.
- Migration `20261013131702_InitialCreate` applied to LocalDB (15 tables).
- Live E2E (run on `http://localhost:5131`):

| # | Scenario | Result |
|---|---|---|
| 1 | Create booking (court 1, slot 13, peak) → `Pending` | ✅ 200 |
| 2 | On-site confirm (admin, QR) → payment `Success`, booking `Confirmed` | ✅ 200 |
| 3 | Refund `>=2h` before slot start → 100%, new `Refunded` record | ✅ 200 |
| 4 | 2nd admin self-register (`role=Admin`) → 409 `DUPLICATE` | ✅ 409 |
| 5 | Voucher 20% cap 50k, court 2 peak 220k → discount 44k, payable 176k | ✅ 200 |
| 6 | Group create + owner-only booking | ✅ 200 |
| 7 | Maintenance set (court 1, tomorrow) | ✅ 200 |
| 8 | Book a locked court day → 409 `SLOT_CONFLICT` | ✅ 409 |
| 9 | 5 slots/day limit → 4 ok, 5th → 409 | ✅ 4×200 + 409 |
| 10 | Webhook success ×2 (idempotent) → booking `Confirmed`, points awarded once | ✅ 200 |

## Pending (not yet in code, documented in worklog)

- [ ] Supabase project + Postgres migration cutover (drop `UseSqlServer`, add `Npgsql`)
- [ ] Supabase Auth phone OTP (replace BCrypt dev auth)
- [ ] Real VnPay/MoMo/Zalopay gateway adapters (replace `MockOnlineGateway`)
- [ ] Hangfire job persistence (replace `IHostedService` jobs)
- [ ] RLS policies on Supabase Postgres (docs 02 §5, 05 §3)
- [ ] Load test (docs 05 §5): 50+ concurrent requests → 0 double-booking, p95 < 300ms
- [ ] UI (React + Vite SPA, docs 04 §3, 07) — separate repo / project
- [ ] Dashboard + reports API (`GET /api/v1/admin/reports`)
