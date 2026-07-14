# Phase 6 — CI/CD Pipeline & Quality Gates

Testing must be an automatic protection system. No code reaches production unless it passes business correctness, integration reliability, and concurrency safety validation.

## Table of Contents
1. [Pipeline Philosophy](#pipeline-philosophy)
2. [Pipeline Architecture](#pipeline-architecture)
3. [Stage 1: Build Verification](#stage-1-build-verification)
4. [Stage 2: Unit Tests](#stage-2-unit-tests)
5. [Stage 3: Integration Tests](#stage-3-integration-tests)
6. [Stage 4: Concurrency Tests](#stage-4-concurrency-tests)
7. [Stage 5: Quality Gate](#stage-5-quality-gate)
8. [Nightly Stress Pipeline](#nightly-stress-pipeline)
9. [Test Reporting](#test-reporting)
10. [Coverage Targets](#coverage-targets)
11. [Code Review Rules](#code-review-rules)

## Pipeline Philosophy

Each test type has different execution time, purpose, and failure impact. The pipeline should respect these differences.

**Best Practice #1 — Separate Tests By Execution Cost**

Do NOT run one `dotnet test` command for everything.

| Pipeline | Tests | Speed | When |
|----------|-------|-------|------|
| Fast | Domain Unit + Application Unit | < 30s | Every commit |
| Validation | Integration | Minutes | PR, pre-merge |
| Heavy | Concurrency + Stress | Longer | PR, nightly |

## Pipeline Architecture

```
Developer Commit
    ↓
Stage 1: Build Verification
    ↓
Stage 2: Fast Feedback (Unit Tests)
    ↓
Stage 3: Real Environment (Integration Tests)
    ↓
Stage 4: Concurrency Protection
    ↓
Stage 5: Quality Gate
    ↓
Merge / Deploy
```

Plus separate nightly pipeline for heavy stress tests.

## Stage 1: Build Verification

```bash
dotnet restore
dotnet build --no-restore --configuration Release
```

**Best Practice #2 — Treat Warnings as Errors**

```bash
dotnet build -p:TreatWarningsAsErrors=true
```

## Stage 2: Unit Tests

Target: `Eventy.Domain.UnitTests` + `Eventy.Application.UnitTests`

```bash
dotnet test tests/Eventy.Domain.UnitTests \
  --no-build --logger trx --collect:"XPlat Code Coverage"

dotnet test tests/Eventy.Application.UnitTests \
  --no-build --logger trx --collect:"XPlat Code Coverage"
```

- No Docker, no database, no external services
- Expected execution: < 30 seconds
- Run Domain and Application tests in parallel

**Best Practice #3 — Keep Unit Tests Pure**

If a unit test requires SQL Server, Redis, or HTTP — it's not a unit test. Move it to integration tests.

## Stage 3: Integration Tests

Target: `Eventy.IntegrationTests`

```bash
dotnet test tests/Eventy.IntegrationTests \
  --no-build --logger trx
```

Requirements:
- CI Runner must have Docker Engine
- Testcontainers creates SQL Server container automatically
- EF migrations applied automatically
- Container destroyed after test completion

**Best Practice #4 — Never Depend on Developer Machines**

Everything required (database, Redis, message broker) must be created automatically in CI. Works locally but fails in CI is unacceptable.

## Stage 4: Concurrency Tests

Target: `Eventy.ConcurrencyTests`

```bash
dotnet test tests/Eventy.ConcurrencyTests \
  --no-build --logger trx
```

Run with controlled environment:
- Local: Small load (validate logic)
- CI: Production-like load (validate protection)
- Nightly: Heavy stress (measure limits)

## Stage 5: Quality Gate

Quality gates decide: "Can this code move forward?"

| Gate | Requirement | Enforcement |
|------|-------------|-------------|
| Gate 1 — Build | Must: SUCCESS | `dotnet build` exits 0 |
| Gate 2 — Unit Tests | Must: 100% pass | All unit tests green |
| Gate 3 — Integration | Must: DB verified | Integration tests green |
| Gate 4 — Critical Business | Must: Booking flow pass | Booking + ticket + payment tests |
| Gate 5 — Concurrency | Must: No overselling | Last-ticket race passes |

**Best Practice #5 — Protect Critical Features With Required Checks**

A developer should not merge code affecting Booking, Ticket Inventory, Payment, or Reservation without running the Concurrency Test Suite.

## Nightly Stress Pipeline

Separate pipeline triggered every night:

```yaml
# .github/workflows/nightly-stress.yml
name: Nightly Stress Tests
on:
  schedule:
    - cron: '0 2 * * *'  # 2 AM daily
jobs:
  stress:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Run Stress Tests
        run: dotnet test tests/Eventy.ConcurrencyTests \
          --filter "Category=Stress" \
          --logger trx
```

Scenarios:
- **Ticket Sale Stress**: 10,000 reservation attempts → measure success rate, errors, latency
- **Search Load Test**: 100,000 event searches → measure query performance, cache efficiency
- **Booking Completion Load**: Payment confirmation + finalization → measure transaction performance

## Test Reporting

Professional pipeline produces:

1. **Test Result Report**: Passed/failed, execution time, failure details
2. **Coverage Report**: Domain coverage, application coverage, critical flow coverage

**Best Practice #6 — Do Not Chase Coverage Percentage Alone**

Bad: 100% coverage with useless tests (testing getters but ignoring `ReserveLastTicket`)
Better: Critical Business Logic = Maximum Coverage

## Coverage Targets

| Layer | Target | Enforcement |
|-------|--------|-------------|
| Domain | 90%+ | Required gate |
| Application | 80%+ | Required gate |
| Infrastructure | 70%+ | Advisory |
| Critical Booking Flow | 100% | Required gate |

## Code Review Rules

Every Pull Request should answer:

1. **Business Rule Changed?** → Unit tests added/updated?
2. **API Contract Changed?** → Integration tests added/updated?
3. **Booking/Ticket Logic Changed?** → Concurrency tests added/updated?
4. **Database Schema Changed?** → Migration test + persistence test added?
5. **New Feature?** → All 3 tiers (Unit + Integration + Concurrency if booking-related)?

## Example GitHub Actions Workflow

```yaml
name: Eventy Test Pipeline
on: [pull_request]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '9.0.x' }
      - run: dotnet restore
      - run: dotnet build --no-restore -c Release -p:TreatWarningsAsErrors=true

  unit-tests:
    needs: build
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '9.0.x' }
      - run: dotnet test tests/Eventy.Domain.UnitTests --no-build -l trx
      - run: dotnet test tests/Eventy.Application.UnitTests --no-build -l trx

  integration-tests:
    needs: unit-tests
    runs-on: ubuntu-latest
    services:
      docker: { image: docker:dind }
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '9.0.x' }
      - run: dotnet test tests/Eventy.IntegrationTests --no-build -l trx

  concurrency-tests:
    needs: integration-tests
    runs-on: ubuntu-latest
    services:
      docker: { image: docker:dind }
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '9.0.x' }
      - run: dotnet test tests/Eventy.ConcurrencyTests --no-build -l trx
```

## Completion Criteria

- Pipeline runs on every PR
- Build fails on warnings
- Unit tests complete in < 30s
- Integration tests use Testcontainers (no developer machine dependencies)
- Concurrency tests validate no overselling
- Coverage reports generated
- Critical features protected by required checks
- Nightly stress tests measure system limits
