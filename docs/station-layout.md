# Station layout

The station uses a connected sequence of asymmetric combat spaces, with short
passages and staggered doorways between them. Each space has a recognizable
industrial purpose and a different movement choice. The opening receives the
same treatment as the four encounters after Medic recruitment.

[`tools/station-layout.json`](../tools/station-layout.json) is the shared offline
authoring input for room bounds, floor openings, doors, and the four new
encounters' entry, crew, and enemy placements. It is not a runtime quest or map
framework. Generated scene navigation and placements remain authoritative at
the Godot adapter boundary; [Product](PRODUCT.md) owns progression and kits.

| Fight | Spatial recipe |
| --- | --- |
| Solo — damaged security deck | An offset enemy and a damaged floor opening introduce movement around a loop while retaining the solo teaching encounter. |
| Duo — transit switchyard | A central service trench divides the approach and gives the recruited Protector a distinct position beside Vanguard. |
| Service — coolant loop | A large coolant opening creates two usable routes; the crew can maintain healing range while choosing which side to advance. |
| Security — scanner checkpoint | Offset scanner pits interrupt the straight approach and separate enemy positions across the room. |
| Dock — freight transfer concourse | A central freight-elevator void creates broad flanking lanes and makes Healing Field placement depend on the crew's chosen route. |
| Launch — servicing bay | Two service pits leave a broad central combat space and an offset exit toward the cutter's boarding apron. |

These are open deck cuts with machinery below walking height. They constrain
movement; firearms can shoot across them. Low rims and floor markings identify
the edges without suggesting ballistic cover. Preserve readable characters,
health bars, targeting previews, and wall cutaways at the supported camera
distances. Three-person encounters frame a wider overview at entry and retry;
subsequent camera movement and zoom remain player-controlled. The evacuation airlock remains the final station exit.

## References

The following primary references informed composition, not copied maps or assets:

- [Aarklash: Legacy publisher gallery](https://store.steampowered.com/app/222640/Aarklash_Legacy/): the inspected woodland and cathedral screenshots use clear combat space, strong landmarks, and room for distinct front and back lines.
- [Obsidian's Pillars II environment breakdown](https://eternity.obsidian.net/eternity/news/pillars-of-eternity-ii-deadfire-update-30---from-blockout-to-completion-the-environments-of-pillars-ii): design the area before decoration, align navigation and collision with the art, and review it at gameplay camera scale.
- [Official VTC headquarters blockout](http://d1079ywfijtdjs.cloudfront.net/deadfire/media/updates/0030/vtc-headquarters-blockout.jpg): differently proportioned spaces and offset openings establish a hierarchy around a recognizable central landmark.

Our transfer from these references is alternating tighter transitions with
wider combat pockets and distinct spatial decisions. The station retains its
own industrial art language and existing combat rules.

## Rebuild and verify

From the repository root, intentionally regenerate both visible structure kits
and the matching invisible scene wrappers after changing the manifest:

```powershell
python tools/layout_station.py
$stationBlender = 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe'
& $stationBlender --background --factory-startup --python-exit-code 1 --python tools/blender/build_station_environment_v2.py -- --asset structure --replace
& $stationBlender --background --factory-startup --python-exit-code 1 --python tools/blender/build_station_escape.py -- --replace
& $stationBlender --background --factory-startup --python-exit-code 1 --python tools/blender/build_station_environment_v2.py -- --asset service-surround --replace
```

The scene tool updates navigation, floor picking surfaces, links, doors, and
encounter markers. Blender authors the visible deck cuts and validates the exact
exported GLBs by fresh import. The pit and combined-deck checks run on that
fresh import before publication, so a failed check leaves the accepted files in place. The service surround rebuild keeps the opening
and transit foundations clear beneath the new recesses. These operations are offline and use retained
sources; `--replace` deliberately overwrites the selected publications. See the
[Blender tools](../tools/blender/README.md) for publication details.

Follow [Testing](testing.md) for build, rule, CLI, Godot import/headless, and live
review commands. Check both routes around each opening, door traversal, spawn
clearance, group travel, healing previews, every encounter's retry, and the full
boarding route. Inspect the opening as well as all four escape encounters at
supported resolutions and camera distances. Automated navigation checks and
agent graphical review do not replace owner visual, handling, and listening
acceptance or the five consecutive blocker-free owner-operated full-route runs.
