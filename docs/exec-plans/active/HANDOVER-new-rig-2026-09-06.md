# Handover: the rig moved, and the comprehensive test has not started

**Written 6 September 2026.** Read this first if you are resuming after the new
server exists. Everything below is the state of the world at the moment the old
rig was destroyed.

---

## Where we got to

The core loop closes. Search → grab → download → refine → import → a file in the
library works, proven on the retired VM after seven defects were fixed in one
night (#453, #451, #450, #455, #445, #448, #454). Six of the seven were a rule
that already existed elsewhere in the codebase, sometimes with a comment naming
the exact failure it was there to prevent.

**Phases 9.4–9.9 and 10–13 of the end-to-end plan have never been run.** That is
the outstanding work, and it is why the new rig exists.

---

## The rig changed, on purpose

The Hyper-V VM at `10.1.1.142` was destroyed on 6 September along with Hyper-V
itself. It was hand-built over six weeks and could no longer answer the question
phases 0 and 1 ask: its `C:\Deluno` held forty-eight directories, twenty-eight
of them `App.rollback-*`. It had already lost a GA gate once, when its SABnzbd
configuration quietly vanished and nobody could rebuild it.

The replacement is a physical server. Three things are different, and each one
tests something the VM could not:

| Change | What it buys |
|---|---|
| Provisioned by script, from bare Windows | Phase 0 is a genuinely clean install, not an install being replaced |
| Library on a **NAS share** | An import crosses a network boundary and is a copy, not a rename — what most people actually run |
| **Emby** points at the same share | Whether Deluno named and nested a file correctly is answered by something other than reading the path |

---

## What is ready

Everything in `scripts/lab/`. All merged to `main`.

| Script | Does |
|---|---|
| `rig.json` + `Get-Rig.ps1` | Where the rig is. One file; every script reads it. Moving is a value change. |
| `rig-software.json` | The pinned software set, by SHA-256, with a size floor and a signature requirement |
| `provision-rig.ps1` | Bare Windows → a rig. Stages are separable and idempotent |
| `ensure-rig-services.ps1` | Holds all four services to one shape: starts without a person, survives a reboot |
| `provision-usenet.ps1` | The usenet half, and proves it by moving a real yEnc article and hash-matching |
| `sync-projects-to-nas.ps1` | Mirrors `C:\Projects`, and reports what git has not got |
| `check-e2e-readiness.ps1` | Says READY only when the catalogue reports `hasFile` and the library is not empty |

**`provision-rig.ps1` has never run against real hardware.** The VM that would
have been its crash-test dummy is gone. Three real bugs were found by review
rather than by running it, so expect more:

- `$Args` as a parameter name. It is an automatic variable, and PowerShell
  silently ignores it rather than refusing it — every stage would have passed
  nulls to the remote machine.
- The security-policy edit appended a missing right to the end of the file,
  which lands it inside `[Version]` where `secedit` ignores it and reports
  success. The account would silently have been unable to log on as a service.
- MediaMop's portable zip unpacks to `current/server/`, not `server/`.

Run it a stage at a time the first time: `-Stage preflight`, then `stage`, then
`account`, and so on. A failure is then that stage again, not the whole thing.

---

## Four things are waiting on you

1. **Reboot this desktop.** Hyper-V is disabled but the removal is not complete
   until a restart. Nothing else waits on it.
2. **`qbittorrent_5.2.1_x64_setup.exe`** into `vendor/rig-installers/`. It cannot
   be fetched by script: SourceForge sits behind a Cloudflare bot challenge, and
   working around bot detection is not something to do. SABnzbd 5.0.4 is already
   staged and pinned (24.3 MB, signature valid, SignPath Foundation).
3. **The NAS share path**, then `sync-projects-to-nas.ps1 -Destination <share>`.
4. **The server's address and credentials**, plus a NAS account that can read and
   write the share.

---

## Decisions already made, so they are not re-litigated

**The services run as a dedicated account, not SYSTEM.** A SYSTEM process
authenticates to SMB as the machine account, so a workgroup NAS refuses it. That
— not networking — is what *"`\\storage-city\Data\Media` is not reachable from
the VM's service account"* always meant. A stored password, not S4U: S4U runs a
task with no network credentials, which would defeat the only reason to use an
account.

**Test the share as the service account, never from a WinRM session.** A WinRM
session has no delegatable network credentials, so a probe from the provisioning
session fails whether or not the service could reach it — and sends you to debug
the wrong machine. `provision-rig.ps1` probes through a one-shot task running as
the account, and stores the share credential the same way, because `cmdkey` only
ever writes to the vault of whoever runs it.

**Provisioning stops before Deluno's first run.** Phase 0.5 is *"a clean install
asks to create an account, not to sign in"*. Provisioning the account, the
libraries or the connections would destroy the first thing the plan tests.

**The software set stays at qBittorrent 5.2.1** though 5.2.3 is current. This run
tests Deluno; holding the client at the version phases 0–8 were walked against
means a failure is Deluno's. The client upgrade is worth testing and is a
different run.

**Binaries are inside `C:\Projects` but gitignored.** `rig-software.json` carries
the versions, URLs, hashes and size floors instead — that is the part worth
versioning. The repository deliberately keeps binaries out (`tools/ffmpeg` is
ignored for the same reason; the largest tracked file is 1.5 MB), it is 250 MB
that every clone would pay forever, and MediaMop's zip is 190 MB, over GitHub's
hard per-file limit anyway. The NAS sync covers the bytes; git covers the recipe.

---

## Open issues

| | |
|---|---|
| [#78](https://github.com/jampat000/Deluno/issues/78) | GA epic. Waits on the soak |
| [#82](https://github.com/jampat000/Deluno/issues/82) | 14-day soak. **Do not start the clock until the end-to-end pass has settled the build** — the plan's own rule sends a P0 or P1 back to Day 1 |
| [#333](https://github.com/jampat000/Deluno/issues/333) | The test-host hang is back, now under the serial runner and on CI. Nineteen silent minutes on `Deluno.Persistence.Tests`, cancelled, then a clean pass on a plain re-run. A cancelled gate is not a failed gate, and neither is evidence |

---

## Things that exist only on this desktop

`sync-projects-to-nas.ps1` reports these every run. As of 6 September, GitHub
does not have:

```
TwinAlpha                  2 stashed on main
TwinAlpha-deploy-2ba07ac   detached HEAD, 2 stashed on HEAD
youtube_ai_factory         2 uncommitted on main
Deluno-cleanup-20260821    not a repository, 13.3 MB
deluno-baseline            not a repository
Deluno.Jobs                not a repository
TwinAlpha-deploy-1576ce1   not a repository
TwinAlpha-deploy-7715ced   not a repository
```

Deluno itself is clean and pushed. The four non-repository directories are
leftovers; nobody has decided whether to keep them.

---

## Starting the run

```powershell
# 1. Build the machine
./scripts/lab/provision-rig.ps1 -ComputerName <ip> -Password <admin> `
    -ServiceAccount deluno -ServiceAccountPassword <pw> `
    -LibraryPath '\\<nas>\<share>' -NasUser <u> -NasPassword <p> -Stage preflight

# 2. The desktop fixtures, before any acquisition step
$env:TORZNAB_BIND='0.0.0.0'; $env:TORZNAB_ADVERTISE='<desktop-ip>'
Start-Process python -ArgumentList '-u','scripts\lab\torznab_seed.py'
./scripts/lab/provision-usenet.ps1 -Verify

# 3. Deploy the build under test
./scripts/publish-windows.ps1 -Fast
$env:DELUNO_LAB_PASSWORD = '<admin>'; ./scripts/deploy-lab.ps1

# 4. Then walk docs/exec-plans/active/E2E-full-product-test.md from phase 0
```

Verify live in **real Chrome**. The in-app browser pane is not a substitute.
