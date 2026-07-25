# Jelevision Extras Enricher

A Jellyfin 10.11 plugin that identifies anonymously named local movie extras
using physical-disc title metadata from [TheDiscDb](https://thediscdb.com) and
the open [Jelevision Extras Catalog](https://github.com/shanemcguffin/jelevision-extras-catalog)
for discs missing from that catalog.

The plugin never downloads or replaces media. It updates Jellyfin's item name
and `ExtraType` only when the complete set of eligible local extras uniquely
matches one disc layout within a tight runtime tolerance, or when the parent
provider id, local title ordinal, and runtime all match a verified catalog
record.

Verified copyright-warning and hardware-disclaimer reels are declassified by
setting `ExtraType` to `null`. This leaves the media file untouched but removes
it from Jellyfin's trailer and special-feature endpoints. Silent video is never
hidden merely because it lacks an audio stream.

Version 0.3 downloads the compact public catalog with a normal HTTP `GET`, then
matches it entirely inside Jellyfin. Movie IDs, filenames, paths, fingerprints,
and results are not sent to Jelevision. A validated bundled snapshot is used
when the network or public catalog is unavailable.

The initial verified catalog includes coverage for the inspected editions of:

- *Stephen King's IT* (1990): hides its warning-screen title, including when
  the parent has only an IMDb id.
- *Dumb and Dumber* (1994): names the extracted supplements, classifies the
  theatrical trailer correctly, and hides its warning-screen title.
- *Obsession* (2026): names *Obsession Unleashed* and hides four
  language-specific technical/legal reels.

## Scheduled tasks

- **Preview local extras enrichment** writes a report without changing metadata.
- **Enrich local extras** applies confident matches and runs daily at 04:00.
- **Undo local extras enrichment** restores the pre-enrichment snapshot when an
  item has not been manually changed afterward.

Reports and undo state are stored in Jellyfin's plugin configuration directory.

## Catalog configuration

The default snapshot URL is:

```text
https://raw.githubusercontent.com/shanemcguffin/jelevision-extras-catalog/main/catalog/v1/catalog.json
```

Set `CommunityCatalogUrl` in the Jellyfin plugin configuration XML to use a
self-hosted snapshot, or set the container environment variable
`JELEVISION_EXTRAS_CATALOG_URL`. Set `EnableCommunityCatalog` to `false` for
bundled-snapshot-only operation.

## Data sources

TheDiscDb's source dataset is MIT-licensed:
<https://github.com/TheDiscDb/data>. Runtime queries use its public GraphQL
endpoint. An outage does not block curated rules; unmatched or ambiguous remote
metadata is reported and skipped without changing Jellyfin.

Verified catalog records and their provenance are public and versioned:
<https://github.com/shanemcguffin/jelevision-extras-catalog>. The bundled rules
in `Overrides/CuratedOverrideCatalog.cs` provide an offline safety net. Exact
rules are deliberately edition-specific rather than fuzzy title guesses.
