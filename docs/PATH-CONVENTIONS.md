# Portable paths

Run `pwsh -NoProfile -File scripts/dev.ps1 path-check` before committing.

- Repository-relative paths: at most **180 characters**, including separators.
- Current Windows absolute paths: at most **259 characters**.
- New directory components: at most 48 characters; filenames: at most 64.
- Use lowercase ASCII letters, digits, dots, hyphens, and underscores; no spaces.
- Put the stable asset ID in the directory hierarchy once. Use short leaf
  names (`model.glb`, `weapon.glb`, `front.png`) and compact revisions (`v01`).
- Provider UUIDs, full model names, hashes, detailed timestamps, and prose
  belong in metadata, never filenames. Do not abbreviate authoritative IDs.

Generated assets use `art/generated/<asset-id>/<run-id>/`. An asset-scoped run
is `prod-<provider>-<model-token>-<yyyymmdd>-<nn>` or `bake-...`; the model token
is at most 12 characters and metadata records the exact provider model.
Untouched exports stay in ignored `raw/`. Review media stays ignored under
`artifacts/reviews/<asset-id>/<run-id>/`, using short view/purpose names.
The stable join is asset ID plus run ID; a provider task ID is separate.

The checker covers tracked and unignored paths. Existing exceptions in
`scripts/path-length-exceptions.txt` are historical, not examples. New exceptions
require explicit owner approval. Long-path settings or a short checkout root
do not waive the budget. When a rename is needed, update manifests, links,
scripts, Godot references, and LFS tracking together. Do not mass-rename
historical provenance solely for style.
