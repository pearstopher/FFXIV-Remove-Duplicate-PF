"""Build repo.json, the custom plugin repository file players add to Dalamud's plugin installer.

Usage:
    python make_repo_json.py <manifest.json> <tag> <owner/repo> <output repo.json>

<manifest.json> is the RemoveDuplicatePF.json manifest produced by `dotnet build -c Release`
(DalamudPackager). This script adds the download links for the GitHub Release of <tag>
and the update time, and writes a one-entry repo.json list.
"""

import json
import sys
import time
from pathlib import Path

# Fields copied from the build's manifest into repo.json, in this order.
MANIFEST_FIELDS = [
    "Author",
    "Name",
    "InternalName",
    "AssemblyVersion",
    "Description",
    "Punchline",
    "ApplicableVersion",
    "RepoUrl",
    "IconUrl",
    "Tags",
    "DalamudApiLevel",
    "LoadRequiredState",
    "LoadSync",
    "CanUnloadAsync",
    "LoadPriority",
    "AcceptsFeedback",
]


def make_entry(manifest: dict, tag: str, repo: str, now: int) -> dict:
    missing = [f for f in ("InternalName", "AssemblyVersion", "DalamudApiLevel") if f not in manifest]
    if missing:
        raise ValueError(f"manifest is missing required fields: {', '.join(missing)}")

    download = f"https://github.com/{repo}/releases/download/{tag}/latest.zip"
    entry = {field: manifest[field] for field in MANIFEST_FIELDS if field in manifest}
    entry["DownloadLinkInstall"] = download
    entry["DownloadLinkUpdate"] = download
    entry["DownloadLinkTesting"] = download
    entry["LastUpdate"] = now
    return entry


def main(argv: list[str]) -> int:
    if len(argv) != 5:
        print(__doc__, file=sys.stderr)
        return 2

    manifest_path, tag, repo, output_path = argv[1:]
    manifest = json.loads(Path(manifest_path).read_text(encoding="utf-8-sig"))
    entry = make_entry(manifest, tag, repo, int(time.time()))

    Path(output_path).write_text(json.dumps([entry], indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"Wrote {output_path}: {entry['InternalName']} {entry['AssemblyVersion']} (API {entry['DalamudApiLevel']}) -> {entry['DownloadLinkInstall']}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
