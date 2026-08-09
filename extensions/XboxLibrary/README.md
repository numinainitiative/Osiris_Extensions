# Xbox Library

- Identity: `XboxLibrary_7e4fbb5e-2ae3-48d4-8ba0-6b30e7a4e287`
- Installed version: `1.0`
- Status: public Osiris extension; upstream provenance and MIT license recorded
  in `UPSTREAM.md` and `LICENSE`.

## Osiris changes

- Present the native Microsoft OAuth browser as the canonical Osiris Middle
  Window before the dialog is shown. This prevents the inherited compact
  Playnite window from flashing and ensures the final browser is centered over
  Osiris from its first visible frame.
- Preserve the original browser session, cookie clearing, OAuth callback,
  encrypted token storage, successful-login close, and import behavior.

## Build

Run `build.ps1`. The build references the Development Osiris SDK and Xbox
`Windows.winmd`, and writes output beneath the ignored `source/bin` directory.
The Windows metadata is a compile-time SDK reference supplied by Windows and is
not copied into the extension package.

Run `package.ps1` to rebuild and create `XboxLibrary_1.0.pext` plus its SHA-256
manifest beneath the ignored `artifacts/XboxLibrary/1.0` directory.
