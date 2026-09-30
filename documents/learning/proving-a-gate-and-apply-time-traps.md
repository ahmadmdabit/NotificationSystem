# Proving a Gate Is Real, and Traps That Bite at Apply Time

[Back to Learning](README.md) · [Verifying and Remediating](verifying-and-remediating.md) · [Testing Seams](testing-seams.md)

This handoff covers the MassTransit 9.2.2 licence gate and the two Criticals it masked. Its durable
content is a method for proving a *negative* result, and three traps that no amount of reading would
have surfaced.

## Prove the gate runs even when you cannot prove it passes

The licence gate could not be shown to accept a valid key — no key was ever available. What could be
done was prove the variable is **genuinely read**, by supplying a deliberately invalid one and checking
that each of the four documented mechanisms produced a **distinct** error:

| Mechanism | Error | Proves |
|---|---|---|
| `MT_LICENSE_PATH` → missing file | `Could not find a part of the path '/nope/missing.txt'` | path honoured |
| `MT_LICENSE_PATH` → file present, bad key | `'0x9E' is an invalid start of a value` | file parsed |
| `:ro` volume mount at `/masstransit/license.txt` | same parse error | compose pattern works as-is |
| `MT_LICENSE` inline | `not a valid Base-64 string` | inline var honoured |

> **A partial proof of wiring is a real, non-vacuous assertion.** Four mechanisms that fail four
> different ways is stronger evidence than one mechanism that fails once — it shows the code reaches
> each of them separately. State precisely which claim you have: *the path is read*, not *the key is
> accepted*. Nothing in this repo has ever verified the second.

Two findings that only surfaced from doing that:

- **The in-memory transport is not a licence-free path.** `UsingInMemory` threw the identical failure
  from `InMemoryRegistrationBusFactory.CreateBus(...)`. The gate runs at bus creation, **before**
  transport selection, so `Messaging__UseRabbitMq=false` does not avoid it.
- **There is no development exemption.** The pricing FAQ line "your license covers all environments"
  presumes a licence exists; it is not a waiver.

> **A flag that selects a subsystem is not a flag that bypasses a gate inside it.** Read where the check
> happens, not what the switch says. A configuration switch that appears to offer a safe path can be
> evaluated strictly earlier than the switch.

## Scope-guard a finding before attributing it

The licence gate was pre-existing, not caused by the 157-file staged diff - and that was established by
one command, not by reasoning:

```
git diff --cached -- "*Infrastructure.csproj"     # empty
```

> **Prove a finding is not yours with a command whose output is empty.** "This looks pre-existing" is an
> opinion that becomes load-bearing as soon as it is used to excuse a commit. The empty diff is the
> evidence, and it costs one command.

This mattered beyond attribution: the gate was masking F-01 in compose testing, and that only appeared
once the bus was bypassed.

## Trap 1: a `Compile Remove` for a file that does not exist yet

`TestDoubles.csproj` already contained `<Compile Remove="Helpers\TestHostEnvironment.cs" />` for a file
that was not present. R-01 *created* that file. If the unrelated cleanup task did not delete that exact
line, MSBuild would silently exclude the new file and the build would break somewhere unrelated.

> **A `Compile Remove` entry for an absent file is a latent trap, not dead config.** Any future file
> with that name is silently excluded and reports no error. The general hazard: build configuration
> that refers to paths is a *claim* about the tree, and it must be re-checked whenever the tree changes -
> including when your own task creates the file.

This was flagged as the single most likely way the phase would go wrong, and it was caught by reading
the csproj rather than by any tooling.

## Trap 2: the "fix" that compiles and is still wrong

The obvious repair for a DI break caused by `ApiExceptionHandler(string environmentName)` is
`AddSingleton<string>(...)`. It compiles, the container resolves it, and it is wrong: a bare `string` in
the container is ambiguous the moment a second consumer needs one, and it leaves
`EnvironmentName` - a **security-relevant** value that decides whether stack traces are redacted -
resolvable as an untyped primitive.

> **A constructor on a DI-registered type takes interfaces, never primitives.** Restoring
> `IHostEnvironment` is the smaller, safer change, and it is also the only one that keeps the type
> meaningful. A registration that makes a build error disappear without making the design right is the
> most dangerous kind of fix, because it converts a loud failure into a silent one.

## Trap 3: a reflection helper that fails open

```csharp
typeof(User).GetProperty("Id")?.SetValue(entity, id);
```

The null-conditional **fails open**: rename `Id` and the entity keeps `Id == 0`, so the in-memory store's
`_store[0]` is silently overwritten on every insert. The consuming test asserts on `Username`, so it
still passes. `User.Rehydrate` already existed and was used by eight other test files.

> **A test double that degrades quietly is worse than one that throws.** Reflection in a stub should
> fail loudly when the member it targets is renamed - that is precisely the regression it exists to
> catch. `?.` on the lookup turns a structural change into silent data corruption, and the assertion
> that would have noticed it was pointed at a different property.

The same pattern, opposite direction: `NotificationColumns` is `public static readonly
IReadOnlyList<string>` but backed by a collection expression - i.e. a real `string[]`. A caller can
downcast and mutate the global used to build every projection. Advertised immutability that is not
enforced is a contract the compiler will not help you keep.
