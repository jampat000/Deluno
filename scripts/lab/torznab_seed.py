"""Real Torznab indexer + HTTP webseed host for a live Deluno acquisition test.

Nothing here is a mock of Deluno's dependencies: it serves genuine .torrent
files (correct bencode, correct SHA1 piece hashes) whose data is fetched by a
real qBittorrent over BEP-19 webseeds. qBittorrent does an actual transfer and
an actual hash check; if the bytes were wrong the torrent would never complete.

The media is Big Buck Bunny (Blender Foundation, CC-BY), which is genuinely
redistributable. The TV releases reuse the same video bytes under episode
filenames -- this exercises Deluno's parse/import/rename path, not video
content, and no claim is made that the bytes are the episode.

Each release is a multi-file torrent: the video plus a .nfo, so the import has
leftover files to clean up rather than a single tidy file.
"""
import hashlib
import http.server
import io
import os
import re
import socketserver
import sys
import threading
import urllib.parse

sys.stdout.reconfigure(encoding="utf-8")

PORT = int(os.environ.get("TORZNAB_PORT", "9117"))
# Bind address and advertised address are separate: the VM under test reaches this
# over the LAN, so the webseed URLs must carry an address it can actually resolve.
BIND = os.environ.get("TORZNAB_BIND", "127.0.0.1")
ADVERTISE = os.environ.get("TORZNAB_ADVERTISE", BIND)
HOST = ADVERTISE
BASE = f"http://{ADVERTISE}:{PORT}"
SOURCE = r"C:\Deluno\e2e\data\bbb.mp4"
# A secondary rig can write its torrent metadata beside the main lab rather
# than racing the long-lived listener for the same files.
OUT = os.environ.get("TORZNAB_OUT", r"C:\Deluno\e2e\torrents")
PIECE_LEN = 262144  # 256 KiB

os.makedirs(OUT, exist_ok=True)

# (release name, torznab category, advertised size in bytes or None)
#
# Advertised size is what an indexer publishes and what Deluno's size rules
# check before it downloads anything; the file actually served is always the
# same ~59 MB clip. Sintel carries three deliberately different advertisements
# so one search exercises all three fixes at once:
#
#   720p  -> advertised 0.06 GB, under every configured floor  -> size reject
#   2160p -> advertised 20 GB, plausible, but outside "Standard Movies"
#            (WEB 720p / WEB 1080p / Bluray 1080p)             -> allowed reject
#   1080p -> advertised 8 GB, plausible and permitted          -> should win
GB = 1024 ** 3
RELEASES = [
    ("Big.Buck.Bunny.2008.1080p.WEB-DL.x264-DELUNO", "2040", 8 * GB),
    ("Big.Buck.Bunny.2008.720p.WEB-DL.x264-DELUNO", "2040", 4 * GB),
    ("Big.Buck.Bunny.2008.2160p.WEB-DL.x265-DELUNO", "2045", 20 * GB),
    ("Sintel.2010.1080p.WEB-DL.x264-DELUNO", "2040", 8 * GB),
    ("Sintel.2010.720p.WEB-DL.x264-DELUNO", "2040", None),
    ("Sintel.2010.2160p.WEB-DL.x265-DELUNO", "2045", 20 * GB),
    ("Breaking.Bad.S01E01.1080p.WEB-DL.x264-DELUNO", "5040", 3 * GB),
    ("Breaking.Bad.S01E02.1080p.WEB-DL.x264-DELUNO", "5040", 3 * GB),
]

# A one-off season-pack fixture can be enabled without disturbing the long-lived
# indexer rig used by the ordinary movie and episode searches.  The release is
# still a genuine multi-file torrent: each requested episode is a separately
# hashed video entry backed by the local CC-BY source.  Keeping this opt-in
# means a second listener can exercise a whole-season replacement while the
# normal listener continues serving its stable catalogue.
SEASON_PACK_RELEASE = os.environ.get("DELUNO_E2E_SEASON_PACK_RELEASE", "").strip()
SEASON_PACK_SEASON = int(os.environ.get("DELUNO_E2E_SEASON_PACK_SEASON", "1"))


def season_pack_episode_numbers():
    raw = os.environ.get("DELUNO_E2E_SEASON_PACK_EPISODES", "1,2,3,4,5")
    numbers = []
    for token in raw.split(","):
        token = token.strip()
        if not token:
            continue
        try:
            episode = int(token)
        except ValueError as exc:
            raise ValueError("DELUNO_E2E_SEASON_PACK_EPISODES must be comma-separated integers") from exc
        if episode < 0 or episode > 999:
            raise ValueError("DELUNO_E2E_SEASON_PACK_EPISODES must contain episode numbers from 0 through 999")
        numbers.append(episode)
    if not numbers:
        raise ValueError("DELUNO_E2E_SEASON_PACK_EPISODES must name at least one episode")
    return tuple(sorted(set(numbers)))


