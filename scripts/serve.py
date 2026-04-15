"""
Tiny static server for testing WebGL builds locally.

Unity WebGL builds can't run from file:// — browsers refuse to fetch the
binary chunks that way. This serves them over HTTP with the right
Content-Encoding headers so gzip-compressed builds work without needing
the Decompression Fallback shim.

Usage:
    python scripts/serve.py                  # serves Build/WebGL on :8000
    python scripts/serve.py docs             # serves docs/ on :8000
    python scripts/serve.py docs 8080        # serves docs/ on :8080

Then open http://localhost:8000 in a browser.
"""

import http.server
import os
import sys
from functools import partial


class WebGLHandler(http.server.SimpleHTTPRequestHandler):
    """Serves Unity WebGL builds with the right Content-Encoding headers."""

    extensions_map = {
        ".js": "application/javascript",
        ".wasm": "application/wasm",
        ".data": "application/octet-stream",
        ".symbols.json": "application/octet-stream",
        ".html": "text/html",
        ".css": "text/css",
        ".png": "image/png",
        ".jpg": "image/jpeg",
        ".json": "application/json",
        "": "application/octet-stream",
    }

    def end_headers(self):
        path = self.translate_path(self.path)
        if path.endswith(".gz"):
            self.send_header("Content-Encoding", "gzip")
            # Strip the .gz so the browser content-types it correctly
            inner = path[:-3]
            for ext, mime in self.extensions_map.items():
                if inner.endswith(ext):
                    self.send_header("Content-Type", mime)
                    break
        elif path.endswith(".br"):
            self.send_header("Content-Encoding", "br")
            inner = path[:-3]
            for ext, mime in self.extensions_map.items():
                if inner.endswith(ext):
                    self.send_header("Content-Type", mime)
                    break
        # Allow SharedArrayBuffer, useful for any future threading
        self.send_header("Cross-Origin-Opener-Policy", "same-origin")
        self.send_header("Cross-Origin-Embedder-Policy", "require-corp")
        super().end_headers()


def main():
    folder = sys.argv[1] if len(sys.argv) > 1 else "Build/WebGL"
    port = int(sys.argv[2]) if len(sys.argv) > 2 else 8000

    if not os.path.isdir(folder):
        print(f"Folder not found: {folder}")
        print("Did you build the project from Unity (MunCraft → Build WebGL)?")
        sys.exit(1)

    # Use SimpleHTTPRequestHandler's `directory` arg instead of os.chdir so the
    # server doesn't hold a Windows file lock on the build folder. (Without this
    # Unity can't delete the folder for the next build while the server runs.)
    handler = partial(WebGLHandler, directory=os.path.abspath(folder))
    server = http.server.ThreadingHTTPServer(("127.0.0.1", port), handler)
    print(f"Serving {folder} at http://localhost:{port}")
    print("Ctrl+C to stop")
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\nStopped")


if __name__ == "__main__":
    main()
