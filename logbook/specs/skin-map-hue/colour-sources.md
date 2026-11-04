# Colour sources of an animal in a solo still (EVOSIM_THEATRE_SKIN_MAP=2, bold tissue look, film dials)

Traced by reading `TheatrePalette.Paint` / `Set` / `RefreshNecks` and the `Fragment` of `TheatreBody.shader` (unity/Assets/Theatre, worktree wt-skin-map at 4fde65ea). Nothing was compiled or rendered. "Turned today" means it takes `partTurn`, the body's palette turn (`SkinMainTurnOf`, hue alone).

| # | Source | Set at | Turned today? | Area |
|---|---|---|---|---|
| 1 | Guild colour (the tissue's `Colour`) | `Paint` TheatrePalette.cs:1183 `ColourOf`, then 1211 `Turned(guild, partTurn, hueOnly)` | yes | everything below derives from it |
| 2 | Body (`_BaseColor`), tissue-look path | `Set` :1423/:1425 `Muted(guild, look.Saturation * reserve * skinSaturation, look.Lightness)` | yes (hue from the turned guild); but saturation is only a cap and lightness is the look's (0.26 to 0.66 in the bold set), so pale tissues stay pastel | most of the body |
| 3 | Rim colour (`_RimColor`) | `Set` :1417 `Tissue(look.RimColour, turn, grey, wideHue)` | yes, hue only | silhouette edge band, glow |
| 4 | Light through the tissue (`_TransTint`) and the inner glow tint | `Set` :1418 `Tissue(look.TransTint, ...)`; shader :7967-7989 `SssTint` (a power of the tint, hue-keeping); glow :8478-8484 | yes | backlit parts, centre glow, small to moderate |
| 5 | Pattern (maze) pigment | shader `PatternSkin` :5479-5480 `lerp(luma, body, 1.35) * 0.42`, from the body colour at that pixel; strength `PatternStrengthOf` | yes, through the body (a darker, more saturated copy of it); no constant colour | the pattern's cover, up to about 90% opacity |
| 6 | Absorb deepening (ABSORB=0.6) | shader :7525-7543 `EvoUptakeDeepen`, HSL, hue kept | yes, hue kept | absorptive parts |
| 7 | Rim band warm band (`RimBandWarm` = 1.35, 0.95, 0.45) | shader :5000, `RimBandMapped` :5101 turned by `_SkinMap.y` = the map's own rim hue only | NO: orange whatever the palette hue | consumer box parts only, the outer band of the broad faces (about 18% of the width), on top of the pattern |
| 8 | Rim band pale line | shader :5099, near white with 15% of the body's hue | follows the body (15% hue), near white otherwise | a thin line, faded under 3 to 6 px |
| 9 | Iridescence (IRIDESCENCE=1) | shader :5291 `IridescentTint`, thin-film interference colour on the sheen only | NO, and no relation to the palette hue (a function of thickness noise and view) | the sheen highlight only, strength 0.2 to 1.8 times the tissue's |
| 10 | Sheen, caustic, key and fill light, ambient, fog | shader :8110-8190, :8500-8525, :8579 | white key (SHEEN_CAP=2 rolls it off), water colours; not body colours | highlight; the water's cold key (0.86, 0.94, 1.0) and teal ambient pale every body |
| 11 | Per-cell shade and warmth | shader :8566-8568 `(1 + 0.05 w, 1, 1 - 0.07 w)` | no, but +-5% | whole body, negligible |
| 12 | Countershade (COUNTERSHADE=1) | shader :4935 `CountershadeMapped` | a grey scalar on the body (0.8 to 1.1), no hue | whole body |
| 13 | Necks (knuckles, bridges) | `RefreshNecks` :3359-3363 `neckTurn = SkinMainTurnOf(skin)`, `Turned(...)`, then the same `Set` | yes | joints |
| 14 | A part the alga look recolours (a leaf on an animal) | `AlgaDressOf`, `alga.Recoloured` | keeps the producers' narrow turn by design | excluded from the dials |
| 15 | Colour ramp tip, chromatophore pigment (`ChromaPigment` 0.5, 0.28, 0.24) | shader :4971, :5149 | NO (fixed warm casts) | not in this look (RAMP, CHROMATOPHORES off), left alone |
| 16 | Skin2 margin and pigment pair, mouth, nose pit, thalli, uptake motes, mouth marks | shader SKIN2/MOUTH2/NOSE_PIT/THALLI blocks; TheatreMouthMarks.cs:204 (yellow) | n/a | not in this look, or not in a solo still |

## What the trace says

- Almost every animal colour in this look already takes the turn (rows 1 to 6, 8, 13). The pattern pigment is computed in the shader from the body's own colour, so it has no colour of its own to turn.
- The one constant colour that does not, in this look, is the rim band's warm yellow-orange on a consumer's box parts (row 7). It is also the only place a genuinely orange "stripe" survives a violet or blue turn. The turn-all dial therefore has one source to act on here.
- The pastel look has another cause, which a hue turn cannot touch: `Muted` fixes the lightness (bold set: bone 0.64, nerve 0.66, absorber 0.64) and caps the HSL saturation at `look.Saturation * StarvingSaturation..1 * skinSaturation`. A pale tissue at HSL lightness 0.64 and saturation 0.3 to 0.45 is a pastel in every hue; the hue turn moves the pastel round the wheel, and the key light and water then pale it further. The saturation gain is `Lerp(0.55, 1.45, base saturation)`, so a low base saturation multiplies the cap by about 0.55. The cap binds on bone (guild HSL saturation about 0.83), nerve (about 0.78) and the bold absorber (about 0.94), so the floor dial raises the saturation of those. It does nothing for a tissue whose own saturation is under the cap.
- Not verified by a render: whether the "cream maze" in the earlier stills is bone-coloured parts (cream, structural tissue, `Bone` (0.96, 0.80, 0.58) in the bold set) or a pattern; the pattern pigment itself is always darker than its body (x0.42), never lighter.
