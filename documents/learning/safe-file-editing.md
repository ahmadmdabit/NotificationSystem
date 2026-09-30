# Editing Files Safely

[Back to Learning](README.md)

Most damage in this session was self-inflicted by editing, not by reasoning. Every case below was
introduced by a scripted multi-line edit and caught by a structural check — **never by re-reading
the prose and feeling satisfied**.

## Verify structurally, after every scripted edit

Three cheap checks, run together:

```powershell
# 1. Duplicated adjacent lines (catches a repeated paragraph)
$v = [IO.File]::ReadAllLines($f)
for ($i=1; $i -lt $v.Count; $i++) { if ($v[$i].Trim() -ne '' -and $v[$i] -eq $v[$i-1]) { "DUP $($i+1)" } }

# 2. Code-fence parity (catches a truncated block)
$c = (Select-String -Path $f -Pattern '^```').Count   # must be even

# 3. Heading sequence (catches a lost section)
Select-String -Path $f -Pattern '^#{1,3} ' | ForEach-Object { "$($_.LineNumber)  $($_.Line)" }
```

These caught every duplication, every lost heading, and every truncated block below. `dotnet build`
catches syntax damage in code but **not** in Markdown, and not duplicated prose.

## Index-based edits corrupt more than they fix

Editing by line number is fine for a single line and dangerous for a block. Observed failures:

| Mistake | Symptom |
|---|---|
| `RemoveRange` then `InsertRange` at the same index, then absolute assignment at a shifted index | Paragraph duplicated **and** `## License` heading deleted |
| `insert_line` at a line inside a method body | Class split in half; the new class nested at **brace depth 1**, reported only as `CS0246` three files away |
| Re-running a repair that assumed its own first attempt had failed | Duplicate blockquote, then a duplicate table row, then a third row |

**Prefer whole-section rewrites** over index surgery when the block is more than a few lines, then
verify with the checks above.

### The nesting case is the nastiest

A class inserted mid-method compiles into a *nested* class, which the compiler reports as "type not
found" in the consuming file — pointing at the wrong file entirely. Confirm scope with brace depth:

```powershell
$d=0; foreach($line in $lines){ if($line -match 'class [A-Za-z]'){ "depth=$d $line" }
                                $d += ([regex]::Matches($line,'\{')).Count - ([regex]::Matches($line,'\}')).Count }
```

Every class should report `depth=0`, and the running total should end at 0.

## Line endings: this repo is mixed

`StoredProcedureContractTests.cs` is 178 CRLF; `NotificationHistoryRepository.cs` is 2 CRLF / 87 LF.
Consequently:

- `replace_in_file` fails with a misleading **"text not found"** when `old_text` uses `\n` and the
  file uses `\r\n`.
- `patch` corrupts non-LF/UTF8-BOM content — LF→CRLF conversion, indent distortion.

When an editor tool reports success on a repo file, **confirm the text actually changed** (grep for
it). A `Replace()` that matched nothing still exits 0. When scripting, read with `ReadAllLines`,
edit as a `List[string]`, and write with `WriteAllLines` — which normalises to the platform's CRLF
consistently, avoiding mixed endings in the file you touch.

## Backticks and PowerShell patterns

`-like '...`gitignore`...'` does not do what it looks like: the backtick is an **escape character**,
so patterns containing markdown code spans silently fail to match. This left a stale table row
behind through three attempts. Use explicit `[char]96` concatenation, or match on a distinctive
substring with no backticks:

```powershell
$target = '| Deferred (step 4) | 4a ' + [char]96 + '.gitignore' + [char]96
$l[$i].StartsWith($target)
```

## Silent no-op writes

A PowerShell array was built and `$lines.AddRange($t)` was never called; the command reported
success and wrote nothing. **After any scripted write, read the file back and count lines** before
moving on.

## `Set-Content $array -NoNewline` destroys the file

Writing a `string[]` with `-NoNewline` concatenates every element with **no separator**, collapsing a
255-line test file (10,955 characters) to **one line**. Use a single here-string, or
`[System.IO.File]::WriteAllText`.

> **Always re-read the line count after a scripted rewrite.** The command reports success.

## `patch` corrupts C# source

`patch` introduces encoding/metadata damage that breaks the **TUnit source generator**, and it
doubles `..` segments in `.csproj` path references. Use `write_file` for C# and Python for `.csproj`.

If a C# file was previously touched by `patch`, the generator needs a **clean rebuild** - delete
`bin`/`obj`, or the damage persists invisibly.

## Delete what the verification itself created

Scratch probe tests (`ProbeTests.cs` was created and removed mid-task), build logs, and temp files all
end up as untracked noise. Check `git status` for verification artefacts before declaring done - and
delete build logs so they do not pollute the next status check.

## Long builds exceed the command window

A full solution build can take 43 s or more, past a 30 s command timeout. Run it detached and poll:

```powershell
Start-Process dotnet -ArgumentList 'build','NotificationSystem.slnx','-c','Debug' `
  -RedirectStandardOutput build.log -RedirectStandardError build.err -NoNewWindow
# then poll the log for: Build succeeded / Warning(s) / Error(s)
```

The `Start-Sleep` + read pattern is reliable; assuming the first read is final is not. Build outputs
are also stale-prone - see the incremental-build trap in
[Verifying a Result Honestly](verification-honesty.md).
