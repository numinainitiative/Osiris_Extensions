# Osiris Extensions repository guidance

This repository is the source of truth for extensions maintained or catalogued
by Numina Initiative for Osiris.

## Boundaries

- Store editable source, extension manifests, build scripts, catalog metadata,
  documentation, and required license notices in Git.
- Store compiled extension packages in GitHub Release assets, not Git history.
- Never commit an Osiris `Data` directory, `ExtensionsData`, credentials, API
  keys, login state, caches, logs, personal libraries, or personal settings.
- The personal G: installation and the Development installation contain deployed
  test copies only. Neither installation is an editable source directory.
- Do not publish or redistribute an extension until its origin and license have
  been verified.

## Testing

- Deploy development builds to
  `Programming/Development/Osiris/Data/Extensions` without clearing the rest of
  that persistent development profile.
- Test new-user installation and package behavior only under
  `Programming/Sandbox/CleanInstall`; use `Programming/Sandbox/UpgradeTest` for
  extension update and rollback tests.
- Preserve the recovery archive under `Development/ExtensionRecovery`; it is not
  a release source and must not be committed.

## Workspace handoff

- Read the workspace-root `PROJECT-STATE.md` before continuing extension work.
- Read the workspace-root `DESIGN-AGENT.md` before creating or modifying any
  extension settings page or modal. Steam Library is the current completed
  implementation baseline; reuse its shared host and canonical design contracts
  instead of creating extension-specific approximations.
