# Green Instruments and Dead Services

[Back to Learning](README.md) · [Broken Diagnostics](broken-diagnostics.md) · [Intermittent Failures and Test Races](intermittent-failures-and-test-races.md)

A broker probe failed intermittently for two hours. The container reported `(healthy)` throughout.
The cause was never the network. Two independent faults had to be removed, and on three separate
occasions I reported the problem resolved while it was not. This file records the shape, because it
is the inverse of the usual one: the instruments were not silent, they were **loudly reassuring**.

## The shape

> The instruments report **presence** — healthy, mapped, running — and the reader treats that as a
> fact about **serving traffic**, when it is a fact about the *container's declared state*.

Every reading below was true. Nothing lied. The set of things they covered simply did not include
the things that were broken.

| Instrument said | Actually established | Did **not** establish |
|---|---|---|
| `docker ps` → `(healthy)` | the container's own in-container check passed | anything is listening on the published port |
| `docker port` → `5672/tcp -> 0.0.0.0:5673` | a mapping is *configured* | `docker-proxy` bound anything |
| `Get-NetFirewallHyperVVMSetting` → `DefaultInboundAction: Block` | inbound defaults to deny | that an allow rule was the thing blocking you |
| Windows probe → `REFUSED` | no listener accepted on `127.0.0.1` | that WSL's relay was the cause |
| `uptime -s` advanced across an idle gap | the distro had restarted | *why* — nothing correlated it with the symptom |

> **Before blaming the layer a symptom names, check the layer one level below it.** A refused TCP
> connection is a statement about a socket. Every hypothesis I formed about it was a statement about
> a relay, and the socket turned out to be closed because the process and the host had both gone
> away.

## Fault 1 — the host was gone, and nothing said so

WSL terminates the VM after `vmIdleTimeout` (default **60 000 ms**), and the distro after
`instanceIdleTimeout` in `[general]` (default **15 000 ms**). Every command in this session was a
short-lived `wsl -d ubuntu -- ...`, so between commands the VM was torn down — killing the container
(`Exited (255)`) and re-initialising the relay on the next invocation.

`restart: "no"` in `docker-compose.test.yml` meant it never came back unattended.

The check I had never run:

```bash
uptime -s     # before and after an idle gap: 08:30:29 -> 08:34:30
```

> **The container's health is not the host's health.** When a service runs inside a VM that
> terminates on idle, "the container is fine" and "the machine is running" are separate facts, and
> only one of them is what `docker ps` reports.

Applied: `vmIdleTimeout=86400000` and `[general] instanceIdleTimeout=-1`, both verified against
Microsoft Learn and then confirmed by an unchanged boot time across a 90 s idle window.

## Fault 2 — a crash that reported itself as healthy

`rabbitmq:3.13-management` intermittently fails to boot:

```
exception exit: {{shutdown,{failed_to_start_child,jose_server,terminating}},
                 {jose_app,start,[normal,[}}}}
```

The image ships OpenSSL 3.1.8 with only the `default` provider active and **no `legacy`**, which
JOSE's elliptic-curve key check needs. The supervision tree then tears down the whole application.

The signature that matters: **the container never became unhealthy, because it never became
unhealthy-looking for long.** It started, and exited, between observations. Combined with
`restart: "no"`, one crash meant a permanently dead service that `docker compose up` would not
report as an error.

Fixed with `RABBITMQ_SERVER_ADDITIONAL_ERL_ARGS=-rabbitmq_jose disable`. Nothing in this repo
exercises OAuth2 token validation, so the plugin is not needed — but it must be disabled
*explicitly*, because its absence is not the default.

## Fault 3 — I caused a fourth one while cleaning up

Launching `dockerd` by hand (`nohup dockerd &`) after `wsl --shutdown` left systemd's
`docker.service` to start a **second** instance fighting for the socket. The result was the purest
form of this failure: container `(healthy)`, `docker port` printing the mapping, and **nothing bound
at all** — not even on the distro's own loopback.

```bash
systemctl is-active docker   # active
pgrep -c dockerd             # 2  <-- the fault, and it is one command to see
```

> **Recovery actions are hypotheses too, and they can create faults.** Every fix in this incident
> was a `.wslconfig` edit, a service restart, or a `dockerd` invocation. One of them made the
> original symptom *harder* to read, and I spent several rounds explaining a self-inflicted fault as
> a platform defect.

## Three times I said "fixed"

Each was a single green observation of an intermittent fault:

1. **Hyper-V firewall rules.** Created with `EnforcementStatus: OK`; ports still refused. I had
   promoted `DefaultInboundAction: Block` from a *fact* to a *cause* without testing it.
2. **`127.0.0.1:5673 → OK`.** One successful probe after a clean restart. It was refused again
   within the minute. I reported it as a fix and it was a sample.
3. **392/392 green.** The suite passed, and the broker was genuinely running — but the crash that
   started it had not been demonstrated to be gone.

> **For an intermittent fault, one green observation measures the fault rate, not the fix.** The
> only thing that distinguishes "fixed" from "got lucky" is time across the failure window.

What actually settled it was a **soak**, not a single run:

```
t+ 15s .. t+270s   broker=OK
t+285s              broker=REFUSED     <-- the earlier soak broke here
```

and, after the fix, 8/8 clean over 8 minutes plus 11 minutes of container uptime with
`RestartCount=0`. Still not proof — the fault is intermittent, so the honest claim is that the
*mechanism* is now prevented, not that the symptom is observed gone.

## The diagnostic that would have ended it in a minute

Ordered cheapest and most-likely first. Steps 0–1 are what I skipped for most of the session.

```bash
uptime -s                                       # 0. did the distro restart?
docker ps -a --filter name=test-rabbitmq        # 1. Up, or Exited? (docker ps hides Exited)
ss -ltn | grep ':5673'                          # 2. is anything actually bound?
docker logs test-rabbitmq | grep -iE 'jose|crash'   # 3. only now, an application fault
```

> **`docker ps` shows only running containers, and `docker port` prints a mapping whether or not
> anything is bound.** Both will happily confirm a healthy-looking service that is not serving.

## Related: the same class, already documented

[Broken Diagnostics](broken-diagnostics.md) records the mirror image — instruments reporting
*absence* that was a fact about the *query*. This is the other direction: instruments reporting
*presence* that was a fact about the *declaration*. The corrective reflex is the same and is worth
stating twice: **name what the instrument actually measures, then compare that sentence to the one
you are about to write.**

Also relevant:

- [Verifying a Result Honestly](verification-honesty.md) — why "one green run" was never a
  legitimate close, and what a soak changes.
- [Intermittent Failures and Test Races](intermittent-failures-and-test-races.md) — measuring a
  flake properly instead of hardening around it.
