# Portable path and filename conventions

## Goal

Keep the repository usable from ordinary Windows checkout locations without
depending on Git long-path support or unusually short worktree roots. A short
local root such as `C:\sa` is convenient but is not a portability solution:
Godot, Blender, archive tools, and other applications may still use normal
Windows path handling.

## Enforced budget

- Every new tracked or unignored repository-relative path is at most 180
  characters, including separators.
- The current absolute path is also at most 259 characters.
- New directory components should be at most 48 characters.
- New filenames should be at most 64 characters, including the extension.
- Use lowercase ASCII letters, digits, dots, hyphens, and underscores. Do not
  use spaces.

Run:

```powershell
pwsh -NoProfile -File scripts/dev.ps1 path-check
```

before committing files. The command checks tracked and unignored files. Three
pre-policy paths are listed in `scripts/path-length-exceptions.txt`; they are
historical warnings, not examples. Do not add a new exception without explicit
project-owner approval. Rename a new violation instead.

## General naming rules

1. Put stable identity in the directory hierarchy once. Do not repeat a parent
   asset ID, scene name, or subsystem name in every descendant filename.
2. Keep descriptive prose in metadata or manifests, not paths.
3. Store provider task UUIDs, hashes, timestamps with time-of-day, full model
   names, prompts, and operation descriptions in metadata. Do not embed them in
   filenames.
4. Use short, established view names such as `front`, `rear`, `left`, `right`,
   `3q`, `top`, `under`, `t14m`, `t20m`, `wire`, and `grip`.
5. Use compact numeric revisions such as `v01` and `v02`. Record the purpose of
   each revision in its manifest or processing history instead of naming a
   directory `v6-dialogue-listen-proof`.
6. When a path approaches the budget, shorten the leaf filename, view label,
   revision, or run token. Do not abbreviate the authoritative asset ID in
   briefs, manifests, or gameplay contracts.

## Generated-art convention

The asset ID is already present in:

```text
art/generated/<asset-id>/
```

The child run ID is asset-scoped and must not repeat it:

```text
prod-<provider>-<model-token>-<yyyymmdd>-<nn>
bake-<provider>-<model-token>-<yyyymmdd>-<nn>
```

`<model-token>` is a compact alias of at most 12 characters. The manifest
records the exact live provider model name separately. Examples:

```text
art/generated/character.npc.station_survivor.v1/
  prod-tripo-v31bq-20260724-01/
    input/front.png
    derived/v01/model.glb
    metadata.md
    raw-export.manifest.json
```

Disposable visual review artifacts live outside the tracked asset run:

```text
artifacts/reviews/<asset-id>/<run-id>/<tool>/<purpose>.<ext>
```

They remain ignored and must not contain provider task UUIDs. The provider task
ID and detailed review purpose belong in `metadata.md` or the textual review
record. Normalized and published assets use short role names such as
`model.glb`, `rigged.glb`, or `weapon.glb` inside the already-specific run
directory.

The canonical review join remains the pair of `asset_id` and asset-scoped
`run_id`. A provider `task_id` is separate and never substitutes for either.

## Renames and historical paths

Historical provenance paths do not need mass renaming solely for style. When a
long path is already being changed, rename it if this can be done without
losing provenance, then update manifests, Markdown links, scripts, Godot
references, and Git LFS tracking as applicable.

New pull requests must not introduce another over-budget path. Enabling
`core.longpaths`, choosing a short checkout root, or demonstrating that one
application can open the file does not waive this rule.
