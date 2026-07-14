# Plenus — Design Implementation Guide (Unity / uGUI)

This guide turns the **Plenus mobile UI design** into your Unity game. It's written for your
actual project: Unity 6, Netcode for GameObjects, classic **uGUI** (Canvas + Image + TextMeshPro +
LayoutGroups), with your existing `BoardManager`, `DiceManager`, `ScoreUI`, `NetworkMenuManager`,
`GameManager`.

---

## 0. First: what "the assets" actually are

There are **no PNG/sprite art files to export** from this design, because the design doesn't use
any. Every visual is built from three things:

1. **Colors** — solid fills written as `oklch(...)`. I've converted all 52 of them to hex/`Color`.
2. **Two free Google fonts** — Baloo 2 + Nunito.
3. **CSS shapes** — rounded rectangles, circles, pills, and a few emoji/Unicode glyphs
   (`★ ‹ › ? ☀ ☾ ×`). No bitmaps.

So "getting all the assets" = this kit. It contains everything the design is actually made of:

```
PlenusDesignKit/
├─ Plenus_Implementation_Guide.md      (this file)
├─ Scripts/
│   ├─ PlenusColors.cs                 all 52 colors as UnityEngine.Color
│   ├─ ScreenManager.cs                screen switching (the design's nav logic)
│   └─ PlenusTheme.cs                  light/dark theme swap + ThemedGraphic
├─ Sprites/
│   ├─ RoundedRect96_r24.png           9-slice card/button background (white, tint it)
│   ├─ Pill160_r32.png                 9-slice pill/tag/toggle-track background
│   └─ Circle128.png                   avatars, icon buttons, toggle knobs
├─ Fonts/
│   └─ README_FONTS.md                 where to get the fonts + how to make TMP assets
└─ Plenus Board UI (interactive).html  the clickable source design (open in a browser)
```

---

## 1. Import the kit into Unity

1. Copy the `Scripts/` files into `Assets/Scripts/UI/` (any folder under Assets).
2. Copy the three PNGs into `Assets/UI/Sprites/`. Select each in the Project window and in the
   Inspector set **Texture Type = Sprite (2D and UI)**. For `RoundedRect96_r24` and `Pill160_r32`
   click **Sprite Editor** and set the **9-slice border** to the corner radius:
   - RoundedRect96_r24 → border **24** on all four sides.
   - Pill160_r32 → border **32** left/right (top/bottom can stay 0 or 32).
   - Circle128 → no border (leave as is; only used at fixed sizes).
3. Add the fonts as described in `Fonts/README_FONTS.md`.

That's the whole import. The sprites are pure white so you tint them any color from
`PlenusColors` at runtime or in the Inspector.

---

## 2. Design tokens (the numbers to reproduce)

### 2.1 Colors

**Light theme (default background chrome)**

| Token          | Hex       | Use |
|----------------|-----------|-----|
| LightBg        | `#F6EDE0` | phone background |
| LightCard      | `#F9F4EE` | cards, panels |
| LightCardAlt   | `#E7DCD0` | secondary buttons, inputs, chips |
| LightTextPrimary   | `#362C24` | headings, main text |
| LightTextSecondary | `#6C6158` | captions, labels |
| LightBorder    | `#E0D6CA` | 1.5px inset card outlines |

**Dark theme**

| Token         | Hex       |
|---------------|-----------|
| DarkBg        | `#201914` |
| DarkCard      | `#2F2722` |
| DarkCardAlt   | `#3B342E` |
| DarkTextPrimary   | `#EFEBE4` |
| DarkTextSecondary | `#ABA39B` |
| DarkInputBg   | `#342C26` |
| DarkBorder    | `#4E463F` |

**Accents (same in both themes)**