SEASON_PACK_EPISODES = season_pack_episode_numbers() if SEASON_PACK_RELEASE else ()
if SEASON_PACK_RELEASE:
    # The synthetic file is small, but the feed advertises a credible 4K
    # Blu-ray size so Deluno's real release-policy filters evaluate the same
    # candidate shape as a production indexer would publish.
    RELEASES.append((SEASON_PACK_RELEASE, "5040", 16 * GB))


# ---------------------------------------------------------------- bencode ---
def bencode(value):
    if isinstance(value, int):
        return b"i" + str(value).encode() + b"e"
    if isinstance(value, bytes):
        return str(len(value)).encode() + b":" + value
    if isinstance(value, str):
        return bencode(value.encode("utf-8"))
    if isinstance(value, list):
        return b"l" + b"".join(bencode(v) for v in value) + b"e"
    if isinstance(value, dict):
        out = b"d"
        for k in sorted(value, key=lambda x: x.encode("utf-8") if isinstance(x, str) else x):
            out += bencode(k) + bencode(value[k])
        return out + b"e"
    raise TypeError(type(value))


# ------------------------------------------------------------ file layout ---
def nfo_bytes(release):
    return (
        f"{release}\r\n"
        "Source: Big Buck Bunny (c) Blender Foundation, CC-BY 3.0\r\n"
        "Served locally for a Deluno end-to-end acquisition test.\r\n"
    ).encode("utf-8")


# Deluno's import preview emitted "<release>.mkv" for an .mp4 source and then
# silently imported nothing, so the container is a variable in this rig: .mkv
# isolates whether the extension mismatch is what breaks the import.
VIDEO_EXT = os.environ.get("DELUNO_E2E_EXT", ".mkv")


def layout(release):
    """Files in the torrent, in order: [(relative path parts, size, reader)]."""
    video_size = os.path.getsize(SOURCE)
    nfo = nfo_bytes(release)
    video_names = [f"{release}{VIDEO_EXT}"]
    if release == SEASON_PACK_RELEASE:
        marker = f"S{SEASON_PACK_SEASON:02d}"
        marker_pattern = re.compile(rf"(?<![A-Za-z0-9]){re.escape(marker)}(?!E\\d)", re.IGNORECASE)
        video_names = []
        for episode in SEASON_PACK_EPISODES:
            stem, replacements = marker_pattern.subn(f"{marker}E{episode:02d}", release, count=1)
            if replacements == 0:
                stem = f"{release}.{marker}E{episode:02d}"
            video_names.append(f"{stem}{VIDEO_EXT}")
    return [
        *[([name], video_size, lambda: open(SOURCE, "rb")) for name in video_names],
        ([f"{release}.nfo"], len(nfo), lambda n=nfo: io.BytesIO(n)),
    ]


def build_torrent(release):
    """Write <release>.torrent with real piece hashes and a webseed."""
    path = os.path.join(OUT, f"{release}.torrent")
    files = layout(release)

    pieces = bytearray()
    buf = b""
    for _parts, _size, opener in files:
        with opener() as fh:
            while True:
                chunk = fh.read(PIECE_LEN - len(buf))
                if not chunk:
                    break
                buf += chunk
                if len(buf) == PIECE_LEN:
                    pieces += hashlib.sha1(buf).digest()
                    buf = b""
    if buf:
        pieces += hashlib.sha1(buf).digest()

    info = {
        "name": release,
        "piece length": PIECE_LEN,
        "pieces": bytes(pieces),
        "files": [{"length": size, "path": parts} for parts, size, _ in files],
    }
    meta = {
        "info": info,
        # BEP-19. qBittorrent appends "<name>/<path>" to this base, which is
        # exactly what /data/ below serves.
        "url-list": f"{BASE}/data/",
        "comment": "Deluno end-to-end acquisition test (Big Buck Bunny, CC-BY)",
        "created by": "deluno-e2e",
    }
    with open(path, "wb") as fh:
        fh.write(bencode(meta))

    infohash = hashlib.sha1(bencode(info)).hexdigest()
    total = sum(size for _p, size, _o in files)
    return path, infohash, total


TORRENTS = {}
for name, cat, advertised in RELEASES:
    p, ih, total = build_torrent(name)
    TORRENTS[name] = {
        "path": p,
        "infohash": ih,
        "size": total,                       # real bytes on the wire
        "advertised": advertised or total,   # what the feed publishes
        "cat": cat,
    }
    print(f"built {name}  infohash={ih}  real={total:,}  advertised={advertised or total:,}", flush=True)

