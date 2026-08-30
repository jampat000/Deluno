"""
Spread the lab's eleven real films across every card state, so the shelf itself
can confirm the design.

James chose this over any test-only page: "seed the lab library instead". The
reason it is needed is that a real library cannot show you the design — the lab
holds eleven films and ten of them are Missing, so Downloading, Upcoming,
Subber-unresolved and every unmonitored variant are unreachable on the shelf, and
those are exactly the cards a design fails on.

No titles are invented. The real eleven are redistributed, and the originals are
written to `undo.json` beside this file so it is one command to put back.

Run against a LOCAL copy taken with the host stopped, together with its -wal and
-shm — see the seed-library.py docstring. The WAL is checkpointed here so a
single movies.db can go back, and the rig's stale sidecars must be moved aside.
"""
import json
import sqlite3
import sys

DB = sys.argv[1]
UNDO = sys.argv[2]

# title -> (monitored, wanted_status, has_file, current_quality)
#
# Chosen to cover the ladder both monitored and not: three at the cutoff, three
# upgradable, one downloading, two missing, two upcoming — and three of those
# unmonitored, spread across different rungs so the grey override can be seen
# overruling more than one colour.
PLAN = {
    "Blade Runner 2049":                 (1, "covered",     1, "Remux-2160p"),
    "Ex Machina":                        (1, "covered",     1, "Bluray-1080p"),
    "Arrival":                           (1, "upgrade",     1, "WEBDL-1080p"),
    "Sicario":                           (1, "upgrade",     1, "WEBRip-720p"),
    "Everything Everywhere All at Once": (1, "upgrade",     1, "WEBDL-2160p"),
    "Dune":                              (1, "downloading", 0, None),
    "Inception":                         (1, "missing",     0, None),
    "Interstellar":                      (1, "upcoming",    0, None),
    # The unmonitored three, on three different rungs.
    "The Martian":                       (0, "missing",     0, None),
    "Mad Max: Fury Road":                (0, "upcoming",    0, None),
    "Big Buck Bunny":                    (0, "covered",     1, "WEB 2160p"),
}

con = sqlite3.connect(DB)
con.execute("PRAGMA foreign_keys = ON")

rows = con.execute("""
    select e.id, e.title, e.monitored, w.wanted_status, w.has_file, w.current_quality
    from movie_entries e left join movie_wanted_state w on w.movie_id = e.id
""").fetchall()

undo = [{"id": r[0], "title": r[1], "monitored": r[2], "wanted_status": r[3],
         "has_file": r[4], "current_quality": r[5]} for r in rows]
with open(UNDO, "w", encoding="utf-8") as f:
    json.dump(undo, f, indent=1)

by_title = {r[1]: r[0] for r in rows}
missing_from_db = [t for t in PLAN if t not in by_title]
if missing_from_db:
    raise SystemExit("not in the catalogue: " + ", ".join(missing_from_db))

for title, (monitored, status, has_file, quality) in PLAN.items():
    mid = by_title[title]
    con.execute("update movie_entries set monitored = ? where id = ?", (monitored, mid))
    # Keep the entry's denormalised copies in step with the wanted row, or the
    # shelf and the card would disagree about the same title.
    con.execute("""
        update movie_entries
           set primary_has_file = ?, primary_current_quality = ?
         where id = ?
    """, (has_file, quality, mid))
    con.execute("""
        update movie_wanted_state
           set wanted_status = ?, has_file = ?, current_quality = ?,
               quality_cutoff_met = ?
         where movie_id = ?
    """, (status, has_file, quality, 1 if status == "covered" else 0, mid))

con.commit()
# Fold the WAL into the database so one file can go back to the rig.
con.execute("PRAGMA wal_checkpoint(TRUNCATE)")
con.commit()

print("assigned:")
for r in con.execute("""
    select e.title, e.monitored, w.wanted_status, w.has_file, w.current_quality
    from movie_entries e left join movie_wanted_state w on w.movie_id = e.id
    order by w.wanted_status, e.monitored desc, e.title
"""):
    print(f"  {r[0][:34]:<34} mon={r[1]} status={r[2]:<12} file={r[3]} q={r[4]}")
con.close()
print("undo written to", UNDO)
