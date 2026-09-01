"""Small deterministic NNTP/NZB fixture for live integration tests.

Without ``--article`` it remains the lightweight control-plane fixture used by
the original E2E run.  With an article it serves one real yEnc payload and an
NZB over HTTP, which is enough to exercise SABnzbd's actual download, history,
and completed-folder behavior without pretending an external Usenet provider
was involved.
"""

from __future__ import annotations

import argparse
import binascii
import html
import http.server
import socketserver
import threading
import time
from pathlib import Path
from urllib.parse import urlparse


def yenc_lines(payload: bytes, line_length: int = 128) -> list[bytes]:
    lines: list[bytes] = []
    current = bytearray()
    for value in payload:
        transformed = (value + 42) % 256
        token = bytearray()
        if transformed in {0, 10, 13, 61}:
            token.append(61)
            transformed = (transformed + 64) % 256
        token.append(transformed)
        if current and len(current) + len(token) > line_length:
            lines.append(bytes(current))
            current.clear()
        current.extend(token)
    if current:
        lines.append(bytes(current))
    return lines


def nzb_document(filename: str, message_id: str, size: int) -> bytes:
    safe_name = html.escape(filename, quote=True)
    safe_id = html.escape(message_id.strip("<>"))
    return f'''<?xml version="1.0" encoding="UTF-8"?>
<nzb xmlns="http://www.newzbin.com/DTD/2003/nzb">
  <file poster="Deluno E2E" date="{int(time.time())}" subject="{safe_name}">
    <groups><group>alt.test</group></groups>
    <segments><segment bytes="{size}" number="1">{safe_id}</segment></segments>
  </file>
</nzb>
'''.encode("utf-8")


class NntpHandler(socketserver.StreamRequestHandler):
    server: "NntpServer"

    def write(self, value: str) -> None:
        self.wfile.write((value + "\r\n").encode("ascii"))
        self.wfile.flush()

    def handle(self) -> None:
        self.write("200 Deluno E2E NNTP fixture ready")
        authenticated = False
        user_seen = False
        while True:
            line = self.rfile.readline()
            if not line:
                return
            command = line.decode("ascii", errors="replace").strip()
            self.server.log(command)
            verb, _, argument = command.partition(" ")
            verb = verb.upper()
            if verb == "QUIT":
                self.write("205 closing connection")
                return
            if verb == "CAPABILITIES":
                self.write("101 capability list follows")
                for capability in ("VERSION 2", "READER", "AUTHINFO USER", "OVER"):
                    self.write(capability)
                self.write(".")
            elif verb == "MODE":
                self.write("200 reader mode")
            elif verb == "AUTHINFO" and argument.upper().startswith("USER"):
                user_seen = True
                self.write("381 password required")
            elif verb == "AUTHINFO" and argument.upper().startswith("PASS"):
                authenticated = user_seen
                self.write("281 authentication accepted" if authenticated else "481 authentication rejected")
            elif verb == "DATE":
                self.write("111 20260831000000")
            elif verb == "GROUP":
                count = 1 if self.server.article is not None else 0
                self.write(f"211 {count} {count} {count} alt.test")
            elif verb == "STAT":
                self.write(f"223 1 <{self.server.message_id}>" if self.server.has_article(argument) else "430 no such article")
            elif verb == "HEAD":
                if not self.server.has_article(argument):
                    self.write("430 no such article")
                else:
                    self.write(f"221 1 <{self.server.message_id}> headers follow")
                    self.server.write_headers(self)
                    self.write(".")
            elif verb in {"BODY", "ARTICLE"}:
                if not self.server.has_article(argument):
                    self.write("430 no such article")
                else:
                    self.write(f"{'222' if verb == 'BODY' else '220'} 1 <{self.server.message_id}> article follows")
                    if verb == "ARTICLE":
                        self.server.write_headers(self)
                        self.write("")
                    self.server.write_body(self)
                    self.write(".")
            elif verb in {"XOVER", "OVER"}:
                self.write("224 overview information follows")
                if self.server.article is not None:
                    self.write(f"1\t{self.server.filename}\tDeluno E2E\tMon, 31 Aug 2026 00:00:00 +0000\t<{self.server.message_id}>\t\t{len(self.server.article)}\t1")
                self.write(".")
            elif verb == "LIST":
                self.write("215 list follows")
                self.write("alt.test 1 1 y")
                self.write(".")
            elif verb in {"HELP", "CHECK"}:
                self.write("100 help text follows" if verb == "HELP" else "238 article wanted")
                if verb == "HELP":
                    self.write(".")
            else:
                self.write("200 command accepted")


