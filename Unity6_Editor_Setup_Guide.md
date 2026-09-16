# Unity 6.3 Editor Setup Guide — Regicide-Style Card Game

This guide covers **everything you must do inside the Unity Editor** to make the game playable. The C# runtime scripts are already written (see `Assets/Scripts/`), but the scene, prefabs, and serialized references cannot be created in code at runtime and must be wired in the Editor.

Target version: **Unity 6.3 (6000.3.x)**, Universal 2D template, uGUI + TextMeshPro + new Input System.

---

## 0. Before you start

Confirm the C# scripts compiled. In Unity:

1. Open the project (it will recompile all scripts).
2. Wait for the spinner in the bottom-right to finish.
3. Open **Window → General → Console** and confirm there are **no compile errors**.
   - If errors appear, fix them before wiring the scene (a missing reference on a script or a typo would show here).

> **Tip:** If you see "The type or namespace name `TMPro` could not be found", the TextMeshPro import may be missing. Run **Window → TextMeshPro → Import TMP Essential Resources** (in Unity 6 it's bundled with uGUI) or re-import the `com.unity.ugui` package.

---

## 1. Generate the data assets (one click)

An editor script creates the relic and enemy `.asset` files automatically so you don't hand-build them.

1. From the top menu: **Game → Generate Data Assets**.
2. Confirm the following appear in the Project window:
   - `Assets/Data/Relics/` → `ClubPower.asset`, `SpadePower.asset`, `HeartPower.asset`, `DiamondPower.asset`
   - `Assets/Data/Enemies/` → `Thief.asset`, `Goblin.asset`, `Knight.asset`

> The individual **Game → Generate Relic Assets / Generate Enemy Assets** submenu items do just one group if you ever need to re-run.

---

## 2. Build the card prefab

The card is the most important prefab. Create it after the Scripts compile.

1. In the **Project** window, right-click → **Create → UI → Panel** and name it `Card`.
2. Give the `Image` component a background color (e.g. white) so the card face is visible.
3. Add children:
   - **NameLabel**: right-click `Card` → **Create → UI → Text – TextMeshPro** → name it `NameLabel`, put it at the top-center of the card.
   - **StateLabel**: another `Text – TextMeshPro` at the bottom (used as a debug/status label).
4. Add the `CardView` component to `Card`:
   - **Assets/Scripts/View/CardView.cs** → drag it onto the GameObject.
5. Wire `CardView`'s fields in the Inspector:
   - **Face** → the root `Image` component.
   - **Name Label** → the `NameLabel` TextMeshPro.
   - **State Label** → the `StateLabel` TextMeshPro.
   - **Canvas Group** → add a `Canvas Group` component to the card and assign it (used to disable raycast-blocking while dragging).
6. Save the prefab:
   - Drag `Card` from the **Hierarchy** into `Assets/Prefabs/` (create the folder).
   - Delete the original from the scene.

> `CardView` requires an `Image` (`[RequireComponent]`) and implements pointer/drag handlers — no extra setup needed for input.

---

## 3. Set up the managers

The managers are auto-created at runtime by `Bootstrap.cs` using `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` and `DontDestroyOnLoad`. **You never need to place or assign manager GameObjects** — all serialized references are wired on `GameController` (§5), which copies them onto `CardManager` at startup.

No Bootstrap scene is required. If you add `Assets/Scenes/Bootstrap.unity` to the build only to set initial scene order, you can instead just order `MainMenu` and `Game` directly.

---

## 4. Build the Game scene

Create the main gameplay scene with a Canvas, the hand field, enemy display, action buttons, deck viewer, shop, and path screen.

### 4.1 Root objects

1. **File → New Scene** and save as `Assets/Scenes/Game.unity`.
2. Create the **Canvas**:
   - **GameObject → UI → Canvas** (name it `Canvas`).
   - Set **CanvasScaler → UI Scale Mode = Scale With Screen Size**, **Reference Resolution = 1920×1080**, **Match = 0.5**.
   - Set **Canvas → Render Mode = Screen Space – Overlay**.
3. An **EventSystem** is auto-created with the Canvas (uses `InputSystemUIInputModule` with the new Input System). Verify it exists; if not, **GameObject → UI → Event System**.

### 4.2 Drag layer canvas

Cards reparent to a dedicated overlay while dragging so they render above everything.

1. **GameObject → UI → Canvas**, name it `DragCanvas`.
2. Set **Render Mode = Screen Space – Overlay** and raise **Sorting Order** (e.g. `50`).
3. Leave it empty — `CardView` parents dragged cards here.

### 4.3 Hand field

1. Under `Canvas`, create a container for the player's hand:
   - **GameObject → UI → Panel**, name `HandField`.
   - Anchor it to the bottom-center.
   - Add a **Horizontal Layout Group** (Alignment Center) so cards auto-arrange.
   - Add a `Content Size Fitter` (Horizontal Fit = Preferred Size) if you want it to hug the cards.
2. Add the **Field** component: **Assets/Scripts/View/Field.cs**.
   - **Cards Holder** → the `RectTransform` of `HandField` itself.

### 4.4 Enemy display

1. **GameObject → UI → Panel**, name `EnemyDisplay`, anchor to the top-center.
2. Add children:
   - **Name** → `Text – TextMeshPro`
   - **HP** → `Text – TextMeshPro`
   - **ATK** → `Text – TextMeshPro`
   - **Status** → `Text – TextMeshPro` (shows "Your Turn" / "VICTORY!" / "GAME OVER")
   - **Portrait** → `Image` (optional, tinted by state)
3. Add the **EnemyDisplayUI** component and assign all four text fields + portrait.

### 4.5 Action buttons

1. **GameObject → UI → Button**, name `PlayButton`, anchor to the bottom-left.
2. **GameObject → UI → Button**, name `DiscardButton`, bottom.
3. Add a **Status** `Text – TextMeshPro` (combat log / status line).
4. Add the **ActionButtonsUI** component and assign all three.

### 4.6 Deck viewer (toggle panel)

1. **GameObject → UI → Button**, name `DeckToggle` (label "Deck").
2. Create a panel `DeckPanel` (start disabled — uncheck it).
3. Inside it, two `Text – TextMeshPro`: `DeckText` and `DiscardText`.
4. Add the **DeckViewerUI** component; assign **Panel** (`DeckPanel`), **Deck Text**, **Discard Text**, **Toggle Button** (`DeckToggle`).

### 4.7 Shop panel

1. **GameObject → UI → Panel**, name `ShopPanel` (start disabled).
2. Inside: a **GoldText** `Text – TextMeshPro`, an **ItemsContainer** (a `Grid Layout Group` / `Vertical Layout Group`), and a **Continue** `Button`.
3. Create a small button prefab `ShopItemButton` (a `Button` with a `Text – TextMeshPro` child) and save it under `Assets/Prefabs/`.
4. Add the **ShopUI** component; assign **Panel**, **Items Container**, **Item Button Prefab**, **Continue Button**, **Gold Text**.

### 4.8 Path screen panel

1. **GameObject → UI → Panel**, name `PathPanel` (start disabled).
2. Inside: a **HeaderText** `Text – TextMeshPro` ("Boss 1/12") and a **MapContainer** (`RectTransform`).
3. Create a node button prefab `PathNodeButton` (a `Button` with `Image` + `Text – TextMeshPro` child) and save it under `Assets/Prefabs/`.
4. Add the **PathScreenUI** component; assign **Panel**, **Map Container**, **Node Button Prefab**, **Header Text**. Defaults: `colSpacing = 200`, `rowSpacing = 120`, `mapOffset = (180,120)`.

### 4.9 GameController (the scene bootstrap + reference hub)

1. Create an empty GameObject, name `GameController`.
2. Add the **GameController** component.
3. Assign on it (this is the single place you wire serialized references — see §5):
   - **Hand Field** → the `HandField` Field component.
   - **Drag Canvas** → the `DragCanvas`.
   - **Card Prefab** → the `Card` prefab (built in step 2).
   - **Relic Catalog** → the 4 relic assets.

---

## 5. Wire references on GameController

The auto-created managers have nothing you can assign in edit mode (they only exist at runtime). **All serialized references are assigned on `GameController`**, which lives in the scene and pushes them to `CardManager` when the game starts.

Select the `GameController` object in the **Game** scene and assign:

- **Hand Field** → the `HandField` Field component.
- **Drag Canvas** → the `DragCanvas`.
- **Card Prefab** → the `Card` prefab.
- **Relic Catalog** → resize the array to 4 and drag in `ClubPower`, `SpadePower`, `HeartPower`, `DiamondPower`.

> `GameController.Start()` copies these onto `CardManager` before the deck is dealt, so the managers stay clean/auto-created and you never touch a runtime-managed object.

---

## 6. Build the Main Menu scene

1. **File → New Scene**, save as `Assets/Scenes/MainMenu.unity`.
2. Add a `Canvas` (same scaler settings as the Game scene) + `EventSystem`.
3. Add two `Button`s: **Play** and **Quit**, each with a `Text – TextMeshPro` child.
4. Create an empty `GameObject` → add the **MainMenuUI** component.
5. Assign **Game Scene Name = `Game`**.
6. Wire the buttons via the Inspector **OnClick** events:
   - `Play Button` → `MainMenuUI.OnPlayClicked`
   - `Quit Button` → `MainMenuUI.OnQuitClicked`
   - (Or add the component and call the public methods.)

---

## 7. Set up build scenes & play

1. **File → Build Settings** (or **Window → General → Services → Build Settings**).
2. Add scenes in order:
   1. `Assets/Scenes/MainMenu.unity`
   2. `Assets/Scenes/Game.unity`
3. Uncheck any other scenes (e.g. the default `SampleScene`).
4. **Window → Game** → press **Play**.

---

## 8. Optional: new Input System settings check

The scripts use `EventSystem` pointer handlers only (no `Input.GetMouseButton` calls), so they work with either input backend. But confirm:

- **Edit → Project Settings → Player → Active Input Handling** → "Input System Package" or "Both".

---

## 9. Common issues & fixes

| Symptom | Cause / Fix |
|---|---|
| Cards don't drag / buttons don't click | Missing `EventSystem` in scene — add one (GameObject → UI → Event System). |
| Cards render *under* other UI while dragging | `DragCanvas.sortingOrder` not high enough — raise it (e.g. 50). |
| Compile error `TMPro` not found | Reimport TextMeshPro (`Window → TextMeshPro → Import TMP Essential Resources`). |
| `RaycastAll` picks up wrong elements | Decorative `Image`s: disable **Raycast Target**. Card root `Image` keep it enabled. |
| Hand doesn't deal cards | `GameController.Hand Field` not assigned, or `CardManager.Card Prefab` not assigned. |
| Path/shop won't show | `PathPanel`/`ShopPanel` must be disabled initially; they're toggled by the UI scripts via events. |
| Boss "VICTORY!" never resolves | Verify all 12 bosses defeated — `RunManager.OnRunCompleted` fires after the 12th. |

---

## 10. File reference map

| Where you need it | Asset |
|---|---|
| Card prefab | `Assets/Prefabs/Card.prefab` (build it) |
| Shop item button prefab | `Assets/Prefabs/ShopItemButton.prefab` |
| Path node button prefab | `Assets/Prefabs/PathNodeButton.prefab` |
| Relic assets | `Assets/Data/Relics/*.asset` (auto-generated) |
| Enemy assets | `Assets/Data/Enemies/*.asset` (auto-generated) |
| Runtime scripts | `Assets/Scripts/**/*.cs` (already written) |
| Editor generator | `Assets/Editor/GameDataGenerator.cs` (already written) |

---

## 11. Testing checklist

1. Play → main menu → **Play** loads the Game scene.
2. Hand deals 8 cards.
3. Select a card (click) → **Play** button enables → click **Play** → enemy takes damage.
4. When enemy attacks, **Play** disables and **Discard** enables; discard cards to reduce pending damage.
5. Defeat enemy → **VICTORY!** → shop appears → buy a relic → **Continue**.
6. Path screen shows 3–4 hidden routes → pick a node → fight.
7. Defeat all 12 bosses → final "VICTORY!".
