# Art pipeline and visual review

## Goal

Produce readable, consistent, and replaceable low-poly game art through a versioned pipeline. AI-generated material is a candidate, never automatically a production asset. Consistency comes from shared proportions, modular kits, rigs, palettes, materials, faction rules, camera constraints, and review—not merely from similar prompts.

The POC begins with Godot primitives and simple authored materials. Blender is opened only for an active asset task.

## Current baseline

- Blender 5.2 LTS is the editable-source and automation baseline.
- The official Blender MCP is installed for interactive agent control and inspection.
- GLB is the published runtime model format.
- Godot 4.7.1 is the final import and tactical-readability check.

MCP actions are useful for exploration and review, but repeatable pipeline operations eventually live in versioned Blender scripts or explicit profiles. A successful interactive session is not sufficient provenance.

## Art direction before generation

Before producing a volume of assets, establish a small art bible containing:

- Tactical camera distance, field of view, lighting assumptions, and minimum readable feature size.
- Character height bands, head-to-body proportions, hand and weapon exaggeration, and silhouette rules.
- Faction shape language and forbidden overlaps.
- A controlled palette and reusable material library.
- Surface detail density, bevel language, emissive rules, and damage conventions.
- Modular environment dimensions, grid, door, cover, stair, and corridor standards.
- One approved reference character and one approved environment module set.

Generation prompts derive from asset briefs and the art bible. They are not the source of truth.

## Asset lifecycle

1. Write an asset brief with a stable asset ID.
2. Generate or model raw candidates and record provenance.
3. Review candidates and select an editable source.
4. Normalize scale, orientation, origin, naming, materials, sockets, and rig.
5. Run mechanical validation.
6. Publish a GLB into a staging area.
7. Render standardized Blender review views.
8. Import the exact published GLB into the Godot asset gallery.
9. Render standardized in-engine review views.
10. Perform structured agent review and, where required, human review.
11. Approve the asset or return it for a named revision.

Direct `.blend` imports are not the controlled runtime publication path.

## Asset brief

Every brief states:

- Asset ID, category, intended gameplay role, dimensions, pivot, forward direction, and sockets.
- Silhouette, proportions, faction shape language, palette, and material IDs.
- Triangle, mesh, material, texture, bone, and animation budgets.
- Rig profile and required clips or key poses.
- Required readability from the tactical gameplay camera.
- Modular connections and collision expectations where relevant.
- References, allowed variations, and forbidden traits.
- Generator, model, version, seed, source files, editing history, and licensing notes.

## Repository shape when real assets begin

```text
art/bible/
art/briefs/
art/kits/
art/materials/
art/rigs/
art/source/<asset-id>/
art/generated/<asset-id>/<run-id>/
game/Assets/Published/
tools/blender/
artifacts/reviews/<asset-id>/<asset-hash>/
```

Large accepted binary sources use Git LFS if repository size demonstrates the need. One worker owns one asset ID at a time because binary sources merge poorly.

## Mechanical validation

Validation should check:

- Units, scale, bounding box, transforms, forward axis, origin, and ground contact.
- Object, mesh, material, texture, socket, and animation naming.
- Mesh, triangle, material, texture, bone, and influence budgets.
- Missing textures, invalid paths, UV availability, and unexpected embedded data.
- Skeleton hierarchy, bone count, weights, deformation coverage, and root behavior.
- Required clips, clip duration, looping, root motion, and key-pose availability.
- GLB structure and Godot import diagnostics.

Record exact tool versions and SHA-256 hashes for the source, brief, normalization profile, published GLB, and render profile.

## Standard multi-angle review

The future `asset-review` command frames normalized bounds and captures:

- Front, back, left, and right.
- Front-left and front-right three-quarter views.
- Top and underside.
- Tactical gameplay camera.
- Wireframe or topology view.
- Scale reference.
- Required animation key poses.

It outputs labeled PNG files, a contact sheet, and a JSON review manifest. This command is planned and does not yet exist.

Use a neutral studio, fixed framing, fixed lights, fixed resolution, fixed color management, consistent padding, and an explicit render-profile version. Render the published GLB in both Blender and Godot: Blender reveals source and topology problems, while Godot reveals import, material, skeleton, animation, scale, and tactical-readability problems.

The agent inspects the contact sheet first, then opens any questionable view at full resolution. Findings name the asset, hash, render profile, view, severity, and violated brief or art-bible rule.

## Tool roles

- Blender and Blender MCP: source editing, normalization, rigging, animation inspection, and controlled renders.
- Godot and Godot MCP: import diagnostics, asset-gallery renders, live tactical camera, animation playback, and viewport capture.
- Khronos glTF Validator: fail malformed or structurally suspicious GLB files before Godot import.
- glTF Transform: inspect, report, optimize, simplify, or transform published GLB files when a concrete publication step needs it.
- ImageMagick: label views, assemble contact sheets, and produce visual diffs.
- FFmpeg: encode turntables and animation previews.

The last four tools are useful at the matching pipeline milestone but are not prerequisites for C# gameplay bootstrap or primitive greyboxing.

## Review decision

Structured review covers silhouette, proportions, material coherence, topology, rig deformation, animation readability, scale, tactical readability, blockers, notes, and a decision of `accept`, `revise`, or `reject`.

Automated captures prove visibility and comparability, not artistic quality. A human approves prominent production characters, creatures, vehicles, hero props, and environments. Proxy and test assets may use a lighter approval level.

Avoid strict PNG hashes across GPUs. Prefer mechanical checks, configuration hashes, generous perceptual thresholds for gross regressions, and structured visual judgment.

## Early AI use

Initially favor AI for concept exploration, palette and material candidates, modular variations, proxy meshes, texture drafts, and reference boards. Do not assume current text-to-3D output has usable topology, rigs, animation, consistency, scale, or licensing provenance.

Keep the gameplay-facing asset contract stable so improved tools can replace early art one asset ID at a time without changing gameplay code, level logic, or saved state.
