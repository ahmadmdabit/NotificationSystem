# Stopgaps That Fail on the Machine They Target

[Back to Learning](README.md) · [Verifying and Remediating](verifying-and-remediating.md)

Two independent defects in the same change set, both in the machinery whose entire purpose was to let
someone run the project without a commercially licensed key. Both would have hit precisely the
developer the stopgap was written for.

## The template named a variable that Compose overwrites

`.env.example` instructed:

```
Messaging__Enabled=false
```

`docker-compose.yml` set, explicitly:

```yaml
environment:
  Messaging__Enabled: ${MESSAGING_ENABLED:-true}
```

Compose's `environment:` entry takes precedence over `.env` for that container. A developer who copies
the template verbatim, fills the five required secrets, and runs `docker compose up -d --build` gets
messaging **ON** and a `MassTransit.ConfigurationException` crash-loop.

> **A variable that Compose interpolates is not configurable from `.env` under its ASP.NET name.** The
> container sees whatever the `environment:` block assigns, whatever else the file contains. When a
> stopgap has a name, check every layer that could rename it — and note which layer wins.

The compose comment named the correct variable. Only the template was wrong, which is what made it
survive: the file a developer opens first was the file that was inaccurate.

> A stopgap is a **contract with someone who has not read the rest of the repo.** It is the piece most
> likely to be followed literally and verified least.

## The workaround assumed a file that cannot be absent

The licence `volumes:` entry was declared unconditionally, independent of `MESSAGING_ENABLED`:

```yaml
volumes:
  - ${MT_LICENSE_PATH}:/masstransit/license.txt:ro
```

If the host path does not exist, Docker **creates an empty directory** there and bind-mounts it. The
result is an unreadable `/masstransit/license.txt` or a mount error — instead of the documented
*"License must be specified"*. The gate does not fire where it was meant to.

> **A stopgap must be tested on the machine it exists for.** This one was verified on a host that had
> a licence file, and it is precisely the host *without* one that the stopgap targets. A guard that is
> only ever exercised in the fully-provisioned case is untested.

The fix was a compose `profiles:` split, or a documented `mkdir`/touch step — both of which move the
provisioning step to the same place as the decision to run without one.

> Shared root: **these are the same class of defect.** A guard that is not reachable from the state it
> protects. The redaction allowlist fails closed on an unknown environment; the bind-mount fails
> *open*, into a confusing error, on the one input the feature exists to handle.
