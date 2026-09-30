# Comments That Describe Past Behaviour

[Back to Learning](README.md) · [Broken Diagnostics](broken-diagnostics.md) · [Natural Keys and Idempotency](natural-keys-and-idempotency.md)

Two comments in `TestBroker.cs` described a configuration the file no longer had. Both were correct
about a version of the code that existed, and both were actively misleading when read.

| File | Claim | Reality |
|---|---|---|
| `TestBroker.cs` | "The endpoint is declared auto-delete, so the queue disappears when the consumer disconnects." | `AutoDelete` was deliberately **removed**; the test file says so and explains why |
| `TestBroker.cs` | "Queue name is explicit so it can be purged and asserted deterministically." | The next lines explain that the name is not referenced, because payload matching makes purging unnecessary |

> **A comment is a claim about the present tense.** When a line is changed and the comment above it is
> not, the comment becomes a second, invisible version of the code - and the more authoritative one,
> because it explains *why*.

## The tell: a comment that contradicts a nearby comment

These were findable without any tooling. Both contradictions sat within a few lines of the
authoritative statement.

> **When two comments disagree, the code is not the tie-breaker - the reader is.** A stale comment is
> worse than none, because it is a plausible explanation of behaviour that does not exist, and it will
> be trusted by exactly the person debugging.

The same class, found repeatedly elsewhere in this work:

- `AGENTS.md` describing MassTransit 9.2.2 licensing after the downgrade to 8.5.10 removed the licence
  apparatus entirely.
- `README.md` claiming a suite runs "everything except the broker-backed guard" when `IntegrationTests`
  is in `NotificationSystem.slnx`.
- `documents/architecture.md` carrying a stale test count after new guards were added.
- A SQL comment promising idempotency for the history write, which the procedure did not have.

> **Documentation is part of the change set, not a follow-up.** Every one of these was introduced or
> left behind by a code edit in the same commit. The rule that prevents them: when a diff changes
> behaviour a comment describes, the comment is part of the diff.

## Cheap detection

Two greps catch most of it, and both are worth running before any commit that changes behaviour:

- `git --no-pager diff -U15 | Select-String '^\+\s*//'` — new comments added alongside changed code,
  which is where a new wrong claim usually appears.
- Search the touched files for load-bearing nouns (`AutoDelete`, `idempotent`, `Guid`, a count) and
  confirm each still describes the code.

> Review the **comments in the diff**, not only the executable lines. A reviewer scanning for logic
  errors will pass a diff whose new comment contradicts the new code, because neither looks wrong in
  isolation.

## And the one that was right to stay

One comment was left deliberately: `MarkSentBatchAsync` trims by count rather than identity, and the
code comment says so explicitly, including that the precise fix needs `OUTPUT INSERTED.Id`.

> **When you decline a fix, write down what the current code does and why it is acceptable today.** An
> omitted justification is indistinguishable from an oversight, and the next reader re-derives the
> problem from scratch.
