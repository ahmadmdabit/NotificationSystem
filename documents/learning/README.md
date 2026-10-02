# Learning Notes

Hard-won lessons from debugging sessions, grouped by topic. These are **not** reference
documentation — they record the reasoning behind decisions and the traps that cost time, so the
next person does not repeat them. For how the system *works*, see the documents linked below.

| Topic | What it covers |
|---|---|
| [Verifying a result honestly](verification-honesty.md) | Reporting a "green" that the user cannot reproduce; mutated evidence; reading inner exceptions |
| [Reviewing work you inherited](reviewing-work-you-inherited.md) | Findings known only through a summary, silently resolved instruction conflicts, and reconciling counts |
| [MassTransit publish routing](masstransit-publish-routing.md) | Exchange naming, why `Publish(object)` works, and why the runtime type is the only safe choice |
| [Broker-backed integration guards](broker-backed-guards.md) | Building a real-broker guard; credential resolution; topology assertions over counters |
| [Investigating with decompiled source](decompiled-source-investigation.md) | Decompile-all-then-grep; why targeted type lookups fail; trusting the shipped assemblies |
| [Editing files safely](safe-file-editing.md) | Line endings, index-based edit damage, `patch` corruption, and the structural checks that catch them |
| [TUnit and mocking](tunit-and-mocking.md) | TUnit 1.69 API names that do not exist, generator limits, async and AsyncLocal rules |
| [Testing seams](testing-seams.md) | Extracting what cannot be mocked; Dapper and source-scanning contract tests |
| [Vacuous and self-referential tests](vacuous-and-self-referential-tests.md) | The three shapes of a test that passes vacuously, and the mutation that proves it bites |
| [Broken diagnostics](broken-diagnostics.md) | Five instruments that reported absence when the query was wrong, and the reflex to question the instrument first |
| [Green instruments and dead services](green-instruments-and-dead-services.md) | `docker ps` healthy, `docker port` mapped, nothing serving; host-state vs component-state; and why a soak is the only close for an intermittent fault |
| [Defects that escape a green suite](defects-that-escape-green-suites.md) | Wiring breaks, lazy DI, unverified mocks, and the review method that found them |
| [Security gates that fail open](security-gates-that-fail-open.md) | Redaction defaults, validation drift, and DTO fields that become API contract |
| [Verifying and remediating](verifying-and-remediating.md) | Executing a written plan: sweeps, earned limitations, stale tooling, and flags that fail loudly |
| [Intermittent failures and test races](intermittent-failures-and-test-races.md) | Measuring a flake, boxed-value assertions, and why fixing a race is not hardening the test |
| [Confident analysis that was wrong](confident-analysis-that-was-wrong.md) | Three research passes, three wrong causes, one right mechanism, and a metric rated 0.98 that was wrong twice over |
| [Decision tables for diagnosis](decision-tables-for-diagnosis.md) | Rows that eliminate hypotheses, correcting a prior document, and metrics that are not oracles |
| [Comments that describe past behaviour](comments-that-describe-past-behaviour.md) | Two comments describing a configuration the file no longer had, and the greps that catch the class |
| [Natural keys and idempotency](natural-keys-and-idempotency.md) | Repeat writes against a natural key, and the four-layer path from PK violation to HTTP 500 |
| [Stopgaps that fail on the machine they target](stopgaps-that-fail-on-the-machine-they-target.md) | A workaround whose documentation and provisioning both break in the no-licence case |
| [Proving a gate, and apply-time traps](proving-a-gate-and-apply-time-traps.md) | Showing a gate runs without being able to pass it; `Compile Remove` traps; the fix that compiles and is still wrong |

## Why these exist
Most entries here began as a wrong conclusion, not a missing fact. Each file therefore records
**what was believed, what the evidence actually showed, and what the rule became** — because the
belief is the part that recurs.
