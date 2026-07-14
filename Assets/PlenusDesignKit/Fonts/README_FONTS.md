# Fonts used by the Plenus design

The design uses two free Google Fonts. There are no licensed/paid assets.

| Where it's used                          | Font        | Weights in the design |
|------------------------------------------|-------------|-----------------------|
| Titles, buttons, numbers, "Plenus" logo  | **Baloo 2** | 700, 800              |
| Body text, labels, captions              | **Nunito**  | 500, 600, 700, 800    |

## Get the TTFs
Download from Google Fonts (free, OFL license — safe to ship in a game):
- Baloo 2 : https://fonts.google.com/specimen/Baloo+2
- Nunito  : https://fonts.google.com/specimen/Nunito

Click "Get font" -> "Download all", unzip, and copy these into `Assets/Fonts/`:
- `Baloo2-Bold.ttf`, `Baloo2-ExtraBold.ttf`
- `Nunito-Medium.ttf`, `Nunito-SemiBold.ttf`, `Nunito-Bold.ttf`, `Nunito-ExtraBold.ttf`

## Make TextMeshPro font assets
For each TTF: select it in the Project window, then
**Window > TextMeshPro > Font Asset Creator** -> Source Font = the TTF -> **Generate Font Atlas** -> **Save**.
(Baloo 2 ExtraBold and Nunito Bold are the two you'll use most.)

Then set each TMP text component's **Font Asset** accordingly:
- Headings / buttons -> `Baloo2-ExtraBold SDF`
- Body / labels      -> `Nunito-Bold SDF` (or SemiBold for lighter captions)

Tip: set a project-wide default in **Project Settings > TextMeshPro > Default Font Asset**
to `Nunito-Bold SDF` so new text starts on-brand.
