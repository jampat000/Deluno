"""Fill a movies catalogue with N synthetic titles, for measuring the shelf.

Nothing here ships. It exists because #312 — one continuous virtualised shelf
with a jump rail — cannot be judged on a library of eleven films, and the rig's
real library is eleven films. Measured with 20,000: first paint 73 ms, the whole
library on the client in 1.4 s over 41 requests, 27.8 MB of heap, 3,507 DOM
nodes, and a median 10 ms frame at an ordinary scroll rate.

The titles are combinations of a word list, chosen so every letter of the rail
has something behind it and "#" gets digits, brackets and an umlaut. Sizes,
qualities, runtimes, ratings and years are spread so the rail's other stops —
decades, size bands, ladder rungs — have something to show too.

    python scripts/lab/seed-library.py <path-to-movies.db> [count]

Seeded rows all carry a `seed` id prefix, so undoing it is one statement:

    DELETE FROM movie_entries WHERE id LIKE 'seed%';

The wanted-state rows go with them by foreign key, and the triggers that keep
`primary_file_size_bytes` and `primary_quality_rank` on the entry fire on the
way in and the way out.

**Edit the VM's database the documented way** (`HANDOVER-live-e2e-run.md`): stop
the host, copy `movies.db` *and* its `-wal`/`-shm` down, run this against the
local copy, then move the VM's stale sidecars aside before copying it back. Skip
that and the stale WAL silently reverts the lot.
"""

import random
import sqlite3
import sys
import uuid
from datetime import datetime, timedelta, timezone

DATABASE = sys.argv[1] if len(sys.argv) > 1 else 'movies.db'
TARGET = int(sys.argv[2]) if len(sys.argv) > 2 else 20000
random.seed(312)

FIRST = ["Absolute","Broken","Crimson","Distant","Eternal","Frozen","Golden","Hidden","Iron","Jagged",
         "Killing","Last","Midnight","Northern","Opaque","Perfect","Quiet","Rising","Silent","Tender",
         "Unseen","Violent","Wandering","Xenon","Yellow","Zero","300","1917","[REC]","Ödipus"]
SECOND = ["Harbour","Fields","Machine","Orbit","Requiem","Signal","Tide","Vault","Winter","Anthem",
          "Bridge","Cathedral","Descent","Echo","Fortune","Gambit","Horizon","Inlet","Junction","Kingdom"]
THIRD = ["", " Part Two", " Redux", " Rising", " Reloaded", " Chapter Three", " Origins", " Forever"]
GENRES = ["Action","Drama","Comedy","Thriller","Science Fiction","Horror","Documentary","Animation","Romance","Crime"]
QUALITIES = ["WEB 1080p","Bluray 1080p","Remux 1080p","WEB 2160p","Bluray 2160p","Remux 2160p","HDTV 720p","Bluray 720p","WEB 720p","DVD"]

db = sqlite3.connect(DATABASE)
db.execute("PRAGMA foreign_keys=ON")

# Whichever library this catalogue already uses. Seeded titles join the one
# that is there rather than inventing a library id nothing else knows about.
row = db.execute("SELECT library_id FROM movie_wanted_state LIMIT 1").fetchone()
if row is None:
    sys.exit("No library in this catalogue yet — add one title through the UI first.")
LIBRARY = row[0]

existing = {r[0].lower() for r in db.execute("SELECT title FROM movie_entries")}
base = datetime(2024, 1, 1, tzinfo=timezone.utc)

entries, states = [], []
seen = set(existing)
made = 0
while made < TARGET:
    title = f"{random.choice(FIRST)} {random.choice(SECOND)}{random.choice(THIRD)}"
    year = random.randint(1936, 2026)
    key = (title.lower(), year)
    if key in seen:
        continue
    seen.add(key)
    made += 1

    mid = "seed" + uuid.uuid4().hex[:28]
    created = (base + timedelta(minutes=made * 13)).isoformat()
    has_file = random.random() < 0.78
    quality = random.choice(QUALITIES) if has_file else None
    runtime = random.choice([None] + list(range(62, 205, 3)))
    size = int(random.uniform(0.6, 62) * 1024 ** 3) if has_file else None
    rating = round(random.uniform(3.2, 9.4), 1) if random.random() < 0.9 else None

    entries.append((
        mid, title, year, None, 1 if random.random() < 0.85 else 0,
        "tmdb", str(900000 + made), None,
        "Seeded by scripts/lab/seed-library.py. Not a real film.", None, None,
        rating, ", ".join(random.sample(GENRES, random.randint(1, 3))), None, None, None,
        created, created, "released", runtime,
        round(random.uniform(0.1, 2400), 3), random.randint(5, 24000)))

    cutoff_met = has_file and random.random() < 0.7
    states.append((
        mid, LIBRARY,
        "covered" if cutoff_met else ("upgrade" if has_file else "missing"),
        "Seeded.", 1 if has_file else 0, quality, "WEB 1080p",
        1 if cutoff_met else 0, created,
        (f"C:\Deluno\Library\Movies\{title} ({year})\{title} ({year}).mkv" if has_file else None),
        size))

db.executemany("""
    INSERT INTO movie_entries
        (id, title, release_year, imdb_id, monitored, metadata_provider, metadata_provider_id,
         original_title, overview, poster_url, backdrop_url, rating, genres, external_url,
         metadata_json, metadata_updated_utc, created_utc, updated_utc, minimum_availability,
         runtime_minutes, popularity, vote_count)
    VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)
""", entries)

db.executemany("""
    INSERT INTO movie_wanted_state
        (movie_id, library_id, wanted_status, wanted_reason, has_file, current_quality,
         target_quality, quality_cutoff_met, updated_utc, file_path, file_size_bytes)
    VALUES (?,?,?,?,?,?,?,?,?,?,?)
""", states)

db.commit()
print("entries", db.execute("select count(*) from movie_entries").fetchone()[0])
print("with rank", db.execute("select count(*) from movie_entries where primary_quality_rank is not null").fetchone()[0])
print("with size", db.execute("select count(*) from movie_entries where primary_file_size_bytes is not null").fetchone()[0])
db.execute("VACUUM")
db.close()
