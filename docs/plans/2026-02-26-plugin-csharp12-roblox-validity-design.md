# Plugin C# 12 + Roblox Validity Design

**Date:** 2026-02-26
**Branch:** `feature/lang-imprv`
**Scope:** Plugin-side only

## Goal
Improve plugin IntelliSense and compile/transpile behavior for C# 12 while enforcing Roblox-valid namespaces/classes using the Roblox engine reference, with reliable fallback behavior offline.

## Decisions
- **Execution contexts:** Single global validity profile.
- **Unsupported C# 12 behavior:** Parse where possible, then emit clear diagnostics when compile/lower/emit is unsupported.
- **Data source model:** Hybrid live fetch + cache with local static snapshot fallback.
- **Implementation scope:** Plugin-side only.

## Architecture

### 1) Language Feature Layer (C# 12)
- Lexer/parser recognition for C# 12 syntax.
- IntelliSense awareness (completion/hover/diagnostics).
- Lowering/emitter support where feasible.
- Explicit unsupported-compile diagnostics for parsed-but-not-transpilable constructs.

### 2) Roblox Validity Layer
- Central validity model for namespaces/classes/services.
- Consumed by:
  - `using` validation
  - type/name completion filtering
  - namespace/class diagnostics
  - service/class checks in `GetService` scenarios

### 3) Freshness Layer
- **Live provider:** fetches latest engine reference payload and caches it.
- **Snapshot provider:** local static Lua snapshot for offline/failed-fetch fallback.
- **Resolver:** chooses best valid profile (`live-cache` if valid/newer, else snapshot).

### 4) Runtime Behavior
- Startup is deterministic and non-blocking.
- Snapshot always available immediately.
- Live updates happen in background and swap atomically on success.
- No editor breakage when network/data is unavailable.

## Freshness Contract (“Most recent as of now”)
Because Roblox docs do not reliably expose `lastmod` for this surface, freshness is tracked by:
- `retrieved_at_utc`
- `source_urls[]`
- `source_content_hash`
- `profile_version`
- `schema_version`

The active profile is the newest successfully validated profile in cache; otherwise the bundled snapshot is used.

## Plugin Components
- `plugin/src/RobloxValiditySnapshot.lua` (bundled fallback data + metadata)
- `plugin/src/RobloxValidityProvider.lua` (load/validate/select profile)
- `plugin/src/RobloxValidityLiveUpdater.lua` (background fetch/cache/update)
- `plugin/src/CSharp12SupportMatrix.lua` (per-feature parse/intellisense/compile status)

## C# 12 Behavior Contract
For each feature, plugin tracks:
- `parse` support
- `intellisense` support
- `compile` support
- fallback diagnostic code/message if compile unsupported

Targeted features:
- Primary constructors
- Collection expressions
- Optional/default lambda parameters
- `ref readonly` parameters
- Alias any type
- Inline arrays
- Experimental attribute
- Interceptors (explicit unsupported diagnostics)

## Required Workstream: Feature Inventory + Gap Closure
Before and during implementation:
1. Inventory existing language support and classify each feature as `implemented|partial|missing|buggy`.
2. Compare against target C# 12 + Roblox-validity requirements.
3. Implement missing features and fix identified bugs.
4. For parsed-but-not-supported compile paths, emit explicit diagnostics (no silent fallback).

## Testing Strategy
- Provider resolution tests (snapshot vs cache, corruption handling, deterministic selection).
- IntelliSense/diagnostic tests for namespace/class validity filtering and errors.
- C# 12 matrix tests per feature (parse/intellisense/compile + unsupported diagnostics).
- Live update tests (success, failure, schema/hash mismatch, atomic swap behavior).

## Acceptance Criteria
1. Plugin works offline with snapshot profile.
2. Live data updates cache/profile safely without restart.
3. IntelliSense and diagnostics enforce active Roblox validity profile.
4. C# 12 paths follow parse+diagnose contract (no silent unsupported transpile).
5. Existing and new plugin tests pass.
