# SpaceAdventure Blender production tools

The scripts in this directory default to the repository containing
`tools/blender/`. Set `SPACE_ADVENTURE_REPOSITORY` or use an explicit
`--repository` argument where supported to target another worktree.

The terminal scripts were originally executed on 2026-07-23 with a
machine-local absolute repository path. Their exact executed hashes remain in
the adjacent publication records. Before branch handoff on 2026-07-24,
repository-root resolution was generalized so a future run cannot silently
write into another worktree. The v3 publisher was also narrowed to the
retained run-local raw export and v2 editable source so the cleaned review
branch does not require duplicate transient v1/v2 GLBs. Under ADR 0017 the
`raw/` payload is an ignored workstation cache entry; hydrate it at the
manifested path and verify its size and SHA-256 before rerunning a raw-dependent
script. Existing final artifacts were not regenerated.

| Script | Executed revision | Current path-safe revision |
|---|---|---|
| `clean_service_terminal_tripo_candidate01.py` | `D2E70DCD2E320208EB99BCD9EE055514E20972E431DBCE19E51D8896F1B604A0` | `E2349A7296697BD36C716A09F880589B2086BD97D68305A29AE6FC0F0DB6A12D` |
| `publish_service_terminal_tripo_candidate01_v2.py` | `79768604DA0253D4CAA8D42CE00923BB0882DC80EC282F779F43BFF0559A4FCC` | `71F8976F2E64667ED3DCC8236D40BE11AF554B84E5552FD756E2C51CAEBBDEDB` |
| `publish_service_terminal_tripo_candidate01_v3.py` | `37CA0F455E9CE5C871E41A124C6097FA9FFA3050D07DF0C7AE0B5865F73B380B` | `2D2636FBFDA8F520012E9C7E3B97A2920C8D76EDDC5427849AAC679F7017AE1B` |
| `render_service_terminal_review_v1.py` | `79CF0CB92D84C83AC5DFDADFC4350BFF11FCAE0101C909D758C850DC20726100` | `86FF8C63310839544F78E1C89C7CAFF60AE8B92A12CC13BEA021252389E455A3` |

`process_wall_utility_candidate.py` was path-parameterized before execution.
`build_service_terminal_v1.py` keeps the editable baseline under `art/source/`
and defaults its GLB to ignored local review staging under `artifacts/`.
`SPACE_ADVENTURE_BASELINE_GLB` may override that staging path explicitly.
All six Python files parse successfully in Blender 5.2.0 LTS.
