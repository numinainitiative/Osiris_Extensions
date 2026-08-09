# Xbox Library upstream provenance

- Upstream repository: `https://github.com/JosefNemec/PlayniteExtensions`
- Upstream component: `source/Libraries/XboxLibrary`
- Upstream license: MIT, copyright 2020 Josef Nemec
- Osiris baseline: the installed Xbox Library 1.0 assembly was decompiled with
  its matching PDB, then compared against the official upstream
  `XboxAccountClient.cs` implementation before migration.

The Osiris fork preserves the extension ID and native Xbox authentication,
token-storage, import, installation, launch, and uninstall behavior. The first
Osiris-specific source change wraps the native Microsoft OAuth browser in the
canonical Osiris Middle Window before it is shown.