| Token          | Hex       | Use |
|----------------|-----------|-----|
| PrimaryRed     | `#D05F43` | main call-to-action buttons |
| PrimaryRedPress| `#9D381F` | the 3px drop-shadow under red buttons |
| Green          | `#418E47` | Start Match / Save / positive |
| GreenPress     | `#1E6626` | drop-shadow under green buttons |
| ReadyGreen     | `#38853E` | "Ready" status text |
| AccentLink     | `#833F27` | links ("Forgot password?", "Create account") |
| LogoutRed      | `#C74C3D` | log-out text |
| StarGold       | `#EAB532` | stars, gold rank |

**Board / match colors** (the 5 game colors + black wildcard)

| Token       | Hex       |
|-------------|-----------|
| BoardGreen  | `#80CD82` |
| BoardRed    | `#F47B74` |
| BoardYellow | `#F1D35D` |
| BoardBlue   | `#79B0E8` |
| BoardOrange | `#FBA962` |
| BoardBlack  | `#505869` (black wildcard) |
| CellMarked  | `#DBD7D0` (a cell after it's marked — greyed) |
| PendingRing | `#0072D5` (blue ring on a temp-selected cell) |
| DiePip      | `#362C24` (dots on numeric dice) |

> These map to your existing `ColorPalette.cs`. The cell inset border in the design is
> `rgba(40,30,20,0.55)` → `Color(0.157,0.118,0.078,0.55)`.

**Avatar swatches** (12): `#CB764E #47944C #488ACB #C99500 #C74B47 #7B63A3 #E1707C #009E98
#B1A93A #887769 #CD6AAF #5D646F`

**Rank tiers** (low→high): Wood III `#8C6D58`, Wood II `#9E7B64`, Wood I `#B0896F`,
Silver II `#ABB2BB`, Silver I `#C2C8CF`, Gold `#E3AE28`, Diamond `#1EBDE3`, Aurelune `#BD5ED2`.

All of the above already live in `PlenusColors.cs`, e.g. `image.color = PlenusColors.PrimaryRed;`

### 2.2 Typography

- **Baloo 2 ExtraBold** — the "Plenus" logo (40px login / 30px menu), screen titles (19px),
  button labels (14–17px), big numbers/stats.
- **Nunito Bold/SemiBold** — everything else (11–15px). Uppercase section labels are
  Nunito ExtraBold 11px, letter-spacing ~0.4px.

### 2.3 Spacing, radius, shadow

- Phone frame: **390 × 844**, frame radius 44px, screen padding ~22–28px.
- Cards / buttons radius **14–18px** → use `RoundedRect96_r24` 9-sliced (looks right at those sizes).
- Pills / tags / toggle tracks radius **100px** → use `Pill160_r32`.
- Gaps between stacked elements: **10–18px** (that's your LayoutGroup *Spacing*).
- The signature **chunky button**: a coloured rounded rect with a *darker copy offset 3px down
  behind it* (`box-shadow: 0 3px 0 <press-color>`). Reproduce with two Images (see §3.2).
- Soft card shadow is subtle; uGUI's `Shadow` component (offset y≈3, low alpha) is close enough,
  or skip it — the design reads fine flat.

---

## 3. Build 3 reusable prefabs first

Everything in the design is assembled from these. Build them once, then reuse.

### 3.1 Card
Image (sprite = `RoundedRect96_r24`, Image Type = **Sliced**), color = `LightCard`.
For the "1.5px inset border" look, add a child Image (same sliced sprite) set to `LightBorder`,
slightly larger, behind — or just skip the border; it's cosmetic.
Add a **VerticalLayoutGroup** + **ContentSizeFitter** if the card should hug its content.

### 3.2 Button (chunky style)
```
Button (RectTransform)
├─ Shadow  (Image, sliced RoundedRect, color = PrimaryRedPress, offset +3px down, behind)
└─ Face    (Image, sliced RoundedRect, color = PrimaryRed)   ← add the Button component here
   └─ Label (TMP, Baloo 2 ExtraBold, white)
```
Secondary buttons: Face = `LightCardAlt`, Label = `LightTextPrimary`, and drop the shadow child.
Green buttons: Face = `Green`, Shadow = `GreenPress`.

### 3.3 Toggle (the settings switches)
```
Track (Image, sliced Pill160_r32, size 42×24, color = LightCardAlt when off / Green when on)
└─ Knob (Image, Circle128, size 18×18, anchored left when off / right when on)
```
Drive on/off + knob position from a small script hooked to your settings.

Other atoms: **icon button** = Circle128 Image (color `CardAlt`) + TMP glyph (`‹`, `?`, `☾`, `×`).
**Tag/chip** = Pill160_r32 Image + TMP. **Avatar** = Circle128 Image tinted, + TMP initial/emoji.

---

## 4. How the design's concepts map to uGUI

| Design (HTML/CSS)                    | uGUI equivalent |
|--------------------------------------|-----------------|
| `<sc-if nav==='menu'>` (one screen)  | one **panel GameObject**; `ScreenManager.Show()` enables one at a time |
| `display:flex; flex-direction:column; gap:14px` | **VerticalLayoutGroup**, Spacing = 14 |
| `display:flex; gap` (row)            | **HorizontalLayoutGroup** |
| `flex:1` spacer (pushes to bottom)   | empty object with **LayoutElement → Flexible Height = 1** |
| `<sc-for list=...>` (repeated rows)  | instantiate a **row prefab in a C# loop** |
| `{{ nav2.profileName }}` binding     | `text.text = ...` in C# |
| `justify-content:space-between`      | HorizontalLayoutGroup + a flexible spacer, or anchor children |
| `grid-template-columns:repeat(2,1fr)`| **GridLayoutGroup** (2 columns) — you already use this for the board |
| `border-radius`                      | sliced rounded sprite (§1) |
| `position:absolute; inset:0` overlay | full-stretch panel on top (modals: Rank info, settings) |
| phone `390×844`                      | **CanvasScaler**: Scale With Screen Size, ref `390×844` (or `1080×2340`), Match ≈ 0.5 |

**Screens present in the design** (build panels for each):
Login · Main Menu · Configuration · Ranked (+ "how ranking works" modal) · Create Lobby ·
Join Lobby · Profile · Personalize Avatar · Scoring Rules · Initiative roll · Match (board) ·
Opponent-board view · live-feedback toast.

---

## 5. Screen-by-screen

Each screen is one panel under `ScreenRoot`, background = theme `PhoneBg`, padding via a
VerticalLayoutGroup with left/right padding ~24. Only the specifics differ:

**Login** — logo "Plenus" (Baloo 40) + "Board Duel" caption; two inputs (`TMP_InputField`,
sliced rounded, `InputBg`); right-aligned "Forgot password?" (AccentLink); red **Log in** button;
an "or" divider (two thin `Border` images + label); secondary **Continue as guest**; footer
"New here? Create an account". Theme toggle icon top-right. `Log in` → `ShowMenu()`.

**Main Menu** — top-left gear icon (→ Config), top-right `?` (→ Rules) and `☾` (theme). Logo.
A profile card (avatar + name + rank chip + `›`) → `ShowProfile()`. Then three big buttons:
**Create Lobby** (red) → CreateLobby, **Join Lobby** (cardAlt) → JoinLobby, **Ranked** (cardAlt)
→ Ranked. Bottom link "Resume match in progress" → Match.

**Configuration** — back `‹`, title. Sections (uppercase labels): *Appearance* = Light/Dark
segmented control (two pills, active = card, calls `PlenusTheme.SetDark`). *Sound & haptics* =
a card with 4 toggle rows (Sound effects, Music, Vibration, Push notifications) using the §3.3
toggle. *Account* card. Red **Log out** text. Version footer.

**Ranked** — back + title + `i` (opens modal). A big tier card: rotated-45° rounded square in the
tier color + "Overall Rating" + tier label + "Global rank #N · x/10 wins". A progress row of 10
pips (filled = `Green`/tier, empty = `CardAlt`). "Leaderboard" list via row prefab
(rank #, tier diamond, name + tier label, wins). Row for "you" is highlighted.
Modal "How ranking works": tier table (8 rows: color diamond, label, "top X% of players").

**Create Lobby** — back + title. "Lobby code" big centered pill (`PLN-482`, letter-spacing 4px).
"Players in lobby" list (avatar + name + green "Ready"). Bottom green **Start Match** →
`ShowInitiative()`.

**Join Lobby** — back + title. "Enter lobby code" input. "Open lobbies nearby" list (avatar +
name + "1/2 players" + red **Join** pill → Initiative).

**Profile** — back + title. Big 96px avatar (tinted circle + initial). Name (Baloo 20).
2-column **GridLayoutGroup** of stat tiles (Matches 24, Wins 15, Win rate 63% [green], Win streak
3 [red], Best streak 6, Stars 128, Rows completed 41, Colours completed 9) — each tile is a
`Card` with a big Baloo number + Nunito caption. "Your nemesis" card. Bottom **Personalize
avatar** → Avatar.

**Personalize Avatar** — back + title. 110px live-preview avatar. "Choose an icon" = wrap of
10 rounded-square emoji buttons (🦊🐻🦉🐺🦁🐢🐙🦅🐯🐨), selected one gets a ring
(`box-shadow` → a highlighted outline Image). "Choose a colour" = wrap of 12 circle swatches
(the 12 avatar colors), selected gets a ring. Bottom green **Save** → Profile.

**Scoring Rules** — back + title, scrollable (**ScrollRect** + Mask + VerticalLayoutGroup
content). "Row bonuses" = 15 rows (A–O), each a mini bar + "1st +x" (gold) / "2nd +y". Then
"Colour bonus", stars penalty, jokers, etc. Values in §8. Back returns to whoever opened it
(`ScreenManager.Back()`).

**Initiative** — both players roll to see who starts; a rolling animation then a result.
Wire to your real initiative logic; visually it's two dice + names + a "You go first" banner.

**Match (the board)** — **you already have this.** Reskin, don't rebuild:
- Top bar: two player chips ("M You" red, "A Alex" blue) + `i` + settings circle.
- Row of joker diamonds + "jokers" + "Your board" + `★ 0/15`.
- The **15×7 board**: your `BoardManager` GridLayoutGroup. Cell = Image (sliced, radius 7,
  size ~33) tinted `PlenusColors.Board(colorName)`, inset border, `★` overlay for star cells,
  greyed to `CellMarked` when committed, blue `PendingRing` when temp-selected. Row status tags
  ("1st +1" gold / "2nd +1") sit to the right of the relevant rows.
- Bottom "felt" panel: current dice (3 color squares + 3 numeric dice with pips), "7 jokers left
  — use one to ignore:" with **Skip colour** / **Skip number** pills, then **Roll** (red) /
  **Confirm** (grey/green) / **Pass** buttons. These map to your `DiceManager` / `DiceButtonUI`
  / `SelectionManager` and the Confirm/Pass/Roll buttons already in your Hierarchy.

**Opponent view + toast** — a toggle flips the board to the opponent's; a small toast
("Alex just completed row H!") slides in for ~2.8s on opponent moves. Toast = a Pill/Card at the
top, shown/hidden by a coroutine.

---

## 6. Wiring navigation & theme

- Add `ScreenManager` to `ScreenRoot`, register every panel with its `Screen` enum value, set
  `startScreen = Login`. Each nav button's `OnClick` calls the matching `Show…()` method
  (e.g. Create Lobby button → `ShowCreateLobby`).
- For light/dark: put `ThemedGraphic` on every Image/TMP that should recolor, set its `Role`
  (PhoneBg, Card, TextPrimary, …). The theme toggle button calls `PlenusTheme.Toggle()`.
  Accent colors (red/green/gold) are theme-independent — leave those as fixed `PlenusColors`.
- The design's screen flow: Login→Menu→(Create/Join Lobby)→Initiative→Match, with Menu also
  reaching Config, Ranked, Profile→Avatar, and Rules from the `?`. Hook the lobby/match
  transitions to your real `NetworkMenuManager` (LobbyScene → MainGameScene via
  `NetworkSceneManager`) rather than just toggling panels once networking is involved.

---

## 7. Map to your existing scripts

| Design piece            | Your project |
|-------------------------|--------------|
| Match board rendering   | `BoardManager`, `CellView`, `GridUtils`, `BoardGenerator` — reskin cell visuals with `PlenusColors.Board()` |
| Dice panel + selection  | `DiceManager`, `DiceButtonUI`, `DiceButton`, `SelectionManager` |
| Score / stats / bonuses | `ScoreManager`, `ScoreUI`, `ScoreConfig`, `ScoreBreakdown`, `PlayerData` (Profile & Rules screens read from these) |
| Lobby / menu flow       | `NetworkMenuManager`, `GameManager` — drive `ScreenManager` from these |
| Colours                 | fold `PlenusColors` into your `ColorPalette.cs` (or reference it directly) |

**Reskin first (fastest win):** the Match screen — new colors, fonts, rounded cells, chunky
buttons — no new logic. **Then build new screens:** Menu is the best second step because it
establishes your Card/Button prefabs; every other screen reuses them.

---

## 8. Data the design encodes (reference)

Your gameplay already implements most of this; included so the UI shows the right numbers.

- **Board:** 15 rows (A–O) × 7 cols = 105 cells. 5 colors, each 21 cells. 2 stars per color
  (STAR total shown as 15 in the design's counter). Black = colour wildcard, numeric **6** =
  number wildcard.
- **Marking rule (from the design's logic):** with numeric die value *N*, a cell is markable if
  `row+1 == N` **or** `col+1 == N` (plus colour match, unless a wildcard/joker is used).
- **Jokers:** max **8** per game; "Skip colour"/"Skip number" each spend one.
- **Row rewards** `{1st, 2nd}` for rows A→O:
  `(5,3)(3,2)(3,2)(3,2)(2,1)(2,1)(2,1)(1,0)(2,1)(2,1)(2,1)(3,2)(3,2)(3,2)(5,3)`
  — the 1st-place column matches your `context.md` reward array `[5,3,3,3,2,2,2,1,2,2,2,3,3,3,5]`.
- **Colour bonus:** first to finish a colour +5, second +3.
- **Penalty:** −2 per unmarked star. **Unused joker:** +1 each.
- **Rank tiers:** Wood III/II/I, Silver II/I, Gold, Diamond, Aurelune; 10 wins to advance a tier.
- **Avatars:** 10 emoji icons + 12 colours (listed in §2.1).

---

## 9. Suggested order (checklist)

1. Import kit (fonts, sprites as 9-slice, scripts). Set CanvasScaler to 390×844.
2. Build the 3 prefabs: Card, Button, Toggle (+ icon button, tag, avatar atoms).
3. Reskin the **Match** screen with the new palette/fonts/cells/buttons.
4. Build **Main Menu** (locks in your prefabs + `ScreenManager`).
5. Build Login, Config (+ theme toggle), Profile, Avatar.
6. Build Create/Join Lobby + Ranked + Rules + Initiative.
7. Wire lobby/match transitions into `NetworkMenuManager`; hook Profile/Rules to `PlayerData`/`ScoreManager`.
8. Add light/dark `ThemedGraphic` pass + the opponent-view toggle and toast.

Open `Plenus Board UI (interactive).html` in a desktop browser side-by-side while you build —
it's the living spec for spacing, states, and interactions.
