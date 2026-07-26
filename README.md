# Jelevision Extras Enricher for Jellyfin

[![CI](https://github.com/shanemcguffin/jelevision-jellyfin-extras/actions/workflows/ci.yml/badge.svg)](https://github.com/shanemcguffin/jelevision-jellyfin-extras/actions/workflows/ci.yml)

A Jellyfin 10.11 plugin that turns anonymous physical-media files such as
`Movie_t05.mkv` into correctly named trailers, deleted scenes, featurettes, and
other bonus features.

It combines full-disc metadata from [TheDiscDb](https://thediscdb.com/) with
an evidence-backed Jelevision catalog feed. A small
[public seed and versioned format](catalog-format/README.md) remain open for
interoperability; the larger production catalog is maintained separately.
Verified copyright, hardware, and legal disclaimer reels can be removed from
Jellyfin's Extras UI without deleting the underlying media. Verified
byte-identical duplicate files can be suppressed the same way.

## Install from Jellyfin

In Jellyfin Dashboard:

1. Open **Plugins → Repositories**.
2. Add a repository named `Jelevision`.
3. Use this repository URL:

   ```text
   https://raw.githubusercontent.com/shanemcguffin/jelevision-jellyfin-extras/main/manifest.json
   ```

4. Open the plugin catalog, install **Jelevision Extras Enricher**, and restart
   Jellyfin.
5. Run **Preview local extras enrichment** from Scheduled Tasks before the
   first Apply.

The current release targets Jellyfin Server `10.11.11`.

## What it changes

The plugin updates only Jellyfin metadata:

- a verified trailer receives `ExtraType.Trailer`, allowing clients to show a
  dedicated Trailer button;
- named supplements receive their verified title and type;
- confirmed technical/legal reels have `ExtraType` cleared so they no longer
  appear as entertainment extras; and
- verified byte-identical duplicates have `ExtraType` cleared while
  stream-layout differences remain visible for review; and
- every applied change records its source and an undo snapshot.

It never renames, moves, replaces, uploads, or deletes a media file.

## Privacy

The configured Jelevision catalog is downloaded with a single HTTP `GET`, then
all matching runs inside Jellyfin. Movie IDs, filenames, paths, media,
fingerprints, and match results are not sent to Jelevision.

The plugin can operate from its previously released public seed when offline.
The default remote feed is the one-record public format sample. To use a local,
self-hosted, or licensed feed, set:

```text
JELEVISION_EXTRAS_CATALOG_URL=http://catalog:8787/v1/catalog
JELEVISION_EXTRAS_CATALOG_TOKEN=replace-with-feed-token
```

The token is optional and is sent only as an HTTP Bearer token to that URL.
Equivalent `CommunityCatalogUrl` and `CommunityCatalogAccessToken` properties
may be set in the Jellyfin plugin configuration XML.

Contribution to the public dataset is a separate, explicit opt-in process.

## Matching safeguards

Automatic community-catalog matches require:

1. a strong TMDb or IMDb parent identifier;
2. the observed local `_tNN` title ordinal;
3. an exact runtime within the record's narrow tolerance; and
4. an unambiguous verified result.

TheDiscDb matches require the complete eligible runtime set to select one
unique disc. Unmatched or ambiguous items are reported and left unchanged.

## Scheduled tasks

- **Preview local extras enrichment** writes a report without changing
  metadata.
- **Enrich local extras** applies verified matches and runs daily at 04:00.
- **Undo local extras enrichment** restores the original metadata when the item
  has not been manually edited afterward.

Reports and undo state live in Jellyfin's plugin configuration directory.

## Build and test

Install the .NET 9 SDK, then run:

```sh
dotnet test Jellyfin.Plugin.JelevisionExtras.Tests/Jellyfin.Plugin.JelevisionExtras.Tests.csproj
```

Release archives are deterministic and can be reproduced with:

```sh
dotnet build Jellyfin.Plugin.JelevisionExtras/Jellyfin.Plugin.JelevisionExtras.csproj \
  --configuration Release
python3 scripts/package.py \
  --dll Jellyfin.Plugin.JelevisionExtras/bin/Release/net9.0/Jellyfin.Plugin.JelevisionExtras.dll \
  --meta Jellyfin.Plugin.JelevisionExtras/manifest.json \
  --output artifacts/Jelevision.Extras.Enricher_0.3.2.0.zip
```

## Public/private boundary

The plugin source and distributed binary are licensed under
[GPL-3.0](LICENSE), matching Jellyfin's plugin-linking requirements. The
schema and sample under [`catalog-format`](catalog-format/README.md) are
MIT-licensed. The production catalog is not included in this repository, and
no production-feed license is granted by installing the plugin. See
[`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md).
