# Scoped edition mirror compatibility releases

These releases add optional page-owner resolution to verified public extension
packages. The editable MIT bridge source is in this directory. The reproducible
IL transformation source is `tools/EditionMirrorExtensions` in the sibling
Osiris_Launcher repository (introduced in commit 52200d5, with release bridge
support committed for Beta 0.0.57). `releases.json` pins each published baseline,
version, URL and SHA-256. Baselines retain all original license notices, including
Game Gallery's MIT upstream attribution. No deployed extension is a build input.

Run `build/New-EditionMirrorPackages.ps1` from the Extensions repository. It
verifies baseline hashes, builds the bridge and guarded adapter, patches only
named page/settings/storage boundaries, verifies them, updates the manifest,
preserves original licenses and produces private-data-free packages and checksums.
Existing output is immutable and must not be overwritten.

On Osiris Beta 0.0.57, mirrored pages resolve to their selected source edition.
On older hosts, missing optional bridge methods preserve independent behaviour.
No native launch/start/stop, session ledger or database identity is rewritten.
Screenshots use the shared page's destination while game lifecycle tracking
retains the physical edition ID. Stats aliases only page enablement.

The paused Screenshots Gallery capture-session changes and Steam Library source
changes in the workspace are excluded from these releases. Screenshots 0.1.3
is derived from public 0.1.1, reserving the unrelated local 0.1.2 work. Future
full source builds should incorporate these page boundaries before superseding
these compatibility packages.
