# Beat and Blade — Image and Model Prompts

Generate each image in a separate ChatGPT chat (with image generation enabled). Paste the prompt below **as is**.

Save the generated file with exactly the name in the table, in this folder:

```
Assets/ArcaneSaber/Resources/Art/
```

Unity handles the import settings itself. If the game finds the file it uses it; otherwise it keeps the current procedural look. So you do not need to make them all at once.

| File | What it is | Size |
| --- | --- | --- |
| `Background.png` | Distant scenery behind the tunnel | 1536×1024 (landscape) |
| `Floor.png` | Floor texture, tiled | 1024×1024 |
| `NoteFace.png` | Cube surface texture | 1024×1024 |
| `NoteArrow.png` | Arrow on laser cubes | 1024×1024 |
| `NoteRune.png` | Rune frame on hand cubes | 1024×1024 |
| `Logo.png` | Main menu logo (transparent background) | 1536×1024 |
| `SaberHilt.fbx` | Sword hilt 3D model | — |
| `Gauntlet.fbx` | Forearm armor 3D model | — |

**Important rule:** `Background`, `NoteArrow` and `NoteRune` are *added as light* in the game: black areas are invisible, bright areas glow. That is why the prompts say "pure black background". If you get a grey or white background instead of black, regenerate.

---

## 1. Background.png — distant scenery

```
Wide cinematic sci-fi backdrop for a neon rhythm game, seen from inside a long tunnel looking toward the horizon. A colossal distant megastructure of glowing magenta and cyan rings and towers on the horizon, vertical light pillars, faint geometric grid mountains, sparse stars. Everything is thin emissive light lines and soft glows on a PURE BLACK background (#000000), no fog haze filling the frame, no ground plane in the foreground, no text, no characters. The centre of the image stays darker so gameplay remains readable. Synthwave meets arcane magic: subtle glowing runes woven into the structures. Landscape 3:2, ultra clean, high contrast, 4k detail.
```

If you do not like it, try again with this added: *"less detail in the middle third, more on the left and right edges"*.

## 2. Floor.png — floor

```
Seamless tileable top-down texture of a futuristic dark stage floor: matte near-black metal panels (#0b0d14) with a fine engraved hexagonal and circuit pattern, thin glowing cyan seams along panel edges, very subtle magenta rune engravings. Perfectly flat orthographic top view, even lighting, no perspective, no shadows, no objects, no text. Edges must tile seamlessly on all four sides. Square 1:1.
```

## 3. NoteFace.png — cube face

```
Seamless square texture for the face of a sci-fi rhythm game cube: dark gunmetal surface with a beveled frame border, fine brushed metal grain, small machined vents near the corners and a faint inner panel line. Neutral grey-blue colour (it will be tinted red or blue in game), flat orthographic front view, even studio lighting, no glow, no arrow, no symbols, no text. Square 1:1.
```

## 4. NoteArrow.png — arrow

```
A single bold chevron arrow pointing straight UP, centered, glowing pure white neon with a soft bloom halo, sharp clean vector-like shape similar to Beat Saber note arrows, slightly futuristic angular cut. PURE BLACK background (#000000), nothing else in the image, no text, no frame. The arrow fills about 60% of the image height. Square 1:1, symmetric left-right.
```

## 5. NoteRune.png — hand note rune

```
A circular arcane magic sigil frame, glowing pure white neon lines with a soft halo: outer thin ring, inner ring of small geometric runes, four small diamond marks at top, bottom, left and right. The CENTER IS EMPTY (a letter will be placed there). PURE BLACK background (#000000), nothing else, no text, no letters. Centered, fills about 85% of the image. Square 1:1, symmetric.
```

## 6. Logo.png — menu logo

```
Game logo wordmark "BEAT & BLADE" for a neon VR-style rhythm game. Bold wide futuristic sans-serif letters, the word BEAT in glowing crimson-magenta and BLADE in glowing cyan, a thin glowing sword blade slicing diagonally through the letters, subtle arcane rune details. TRANSPARENT BACKGROUND (PNG with alpha), no other elements, no tagline, centered, landscape 3:2, crisp edges.
```

---

## 7–8. 3D models

ChatGPT cannot make 3D models by itself. The workflow:

1. Generate a **reference image** in ChatGPT with the prompt below.
2. Upload that image to an image-to-3D tool (Meshy, Tripo, Rodin, etc.). Download it as **FBX** with its textures.
3. Put the FBX and texture files in `Resources/Art/`. The file name must be `SaberHilt.fbx` / `Gauntlet.fbx`.

The game stands the model up along its long axis, scales and centers it, and converts its materials to HDRP. If you use GLB/GLTF, install the *glTFast* package in Unity first; FBX does not need it.

### SaberHilt — sword hilt

```
Single 3D-model reference of a futuristic energy sword HILT only (no blade), standing perfectly vertical, emitter end at the top, pommel at the bottom. Dark gunmetal and brushed steel with ribbed grip, two small swept cross-guard fins near the top, a ring-shaped emitter, thin glowing cyan inlay lines. Shown in a clean 3/4 front view, centered, full object visible, neutral light-grey studio background, soft even lighting, no hands, no text. Game asset, realistic PBR materials, about 4 times taller than wide.
```

### Gauntlet — forearm armor

```
Single 3D-model reference of a sci-fi armored forearm bracer (vambrace) that covers the forearm, wrist and the back of the hand, with NO fingers and NO hand inside it. Standing vertical, wrist end at the top. Dark violet-black layered armor plates, brushed metal edges, a small glowing crimson crystal on the back-of-hand plate, thin glowing seams. Clean 3/4 view, centered, full object visible, neutral light-grey studio background, soft even lighting, no text. Game asset, realistic PBR materials.
```

The left hand needs no separate model; the game mirrors the same one. Fingers stay procedural and keep animating.

---

## Checklist

- [ ] File names match the table exactly, including case.
- [ ] `Background`, `NoteArrow` and `NoteRune` have pure black backgrounds.
- [ ] `Logo` has a transparent background.
- [ ] Files are in `Assets/ArcaneSaber/Resources/Art/`.
- [ ] Press Play. If something looks wrong (e.g. the arrow points the wrong way), just fix the image and replace it with the same name.
