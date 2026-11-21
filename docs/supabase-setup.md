# Supabase (PostgreSQL) Configuration Guide

FixFlow uses **PostgreSQL (`Npgsql.EntityFrameworkCore.PostgreSQL`)** and isolates all domain tables and EF Core migration history inside the dedicated **`fixflow`** schema (`modelBuilder.HasDefaultSchema("fixflow")` and `fixflow."__EFMigrationsHistory"`).

Using a dedicated schema keeps FixFlow cleanly separated when sharing a Supabase PostgreSQL instance with other systems and avoids exposing internal tables through Supabase's default `public` PostgREST schema.

---

## 1. Local Credentials via .NET User Secrets (Do Not Commit Secrets)

Never place real passwords or project references inside `appsettings.json` or `appsettings.Development.json`. Configure your local connection string with User Secrets:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=db.YOUR_PROJECT_REF.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=YOUR_PASSWORD;SSL Mode=Require" --project src/FixFlow.API/FixFlow.API.csproj
```

To verify what is stored locally:

```bash
dotnet user-secrets list --project src/FixFlow.API/FixFlow.API.csproj
```

---

## 2. Direct/Session Connection vs. Transaction Pooler

Supabase provides different connection endpoints; choose the right one depending on the operation:

| Use Case | Port | Recommended Connection Parameters | Why |
| :--- | :--- | :--- | :--- |
| **EF Core Migrations (`dotnet ef database update`)** | `5432` (Direct / Session Pooler) | `Host=db.YOUR_PROJECT_REF.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=YOUR_PASSWORD;SSL Mode=Require` | EF Core acquires session-level migration locks (`__EFMigrationsLock`) that are unsupported in Supavisor transaction mode. |
| **High-Concurrency Runtime (Production API)** | `6543` (Transaction Pooler) | `Host=aws-0-REGION.pooler.supabase.com;Port=6543;Database=postgres;Username=postgres.YOUR_PROJECT_REF;Password=YOUR_PASSWORD;SSL Mode=Require;No Reset On Close=true;Max Auto Prepare=0` | Prevents connection exhaustion across serverless/container replicas; `No Reset On Close=true` and `Max Auto Prepare=0` avoid prepared-statement mismatches across pooled transactions. |

---

## 3. Applying Migrations to Supabase

Once your connection string (port `5432`) is configured in User Secrets or the `ConnectionStrings__DefaultConnection` environment variable:

```bash
dotnet ef database update --project src/FixFlow.API --startup-project src/FixFlow.API
```

This creates the `fixflow` schema, `fixflow."__EFMigrationsHistory"`, and all domain tables without modifying `public`.

---

## 4. UTC `DateTime` Requirement in Npgsql

Npgsql 6+ strictly enforces UTC for PostgreSQL `timestamp with time zone` (`timestamptz`) columns:
- Always store and pass `DateTime` values with `DateTimeKind.Utc` (e.g., `DateTime.UtcNow`, or normalize incoming client timestamps via `.ToUniversalTime()`).
- Passing `DateTimeKind.Local` or `DateTimeKind.Unspecified` to a `timestamptz` column throws an `ArgumentException` at runtime.
