# Exophase 0.2.2

This patch lets one Osiris game combine activity from multiple Exophase
editions, such as a Game of the Year release on PlayStation and a Complete
Edition on Steam.

- Link up to 32 synchronized Exophase editions to one Osiris game.
- Add and remove individual editions from the Exophase page in Game Edit.
- Combine play time from every linked edition by platform.
- Add activity from distinct editions on the same platform while keeping the
  native Osiris or integrated-library platform authoritative.
- Preserve manual platform rows and synchronized-time corrections while the
  linked-edition list changes.
- Migrate existing single-title matches automatically while retaining safe
  rollback compatibility.

Exophase does not publish a supported API contract for this profile data. The
extension continues to validate responses, retain the last usable snapshot on
failure, and never replace Osiris's native Time Played value.
