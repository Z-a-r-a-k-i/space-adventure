# Blender processing history — station wall utility candidate 01

Status: final deterministic derivative passed; earlier repair trials retained as evidence

The raw Tripo GLB was never edited. All trials used Blender 5.2.0 LTS in a
factory-startup background scene and verified raw SHA-256
`7D1B87029212C9DA8757DABBA7643B7811808F124FFEC7E80C4A7E546F969059`
before import.

| Derived GLB SHA-256 | Result | Named finding | Automated cleanup seconds |
|---|---|---|---:|
| `518B40F459F13F5ED622EE85361389FF6F25FFD1CA117EDFB1189803D35C926A` | Reject | UV-split cut loop did not close; rear cap absent | 72.337 |
| `5EB7EF9AEB8787624F953A5D23908EAC8CA531F4567CEFE27F5F4163DE5806DA` | Reject | Cap present; 8 boundary and 13 non-manifold diagnostic edges remained | 31.483 |
| `25C555865CD6790BA2F2457887F2A86F4CFFEB9159E9801F1A6841E082A96E22` | Reject | Local repair reduced the defect to 4 boundary edges | 25.442 |
| `EA4A668EB65688E4984590AE1B7AA13C0C63F4170CD31464CE884C06A335A0EC` | Reject | Tiny quad fill exported with one over-subscribed diagnostic edge | 24.288 |
| `875CB754C1212F1C88F951F44F1130A4312B5ED80204B6BC354E282FD641B095` | Reject | Blender gates passed, but Godot exposed published Z `[0,+0.22]`; camera on `-Z` saw the flat rear | 27.698 |
| `6AA48E8EBC5FE4B6D41F646A576A17D3F2691517BC7644E773236298383150D6` | Reject | Signed depth corrected, but a one-axis reflection mirrored the approved front layout | 35.923 |
| `750CF70EEB54962B53D5F2E0FA1EA6E50F42AD391E971E184AF5AA40BD131FB8` | Reject | Euler rotation was ignored by the imported object's quaternion rotation mode; detailed face remained on the wrong side | 26.994 |
| `104B03AAF161192610D9F8F1089B092C1D1EE6140F1333C13FAE3235F1E6BAF2` | Pass | Mesh-data Z rotation preserves identity and publishes Z `[-0.22,0]` with the detailed face toward `-Z` | 27.853 |

Cumulative automated cleanup time: 272.018 seconds. The end-to-end diagnosis,
script iteration, Blender review, and visual inspection remained below the
30-minute active-work cap.

The failed derivative review directories are retained under
`artifacts/reviews/prop.station.wall_utility.v1/<derived-hash>/blender/`.
They are processing evidence, not additional Tripo candidates and consumed no
provider credits.
