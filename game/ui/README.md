# Tactical HUD assets

`icons/*.svg`, `icons/ship/*.svg` and the controller glyphs in `icons/pad/*.svg`
are original project-authored interface symbols; glyph letters are drawn as
strokes, and `InputPrompts` tints the white shapes (A/B/X/Y in their Xbox
colours). `fonts/chakra_petch_*.ttf` is Chakra Petch (SIL Open Font License 1.1,
`fonts/ofl.txt`, from github.com/google/fonts); SemiBold is the project default
font and Bold is used for headings and numbers.
`portraits/*.png` are rendered from the accepted Vanguard and Protector rigs
and materials by `tools/blender/render_crew_portraits.py`; their source identity
and rights remain in the character production records under `art/source/`.
Regenerate portraits with Blender 5.2 using that script. Layout and colors live
in `TacticalUi`, `CrewCard`, `ActionTile`, and `GameHost.Hud`, not in an asset pack.
