#!/usr/bin/env python3
"""Validate the public Jellyfin repository manifest."""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path
from urllib.parse import urlparse


def main() -> None:
    path = Path(sys.argv[1] if len(sys.argv) > 1 else "manifest.json")
    document = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(document, list) or len(document) != 1:
        raise SystemExit("manifest must contain exactly one plugin")

    plugin = document[0]
    required(plugin, "guid", "name", "description", "owner", "overview", "versions")
    if plugin["guid"] != "8b43a8c3-42ed-4fdf-8fb7-41d853b85ef4":
        raise SystemExit("unexpected plugin guid")
    if not isinstance(plugin["versions"], list) or not plugin["versions"]:
        raise SystemExit("manifest must contain at least one version")

    seen_versions: set[str] = set()
    for version in plugin["versions"]:
        required(
            version,
            "version",
            "changelog",
            "targetAbi",
            "sourceUrl",
            "checksum",
            "timestamp",
        )
        if version["version"] in seen_versions:
            raise SystemExit(f"duplicate version: {version['version']}")
        seen_versions.add(version["version"])
        if not re.fullmatch(r"[0-9a-f]{32}", version["checksum"]):
            raise SystemExit(f"invalid MD5 checksum for {version['version']}")
        parsed_url = urlparse(version["sourceUrl"])
        if parsed_url.scheme != "https" or parsed_url.netloc != "github.com":
            raise SystemExit(f"invalid source URL for {version['version']}")

    print(
        f"Validated {plugin['name']} manifest with "
        f"{len(plugin['versions'])} version(s)"
    )


def required(value: dict[str, object], *names: str) -> None:
    missing = [name for name in names if not value.get(name)]
    if missing:
        raise SystemExit(f"missing required fields: {', '.join(missing)}")


if __name__ == "__main__":
    main()
