# Arcane Saber — Görsel ve Model Promptları

Her görseli ChatGPT'de (görsel üretimi açık) ayrı bir sohbette üret. Aşağıdaki promptu **olduğu gibi** yapıştır; İngilizce bırak, görsel modeller İngilizceyi daha iyi uyguluyor.

Üretilen dosyayı tam olarak tablodaki adla şu klasöre koy:

```
Assets/ArcaneSaber/Resources/Art/
```

Unity içe aktarma ayarlarını kendisi yapar. Oyun dosyayı bulursa onu kullanır, bulamazsa şimdiki prosedürel görünümde kalır. Yani hepsini aynı anda yapman gerekmiyor.

| Dosya | Ne olur | Boyut |
| --- | --- | --- |
| `Background.png` | Tünelin arkasındaki uzak manzara | 1536×1024 (yatay) |
| `Floor.png` | Zemin dokusu, tekrarlanır | 1024×1024 |
| `NoteFace.png` | Küplerin yüzey dokusu | 1024×1024 |
| `NoteArrow.png` | Lazer küplerindeki ok | 1024×1024 |
| `NoteRune.png` | El küplerindeki rün çerçevesi | 1024×1024 |
| `Logo.png` | Ana menü logosu (şeffaf arka plan) | 1536×1024 |
| `SaberHilt.fbx` | Kılıç kabzası 3D modeli | — |
| `Gauntlet.fbx` | Kol zırhı 3D modeli | — |

**Önemli kural:** `Background`, `NoteArrow` ve `NoteRune` oyunda *ışık olarak eklenir*: siyah alanlar görünmez, parlak alanlar parlar. Bu yüzden promptlarda "pure black background" diyoruz. Siyah arka plan yerine gri veya beyaz gelirse yeniden üret.

---

## 1. Background.png — uzak manzara

```
Wide cinematic sci-fi backdrop for a neon rhythm game, seen from inside a long tunnel looking toward the horizon. A colossal distant megastructure of glowing magenta and cyan rings and towers on the horizon, vertical light pillars, faint geometric grid mountains, sparse stars. Everything is thin emissive light lines and soft glows on a PURE BLACK background (#000000), no fog haze filling the frame, no ground plane in the foreground, no text, no characters. The centre of the image stays darker so gameplay remains readable. Synthwave meets arcane magic: subtle glowing runes woven into the structures. Landscape 3:2, ultra clean, high contrast, 4k detail.
```

Beğenmezsen şunu ekleyerek yeniden dene: *"less detail in the middle third, more on the left and right edges"*.

## 2. Floor.png — zemin

```
Seamless tileable top-down texture of a futuristic dark stage floor: matte near-black metal panels (#0b0d14) with a fine engraved hexagonal and circuit pattern, thin glowing cyan seams along panel edges, very subtle magenta rune engravings. Perfectly flat orthographic top view, even lighting, no perspective, no shadows, no objects, no text. Edges must tile seamlessly on all four sides. Square 1:1.
```

## 3. NoteFace.png — küp yüzeyi

```
Seamless square texture for the face of a sci-fi rhythm game cube: dark gunmetal surface with a beveled frame border, fine brushed metal grain, small machined vents near the corners and a faint inner panel line. Neutral grey-blue colour (it will be tinted red or blue in game), flat orthographic front view, even studio lighting, no glow, no arrow, no symbols, no text. Square 1:1.
```

## 4. NoteArrow.png — ok

```
A single bold chevron arrow pointing straight UP, centered, glowing pure white neon with a soft bloom halo, sharp clean vector-like shape similar to Beat Saber note arrows, slightly futuristic angular cut. PURE BLACK background (#000000), nothing else in the image, no text, no frame. The arrow fills about 60% of the image height. Square 1:1, symmetric left-right.
```

## 5. NoteRune.png — el notu rünü

```
A circular arcane magic sigil frame, glowing pure white neon lines with a soft halo: outer thin ring, inner ring of small geometric runes, four small diamond marks at top, bottom, left and right. The CENTER IS EMPTY (a letter will be placed there). PURE BLACK background (#000000), nothing else, no text, no letters. Centered, fills about 85% of the image. Square 1:1, symmetric.
```

## 6. Logo.png — menü logosu

```
Game logo wordmark "ARCANE SABER" for a neon VR-style rhythm game. Bold wide futuristic sans-serif letters, the word ARCANE in glowing crimson-magenta and SABER in glowing cyan, a thin glowing sword blade slicing diagonally through the letters, subtle arcane rune details. TRANSPARENT BACKGROUND (PNG with alpha), no other elements, no tagline, centered, landscape 3:2, crisp edges.
```

---

## 7–8. 3D modeller

ChatGPT tek başına 3D model üretmez. İki adımlı yol:

1. Aşağıdaki promptla ChatGPT'de **referans görsel** üret.
2. O görseli bir görselden-3D aracına yükle (Meshy, Tripo, Rodin vb.). **FBX** olarak, dokularla birlikte indir.
3. FBX'i ve doku dosyalarını `Resources/Art/` içine koy. Dosya adı `SaberHilt.fbx` / `Gauntlet.fbx` olmalı.

Oyun modelin uzun kenarını kendisi dikleştirir, boyutlandırır ve ortalar. Malzemeleri de HDRP'ye çevirir. GLB/GLTF kullanacaksan önce Unity'ye *glTFast* paketini kur; FBX ile buna gerek yok.

### SaberHilt — kılıç kabzası

```
Single 3D-model reference of a futuristic energy sword HILT only (no blade), standing perfectly vertical, emitter end at the top, pommel at the bottom. Dark gunmetal and brushed steel with ribbed grip, two small swept cross-guard fins near the top, a ring-shaped emitter, thin glowing cyan inlay lines. Shown in a clean 3/4 front view, centered, full object visible, neutral light-grey studio background, soft even lighting, no hands, no text. Game asset, realistic PBR materials, about 4 times taller than wide.
```

### Gauntlet — kol zırhı

```
Single 3D-model reference of a sci-fi armored forearm bracer (vambrace) that covers the forearm, wrist and the back of the hand, with NO fingers and NO hand inside it. Standing vertical, wrist end at the top. Dark violet-black layered armor plates, brushed metal edges, a small glowing crimson crystal on the back-of-hand plate, thin glowing seams. Clean 3/4 view, centered, full object visible, neutral light-grey studio background, soft even lighting, no text. Game asset, realistic PBR materials.
```

Sol el için ayrı model gerekmez; oyun aynı modeli aynalar. Parmaklar prosedürel kalır ve animasyonlu çalışmaya devam eder.

---

## Kontrol listesi

- [ ] Dosya adları tablodakiyle birebir aynı, büyük/küçük harf dahil.
- [ ] `Background`, `NoteArrow` ve `NoteRune` simsiyah arka planlı.
- [ ] `Logo` şeffaf arka planlı.
- [ ] Dosyalar `Assets/ArcaneSaber/Resources/Art/` içinde.
- [ ] Play'e bas. Bir şey ters görünürse (ör. ok yanlış yöne bakıyor), görseli düzeltip aynı adla değiştirmen yeterli.
