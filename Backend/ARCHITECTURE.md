# Backend architecture and naming

The maintained specification is [docs/04-system-architecture.md](../docs/04-system-architecture.md). Start with [docs/00-README.md](../docs/00-README.md) for current requirements, limitations and evidence.

```text
Backend/
  API/                    Badminton.API.csproj — HTTP, middleware, jobs, startup
  Services/               Business service implementations
  Contract.Services/      Service interfaces
  Repositories/           EF context, migrations, persistence
  Contract.Repositories/  Entities and repository interfaces
  ModelViews/             Request/response DTOs
  Core/                   Shared types and domain abstractions
  Tests/                  Badminton.Tests.csproj — service/database integration
  Badminton.slnx          Solution
```

API namespace: Badminton.API. Core namespace: Badminton.Core. Test namespace: Badminton.Tests. Folders name the layer; executable/test projects retain the product prefix. The EF context file is Context/BadmintonBookingDbContext.cs, matching its class.

The solution needs the SDK selected in [global.json](../global.json), while projects target .NET 8. Restore EF tools with dotnet tool restore; the local manifest pins the tool to the EF package version.

From the repository root, run build.ps1 or follow [local setup](../docs/10-setup-local.md). The script does not stop a running API unless -StopApi is supplied. -Run starts the API using its Development http profile.

PostgreSQL/OTP/Storage and real online payment adapters remain planned; renaming projects does not implement them. Database table names, migration history and HTTP endpoints are unchanged.
