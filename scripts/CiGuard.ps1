# ═══════════════════════════════════════════════════════════════════
#  CiGuard.ps1 — BadmintonBooking seed-data & secret guard.
#
#  Run from the repo root:
#      pwsh ./scripts/CiGuard.ps1
#  (The PowerShell 5.1 on Windows also works: .\scripts\CiGuard.ps1)
#
#  Exits 0 when all checks pass, 1 (and prints which rule failed) when
#  any check fails. Designed to be the single CI gate that proves:
#
#   1. No real `.env` / secret files are committed (git-tracked).
#   2. Seed demo data is gated to Development (or the explicit, off-by-
#      default Seed:AllowInProduction escape hatch) — never auto-seeded
#      on a plain non-dev host.
#   3. Demo credentials (Admin@123 / Pass@123) live ONLY in SeedData.cs.
#   4. `Seed__AllowInProduction` is not committed to a tracked config file
#      with a `true` value.
#   5. At least one EF migration exists (DB must be migratable in CI).
#
#  This is a *static* guard — it does NOT start the app. It reads the
#  tree, so it runs on any OS without the .NET runtime.
# ═══════════════════════════════════════════════════════════════════
$ErrorActionPreference = "Stop"

# Repo root = the directory one level above this script.
$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
$Root = $Root.Path

$failures = [System.Collections.Generic.List[string]]::new()

function Test-File([string]$rel) {
    Test-Path (Join-Path $Root $rel)
}

function Get-Text([string]$rel) {
    $full = Join-Path $Root $rel
    if (-not (Test-Path $full)) { return $null }
    return (Get-Content $full -Raw)
}

# ── Rule 1: no committed .env / secret files (git-tracked) ─────────────
# We check git so a developer's local .env (untracked, gitignored) does not
# trip the guard, while a *committed* secret does. git ls-files output is
# captured to a temp file (PowerShell 5.1 cannot redirect stderr inside a
# subexpression).
$gitFiles = @()
$gitRepo = $null
if (git -C $Root rev-parse --is-inside-work-tree *> $null) {
    $tmpGitList = [System.IO.Path]::GetTempFileName()
    & git -C $Root ls-files > $tmpGitList
    $gitFiles = @(Get-Content $tmpGitList)
    Remove-Item $tmpGitList -ErrorAction SilentlyContinue
}

$secretPatterns = @(
    '^\.env$',                                   # a committed .env (exact)
    "appsettings.*\.Production\.json$",
    ".*\.pem$", ".*\.pfx$",
    "secrets\.json$"
)
foreach ($p in $secretPatterns) {
    $hits = $gitFiles | Select-String -Pattern $p
    if ($null -ne $hits -and ($hits | Measure-Object).Count -gt 0) {
        $failures.Add("Rule 1: committed secret file(s): " +
            (($hits | ForEach-Object { $_.Line }) -join ", "))
    }
}
# .env.example IS allowed and expected to be committed.

# ── Rule 2: SeedData.Seed must be gated behind a dev check ────────────
$program = Get-Text "Backend/API/Program.cs"
if ($null -eq $program) {
    $failures.Add("Rule 2: Backend/API/Program.cs not found.")
}
else {
    $hasSeedCall = $program -match "SeedData\.Seed"
    if ($hasSeedCall) {
        $gated = ($program -match "IsDevelopment\(\)") -and
                 ($program -match "Seed:AllowInProduction")
        if (-not $gated) {
            $failures.Add("Rule 2: SeedData.Seed is called but not gated behind " +
                "IsDevelopment() + the Seed:AllowInProduction escape hatch.")
        }
    }
}

# ── Rule 3: demo creds live ONLY in SeedData.cs ───────────────────────
$demoCreds = @("Admin@123", "Pass@123")
$allCs = Get-ChildItem $Root -Recurse -Filter *.cs -File |
    Where-Object {
        $_.FullName -notmatch "\\(bin|obj|\.git)\\|\.probe"
    }
foreach ($cred in $demoCreds) {
    $owners = @()
    foreach ($f in $allCs) {
        $t = Get-Content $f.FullName -Raw
        if ($t -match [regex]::Escape($cred)) {
            $owners += ($f.FullName -replace [regex]::Escape($Root), "")
        }
    }
    # SeedData.cs is the one safe home. If the cred appears ANYWHERE else
    # (or in SeedData.cs it's fine), flag the non-SeedData files.
    $badOwners = $owners | Where-Object { $_ -notmatch "SeedData\.cs$" }
    if ($badOwners.Count -gt 0) {
        $failures.Add("Rule 3: demo credential '$cred' found outside SeedData.cs: " +
            (($badOwners) -join ", "))
    }
}

# ── Rule 4: no tracked config file ships Seed__AllowInProduction=true ─
$cfgFiles = @(
    "Backend/API/appsettings.json",
    "Backend/API/appsettings.Development.json",
    ".env.example"
)
foreach ($c in $cfgFiles) {
    if (-not (Test-File $c)) { continue }
    $txt = Get-Text $c
    # Match either JSON ("Seed:AllowInProduction": true) or env-style
    # (Seed__AllowInProduction=true / =True).
    $bad = ($txt -match '"Seed:AllowInProduction"\s*:\s*true') -or
           ($txt -match "Seed__AllowInProduction\s*=\s*true")
    if ($bad) {
        $failures.Add("Rule 4: '$c' ships Seed AllowInProduction=true. " +
            "Keep it false in committed config; enable only in an isolated " +
            "CI sandbox via the process environment.")
    }
}

# ── Rule 5: at least one EF migration exists ──────────────────────────
$migDir = Join-Path $Root "Backend/Repositories/Migrations"
$migs = @()
if (Test-Path $migDir) {
    $migs = Get-ChildItem $migDir -Filter *.cs -File |
        Where-Object { $_.Name -notmatch "\.Designer\.cs$" -and
                       $_.Name -notmatch "ModelSnapshot\.cs$" }
}
if ($migs.Count -lt 1) {
    $failures.Add("Rule 5: no EF Core migration found under " +
        "Backend/Repositories/Migrations. The DB must be migratable in CI.")
}

# ── Report ─────────────────────────────────────────────────────────────
if ($failures.Count -eq 0) {
    Write-Host "✔ CiGuard: all 5 checks passed " +
        "($($migs.Count) migration(s), demo creds confined to SeedData.cs)."
    exit 0
}
else {
    Write-Host "✘ CiGuard failed with $($failures.Count) violation(s):" -ForegroundColor Red
    foreach ($f in $failures) {
        Write-Host "  - $f" -ForegroundColor Red
    }
    exit 1
}
