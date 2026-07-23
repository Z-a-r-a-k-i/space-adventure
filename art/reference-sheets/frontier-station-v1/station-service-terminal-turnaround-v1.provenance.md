# Provenance — station service terminal turnaround v1

Status: human-approved provider-input source  
Generated: 2026-07-23  
Approved: 2026-07-23  
Asset brief: `art/briefs/station-service-terminal-v1.md`

## Artifact

| Field | Value |
|---|---|
| File | `station-service-terminal-turnaround-v1.png` |
| Dimensions | 1254 × 1254 pixels |
| Pixel format | RGB PNG |
| Bytes | 1,560,147 |
| SHA-256 | `B433BAE19A05A506257692B0E9E5C13235295CB3306ACFB56A2166EB15C85503` |
| Tool | Codex built-in `image_gen` |
| Exact underlying model/version | Not exposed by the built-in tool |
| Seed / job ID | Not exposed |

This is an approved reference source, not a production texture or mesh. No 3D
provider upload has occurred. Approval authorizes lossless cropping and input
packaging; it does not authorize account spending or an external upload.
Commercial-use and provider-upload terms must be reviewed separately before
the sheet is sent to Tripo or Meshy.

## Source reference

| Field | Value |
|---|---|
| File | `art/concepts/frontier-station-v1/station-route-key-art.png` |
| Role | Visual and style reference; not an edit target |
| Bytes | 1,670,016 |
| SHA-256 | `A540D65AFB6144030C1177478A0F2B4653146F76EF4EB9A4DAF65FBC7E7294B1` |

## Generation history

### Pass 1 — initial turnaround

Result: rejected because the upper purple display made the silhouette read too
strongly as an arcade cabinet.

Output SHA-256:
`43952D00CBF56B7787E0C00FB107C93EB7F3E0F99901A3BD3AA36976511453A1`

Prompt:

```text
Use case: stylized-concept
Asset type: provider-neutral 3D game-asset turnaround / model sheet
Input images: Image 1 is a visual and style reference only, especially the purple service terminal near the center-right; do not edit or reproduce the whole station scene.
Primary request: design one identical stylized low-poly frontier-station service terminal and show that exact same object in four consistent views for image-to-3D reference.
Subject: a freestanding industrial sci-fi terminal constrained to the proportions 0.80 m wide × 1.30 m high × 0.42 m deep. Chunky stable pedestal, broad grounded base, tapered armored middle, and a raised angled trapezoidal display hood on the front. One dominant violet screen; dark navy armored housing; restrained neutral metal edge caps; at most one thin subordinate cyan utility strip. The rear has one simple broad maintenance panel. Thick supports and bevels, clear front/back distinction, one dominant mass plus only two to five medium panel forms.
Style/medium: polished stylized low-poly 3D game model render matching Image 1's authored dark industrial science-fiction shape language, but presented as a clean neutral production model sheet rather than cinematic concept art.
Composition/framing: clean 2×2 grid with generous equal margins. Top-left exact front orthographic view, top-right exact right-side orthographic view, bottom-left exact rear orthographic view, bottom-right front-right three-quarter view. Same object identity, dimensions, feature placement, materials, scale, vertical alignment, and ground line in every panel. The orthographic views must have no perspective distortion.
Scene/backdrop: perfectly plain light neutral gray-blue background, uniform in all four panels, with subtle panel separation only.
Lighting/mood: soft even diffuse studio illumination intended to reveal form; minimal ambient occlusion; no dramatic lighting, no cast shadows, no bloom, no reflections, no environment.
Materials/textures: de-lit flat PBR-like material colors; matte dark navy metal shell, restrained worn neutral metal, violet screen emission area without visible glow spill, tiny cyan accent only.
Constraints: no text, no labels, no dimensions, no logos, no glyphs, no watermark. No green destination color, no hostile red, no keyboard, no cables, no antennae, no hologram, no floating pieces, no thin fragile geometry, no random greeble noise, no heavy damage or grime. Do not show a room, character, floor scenery, props, UI, blueprint lines, or extra design variants. This must be one coherent manufacturable object repeated consistently in four views.
```

### Pass 2 — targeted correction

Result: retained as `station-service-terminal-turnaround-v1.png`. The second
pass removes the upper purple marquee, retains a single angled violet screen,
and simplifies the top into a protective armored hood.

Inputs:

- Pass 1 output, as the edit target.
- `station-route-key-art.png`, as a visual/style reference only.

Prompt:

```text
Use case: precise-object-edit
Asset type: provider-neutral 3D game-asset turnaround / model sheet
Input images: Image 1 is the edit target. Image 2 is a visual/style reference only, especially its purple station service terminal.
Primary request: revise only the service-terminal design in Image 1 so it no longer resembles an arcade cabinet. Keep the exact 2×2 turnaround layout, four viewpoints, neutral background, uniform scale, ground line, lighting, and model-sheet presentation.
Required design change: remove the wide upper purple marquee/display entirely. Replace that upper section with a compact protective armored hood or shallow cap, visually integrated with the side rails. Keep exactly one dominant violet screen: the existing main screen on the angled/sloped front console. Preserve one tiny cyan status strip beneath it. Make the overall silhouette read as a practical frontier-station maintenance and diagnostic terminal: chunky grounded pedestal, broad stable base, tapered armored middle, one angled workstation screen protected by a modest hood, and one simple lower maintenance hatch. Slightly lower and simplify the top silhouette. The rear should retain one broad access panel, and the right side should remain shallow and manufacturable.
Dimensions/proportions: retain the intended proportion relationship 0.80 m wide × 1.30 m high × 0.42 m deep; do not make it deeper or taller.
Identity invariants: the exact same revised object must appear in all four panels, with identical feature placement, dimensions, materials, bevels, and panel seams. Top-left front orthographic, top-right right-side orthographic, bottom-left rear orthographic, bottom-right front-right three-quarter.
Materials: preserve matte dark navy armor, restrained neutral metal edge caps, exactly one violet screen without glow spill, and one very small cyan indicator.
Constraints: change only the object design as described. No text, labels, logos, glyphs, watermark, keyboard, controls, cables, antennae, hologram, floating parts, green, hostile red, extra screens, dramatic glow, cast shadows, room scenery, characters, props, blueprint markings, or extra variants. Keep soft even diffuse lighting and the plain light gray-blue background.
```

## Human review decision

Decision: `accept` as the provider-input source.

The approved sheet may be cropped without regeneration into front, right,
rear, and front-right three-quarter inputs. Any semantic redesign, repaint, or
new view requires another review. Provider upload and credit use remain
separate actions.
