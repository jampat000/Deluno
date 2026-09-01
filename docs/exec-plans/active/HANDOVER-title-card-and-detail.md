# Handover — the title card, the detail pages, and the live shelf implementation

Paste this whole file as the opening prompt of a fresh session. It is written to
be self-contained: nothing below assumes you saw the conversation that produced
it.

---

## 0. Who you are working for, and how he works

James. Windows, .NET 10 + React 19. He reads the product like a designer and will
tell you plainly when something is wrong ("its all cock eyed", "you gave me
lemons"). Four standing rules, learned the hard way:

1. **Verify live, in the browser, on the rig. Measure, do not eyeball.** Almost
   every defect in this file was found by reading a computed value off the real
   page, and several were *missed* for hours by looking at screenshots. If you
   are about to say "that looks right", read the number instead.
2. **Repetition is a defect, in code too.** After you fix something, go and find
   where else that shape lives. Section 3 below contains four bugs that were one
   bug in four places, and that was found by looking, not by waiting.
3. **The design bar:** a pane fits one screen, it feels alive, and it never says
   the same thing twice. Duplicated information on a page is a defect he will
   circle.
4. **Never run GitHub Actions for this repo.** Verification is local + the rig.

Two more that cost real time if forgotten:

- **Never `Start-Process` over WinRM** — it dies with the runspace and breaks
  DPAPI. Use `Stop-ScheduledTask` / `Start-ScheduledTask`.
- **Stop `Deluno.Host` and kill stray `testhost` before any build** or you get
  MSB3027 file locks.

---

## 1. Baseline — this is a clean, verified starting point

| | |
|---|---|
| Branch / HEAD | `main` @ **`92b2446`**, working tree clean, pushed (`HEAD == origin/main`) |
| .NET | **1144 passed**, 1 skipped, 0 failed (6 assemblies) |
| Web unit (vitest) | **222 passed** (25 files) |
| Smoke (playwright, chromium + mobile) | **266 passed, 0 failed** |
| Metadata gateway (node --test) | **18 passed** |
| `npm run ci:check` | 7 passed, 0 warned, 0 failed |
| Rig | `10.1.1.142:5099` returns 200, scheduled task Running, serving this exact build |
| Cloudflare Worker | `deluno-metadata-gateway` deployed at version `888615dc-3e98-4192-95a8-7b5d546842cf` |

There are **no known failing tests and no known regressions**. Two smoke specs
that had been red for some time were fixed in `92b2446`; see §3.

### Recent commits, newest first

```
92b2446 fix(marks): a list row and its poster paint the same colour, and clicking a face goes somewhere
0f479c3 feat(detail): the whole cast, the crew, and a hero you can see again
34bceaa fix(detail): centre the shield on the title, not on its baseline
30e3950 fix(detail): a card asked to be a line — restack the header
cd9d35b feat(detail): a header worth the name — bigger poster, the cast, and the facts
f85eac2 fix(catalogue): a detail page can never know less than the shelf again
```

---

## 2. The rig, and how to deploy to it

`http://10.1.1.142:5099` — app login `admin` / `Deluno-Lab-2026!`.
Windows `Administrator` / `Deluno-MM-Lab-2026!`.

It holds **10 movies and 6 shows**. Big Buck Bunny was removed this session
(record *and* files) at James's word — *"kill the big buck bunny file off disk so
it never was here"*. **The reference title for movie work is now Arrival**
(`01a03ddbaf237f1195daf8992aa529da`), also his instruction. Note that this means
**no movie on the rig currently has a real file on disk**, so file-facts paths
(path, size, codec, release group) render empty everywhere. If you need to
exercise them, import a file first.

```powershell
$p = ConvertTo-SecureString 'Deluno-MM-Lab-2026!' -AsPlainText -Force
$c = New-Object System.Management.Automation.PSCredential('Administrator',$p)
$s = New-PSSession -ComputerName 10.1.1.142 -Credential $c
Invoke-Command -Session $s -ScriptBlock { Stop-ScheduledTask -TaskName 'Deluno Host'; Start-Sleep 3 }
Copy-Item -ToSession $s -Path 'C:\Projects\Deluno\artifacts\publish\win-x64\*' -Destination 'C:\Deluno\App' -Recurse -Force
Copy-Item -ToSession $s -Path 'C:\Projects\Deluno\apps\web\dist\*' -Destination 'C:\Deluno\App\wwwroot' -Recurse -Force
Invoke-Command -Session $s -ScriptBlock { Start-ScheduledTask -TaskName 'Deluno Host'; Start-Sleep 14 }
Remove-PSSession $s
```

- **Front-end-only change:** `npm run build:web`, copy `apps/web/dist/*` to
  `C:\Deluno\App\wwwroot`, hard reload. No publish, no restart.
- **Any C# change needs a republish:**
  `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/publish-windows.ps1`
  (~5 minutes — background it). `pwsh` is **not** on PATH here; use `powershell`.
- **API auth** is a bearer token in `sessionStorage` under `deluno-auth-token`
  (per tab). `POST /api/auth/login` with `{username,password}` returns
  `accessToken`. There is no `/api/health` — a 404 there does not mean it is
  down; hit `/`.
- James's live arr instances are at `10.1.1.35` — Radarr `:8310`, Sonarr `:8989`,
  Prowlarr `:9696`, Bazarr `:6767`. **Look, do not save.**
- **Do not touch issues #78, #81, #82, #269. Do not close #329 or #330.**

### The metadata gateway is a separate deploy

`services/metadata-gateway` is a Cloudflare Worker at
`https://deluno-metadata-gateway.ejmdigital.workers.dev`. The rig reads metadata
through it, so **a change to cast/crew/artwork behaviour is not visible on the
rig until the Worker is deployed**, even after a full app republish.

```bash
cd services/metadata-gateway && npm test && npx wrangler deploy
```

Two traps that cost time this session:

- **Its KV cache holds search answers for 12 hours.** A gateway change will not
  show on an already-refreshed title until that expires. To test immediately,
  refresh a title the cache has not seen.
- **A plain search returns no cast at all.** Credits only come back on a
  *detail* lookup, i.e. when `providerId` is in the query. This is by design (one
  request, no detail fan-out) — do not "fix" it.

---

## 3. What was done, and why (all of it is on `main` and on the rig)

### 3a. Cast went from ten to thirty, and crew exists at all

Both caps lived in the **gateway**, not in Deluno. TMDb bills its cast in order,
so ten was the whole ensemble of a small film and the opening titles of a big
one — Arrival's page stopped at Frank Schorpion.

- `MAX_CAST = 30`, `MAX_CREW = 20` in `services/metadata-gateway/src/index.js`.
- Crew is filtered to `CREW_JOBS` — the jobs a viewer recognises, in the order a
  title card lists them — and **each person is folded into one entry** carrying
  every job they did. Villeneuve directs *and* produces; two identical portraits
  in a row reads as a bug, not a fuller credit.
- `MetadataCrewMember` added to `src/Deluno.Integrations/Metadata/MetadataModels.cs`
  so the broker response survives the round-trip. **Without this the gateway's
  `crew` is silently dropped in transit** — the broker JSON is deserialised into
  `MetadataSearchResult` and re-serialised.
- While in there: the **direct** TMDb provider never set `Director` at all,
  though it had fetched the crew list all along — so a direct-TMDb library sorted
  every title as blank on a column the catalogue offers. Fixed.

### 3b. One credits component, not two copies

`readStoredCast` was written out twice, character for character, once per detail
page — which is exactly why the film page grew portrait cards and the show page
kept six 40px avatars. Now:

- `apps/web/src/components/app/credits-row.tsx` — `CreditsRow`, `CreditCard`,
  `ScrollArrow`, `readStoredCredits`. Both detail pages use it, so TV got the
  bigger faces and the crew row for free.
- Reads **both** casings: the gateway answers camelCase, Deluno stores what its
  own record serialises (Pascal). A reader that knows one returns an empty list
  for half the installs while looking perfectly correct.

### 3c. Cast and crew moved out of the header card

They lived inside it, which made it 989px tall. A header you have to scroll is
not a header. Radarr and Sonarr break them out; so do we — one `<Card>` each,
below the hero, on both detail pages.

### 3d. Arrows AND scrolling on the credit rows

James asked which is better. The answer given, and the reasoning, because he may
revisit it: **neither alone.** A scrollbar is a poor *signal* — thirty faces cut
off at the edge reads as the end of the list — but replacing scrolling with
buttons breaks the three ways people actually move a row like this (trackpad
swipe, touch drag, arrow keys after tabbing in). So the scrolling stays, the bar
is hidden with the existing `.no-scrollbar` utility, and arrows do the
signalling, live only on the side that has more to show.

### 3e. The hero backdrop is *solved*, not set

It never stopped rendering. Two stacked full-coverage scrims multiplied it down
to roughly a tenth of itself, and the header at 989px was showing a zoomed 40%
slice of a 16:9 plate.

`apps/web/src/components/app/hero-backdrop.tsx` now draws the plate to a 24×12
canvas and takes the **Rec. 709 luma of the band the text sits over**, then sets
the scrim from it. Measured on the rig:

| Title | Luminance | Scrim | Artwork opacity |
|---|---|---|---|
| Arrival (pale fog plate) | 0.575 | 0.74 | 0.52 |
| Interstellar (dark plate) | — | 0.41 | 0.64 |

One constant could never serve both, which is precisely what James reported
(*"some of the text in arrival is hard to read now but on other titles its OK"*).
Reading the pixels needs the artwork proxy to allow it, so the gateway now sends
`Access-Control-Allow-Origin: *` on `/artwork/*`. **A tainted canvas, a missing
header or no canvas at all falls back to `FALLBACK_SCRIM = 0.82`** — the
readable end, deliberately.

### 3f. Source marks instead of grey uppercase words

`apps/web/src/components/app/source-mark.tsx` — TMDb, IMDb, Rotten Tomatoes and
Metacritic in their own palettes, **drawn inline**, so nothing on the page
reaches a third-party asset host to render itself. Used by `RatingStrip`,
`RatingLine` and the metadata aside on both detail pages. Unknown sources fall
back to the text label, which is what all of them used to be.

### 3g. Header proportions

Poster 30rem → 24rem (`h-96 w-64`), grid column `20rem` → `16rem`, the
`min-h-[30rem]` floor removed, and the facts `<dl>` changed from
`flex flex-wrap` (which packed left against a void) to a responsive grid that
spreads across the column.

### 3h. Credits click through to the person

TMDb person ids are now passed through the gateway (`personId`), the .NET
contract (`MetadataCastMember.PersonId` / `MetadataCrewMember.PersonId`) and the
reader; a card is an `<a>` when we have one. **Verified live: 50 links on
Sicario, all resolving.** See §4 for the TMDb-vs-IMDb decision.

### 3i. The subtitle bar's lead sits on the number's baseline

`items-center` centres two *boxes*. The lead is 0.72em and uppercase with no
descenders, so its glyphs sat in the top of its box and the word floated above
the number. Two sizes of type on one line share a baseline. Measured on the rig:
**1.5px out before, 0.1px after.**

### 3j. Four instances of one colour bug — this is the important one

Two smoke specs had been failing at HEAD. **Both were telling the truth.**

1. **The Quality met legend chip had silently lost its gold.** Painting the
   legend from the DESIGN-006 bar surfaces set `background: <colour>`, and the
   `background` **shorthand resets `background-image`** — which is the whole of
   `.mark-grail`. The one rung with a treatment of its own went flat while the
   card's own bar stayed gold. Fixed with the `backgroundColor` longhand, so the
   surface is the colour underneath and the gradient sits on top — the same
   layering the bar itself uses.
2. **A compact list row drew the wrong red** — `rgb(239,77,77)` in the row
   against `rgb(192,17,28)` on the card for the same title, the exact pair
   `MarkStrip`'s own docstring quotes. `TitleMarkLabel` has taken a `type` since
   the legend was fixed; `library-table.tsx` never passed one.
3. **`MarkStrip` knew nothing about monitoring**, so an unmonitored title read
   Missing red in the list and grey on its poster — the override that is supposed
   to beat every colour rule, ignored. It now takes `monitored` (defaulting to
   `true`, because a *legend* is not a title) and drops the gold leaf on a title
   nothing is watching.
4. Then, per rule 2 above, **the rest of the shape**: the calendar mixes both
   media and had no medium to pass (it now carries `mediaType` per entry), and
   episode rows on the show page and episode-search page now pass `show`. **Every
   `TitleMarkLabel` in the product now says which shelf it belongs to.**

The specs were also wrong in the other direction: they asserted `bg-destructive`
and a half-gradient, both of which the design moved past (the half was ditched
for the flat grey override). **Three attempts to replace that with a
computed-colour assertion all failed**, because a mark is three different
components across two shelves and two densities, and "the painted element" is a
fill in one, a track in another and a dot in the third. That is a *wrong-place*
assertion, not a rule needing more clauses — which token each rung uses has an
exact answer and now has an exact test, by name, in
`apps/web/src/components/ui/title-mark.test.tsx`. The smoke spec keeps only the
claim a live page can make. **If you find yourself adding clauses to a colour
heuristic in a smoke test, stop and move the assertion.**

---

## 4. Decisions taken — do not silently reverse these

| Decision | Why | Status |
|---|---|---|
| **Credits link to TMDb, not IMDb** | The TMDb person id arrives free with the credits. A name is not a link (names collide), and an IMDb id needs `/3/person/{id}/external_ids` — **~50 extra upstream calls per title refresh**, against a Cloudflare subrequest limit of 50 on the free plan. Radarr links TMDb too. | **Open with James.** He asked for IMDb; he was given TMDb with this reasoning and has not ruled. If he wants IMDb: cache per-person external ids in the gateway's KV (person→imdb is immutable, so a long TTL is safe) and check the plan's subrequest limit first. |
| Unmonitored is one flat grey, fill and track alike | *"unmonitored titles are the override, they are always grey - once they are monitored they inherit the normal statuses"*. Grey therefore means exactly one thing on a card. | Settled |
| The half/drained dot is gone | *"ditch the half and just overright it when its unmonitored its just black or grey period"* | Settled |
| Vocabulary is **"Unmonitored"**, one word | *"change to unmonitored then"* | Settled |
| Movie and TV monitored bars use a Missing-red remainder; held coverage is green and Quality met is gold | Both shelves use the same composition grammar where the state matches; TV still measures the top bar over aired episodes, and Unmonitored always overrides both bars with one flat grey | Settled |
| Monitoring is a **configurable toggle under the poster**, plus a shield on the detail page | *"it should be a configurable toggle that displays under the poster like all the other toggles"*. An earlier attempt removed the toggle entirely and was wrong. | Settled |
| The detail page shows **everything, unconditionally** | A shelf lets you choose what a card carries because you are scanning; here you have stopped and gone looking, so mirroring the toggles would hide a fact at the moment you want it. | Settled |
| A detail page can never know less than the shelf | Guarded by a reflection test over every property — `tests/Deluno.Persistence.Tests/Catalogue/DetailMatchesListProjectionTests.cs`. **Its fixture imports a real file on purpose**: without one the file facts are null on both sides and the test passes while guarding nothing. | Settled |
| The movie and TV card specs are settled independently | *"they should be independant of each other, tv and movie"* | Settled |

---

## 5. TV card implementation — completed

### 5a. The approved TV card is built and shipped

`CARD_DESIGN.show.bars` is now **`true`** in `apps/web/src/lib/card-design.ts`.
The movie card remains independently declared and unchanged. TV now uses the
shared two-bar mechanism with its own choices:

- the top bar says aired episodes held and fills to coverage of what has aired;
- the bottom bar says subtitle coverage across episodes actually held;
- the remainder is Missing red, the held portion is green, and a fully held
  Quality met show is gold;
- Unmonitored takes priority over every other state and paints both bars one flat
  grey, including partial and empty coverage;
- Continuing remains a TV-only lifecycle label and legend colour (**Magenta
  `318 78% 38%`**), while its held coverage follows the green rule;
- the top shelf legend places **Unmonitored immediately after Upcoming behind a
  divider** on both Movies and TV; the separate subtitle legend contains only
  subtitle segments;
- episode-count and subtitle-count words are independently switchable, on by
  default, and the only lead word is `SUBS`.

The TV renderer is now the implementation record for this direction. Its
alternate hue, track and fill treatments remain available for comparison, while
the product defaults are the approved recommendation. The live lab has been
updated and the grid, table, overview, legend, calendar, search and detail
surfaces all use the same state palette.

### 5b. TV detail page parity — completed

The TV detail page now uses the same visual format as the movie detail page while
retaining TV-specific content:

- Shared 16:9 hero/backdrop treatment, `16rem` poster column, and `h-96 w-64`
  poster artwork.
- Certification, year, and runtime metadata on the title line, followed by
  monitoring/status and genre marks.
- A TV facts `<dl>` for episodes, network, studio, language, collection,
  director, status, added date, and import issues when available.
- The shared `Ratings & IDs` aside with `RatingStrip`; its `xl:min-h-96` height
  is now exactly aligned with the poster on both movie and TV pages.
- Shared cast and crew rows using the same portrait-card treatment.

The page is deployed and verified on the live lab. This detail-page work is
complete; the unresolved TV decision below applies to the library card, not this
page.

### 5c. Monitor a person → a TMDb Person import list — completed

The `tmdb-person` intake provider is implemented end to end. It accepts a numeric
TMDb person id or TMDb person URL, fetches combined credits, separates movie and
TV entries, deduplicates by provider id, and supports the Radarr-style Cast,
Director, Producer, Sound, and Writing role filters. Cast is enabled by default;
the selected roles are stored in the canonical feed URL and used by preview and
sync.

The UI exposes the provider and filters in the Import Lists drawer. IMDb credit
destinations are also implemented through the metadata gateway's cached
per-person resolver; existing records may need a metadata refresh before their
stored credit cards receive the new destination.

### 5d. Smaller loose ends

- **`canBeHalf`** in `TITLE_MARK_PRESENTATION` may now have no consumer on the
  card since the half was ditched. **Check before deleting** — `TitleMarkLabel`
  still reads it for the `· unmonitored` suffix.
- **RSS sync / "Watch for new releases"** was agreed earlier in the project and
  has never been filed as an issue.
- **Backlog order** agreed with James: #314 (interval walk-through), then #328,
  #313, #315, #316, #317, #318, #320, #305, then #321 / #301. His standing rule
  is **finish and close open issues before broader work**.

---

## 6. Where things live

| | |
|---|---|
| The spec | `docs/exec-plans/active/DESIGN-006-the-title-card.md` |
| Per-medium card declaration | `apps/web/src/lib/card-design.ts` |
| Marks, bars, legend | `apps/web/src/components/ui/title-mark.tsx` (+ `.test.tsx`, `title-bars.test.tsx`) |
| Colour tokens | `apps/web/src/lib/status-tones.ts`, `apps/web/src/index.css` |
| Credits | `apps/web/src/components/app/credits-row.tsx` (+ `.test.tsx`) |
| Hero backdrop | `apps/web/src/components/app/hero-backdrop.tsx` |
| Source marks | `apps/web/src/components/app/source-mark.tsx` |
| Ratings | `apps/web/src/components/app/rating-strip.tsx` (`RatingStrip` = cards, `RatingLine` = one line) |
| Detail pages | `apps/web/src/routes/movie-detail-page.tsx`, `show-detail-page.tsx` |
| Shelf | `apps/web/src/components/app/library-grid.tsx`, `library-table.tsx` |
| Gateway | `services/metadata-gateway/src/index.js` (+ `test/index.test.js`) |
| Metadata contracts | `src/Deluno.Integrations/Metadata/MetadataModels.cs`, `TmdbMetadataProvider.cs` |
| Detail-vs-shelf guarantee | `tests/Deluno.Persistence.Tests/Catalogue/DetailMatchesListProjectionTests.cs` |
| Render audits | `ui-explorations/card-decider-movies.html`, `card-decider-tv.html` |

**Suggested next move:** review the implemented TV shelf on the live lab across
grid, table and overview density, then capture any final visual adjustments for
the implementation PR. The credit-type-filter decision is no longer blocking;
the implemented canonical feed URL is the current approach.
