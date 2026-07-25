#!/usr/bin/env python3
"""Create a byte-reproducible Jellyfin plugin ZIP."""

from __future__ import annotations

import argparse
from pathlib import Path
from zipfile import ZIP_DEFLATED, ZipFile, ZipInfo


FIXED_TIMESTAMP = (2026, 1, 1, 0, 0, 0)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--dll", required=True, type=Path)
    parser.add_argument("--meta", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    arguments = parser.parse_args()

    for source in (arguments.dll, arguments.meta):
        if not source.is_file():
            parser.error(f"missing input: {source}")

    arguments.output.parent.mkdir(parents=True, exist_ok=True)
    with ZipFile(
        arguments.output,
        mode="w",
        compression=ZIP_DEFLATED,
        compresslevel=9,
    ) as archive:
        add_file(
            archive,
            arguments.dll,
            "Jellyfin.Plugin.JelevisionExtras.dll",
        )
        add_file(archive, arguments.meta, "meta.json")


def add_file(archive: ZipFile, source: Path, destination: str) -> None:
    information = ZipInfo(destination, date_time=FIXED_TIMESTAMP)
    information.compress_type = ZIP_DEFLATED
    information.external_attr = 0o100644 << 16
    information.create_system = 3
    archive.writestr(information, source.read_bytes(), compresslevel=9)


if __name__ == "__main__":
    main()
