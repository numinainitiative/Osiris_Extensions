# Upstream provenance

Steam Library is an Osiris-maintained fork of the Steam integration in Josef
Nemec's `PlayniteExtensions` repository.

- Upstream repository: https://github.com/JosefNemec/PlayniteExtensions
- Baseline commit: `e222cfb072808cd546b8d683a6f02e6a4b62c03a`
- Baseline release: Steam Library `2.40`
- Playnite source dependency: `39e7ff05696d9f3f5561e4e62f4aa21cbb4cc2df`
- Playnite Backend source dependency: `cacc207a213e10c86c9c7469b25865f557a5a19b`

The Steam Library and linked Playnite source are MIT licensed. The linked
Playnite Backend model is distributed under EUPL-1.2-or-later. The applicable
license texts are retained in `LICENSE` and `LICENSES/`.

The upstream directory layout is intentionally preserved so the original
project's linked compile paths remain reproducible. Only the source files used
by this extension are vendored; downloaded NuGet packages and build outputs are
ignored.
