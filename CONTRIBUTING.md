# Contributing

Bug fixes, server compatibility improvements, new tests, and privacy-preserving
catalog integration changes are welcome.

For a new title or extra mapping, contribute to the
[Jelevision Extras Catalog](https://github.com/shanemcguffin/jelevision-extras-catalog)
instead of hard-coding it in this plugin. The bundled rules exist only as an
offline fallback.

Before opening a pull request:

```sh
dotnet test Jellyfin.Plugin.JelevisionExtras.Tests/Jellyfin.Plugin.JelevisionExtras.Tests.csproj \
  --configuration Release
python3 scripts/validate_manifest.py manifest.json
```

Do not include media, screenshots, local paths, server identifiers, API keys,
tokens, or personal information in tests or issues.

By contributing, you agree to license your contribution under GPL-3.0.