CAPS = """<?xml version="1.0" encoding="UTF-8"?>
<caps>
  <server title="Deluno E2E Torznab"/>
  <limits max="100" default="50"/>
  <searching>
    <search available="yes" supportedParams="q"/>
    <tv-search available="yes" supportedParams="q,season,ep"/>
    <movie-search available="yes" supportedParams="q"/>
  </searching>
  <categories>
    <category id="2000" name="Movies"><subcat id="2040" name="Movies/HD"/><subcat id="2045" name="Movies/UHD"/></category>
    <category id="5000" name="TV"><subcat id="5040" name="TV/HD"/></category>
  </categories>
</caps>"""


# Deluno sends queries like "Breaking Bad Season 01" and "Breaking Bad 2008".
# A real indexer matches those loosely; requiring every token literally made this
# rig return nothing and look like a Deluno failure when it was ours.
STOPWORDS = {"season", "complete", "series", "the", "and"}


def matches(release, query, season, ep):
    # The isolated season-pack listener is intentionally a one-candidate
    # fixture for a whole-season query.  Leaving the ordinary E01/E02 releases
    # in its response made the real decision pipeline select an episode
    # release first, then truthfully hold because that single-file candidate
    # could not improve every installed episode.  The long-lived default
    # listener keeps its ordinary catalogue; this applies only when the
    # opt-in pack release is configured on the secondary listener.
    if SEASON_PACK_RELEASE and season and not ep and release != SEASON_PACK_RELEASE:
        return False

    hay = release.lower().replace(".", " ")
    low = release.lower()
    if season and ep:
        if f"s{int(season):02d}e{int(ep):02d}" not in low:
            return False
    elif season:
        if f"s{int(season):02d}" not in low:
            return False

    tokens = []
    for raw in re.split(r"[\s.:]+", (query or "").lower()):
        t = raw.strip()
        if len(t) < 2 or t in STOPWORDS:
            continue
        if re.fullmatch(r"(19|20)\d{2}", t):      # a year the release need not carry
            continue
        if re.fullmatch(r"\d{1,2}", t) and season:  # the season number, already filtered
            continue
        tokens.append(t)
    return all(t in hay for t in tokens)


def feed(query, season, ep, cat):
    items = []
    for name, meta in TORRENTS.items():
        if not matches(name, query, season, ep):
            continue
        if cat and not any(meta["cat"].startswith(c[:2]) for c in cat.split(",") if c.strip()):
            pass  # category filtering kept permissive; Deluno re-filters anyway
        url = f"{BASE}/dl/{urllib.parse.quote(name)}.torrent"
        items.append(f"""    <item>
      <title>{name}</title>
      <guid>{url}</guid>
      <link>{url}</link>
      <comments>{BASE}/details/{name}</comments>
      <pubDate>Mon, 18 Aug 2026 09:00:00 +0000</pubDate>
      <size>{meta['advertised']}</size>
      <category>{meta['cat']}</category>
      <enclosure url="{url}" length="{meta['advertised']}" type="application/x-bittorrent"/>
      <torznab:attr name="category" value="{meta['cat']}"/>
      <torznab:attr name="seeders" value="42"/>
      <torznab:attr name="peers" value="45"/>
      <torznab:attr name="leechers" value="3"/>
      <torznab:attr name="infohash" value="{meta['infohash']}"/>
      <torznab:attr name="downloadvolumefactor" value="1"/>
      <torznab:attr name="uploadvolumefactor" value="1"/>
    </item>""")
    return f"""<?xml version="1.0" encoding="UTF-8"?>
<rss version="2.0" xmlns:atom="http://www.w3.org/2005/Atom" xmlns:torznab="http://torznab.com/schemas/2015/feed">
  <channel>
    <atom:link href="{BASE}/api" rel="self" type="application/rss+xml"/>
    <title>Deluno E2E Torznab</title>
    <description>Local Torznab feed serving real torrents for an end-to-end test</description>
    <link>{BASE}/</link>
{chr(10).join(items)}
  </channel>
</rss>"""


