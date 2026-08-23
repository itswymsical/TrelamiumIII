# FontBaker

Bakes a TrueType font into the atlas + metric pair that `TrelamiumIII.Core.Text.BakedFont`
rebuilds into a ReLogic `DynamicSpriteFont` at load.

## Why a bake step exists

Terraria's fonts are `.xnb` files compiled by ReLogic's content pipeline. tModLoader ships the
*reader* for that format but not the pipeline, so a font `.xnb` cannot be authored offline. The
font is assembled in memory instead, via `DynamicSpriteFont.SetPages`.

`SetPages` is private and `FontPage` is internal in the shipped ReLogic assembly. Both are
reachable only because `build/TrelamiumIII.Publicizer.props` publicizes ReLogic and the build
emits `IgnoresAccessChecksTo("ReLogic")`. **Removing ReLogic from the publicize list breaks
font loading at compile time.**

## Re-baking

From the repository root:

```bash
dotnet run --project tools/FontBaker -- --out src/TrelamiumIII/Common/Fonts --size 22 src/TrelamiumIII/Common/Fonts/Andika-Regular.ttf src/TrelamiumIII/Common/Fonts/Andika-Italic.ttf src/TrelamiumIII/Common/Fonts/Andika-Bold.ttf src/TrelamiumIII/Common/Fonts/Andika-BoldItalic.ttf src/TrelamiumIII/Common/Fonts/Paprika-Regular.ttf
```

Each face produces `<name>.dsf` (metrics) and `<name>_0.png` (atlas). The `.ttf` sources are
bake input only — `build.txt` keeps them out of the `.tmod`.

## Faces

| Face | Glyphs | Role |
| --- | --- | --- |
| Andika Regular / Italic / Bold / Bold Italic | 606 | Body copy. Wide Latin coverage. |
| Paprika Regular | 353 | Display only — titles and chapter heads. |

Both are SIL Open Font License 1.1 but under different copyright holders, so their licenses ship
separately as `OFL-Andika.txt` and `OFL-Paprika.txt`. Do not merge them into one `OFL.txt`.

The baker reports how many requested codepoints a face did not map, which is the quickest read on
whether a face is safe for arbitrary player-facing text. Paprika leaves 292 unmapped and should be
kept to strings you control.

Run with `--help` for the full option list.

## Line spacing

The baker ignores the font's own line metrics and measures the line box from the ink it actually
rasterised, then shifts every glyph so nothing sits above the top of the box.

This matters because a box taken from the font tables is wrong in both directions: Andika's
stacked diacritics reach 8px above the nominal line top at size 22, and its low-line punctuation
falls 5px below it. The first would clip, the second would collide with the line beneath.

The consequence is that rare glyphs set the leading for ordinary prose. At size 22 the default
range set yields a 35px box, driven by `Ǻ` (U+01FA) and `‗` (U+2017). Two ways to tighten it:

- Narrow `--ranges` to drop the blocks you do not need. Latin Extended-B (`180-24F`) is where the
  stacked diacritics live.
- Pass `--line-spacing` to override. Glyphs are never clipped by this — `InternalDraw` does not
  clip — so a shorter box only means exotic glyphs may touch the neighbouring line. The baker
  warns when the override is shorter than the measured ink.

## Notes

- Atlases are written with **straight** alpha. ReLogic's `PngReader` premultiplies on load, so
  premultiplying here would darken every glyph edge twice.
- `DynamicSpriteFont` has no pair-kerning table, so BMFont-style kerning pairs cannot be
  represented. Only per-glyph ABC bearings survive, which is why the baker measures with
  `KerningMode.None`.
- Glyphs are keyed on `char`, so codepoints above the BMP are skipped.
- ImageSharp is pinned to the 2.x line; 3.x reworked the drawing API.
