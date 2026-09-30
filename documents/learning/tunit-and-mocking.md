# TUnit and Mocking

[Back to Learning](README.md) · [Development](../testing.md)

Everything here is specific to **TUnit 1.69.0** with `TUnit.Mocks`. The official examples were
frequently wrong as printed; several items below exist because the docs did not compile.

## API names that do not exist

| You might write | Actually |
|---|---|
| `MockLogger<T>()` | `Mock.Logger<T>()` |
| `.WasCalledOnce()` | `.WasCalled(Times.Once)` — a **property**, not a method |
| `.DoesContain("x")` | `.Contains("x")` |
| `.HasCount().EqualTo(n)` | `.Count().IsEqualTo(n)` |
| `Assert.That(true)` | TUnitAssertions0005 — use a variable |

Required `using` directives that are easy to miss: `IsTrue()` needs
`TUnit.Assertions.Extensions`; `Times` needs `TUnit.Mocks`; `VerifyLog()` / `VerifyNoLog()` live in
`TUnit.Mocks.Logging`, **not** `TUnit.Mocks`.

## `Assert.Multiple()` exists

Several earlier revisions of the plan claimed TUnit had no way to combine assertions. It does:
`public static IDisposable Assert.Multiple()`. `.And` and `.Or` **cannot be mixed in one chain** —
that throws `MixedAndOrAssertionsException` at runtime. Split into separate `Assert.That(...)` calls.

The awaited assertion returns its subject, so `var user = await Assert.That(result).IsNotNull();`
narrows the type and makes a following `!` redundant.

## Void methods chain normally

An earlier note claimed void methods "cannot chain `.Callback()` or `.WasCalled()`". **That is wrong.**
They support `.Callback()` and `.Throws()` exactly like any other method, and they are **eagerly
registered** — `mock.Log(Any())` with no chain already allows the call under
`MockBehavior.Strict`. To inspect call history use `mock.Invocations`.

## The generator fails on generic types whose parameter is named `T`

`T.Mock()` and `Mock.Of<T>()` are the same generator, and both fail on MassTransit's
`ConsumeContext<T>`: **853 generated-code errors** (`CS9289` ×416, `CS0229` ×1194, `CS0692` ×48,
`CS0409` ×48). This is a **generator defect**, not a complexity limit — an earlier revision of the
plan mis-attributed it.

**The fix is a seam, not another mocking library** — see
[Testing Seams](testing-seams.md). MassTransit 9.x also offers no escape hatch: no
`ConsumeContext.For<T>()` factory and no public concrete implementation (only
`MissingConsumeContext`, which throws by design).

## Async rules that bite

- **Assertions must be awaited.** This is a compile *error* (`TUnitAssertions0002`), so a `void` test
  method cannot contain one — use the static `Assert.Throws<T>(…)`, which **returns** the exception
  rather than a `Task`. Encapsulating assertions in a helper does not exempt it; the helper must be
  `async Task` and let the compiler supply `[CallerArgumentExpression]` (else `TUnitAssertions0003`).
- **`Assert.Multiple()` and side effects do not mix.** A method that mutates shared state should be
  called directly, not wrapped in `ThrowsNothing()`, which may execute out of order.
- **Tests with no `await` should not be `async Task`.**
- **`AsyncLocal` flows *into* a child task, never back out.** `Drain()` in a child leaves the parent
  holding its original value — this silently dropped every domain event in production until
  `Seed()` was added to the pipeline.

## C# constraints hit while testing

- **CS0552** — user-defined conversions to or from an interface are illegal. Expose a property
  (`db.Connection`), never an `implicit operator`.
- **Cross-assembly CS0121** recurs for *any* type mocked in both `TestDoubles` and a test project,
  because `.Mock()` generates an extension in each referencing assembly. It hit `IDbConnection`, then
  `DbTransaction`. The fix is always the same: create it in a `TestDoubles` factory, which also
  satisfies the DRY criterion.
- **`[Arguments(a, b, c)]` creates three separate test runs**, not one — useful for keeping
  parameterised cases distinguishable, easy to misread as a single case.

## Running tests

`dotnet run` and `dotnet test` both work. A root `global.json` with
`{"test":{"runner":"Microsoft.Testing.Platform"}}` opts .NET 10 out of the removed VSTest adapter.
With `dotnet test`, runner flags go after `--`.

**Never run `dotnet test` for several projects in parallel** — concurrent dotnet invocations collide
on `bin`/`obj` and time out. Run sequentially, with `--no-build` after the solution build.
