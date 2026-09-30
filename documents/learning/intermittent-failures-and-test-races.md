# Intermittent Failures and Test Races

[Back to Learning](README.md) · [Verification Honesty](verification-honesty.md) · [Vacuous and Self-Referential Tests](vacuous-and-self-referential-tests.md)

`WiringTests.Dapper_DateTime_Maps_To_DateTime2_And_Configure_Is_Idempotent` failed once in five
solution-level runs. This is the first defect in this repo found by **reproducing a nondeterministic
failure** rather than by reading code.

## Measure the flake before diagnosing it

The reporting run was documentation-only, so a code regression was impossible on its face. That is not
evidence, so it was measured:

| Scope | Runs | Failures |
|---|---|---|
| Solution-wide, before the fix | 5 | **1** |
| `WiringTests` project-scoped | 4 | 0 |
| `WiringTests --maximum-parallel-tests 16` | 3 | 0 |

Intermittent, and only under solution-level load — which widens the timing window rather than changing
behaviour. That is the signature of a race.

> **A flake is a claim about frequency, so measure the frequency before theorising the cause.** Five
> solution runs, seven scoped runs, and one controlled parallel run cost about a minute and converted
> "this is a regression" into "this is a race" with evidence rather than intuition.

## A "wrong value" message can be a type-identity message

```
Expected to be equal to DateTime2 … but received -2
```

`DbType.DateTime2` **is** `-2`. The message reads as arithmetic and is actually `Equals` on two boxes.
`dbType` was `var` over a null-conditional chain, so its static type was `object?`; `Assert.That(object)`
compares by `Equals`, and a boxed `int` never equals a boxed `DbType`.

> Before chasing a value bug, check that expected and actual are even the **same runtime type**. TUnit's
> `Assert.That` is type-strict and the failure message will not tell you so.

Two independent defects were stacked here, and both had to be fixed:

1. **Unsynchronised read** of a `Dictionary<Type, object>` being concurrently mutated — a defect on
   inspection, whatever the boxing does.
2. **A type-strict assertion on a boxed value** — fails on a *representation* difference and reports it
   as a value difference.

> **Hardening an assertion and fixing a race are not substitutes.** `[NotInParallel]` alone leaves the
> comparison broken; the numeric comparison alone would have **masked** the race — passing while reading
> an inconsistent dictionary, which is exactly the defect class this repo's rules exist to catch. Doing
> only the second converts a visible flake into an invisible bug, which is strictly worse than either
> leaving it or fixing it properly.

The fix, both parts: `[NotInParallel]` (no constraint keys, so it runs completely alone — correct
here because the contended resource is a process-wide static, not a named fixture, so a shared key
would have to be applied to every other test in the class) and `Convert.ToInt32(dbType, …)` compared
against `(int)DbType.DateTime2`.

## "Pre-existing" is not "not our problem"

The parameterisation of the messaging-disabled test **doubled** the number of tests in
`DependencyInjectionTests` — the same class as the flaky one — increasing parallel pressure on it. The
defect predates the change; the change plausibly made it visible sooner.

> When a change increases the load on a latent defect, record the **interaction** even when the defect
> itself is older. A reader who sees only "pre-existing" will correctly conclude it is not theirs, and
> incorrectly conclude nothing they did contributed to it.

## Three green runs are a strong signal, not a proof

The pre-fix rate was ~1 in 5, so 3/3 clean has roughly a 0.8% chance under the old rate. Suggestive,
not conclusive; a CI soak of ~20 runs would close it. The exact boxing path was never decompiled either —
the `-2`-vs-`DateTime2` rendering is strong circumstantial evidence that an `int` was boxed, and the
numeric comparison is correct under either boxing, but the mechanism is inferred.

> State the strength of the evidence, and name the cheap experiment that would settle it. "Probably
> fixed" and "proven fixed" are different claims and only one of them is actionable.