class Handler(http.server.BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"

    def log_message(self, fmt, *args):
        print(f"[torznab] {self.address_string()} {fmt % args}", flush=True)

    def _send(self, body, ctype, code=200, extra=None, head_only=False):
        if isinstance(body, str):
            body = body.encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", ctype)
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Accept-Ranges", "bytes")
        for k, v in (extra or {}).items():
            self.send_header(k, v)
        self.end_headers()
        if not head_only:
            self.wfile.write(body)

    def do_HEAD(self):
        self._guarded(True)

    def do_GET(self):
        self._guarded(False)

    def _guarded(self, head_only):
        """One bad request must not take the listener down with it."""
        try:
            self._route(head_only)
        except (ConnectionAbortedError, ConnectionResetError, BrokenPipeError):
            pass
        except Exception as exc:  # noqa: BLE001 - a test rig, log and keep serving
            print(f"[torznab] ERROR {self.path}: {exc!r}", flush=True)
            try:
                self._send(f"error: {exc}", "text/plain", 500, head_only=head_only)
            except Exception:
                pass

    def _route(self, head_only=False):
        parsed = urllib.parse.urlparse(self.path)
        qs = urllib.parse.parse_qs(parsed.query)
        path = urllib.parse.unquote(parsed.path)

        if path in ("/api", "/api/"):
            t = (qs.get("t") or ["search"])[0]
            if t == "caps":
                return self._send(CAPS, "application/xml", head_only=head_only)
            q = (qs.get("q") or [""])[0]
            season = (qs.get("season") or [None])[0]
            ep = (qs.get("ep") or [None])[0]
            cat = (qs.get("cat") or [""])[0]
            return self._send(feed(q, season, ep, cat), "application/rss+xml", head_only=head_only)

        if path in ("/list/movies.txt", "/list/movies"):
            body = "\n".join(["Big Buck Bunny (2008)", "Sintel (2010)", "Tears of Steel (2012)", "Elephants Dream (2006)", ""])
            return self._send(body, "text/plain", head_only=head_only)

        if path in ("/list/tv.txt", "/list/tv"):
            body = "\n".join(["Breaking Bad (2008)", ""])
            return self._send(body, "text/plain", head_only=head_only)

        if path.startswith("/dl/") and path.endswith(".torrent"):
            name = path[len("/dl/"):-len(".torrent")]
            meta = TORRENTS.get(name)
            if not meta:
                return self._send("not found", "text/plain", 404, head_only=head_only)
            with open(meta["path"], "rb") as fh:
                data = fh.read()
            return self._send(
                data, "application/x-bittorrent", head_only=head_only,
                extra={"Content-Disposition": f'attachment; filename="{name}.torrent"'})

        # Webseed. qBittorrent asks for /data/<torrent name>/<file path>.
        if path.startswith("/data/"):
            rel = path[len("/data/"):]
            parts = [p for p in rel.split("/") if p]
            if len(parts) != 2 or parts[0] not in TORRENTS:
                return self._send("not found", "text/plain", 404, head_only=head_only)
            release, filename = parts
            valid_files = {entry[0][0] for entry in layout(release)}
            if filename not in valid_files:
                return self._send("not found", "text/plain", 404, head_only=head_only)
            if filename == f"{release}.nfo":
                return self._serve_bytes(nfo_bytes(release), head_only)
            if filename.endswith(VIDEO_EXT):
                return self._serve_file(SOURCE, head_only)
            return self._send("not found", "text/plain", 404, head_only=head_only)

        return self._send("deluno e2e torznab", "text/plain", head_only=head_only)

    # Webseeds require byte-range support.
    def _range(self, total):
        header = self.headers.get("Range")
        if not header:
            return 0, total - 1, False
        m = re.match(r"bytes=(\d*)-(\d*)", header.strip())
        if not m:
            return 0, total - 1, False
        start = int(m.group(1)) if m.group(1) else 0
        end = int(m.group(2)) if m.group(2) else total - 1
        return start, min(end, total - 1), True

    def _serve_bytes(self, data, head_only):
        start, end, partial = self._range(len(data))
        chunk = data[start:end + 1]
        self.send_response(206 if partial else 200)
        self.send_header("Content-Type", "application/octet-stream")
        self.send_header("Content-Length", str(len(chunk)))
        self.send_header("Accept-Ranges", "bytes")
        if partial:
            self.send_header("Content-Range", f"bytes {start}-{end}/{len(data)}")
        self.end_headers()
        if not head_only:
            self.wfile.write(chunk)

    def _serve_file(self, path, head_only):
        total = os.path.getsize(path)
        start, end, partial = self._range(total)
        length = end - start + 1
        self.send_response(206 if partial else 200)
        self.send_header("Content-Type", "video/mp4")
        self.send_header("Content-Length", str(length))
        self.send_header("Accept-Ranges", "bytes")
        if partial:
            self.send_header("Content-Range", f"bytes {start}-{end}/{total}")
        self.end_headers()
        if head_only:
            return
        remaining = length
        with open(path, "rb") as fh:
            fh.seek(start)
            while remaining > 0:
                chunk = fh.read(min(65536, remaining))
                if not chunk:
                    break
                self.wfile.write(chunk)
                remaining -= len(chunk)


class Server(socketserver.ThreadingTCPServer):
    allow_reuse_address = True
    daemon_threads = True


if __name__ == "__main__":
    with Server((BIND, PORT), Handler) as httpd:
        print(f"torznab listening on {BASE}/api", flush=True)
        httpd.serve_forever()
