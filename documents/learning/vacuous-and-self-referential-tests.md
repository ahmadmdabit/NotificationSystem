# Vacuous and Self-Referential Tests

[Back to Learning](README.md) · [Development](../testing.md)

This repo once carried **106 passing tests with empty bodies**. They were discovered, counted, and
reported green. This file is about that class of defect — the tests that pass without testing
anything — because **a green suite cannot detect it**.

## The three shapes

### 1. Empty bodies

A test with no assertions passes. Filling them exposed a **live production defect** (`AsyncLocal` not
flowing back out of an awaited callee, which silently dropped every domain event). So the cost of the
defect class is not theoretical.

> **Detection:** scan for test methods whose body contains no assertion. `Assert.That` is the floor.

### 2. Constants asserting constants

```csharp
var success = true;
Assert.That(success).IsTrue();          // tautology

Assert.That(1 > 0).IsTrue();            // constant — TUnitAssertions0005 flags this

Assert.That(() => throw new InvalidOperationException("x"))
      .ThrowsExactly<InvalidOperationException>();   // "tests" a throw the test itself raises
```

The last is the subtlest: it looks like a real exception test, and it is entirely self-produced.

### 3. Asserting a value against the constant it was built from

```csharp
Assert.That(sql).Contains(NotificationCommandFactory.NotificationColumns);   // circular
```

Mutating the constant to drop a column leaves the assertion **green**, because both sides changed
together. A dropped projection column then silently maps to `default` on the entity — a real data bug
with no exception anywhere.

**The fix:** name each required column explicitly *in the test*, listing both sets rather than reading
the production constant. This was found **only because the mutation was run after the test was
written**; the suite was green throughout.

> **The defect recurs under time pressure.** Two of these were written *while filling hollow tests* —
> the very work meant to remove them. The same review discipline applies to your own new tests as to
> inherited ones.

## The required guard

**A new or rewritten test must be shown capable of failing.** Break the production code, confirm the
intended test goes red, revert, confirm green. Examples that earned their keep:

| Mutation | Result |
|---|---|
| `LogInformation` → `LogWarning` in the consumer | 2 tests failed — **the old tautology passed** |
| Deleting `_transaction.Dispose()` from `UnitOfWork.CommitAsync` | the disposal-contract test failed; the pre-existing test passed it |
| `NotificationColumns` drops `UpdatedAt` | the self-referential assertion **stayed green** |
| `200` → `199` in a validator limit | **only** the two boundary tests failed |

## Boundary rules: test *at* the limit, not only over it

`MaximumLength(200)` and `Count() <= 1000` are **inclusive**. A suite containing only "201 chars is
invalid" passes unchanged if the rule is tightened to 199 or 999. The mutation above proves it: every
pre-existing validator test stayed green.

An off-by-one in a validation rule is a shipping bug that ships silently.

Also worth pinning:

- **Assert `PropertyName` and `ErrorMessage`, not just `IsValid == false`.** `WithMessage(...)` is the
  client-facing contract — the UI surfaces it — so a message change is a breaking change.
- **A `Must(...)` rule guarding `null` needs a null-input test.** `items is not null && items.Count()
  <= 1000` exists to avoid a `NullReferenceException`; a test with only empty and oversized
  collections never reaches that branch.
- **`RuleForEach` needs the offending property asserted.** With `[Arguments(0, 10)]` and
  `[Arguments(1, 0)]` both invalid, `IsValid == false` is satisfied by *either* rule firing. Assert
  `PropertyName` so the cases stay distinguishable.

## Aggregate factories and identity

`Notification.Create` always yields `Id = 0`, so **any handler filtering on a positive id is untestable
without a `Rehydrate(...)` factory** — the same reason `User.Rehydrate` exists. When a mock store is
keyed by id and `Create()` always returns 0, store seeding cannot distinguish two entities; stub the
specific call instead.

## A mock factory is incomplete until it covers the full interface

`MockNotificationRepository` stubbed 3 of 7 members; the other four returned loose defaults, so every
test touching them would have hit a null. Filling the tests surfaced all four, and they were added to
the **factory**, not to individual tests.

> Check the base type before adding disposal in a controller test: types deriving from `ControllerBase`
> do, those deriving from `Controller` do not.

## Two more caught during remediation, both passing while asserting nothing

### `DefaultHttpContext.Response.Body` is `Stream.Null`

A redaction test asserted `DoesNotContain("boom")` on the serialised body — and **passed**, because
anything written to `Stream.Null` is discarded. The assertion could never fail for any input.

> Set a real `MemoryStream`, and assert `IsNotEmpty()` **first** so the test can never be vacuous
> again. A negative assertion over a possibly-empty buffer is a test that always passes.

### An exception that is only `new`-ed has a null `StackTrace`

A "Development must include diagnostics" test asserted a populated stack trace and **failed** -
correctly, but for the wrong reason: `new Exception(...)` has never been thrown, so `StackTrace` is
null. Fixed with a helper that **throws and catches**, so `StackTrace` and `InnerException` are both
genuinely populated.

> An exception object carries no stack trace until it is thrown. If the test asserts on diagnostics,
> produce them by actually throwing — never by constructing.

Related: the literal `"stackTrace"` **key** is always serialised even when the value is null, so a
test asserting the key's *presence* is vacuous. Assert the **value** (`"stackTrace": null`).

## Three more found by construction rather than by review

While writing the remediation, three vacuous-green candidates appeared spontaneously: an empty
response body, an unthrown exception, and an assertion read against a constant.

> **The defect recurs while you are fixing it.** These were written by the same work that was
> eliminating them. A green run is not evidence that your own new tests are non-vacuous — run the
> mutation.
