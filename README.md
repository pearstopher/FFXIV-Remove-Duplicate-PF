# Remove Duplicate PF

[![Build and test](https://github.com/pearstopher/FFXIV-Remove-Duplicate-PF/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/pearstopher/FFXIV-Remove-Duplicate-PF/actions/workflows/build.yml)

A Dalamud plugin for FINAL FANTASY XIV that collapses duplicate Party Finder listings into one, prefixed with how many duplicates were combined. Listings count as duplicates when their descriptions start with the same 30 characters (shorter descriptions must match in full). This is deliberate: ads that open with the same 30 characters are treated as the same boilerplate ad, so they're merged even if their endings differ (for example, the same venue ad posted with different times). See [the design decision](docs/party-finder-notes.md#design-decision-match-on-the-first-30-characters-42) for the reasoning.

Work in progress. Install instructions will follow (#15).

For building and testing locally, see [DEVELOPMENT.md](DEVELOPMENT.md).