class NntpServer(socketserver.ThreadingTCPServer):
    allow_reuse_address = True

    def __init__(self, address: tuple[str, int], log_path: Path, article_path: Path | None, message_id: str) -> None:
        self.log_path = log_path
        self.log_lock = threading.Lock()
        self.message_id = message_id.strip("<>")
        self.filename = article_path.name if article_path else "deluno-e2e.bin"
        self.article = article_path.read_bytes() if article_path else None
        self.article_crc = binascii.crc32(self.article or b"") & 0xFFFFFFFF
        self.encoded_lines = yenc_lines(self.article) if self.article is not None else []
        super().__init__(address, NntpHandler)

    def log(self, line: str) -> None:
        with self.log_lock:
            with self.log_path.open("a", encoding="utf-8") as handle:
                handle.write(line + "\n")

    def has_article(self, argument: str) -> bool:
        normalized = argument.strip().strip("<>")
        return self.article is not None and normalized in {"", "1", self.message_id}

    def write_headers(self, handler: NntpHandler) -> None:
        handler.write(f"From: Deluno E2E <fixture@deluno.invalid>")
        handler.write(f"Newsgroups: alt.test")
        handler.write(f"Subject: {self.filename}")
        handler.write(f"Message-ID: <{self.message_id}>")

    def write_body(self, handler: NntpHandler) -> None:
        assert self.article is not None
        handler.write(f"=ybegin line=128 size={len(self.article)} name={self.filename}")
        for line in self.encoded_lines:
            if line.startswith(b"."):
                line = b"." + line
            handler.wfile.write(line + b"\r\n")
        handler.write(f"=yend size={len(self.article)} crc32={self.article_crc:08x}")


class NzbHandler(http.server.BaseHTTPRequestHandler):
    server: "NzbServer"

    def do_GET(self) -> None:  # noqa: N802 - HTTP handler contract
        if urlparse(self.path).path != f"/{self.server.nzb_name}":
            self.send_error(404)
            return
        self.send_response(200)
        self.send_header("Content-Type", "application/x-nzb")
        self.send_header("Content-Length", str(len(self.server.document)))
        self.end_headers()
        self.wfile.write(self.server.document)

    def log_message(self, format: str, *args: object) -> None:
        self.server.nntp_server.log("HTTP " + (format % args))


class NzbServer(http.server.ThreadingHTTPServer):
    def __init__(self, address: tuple[str, int], nntp_server: NntpServer, nzb_name: str) -> None:
        self.nntp_server = nntp_server
        self.nzb_name = nzb_name
        self.document = nzb_document(nntp_server.filename, nntp_server.message_id, len(nntp_server.article or b""))
        super().__init__(address, NzbHandler)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--bind", default="0.0.0.0")
    parser.add_argument("--port", type=int, default=1119)
    parser.add_argument("--log", type=Path, required=True)
    parser.add_argument("--article", type=Path)
    parser.add_argument("--message-id", default="deluno-e2e@fixture")
    parser.add_argument("--http-port", type=int, default=0)
    parser.add_argument("--nzb-name", default="fixture.nzb")
    args = parser.parse_args()
    args.log.parent.mkdir(parents=True, exist_ok=True)
    with NntpServer((args.bind, args.port), args.log, args.article, args.message_id) as server:
        http_server = NzbServer((args.bind, args.http_port), server, args.nzb_name) if args.article and args.http_port else None
        http_thread = threading.Thread(target=http_server.serve_forever, daemon=True) if http_server else None
        if http_thread:
            http_thread.start()
        print(f"fake nntp listening on {args.bind}:{args.port}", flush=True)
        if http_server:
            print(f"fixture nzb listening on {args.bind}:{args.http_port}/{args.nzb_name}", flush=True)
        try:
            server.serve_forever()
        finally:
            if http_server:
                http_server.shutdown()
                http_server.server_close()


if __name__ == "__main__":
    main()
