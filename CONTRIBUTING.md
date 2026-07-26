# Contributing

Bug fixes, server compatibility improvements, new tests, and privacy-preserving
catalog integration changes are welcome.

Catalog-format improvements and corrections to the previously released public
seed are welcome. New production-catalog records are maintained separately and
must not be added to this repository unless Jelevision has explicitly selected
them for the public seed.

Before opening a pull request:

```sh
dotnet test Jellyfin.Plugin.JelevisionExtras.Tests/Jellyfin.Plugin.JelevisionExtras.Tests.csproj \
  --configuration Release
python3 scripts/validate_manifest.py manifest.json
```

Do not include media, screenshots, local paths, server identifiers, API keys,
tokens, or personal information in tests or issues.

By contributing plugin code, you agree to license it under GPL-3.0. Changes
made specifically to `catalog-format` are contributed under the MIT license in
that directory.
