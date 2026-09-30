# Natural Keys and Idempotency

[Back to Learning](README.md) · [Testing Seams](testing-seams.md) · [Defects That Escape a Green Suite](defects-that-escape-green-suites.md)

`NotificationHistories` declares `PRIMARY KEY ([NotificationId], [UserId])`.
`SPNotificationHistoryInsert` was `INSERT INTO ... SELECT ... FROM @Entities` — a bare insert, with
no `WHERE NOT EXISTS` and no `MERGE`. Re-sending the same pair therefore violated the key.

## De-duplicating within a request is not idempotency

`SendNotificationsCommandHandler` did `GroupBy` on the input before dispatching. That removes
duplicates *inside one payload*, and reads like idempotency protection. It says nothing about a second
request carrying the same pair.

> **"Idempotent" describes the operation across calls. De-duplication describes one call's payload.**
> When a table has a natural key, the write must tolerate repeats — and only the storage layer can
> guarantee that, because only it sees what is already stored.

The full failure chain, each link converting one thing into another:

| Layer | Effect |
|---|---|
| SP `INSERT` | PK violation, caught by `BEGIN CATCH` |
| SP | sets `@SPSuccess = 0` |
| Repository | sees `0` and throws `InvalidOperationException` |
| `ApiExceptionHandler` | unrecognised exception → **500** |

A retry, a double-click, or two open browser tabs all reach it.

> Trace a suspected defect **through every layer to the HTTP status**. Each boundary converts a
> specific signal into another, and a "correct" local check can become a generic 500 four layers up.
> That is also why the fix belongs at the lowest layer that can see the truth — the `WHERE NOT EXISTS`
> guard — and not in a caller.

## The fix

```sql
INSERT INTO NotificationHistories (NotificationId, UserId, ...)
SELECT e.NotificationId, e.UserId, ...
FROM @Entities e
WHERE NOT EXISTS (
    SELECT 1 FROM NotificationHistories h
    WHERE h.NotificationId = e.NotificationId AND h.UserId = e.UserId)
```

Soft-deleted rows still occupy the key, so the guard covers them too — deliberately. A repeat send
after a soft delete must stay a no-op, not resurrect or collide.

> **Any table you add follows the same rule:** if a natural key exists, the procedure must tolerate
> repeats, or the caller must de-duplicate *across* requests, not only within one.

## The existing test could not see any of this

`Handle_WhenNotificationAlreadySent_DoesNotReEmitDomainEvent` mocked `INotificationHistoryRepository`.
It genuinely proves the handler emits no second event — and is structurally incapable of observing a
PK violation, because the mock is the boundary where the database stops existing.

> A mocked repository makes every storage constraint invisible. When a finding is about a **stored**
> invariant, the guard that "already covers it" is usually proving a different property entirely. The
> suite was green and the 500 was real; the two facts never met.

The property is now pinned at the level that owns it: the SP text is scanned for the existence guard,
and the idempotency claim is stated in terms of the history write rather than the status flip.
