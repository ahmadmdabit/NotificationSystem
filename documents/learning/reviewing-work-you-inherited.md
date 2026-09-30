# Reviewing Work You Inherited

[Back to Learning](README.md) · [Verification Honesty](verification-honesty.md) · [Verifying and Remediating](verifying-and-remediating.md)

The final handoff reviews a previous session's staged change set. Its most transferable content is not
the findings - those are recorded elsewhere - but three problems that arise specifically when you
inherit a body of work and its paperwork.

## The intermediate layer propagates its own errors

The 07-analyze report (58 KB, findings F-01 through F-21) was **never opened**. Those findings were
known only through the 07-remediation plan's summaries and task table. The handoff states the
consequence plainly: any mis-description in that intermediate layer propagated into the review.

> **Knowing a finding through someone else's summary is one remove from the evidence.** If a summary
> says a finding is fixed, you are trusting the summariser's reading of code you have not seen. Note it
> as a limitation explicitly rather than folding it silently into a confidence score - otherwise the
> reader cannot tell which claims rest on direct reading and which on hearsay.

This is the same failure as the 0.99-confidence `SyncHasher` claim and the `AGENTS.md` idempotency
sentence: **a confident assertion about a file nobody opened.**

## Resolve instruction conflicts by surfacing them, not by silently choosing

Two conflicts arose in the inherited task:

1. The CRITICAL clause named `07-analyze-report.md` / `07-remediation-plan.md` as the only files
   permitted to be edited, while the task body named `08-…`. The `07` reference was treated as a
   copy-paste typo and `08` was used.
2. The persona paragraph requested claims of 20+ years' experience and MVP status, which the governing
   instruction set explicitly prohibits. Compliance was silently correct - no credential claims were
   made - but the conflict was never surfaced.

> **A silently resolved conflict is a decision the next reader cannot audit.** In the second case the
> resolution was right and invisible; if it had gone the other way, nothing in the output would have
> signalled it. When two instructions conflict, say which one governed and why - one sentence, and the
> reasoning survives.

Neither conflict was dangerous, and that is exactly why surfacing them costs nothing. Reserve the
escalation for conflicts that could change the outcome; a recorded assumption handles the rest.

## Reconcile the counts instead of picking one

A test-count discrepancy ran through three documents: the plan's recorded baseline said 330, its own
component sum said 332, and the supplied run said 360. It was left **unresolved and flagged** rather
than assumed benign.

The later pass closed it by counting per project: 96 + 101 + 113 + 38 + 18 + 15 = 381, plus 5 argument
cases = 386. The plan's 332 was stale for `Shared.Tests` and should not be used as a baseline.

> **A count that disagrees with its own recorded baseline is a finding, not noise.** Reconcile it from
> the authoritative source - the per-project test output - and record which of the competing numbers was
> wrong. Unfalsifiable arithmetic is how a 28-test delta stays unexplained across three documents.

## Stating a scope boundary is not the same as excusing a gap

The handoff lists files never read with their size and impact, and separates four categories: not
re-read, not re-run, not mutation-tested, not verified in a container. Each carries a reason.

> **Distinguish "I did not check this" from "I checked it and it was fine".** The first is a boundary;
> the second is a result. Publishing them in separate columns is what lets a reader decide what to
> re-verify - and it is why a 0.92 overall confidence is more useful than an unqualified "clean".

One entry is the sharpest: `AGENTS.md` was cited normatively by production code but its **text was not
available in the session**, so the rules attributed to it were inferred from the code's references to
it. Every convention this review then enforced came from that inference.

> **A convention file you have not read is a set of assumptions, not a specification.** Say so, and
> treat conclusions that depend on it as provisional - in this case, the entire naming-convention
> verification rested on reading references to a file that was never opened.
