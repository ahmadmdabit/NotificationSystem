# Security Gates That Fail Open

[Back to Learning](README.md) · [API Reference](../api-reference.md)

A control is only as strong as its **default**. Two findings in this repo had the same root cause: a
guard that was fail-closed became fail-open, and no test covered the changed branch.

## The removed null check

A diff deleted one conjunct from a redaction gate:

```diff
-if (exception is not null && env != null && !string.Equals(env.EnvironmentName, "Production", ...))
+if (exception is not null && !string.Equals(environmentName, "Production", ...))
```

The `env != null` term was a **fail-closed** guard: an unknown or absent environment produced no
diagnostics. Remove it and `environmentName == null` evaluates as `!string.Equals(null, "Production")`
→ `true`, so the branch is taken and `StackTrace`, `InnerMessage` and `InnerStackTrace` are all
populated. An overload that **defaults the environment to `null`** now leaks the full exception chain
unless a caller remembers to pass a name.

All four live callers happened to be safe. It was still ranked Critical: a defence-in-depth control
whose *default* is the unsafe branch sits one careless call away in a codebase with four such sites,
and the existing tests covered only the two non-null branches.

> **Redaction gates must be an allowlist, not a denylist.** "Redact unless the environment is a
> *known* non-production name" fails closed on every input — including `null`, blank, and any name
> added later. A `!= "Production"` check fails **open** the moment an environment name is unknown.
> Adding a new environment name then requires editing the list, which is the correct friction.

**When adding an environment, the allowlist is the checklist.** A denylist needs no edit and silently
starts working.

## One member bypassing validation nine siblings use

Nine of eleven builders in `NotificationCommandFactory` delegate to `SqlCommands`, which runs every
interpolated table and column name through `ValidateIdentifier`. One builds its projection with a raw
`string.Join` and a hardcoded table name, bypassing it.

Not an injection today — the columns are a compile-time literal and the ids are a bound parameter. It
matters for two reasons:

1. The file's own XML remarks claim *"no caller ever supplies a SQL fragment… the identifiers are
   validated."* **The claim is false for that member**, and a false comment is a trap for the next
   reader.
2. It establishes exactly the drift the shared helper exists to prevent. The next person who needs
   this query will copy **this** member, not the correct one.

> **Consistency is a security control.** A helper that 9 of 11 call sites use is a strong default; the
> tenth call site removes that strength for whoever copies it next. When bypassing a guard, make the
> bypass loud or impossible.

## A junk field on a live response contract

`UserDto.UtcNow` was an untested, unread, unassigned public property added for a test. Because
`System.Text.Json` serialises get-only properties, **every successful registration response now
carries a spurious `"utcNow":"0001-01-01T00:00:00"` field** — an unversioned, client-visible contract
change with no consumer, no justification, and no test.

> **A public property on a response DTO is part of the API contract.** Read-only is not invisible.
> Check the serialiser before adding one, and ask what breaks in a client's deserialiser.

## Files that give wrong advice are worse than no files

`TestDoubles/Stubs/SyncHasher.cs` carried an XML remark instructing the next developer to *"use when
TUnit.Mocks invocation forks ExecutionContext and orphans AsyncLocal writes"* — advice the same
commit had already disproved elsewhere.

> Delete a stub that teaches a falsehood. An unreferenced file with correct advice is dead weight; an
> unreferenced file with **wrong** advice is an active hazard, because it looks authoritative.

Eight `<Compile Remove>` entries pointed at files that did not exist — residue from an abandoned
attempt. Beyond the noise, each is a latent trap: creating `Stubs/StubDbConnection.cs` expecting it
to compile would be silently excluded by MSBuild, producing a "missing type" that reports no error.

## Unverifiable claims in documentation

The README documented a coverage workflow Microsoft.Testing.Platform ignores, and asserted a
"99-100% coverage" figure no one could reproduce. It also described `ArchitectureTests` as NUnit in
three places, **in the same file that was rewritten to say TUnit**.

> A claim nobody can re-derive is worse than no claim, because it will be trusted. Documentation that
> asserts a number should say how the number was obtained.
