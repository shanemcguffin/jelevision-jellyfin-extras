# Jelevision catalog format

This directory is the public interoperability layer for Jelevision extras
metadata. It contains the version 1 JSON Schema and a deliberately small,
MIT-licensed sample feed.

The production catalog is maintained separately. A compatible feed may be
public, self-hosted, or protected with an HTTP Bearer token. The Jellyfin
plugin downloads the snapshot with one `GET` request and performs all matching
locally; it does not send library identifiers, filenames, paths, fingerprints,
or match results.

## Matching contract

A version 1 record identifies its parent with a TMDb or IMDb id and requires:

1. the observed local `_tNN` title ordinal;
2. an exact runtime within the stated tolerance;
3. an optional content fingerprint when the record supplies one; and
4. verified, unambiguous metadata.

Clients must leave an item unchanged when these signals conflict or when
equally strong records disagree.

## Feed configuration

The public sample is the plugin default:

```text
https://raw.githubusercontent.com/shanemcguffin/jelevision-jellyfin-extras/main/catalog-format/public-sample.json
```

Private feeds use the same document format. Configure
`CommunityCatalogUrl` and `CommunityCatalogAccessToken`, or use:

```text
JELEVISION_EXTRAS_CATALOG_URL=https://catalog.example/v1/catalog
JELEVISION_EXTRAS_CATALOG_TOKEN=replace-with-feed-token
```

The environment token takes precedence over the XML configuration value and
is sent only as an `Authorization: Bearer` header to the configured catalog
URL.

The schema and public sample in this directory are licensed under the
[MIT License](LICENSE). The plugin itself remains GPL-3.0. No license for a
separate production feed is implied.
