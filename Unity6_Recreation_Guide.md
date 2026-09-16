# Recreating the Regicide-Inspired Card Game in Unity 6.3

This document describes how to rebuild the current Godot 4.6 project (`smart-drag-and-drop-cards`) in **Unity 6.3** (Unity 6000.3.x). It assumes you understand the Godot source and focuses on the correct Unity 6.3 idioms, package choices, and the tricky parts (drag-and-drop, ScriptableObject data, singleton managers, event flow).

---

## 1. What the game is

A single-player roguelike card game inspired by *Regicide*:

- A standard 52-card deck (4 suits × 13 ranks) is your entire resource.
- You fight a sequence of **enemies** (Thief / Goblin / Knight) then a **boss** by playing cards to deal damage and discarding cards to defend.
- Each **suit** has a relic-gated power: ♣ double damage, ♠ reduce enemy ATK, ♥ recycle discard into deck, ♦ draw extra cards.
- Defeating enemies awards **gold** spent in a **shop** on relics.
- A **path screen** lets you pick one of 3–4 routes of different lengths; route contents are **hidden** until revealed.
- **Bosses** are the 12 face cards: 4 Jacks → 4 Queens → 4 Kings, suits shuffled within each rank.
- The game also features a **smart drag-and-drop card system** (reordering cards within a hand, moving between fields) — the original "asset" feature.

---

## 2. Godot → Unity concept map

| Godot (source) | Unity 6.3 equivalent |
|---|---|
| `Resource` + `class_name` (`CardData`, `EnemyTypeData`) | `ScriptableObject` + `[CreateAssetMenu]` |
| Autoload singleton (`CardManager`, `CombatManager`, `RunManager`) | `MonoBehaviour` singleton with `DontDestroyOnLoad` (see §5) |
| `signal` / `emit` | C# `event` / `Action` (or `UnityEvent<T>`) |
| `@export` | `[SerializeField]` |
| `preload("res://...")` | `[SerializeField]` reference, `Resources.Load<T>`, or Addressables |
| `PackedScene.instantiate()` | `Instantiate(prefab)` |
| `Node` / `Control` tree | `GameObject` + `MonoBehaviour`, `RectTransform` hierarchy |
| `CanvasLayer` | `Canvas` (Screen Space – Overlay) |
| `Label`, `Button`, `PanelContainer`, `HBoxContainer`, `VBoxContainer`, `MarginContainer` | `TextMeshProUGUI`, `Button`, `Image`, `HorizontalLayoutGroup`, `VerticalLayoutGroup`, layout + padding |
| `RichTextLabel` (BBCode) | `TextMeshProUGUI` with rich text |
| `Area2D` + `CollisionShape2D` (overlap detection) | EventSystem raycasting (`IDropHandler`, `IPointer*Handler`) or `BoxCollider2D` (see §7) |
| Node-based `CardStateMachine` | enum-driven FSM in `CardView` (or state classes) |
| `RefCounted` runtime data (`EnemyData`) | plain C# `class` (not a MonoBehaviour) |
| `.tres` data assets | `.asset` ScriptableObject assets |
| `match` / enum | C# `enum` + `switch` |

---

## 3. Project setup (Unity 6.3)

1. **New project** via Unity Hub → Unity 6.3 (6000.3.x).
   - Template: **Universal 2D** (gives URP + 2D renderer) or **Built-in** if you prefer zero pipeline overhead. This game is almost entirely UI, so either works; **Universal 2D** is the current default recommendation.
2. **Packages** (Window → Package Manager) — confirm these are installed:
   - `com.unity.textmeshpro` (TextMeshPro — included by default in Unity 6 templates).
   - `com.unity.inputsystem` (**new Input System** — enabled by default in Unity 6 URP templates; set *Project Settings → Player → Active Input Handling* to "Input System Package" if not already).
   - `com.unity.ugui` (uGUI — included by default).
3. **Game view / canvas**: create one `Canvas` (Screen Space – Overlay) with a **CanvasScaler** set to *Scale With Screen Size* (reference resolution e.g. 1920×1080, match 0.5). Every screen below lives under this canvas or its own overlay canvas.
4. **EventSystem**: the Canvas auto-creates an `EventSystem` + `StandaloneInputModule` (with the new Input System it uses `InputSystemUIInputModule`). Required for all `IPointer*` handlers.
5. **UI Toolkit vs uGUI**: use **uGUI** for this project. uGUI's `EventSystem` pointer handlers are the simplest path for drag-and-drop UI cards. (UI Toolkit runtime is viable but its pointer/drag story is more manual.)

---

## 4. Data layer — ScriptableObjects

### 4.1 `CardData.cs`

```csharp
using UnityEngine;

[CreateAssetMenu(fileName = "Card", menuName = "Game/Card Data")]
public class CardData : ScriptableObject
{
    public enum Suit { Hearts, Diamonds, Clubs, Spades }
    public enum Rank { Ace, Two, Three, Four, Five, Six, Seven, Eight, Nine, Ten, Jack, Queen, King }

    public Suit suit;
    public Rank rank;

    public string SuitSymbol => suit switch {
        Suit.Hearts   => "♥",
        Suit.Diamonds => "♦",
        Suit.Clubs    => "♣",
        Suit.Spades   => "♠",
        _ => ""
    };

    public string SuitName => suit switch {
        Suit.Hearts   => "Hearts",
        Suit.Diamonds => "Diamonds",
        Suit.Clubs    => "Clubs",
        Suit.Spades   => "Spades",
        _ => ""
    };

    public string RankLabel => rank switch {
        Rank.Ace  => "A", Rank.Two => "2", Rank.Three => "3", Rank.Four => "4",
        Rank.Five => "5", Rank.Six => "6", Rank.Seven => "7", Rank.Eight => "8",
        Rank.Nine => "9", Rank.Ten => "10", Rank.Jack => "J", Rank.Queen => "Q", Rank.King => "K",
        _ => ""
    };

    public string RankName => rank switch {
        Rank.Jack => "Jack", Rank.Queen => "Queen", Rank.King => "King",
        _ => ""
    };

    public string DisplayName => RankLabel + SuitSymbol;

    public int AttackValue => rank switch {
        Rank.Ace => 1, Rank.Two => 2, Rank.Three => 3, Rank.Four => 4, Rank.Five => 5,
        Rank.Six => 6, Rank.Seven => 7, Rank.Eight => 8, Rank.Nine => 9, Rank.Ten => 10,
        Rank.Jack => 10, Rank.Queen => 15, Rank.King => 20,
        _ => 0
    };
}
```

> **Data-source note:** the Godot `CardData` is created **procedurally** at runtime (a 52-card deck is built in `CardManager`). You have two options in Unity:
> - **Runtime generation (recommended):** write a factory `CardData.Create(suit, rank)` using `ScriptableObject.CreateInstance<CardData>()`, mirroring the Godot `build_deck()`. No 52 asset files.
> - **Asset-based:** generate 52 `.asset` files with an editor script. Prefer the factory for simplicity.

### 4.2 `EnemyTypeData.cs` (static enemy definition)

```csharp
using UnityEngine;

[CreateAssetMenu(fileName = "Enemy", menuName = "Game/Enemy Type")]
public class EnemyTypeData : ScriptableObject
{
    public string enemyName;
    public int maxHp;
    public int baseAttack;
    public int goldReward;
    public CardData sourceCard;      // only meaningful for bosses
    public EnemyAbility[] abilities; // stub, empty for now
}
```

### 4.3 `EnemyAbility.cs` (stub hook, mirrors `enemy_ability.gd`)

```csharp
using UnityEngine;

public abstract class EnemyAbility : ScriptableObject
{
    // Mirrors EnemyAbility.execute(enemy, context)
    public abstract void Execute(EnemyRuntime enemy, CombatManager context);
}
```

### 4.4 `EnemyRuntime.cs` (runtime instance — NOT a MonoBehaviour)

Mirrors `enemy_data.gd` (`RefCounted`). Use a plain class:

```csharp
public class EnemyRuntime
{
    public EnemyTypeData type;
    public int maxHp;
    public int currentHp;
    public int currentAttack;

    public EnemyRuntime(EnemyTypeData enemyType)
    {
        type = enemyType;
        maxHp = enemyType.maxHp;
        currentHp = enemyType.maxHp;
        currentAttack = enemyType.baseAttack;
    }

    public string DisplayName => type.enemyName;
    public int GoldReward => type.goldReward;

    public bool IsDefeated => currentHp <= 0;

    public void TakeDamage(int amount) => currentHp = Mathf.Max(0, currentHp - amount);
    public void ReduceAttack(int amount) => currentAttack = Mathf.Max(0, currentAttack - amount);
}
```

### 4.5 `RelicData.cs` (ScriptableObject)

Each relic is a ScriptableObject asset. Since there will be many relics, data-driven assets beat a hardcoded array in `CardManager`:

```csharp
using UnityEngine;

[CreateAssetMenu(fileName = "Relic", menuName = "Game/Relic Data")]
public class RelicData : ScriptableObject
{
    public string id;          // stable key used in code, e.g. "club_power"
    public string displayName; // "Club Power"
    public string icon;        // "♣"
    public string description; // "♣ cards deal double damage"
    public int price;
}
```

Create four `.asset` files under `Assets/Data/Relics/`: `ClubPower.asset`, `SpadePower.asset`, `HeartPower.asset`, `DiamondPower.asset`, with:

| id | displayName | icon | description | price |
|---|---|---|---|---|
| `club_power` | Club Power | ♣ | ♣ cards deal double damage | 15 |
| `spade_power` | Spade Power | ♠ | ♠ cards reduce enemy ATK | 15 |
| `heart_power` | Heart Power | ♥ | ♥ cards recycle discard to deck | 20 |
| `diamond_power` | Diamond Power | ♦ | ♦ cards draw extra cards | 20 |

---

## 5. Singleton managers

Godot autoloads → Unity singletons. Create empty `GameObject`s named `CardManager`, `CombatManager`, `RunManager` (or one "GameManager" scene). Pattern:

```csharp
public class CardManager : MonoBehaviour
{
    public static CardManager Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
```

Register them via a bootstrap scene or `RuntimeInitializeOnLoadMethod`, and ensure they're ordered so `CardManager` initializes before `CombatManager` (Godot autoload order). Use `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]` or a static `Bootstrap` scene.

---

## 6. CardManager (deck, discard, hand, gold, relics)

Mirrors `card_manager.gd`.

Key fields:

```csharp
public const int HAND_SIZE = 8;

public Field handField;                 // reference set by GameController
public CardView selectedCard;
public List<CardData> deck = new();
public List<CardData> discardPile = new();
public int gold;
public HashSet<string> relics = new();  // owned relic ids

public CardView cardPrefab;             // card view prefab to instantiate
public List<RelicData> relicCatalog = new();  // filled with .asset files in the Inspector

public event Action<int> OnGoldChanged;
```

### Relic catalog & ownership

`RelicData` assets live in `relicCatalog` (assigned in the Inspector — see §4.5). Ownership tracks ids so combat code can query by string:

```csharp
public bool HasRelic(string id) => relics.Contains(id);

public bool BuyRelic(RelicData relic)
{
    if (relic == null || HasRelic(relic.id)) return false;
    if (!SpendGold(relic.price)) return false;
    relics.Add(relic.id);
    return true;
}
```

> The four suit powers are queried by id in `CombatManager` (`"club_power"`, `"spade_power"`, `"heart_power"`, `"diamond_power"`). Keeping ids as strings lets combat code reference them without hard asset references.

### Deck building & draw (with recycle-on-empty)

Mirror `build_deck()`, `draw_cards()`, `draw_to_hand()`, `refill_hand()`, `_recycle_discard()`:

```csharp
public void BuildDeck()
{
    deck.Clear();
    discardPile.Clear();
    foreach (Suit suit in Enum.GetValues(typeof(CardData.Suit)))
    foreach (Rank rank in Enum.GetValues(typeof(CardData.Rank)))
    {
        var c = ScriptableObject.CreateInstance<CardData>();
        c.suit = (CardData.Suit)suit;
        c.rank = (CardData.Rank)rank;
        deck.Add(c);
    }
    ShuffleDeck();
}

public List<CardData> DrawCards(int count)
{
    var drawn = new List<CardData>();
    for (int i = 0; i < count; i++)
    {
        if (deck.Count == 0) RecycleDiscard();
        if (deck.Count == 0) break;
        var c = deck[^1]; deck.RemoveAt(deck.Count - 1);
        drawn.Add(c);
    }
    return drawn;
}

public int DrawToHand(int count)
{
    if (handField == null) { Debug.LogError("CardManager: handField not set"); return 0; }
    int maxDraw = Mathf.Max(0, HAND_SIZE - handField.CardCount);
    var drawn = DrawCards(Mathf.Min(count, maxDraw));
    foreach (var data in drawn)
        handField.AddCard(Instantiate(cardPrefab), data);
    return drawn.Count;
}

public void DealHand()  => DrawToHand(HAND_SIZE);
public void RefillHand() => DrawToHand(HAND_SIZE);

public void AddToDiscard(CardData data) => discardPile.Add(data);

void RecycleDiscard()
{
    if (discardPile.Count == 0) return;
    deck.AddRange(discardPile);
    discardPile.Clear();
    ShuffleDeck();
}
```

Selection, gold, and relic logic map 1:1:

```csharp
public void SelectCard(CardView card)
{
    if (selectedCard != null && selectedCard != card)
        selectedCard.ForceToIdle(); // see §7 FSM
    selectedCard = card;
}
public void DeselectCard() => selectedCard = null;

public void AddGold(int amount) { gold += amount; OnGoldChanged?.Invoke(gold); }
public bool SpendGold(int amount) { if (gold < amount) return false; gold -= amount; OnGoldChanged?.Invoke(gold); return true; }
```

---

## 7. The card view + smart drag-and-drop

This is the core "asset" feature and the most Unity-specific part.

### 7.1 `CardView.cs` (mirrors `card.gd` + the state machine)

Structure: a `CardView` prefab containing:
- `Image` (the card face, colored by state).
- `TextMeshProUGUI` name label (`%NameLabel` equivalent).
- `TextMeshProUGUI` state label (debug).

```csharp
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class CardView : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public enum State { Idle, Hover, Click, Drag, Release }

    public CardData data;
    public Field homeField;
    public int index;               // original index in the field layout

    public Image face;
    public TMP_Text nameLabel;
    public TMP_Text stateLabel;

    State _state = State.Idle;

    public State CurrentState => _state;

    public void Setup(CardData cardData)
    {
        data = cardData;
        if (nameLabel) nameLabel.text = data.DisplayName;
    }

    void ChangeState(State next)
    {
        ExitState(_state);
        _state = next;
        EnterState(next);
    }

    void EnterState(State s)
    {
        switch (s)
        {
            case State.Idle:    face.color = Color.green;  stateLabel.text = "Idle";  break;
            case State.Hover:   face.color = Color.magenta; stateLabel.text = "Hover"; break;
            case State.Click:   face.color = new Color(1f, 0.5f, 0f); stateLabel.text = "Selected"; CardManager.Instance.SelectCard(this); break;
            case State.Drag:    face.color = Color.blue;  stateLabel.text = "Drag";
                                index = transform.GetSiblingIndex();
                                transform.SetParent(GetTopOverlay()); // reparent above everything (Godot CanvasLayer)
                                break;
            case State.Release: break;
        }
    }

    void ExitState(State s) { /* nothing needed in source */ }

    // Input handlers
    public void OnPointerEnter(PointerEventData e) { if (_state == State.Idle) ChangeState(State.Hover); }
    public void OnPointerExit(PointerEventData e)  { if (_state == State.Hover) ChangeState(State.Idle); }
    public void OnPointerDown(PointerEventData e)
    {
        // Hover -> Click
        if (_state == State.Hover) ChangeState(State.Click);
    }
    public void OnPointerUp(PointerEventData e)
    {
        // Clicking an already-selected card deselects it
        if (_state == State.Click) { CardManager.Instance.DeselectCard(); ChangeState(State.Hover); }
    }
    public void OnBeginDrag(PointerEventData e)
    {
        if (_state == State.Click && Input.GetMouseButton(0)) ChangeState(State.Drag);
    }
    public void OnDrag(PointerEventData e) => transform.position = e.position; // screen-space overlay
    public void OnEndDrag(PointerEventData e) => ChangeState(State.Release);

    public void ForceToIdle() => ChangeState(State.Idle); // used by SelectCard to deselect old card
}
```

> **Godot `_input` vs Unity pointer handlers.** Godot uses global `_input` for mouse motion + left-button release. In Unity uGUI, the equivalent is the `EventSystem` pointer interfaces above (works with both legacy and new Input System). Use `IBeginDragHandler` for the "mouse moved while held" trigger, and `IEndDragHandler` for release.

### 7.2 Release logic & field placement (mirrors `release_card_state.gd`)

In Godot, `release_card_state` uses `DropPointDetector`/`CardsDetector` (Area2D) overlap to decide where a card lands. In Unity uGUI, replace Area2D with **EventSystem raycasts**:

```csharp
void ReleaseCard(PointerEventData e)
{
    // Raycast all UI under the pointer
    var results = new List<RaycastResult>();
    EventSystem.current.RaycastAll(e, results);

    Field field = null;
    CardView leftCard = null, rightCard = null;
    foreach (var r in results)
    {
        if (field == null && r.gameObject.TryGetComponent<Field>(out var f)) field = f;
        if (r.gameObject.TryGetComponent<CardView>(out var cv) && cv != this) { /* determine left/right */ }
    }

    if (field == null)
        homeField.ReturnCardToStart(this);          // no field → go home
    else if (field == homeField)
        homeField.RepositionCard(this, leftCard, rightCard);
    else
        field.SetNewCard(this, leftCard, rightCard); // new field → adopt

    CardManager.Instance.DeselectCard();
    ChangeState(State.Idle);
}
```

### 7.3 `Field.cs` (mirrors `field.gd`)

```csharp
using UnityEngine;
using UnityEngine.UI;

public class Field : MonoBehaviour
{
    public RectTransform cardsHolder;          // HorizontalLayoutGroup container
    public RectTransform dropAreaRight;        // optional marker rects (replaces Area2D)
    public RectTransform dropAreaLeft;

    public int CardCount => cardsHolder.childCount;

    public void ReturnCardToStart(CardView card)
    {
        card.transform.SetParent(cardsHolder);
        card.transform.SetSiblingIndex(card.index);
    }

    public void SetNewCard(CardView card, CardView left, CardView right)
    {
        RepositionCard(card, left, right);
        card.homeField = this;
    }

    public void RepositionCard(CardView card, CardView left, CardView right)
    {
        card.transform.SetParent(cardsHolder);
        int targetIndex = /* 0 if empty, or between left/right siblings */;
        card.transform.SetSiblingIndex(targetIndex);
        card.index = targetIndex;
    }

    public void AddCard(CardView card, CardData data)  // used by CardManager draw
    {
        card.transform.SetParent(cardsHolder);
        card.Setup(data);
        card.homeField = this;
    }
}
```

> **Insertion-index logic** (from `field.gd.card_reposition`): if no cards overlap → place at end; if one card overlaps → before or after it depending on left/right; if two overlap → between them. Compute the target `SetSiblingIndex` from the raycasted left/right card indices. Use a `HorizontalLayoutGroup` (alignment center) on `cardsHolder` so cards auto-arrange, exactly like Godot's `HBoxContainer`.

---

## 8. CombatManager (play / discard / counter-attack)

Mirrors `combat_manager.gd`.

```csharp
public enum GameState { Idle, PlayerTurn, EnemyAttacking, GameOver, GameWon }

public GameState currentState = GameState.Idle;
public EnemyRuntime currentEnemy;
public int pendingDamage;

public event Action<GameState> OnStateChanged;
public event Action OnEnemyChanged;
public event Action OnEnemyHpChanged;
public event Action<int> OnPendingDamageChanged;
public event Action<string> OnCombatLog;
```

### Start encounter + refill hand

```csharp
public void StartEnemy(EnemyRuntime enemy)
{
    currentEnemy = enemy;
    currentState = GameState.PlayerTurn;
    CardManager.Instance.RefillHand();
    OnEnemyChanged?.Invoke();
    SetState(GameState.PlayerTurn);
    Log($"Enemy appears: {enemy.DisplayName} (HP: {enemy.maxHp}, ATK: {enemy.currentAttack})");
}
```

### Play a card (mirror `play_card`)

```csharp
public void PlayCard(CardView card)
{
    if (currentState != GameState.PlayerTurn || card?.data == null) return;

    var data = card.data;
    int damage = data.AttackValue;
    var suit = data.suit;

    CardManager.Instance.AddToDiscard(data);
    RemoveCardNode(card);

    var cm = CardManager.Instance;

    if (cm.HasRelic("club_power") && suit == CardData.Suit.Clubs)
        { damage = data.AttackValue * 2; Log("♣ Clubs: Double damage!"); }
    if (cm.HasRelic("spade_power") && suit == CardData.Suit.Spades)
        { int reduce = data.AttackValue; currentEnemy.ReduceAttack(reduce); Log($"♠ Spades: Enemy ATK reduced by {reduce} to {currentEnemy.currentAttack}!"); }
    if (cm.HasRelic("heart_power") && suit == CardData.Suit.Hearts)
        ActivateHearts(data.AttackValue);
    if (cm.HasRelic("diamond_power") && suit == CardData.Suit.Diamonds)
        ActivateDiamonds(data.AttackValue);

    currentEnemy.TakeDamage(damage);
    OnEnemyHpChanged?.Invoke();
    Log($"Played {data.DisplayName} for {damage} damage! (HP: {currentEnemy.currentHp}/{currentEnemy.maxHp})");

    if (currentEnemy.IsDefeated)
    {
        cm.AddGold(currentEnemy.GoldReward);
        SetState(GameState.GameWon);
        Log($"Enemy defeated! +{currentEnemy.GoldReward}g");
        return;
    }

    if (HandCount() == 0) { EndGameOver(); return; }

    pendingDamage = Mathf.Max(0, currentEnemy.currentAttack);
    if (pendingDamage <= 0) { Log("Enemy attack is 0! Your turn."); return; }

    SetState(GameState.EnemyAttacking);
    OnPendingDamageChanged?.Invoke(pendingDamage);
    Log($"Enemy attacks for {pendingDamage}! Discard to defend!");
}
```

### Discard to defend (mirror `discard_card`)

```csharp
public void DiscardCard(CardView card)
{
    if (currentState != GameState.EnemyAttacking || card?.data == null) return;

    var data = card.data;
    int value = data.AttackValue;
    CardManager.Instance.AddToDiscard(data);
    RemoveCardNode(card);

    pendingDamage = Mathf.Max(0, pendingDamage - value);
    OnPendingDamageChanged?.Invoke(pendingDamage);
    Log($"Discarded {data.DisplayName} (-{value}). Remaining: {pendingDamage}");

    if (HandCount() == 0) { EndGameOver(); return; }

    if (pendingDamage <= 0) { SetState(GameState.PlayerTurn); Log("Defended! Your turn."); }
}
```

### Suit powers (mirror `_activate_hearts` / `_activate_diamonds`)

```csharp
void ActivateHearts(int value)
{
    var discard = CardManager.Instance.discardPile;
    int count = Mathf.Min(value, discard.Count);
    if (count <= 0) return;
    discard.Shuffle();
    for (int i = 0; i < count; i++)
    {
        CardManager.Instance.deck.Add(discard[^1]);
        discard.RemoveAt(discard.Count - 1);
    }
    Log($"♥ Hearts: {count} cards returned to deck!");
}

void ActivateDiamonds(int value)
{
    int drawn = CardManager.Instance.DrawToHand(value);
    if (drawn > 0) Log($"♦ Diamonds: Drew {drawn} cards!");
}
```

Helpers (`_hand_count`, `_end_game_over`, `_remove_card_node`):

```csharp
int HandCount() => CardManager.Instance.handField.CardCount;
void EndGameOver() { SetState(GameState.GameOver); Log("No cards left! Game Over!"); }
void RemoveCardNode(CardView card)
{
    CardManager.Instance.DeselectCard();
    Destroy(card.gameObject);
}
void Log(string msg) { OnCombatLog?.Invoke(msg); Debug.Log("[Combat] " + msg); }
void SetState(GameState s) { currentState = s; OnStateChanged?.Invoke(s); }
```

---

## 9. RunManager (boss deck + randomized hidden paths)

Mirrors `run_manager.gd`.

```csharp
public class RunManager : MonoBehaviour
{
    public static RunManager Instance { get; private set; }

    public List<EnemyTypeData> bossDeck = new();   // 12 bosses
    public int bossIndex;
    public List<PathNode> currentPath = new();     // see PathNode below

    public event Action OnShowPathScreen;
    public event Action OnHidePathScreen;
    public event Action OnRunCompleted;

    public int CurrentCycle => bossIndex + 1;
    public int BossTotal => bossDeck.Count;
}
```

### Path node (mirrors the node Dictionary)

```csharp
[Serializable]
public class PathNode
{
    public int id;
    public string type;       // "thief" | "goblin" | "knight" | "boss"
    public float col, row;    // grid position for the map
    public List<int> next = new();
    public bool completed, accessible, revealed;
}
```

### Boss deck (mirror `_build_boss_deck` + boss stat tables)

```csharp
void BuildBossDeck()
{
    bossDeck.Clear();
    foreach (var rank in new[] { CardData.Rank.Jack, CardData.Rank.Queen, CardData.Rank.King })
    {
        var rankBosses = new List<EnemyTypeData>();
        foreach (CardData.Suit suit in Enum.GetValues(typeof(CardData.Suit)))
        {
            var card = ScriptableObject.CreateInstance<CardData>();
            card.rank = rank;
            card.suit = suit;
            rankBosses.Add(CreateBossType(card));
        }
        rankBosses.Shuffle();
        bossDeck.AddRange(rankBosses);
    }
}

EnemyTypeData CreateBossType(CardData card)
{
    var t = ScriptableObject.CreateInstance<EnemyTypeData>();
    t.enemyName = $"{card.RankName} of {card.SuitName}";
    (t.maxHp, t.baseAttack, t.goldReward) = card.rank switch {
        CardData.Rank.Jack  => (20, 10, 10),
        CardData.Rank.Queen => (30, 15, 15),
        _                    => (40, 20, 20),   // King
    };
    t.sourceCard = card;
    return t;
}
```

### Path generation (mirror `_generate_path`)

3–4 routes, distinct lengths `1..routeCount` shuffled among the lanes, all converging on one boss node:

```csharp
void GeneratePath()
{
    int routeCount = UnityEngine.Random.Range(3, 5); // 3 or 4

    var lengths = new List<int>();
    for (int i = 0; i < routeCount; i++) lengths.Add(i + 1);
    lengths.Shuffle();

    currentPath.Clear();
    int nextId = 0;

    int maxLen = routeCount;
    float bossRow = (routeCount - 1) / 2f;
    var boss = MakeNode(nextId++, "boss", maxLen, bossRow, accessible: false);
    currentPath.Add(boss);

    for (int lane = 0; lane < routeCount; lane++)
    {
        PathNode prev = null;
        for (int step = 0; step < lengths[lane]; step++)
        {
            var node = MakeNode(nextId++, RandomRegularType(), step, lane, accessible: step == 0);
            if (prev != null) prev.next.Add(node.id);
            prev = node;
            currentPath.Add(node);
        }
        prev.next.Add(boss.id);
    }
}

string RandomRegularType()
{
    string[] types = { "thief", "goblin", "knight" };
    return types[UnityEngine.Random.Range(0, types.Length)];
}
```

### Encounter start + progression (mirror `_start_encounter`, `on_shop_done`, `on_path_chosen`)

```csharp
void StartEncounter(PathNode node)
{
    var enemy = node.type == "boss"
        ? new EnemyRuntime(bossDeck[bossIndex])
        : new EnemyRuntime(GetEnemyType(node.type));
    CombatManager.Instance.StartEnemy(enemy);
}

EnemyTypeData GetEnemyType(string type) => type switch {
    "thief"  => thiefType,
    "goblin" => goblinType,
    _        => knightType,
};

public void OnShopDone()
{
    foreach (var n in currentPath)
        if (n.type == "boss" && n.completed)
        {
            bossIndex++;
            if (bossIndex >= bossDeck.Count) { OnRunCompleted?.Invoke(); return; }
            NextCycle(); return;
        }
    OnShowPathScreen?.Invoke();
}

public void OnPathChosen(int id)
{
    var node = FindNode(id);
    if (node == null || !node.accessible) return;

    node.completed = true;
    node.accessible = false;
    node.revealed = true;

    // lock siblings in same column (other routes' choice)
    foreach (var n in currentPath)
        if (n.col == node.col && n.id != node.id)
            n.accessible = false;

    foreach (var nextId in node.next)
        if (FindNode(nextId) is { } next)
            next.accessible = true;

    OnHidePathScreen?.Invoke();
    StartEncounter(node);
}
```

---

## 10. UI screens

All are uGUI scenes under overlay canvases, subscribing to manager events in `OnEnable`/`OnDisable` (mirroring Godot `_ready` signal connects).

### 10.1 `GameController.cs` (mirrors `game.gd`)

Bootstrap: wire `CardManager.handField`, hook gold display, `DealHand()`, then `RunManager.StartRun()` deferred.

```csharp
void Start()
{
    CardManager.Instance.handField = handField;
    CardManager.Instance.OnGoldChanged += _ => UpdateGold();
    UpdateGold();
    CardManager.Instance.DealHand();
    StartCoroutine(DeferStartRun());
}

IEnumerator DeferStartRun() { yield return null; RunManager.Instance.StartRun(); }
```

### 10.2 `EnemyDisplayUI.cs` (mirrors `enemy_display.gd`)

Subscribes to `OnEnemyChanged`, `OnEnemyHpChanged`, `OnStateChanged`; renders name / HP / ATK; shows "VICTORY!" on `GameWon`.

### 10.3 `ActionButtonsUI.cs` (mirrors `action_buttons.gd`)

Play/Discard buttons + status label. Enable/disable based on `currentState` and `selectedCard`. In Godot the buttons are re-checked every frame in `_process`; in Unity, subscribe to `OnStateChanged`, `OnPendingDamageChanged`, `OnCombatLog`, and also re-evaluate in `Update()` (selection changes without an event) — or add an `OnSelectionChanged` event to `CardManager` to avoid polling.

### 10.4 `DeckViewerUI.cs` (mirrors `deck_viewer.gd`)

Toggle a panel showing remaining deck grouped by suit; use `TextMeshProUGUI` rich text (`<color=red>♥</color>`) in place of Godot BBCode.

### 10.5 `ShopUI.cs` (mirrors `shop.gd`)

Shown on `GameState.GameWon`. Iterates `CardManager.Instance.relicCatalog` and builds a button per `RelicData` (owned / affordable / disabled states). "Continue" → `RunManager.OnShopDone()`.

```csharp
void RefreshItems()
{
    // clear old buttons
    foreach (Transform child in itemsContainer) Destroy(child.gameObject);

    var cm = CardManager.Instance;
    foreach (var relic in cm.relicCatalog)
    {
        bool owned = cm.HasRelic(relic.id);
        bool canAfford = cm.gold >= relic.price;

        var btn = Instantiate(itemButtonPrefab, itemsContainer);
        btn.GetComponentInChildren<TMP_Text>().text =
            $"{relic.icon} {relic.displayName} — {relic.price}g\n{relic.description}";

        btn.interactable = !owned && canAfford;
        btn.onClick.AddListener(() => { if (cm.BuyRelic(relic)) RefreshItems(); });
    }
}
```

### 10.6 `PathScreenUI.cs` (mirrors `path_screen.gd`)

Builds the map from `RunManager.currentPath`:

```csharp
void BuildMap()
{
    // clear old buttons
    foreach (Transform child in mapContainer) Destroy(child.gameObject);

    // draw connections (optional: UI Image lines, or a LineRenderer on an overlay canvas)
    // create a Button per node
    foreach (var node in RunManager.Instance.currentPath)
    {
        var btn = Instantiate(nodeButtonPrefab, mapContainer);
        btn.GetComponentInChildren<TMP_Text>().text =
            node.revealed
                ? $"{RunManager.Instance.GetNodeLabel(node)}\n{RunManager.Instance.GetNodeDesc(node)}"
                : "?\n???";

        btn.transform.localPosition = GridToLocal(node.col, node.row);

        btn.interactable = !node.completed && node.accessible;
        // color: completed → green tint, inaccessible → gray
        int id = node.id;
        btn.onClick.AddListener(() => RunManager.Instance.OnPathChosen(id));
    }
}
```

Header shows `Boss {CurrentCycle} / {BossTotal}`; on `OnRunCompleted` show "VICTORY!".

> **Map coordinates:** `col` = horizontal step (columns), `row` = route lane (rows). Boss sits at `col = routeCount`, `row = center`. Use a local position helper: `pos = offset + new Vector2(col * 200f, row * 120f)` (adjust to your canvas). See the source constants (`COL_SPACING = 200`, `ROW_SPACING = 120`, `MAP_OFFSET = (180,120)`, `NODE_SIZE = (160,60)`).

### 10.7 `MainMenuUI.cs` (mirrors `main_menu.gd`)

Play button → load the game scene (`SceneManager.LoadScene("Game")`); Quit → `Application.Quit()`.

---

## 11. Event/state flow (end-to-end)

1. `MainMenu` → Play → load `Game` scene.
2. `GameController.Start()` → set hand field → `DealHand()` → `RunManager.StartRun()`.
3. `StartRun` → `BuildBossDeck()` → `NextCycle()` → `GeneratePath()` → show path screen ("Boss 1/12").
4. Player picks first node of a route → `OnPathChosen` → mark/reveal/lock/unlock → `StartEncounter`.
5. `CombatManager.StartEnemy` → refill hand → `PlayerTurn`.
6. Player selects a card (Click state) → Play/Discard button → `PlayCard`/`DiscardCard` (suit powers, damage, counter-attack, defend).
7. Win → `GameWon` → shop → buy relics → Continue → `OnShopDone` → next node in route, or (boss done) next boss, or `OnRunCompleted` → "VICTORY!".

---

## 12. Unity-specific pitfalls & notes

- **Reparenting while dragging:** Godot reparents the card to a `CanvasLayer` so it renders above everything. In Unity, set `card.transform.SetParent(topOverlayCanvas.transform)` in `OnBeginDrag`, and set `card.transform.SetAsLastSibling()` (or use a dedicated "drag layer" canvas with a high `sortingOrder`) so it stays on top. Restore to the field on release.
- **Canvas ordering:** give the path screen and shop their own overlay `Canvas` with increasing `sortingOrder` (Godot `CanvasLayer.layer = 10`).
- **Raycast blocking:** set `Image.raycastTarget = true` on cards and `false` on decorative images so `RaycastAll` results are meaningful. Use a `CanvasGroup` with `blocksRaycasts = false` on the card while dragging if its own raycast interferes.
- **Shuffle:** `System.Linq` doesn't provide `Shuffle`; write a Fisher–Yates extension (`List<T>.Shuffle()`). Note Godot's `discard.shuffle()` (hearts power) and `lengths.shuffle()` both use this.
- **Enum iteration:** `Enum.GetValues(typeof(CardData.Suit))` returns untyped values — cast to `(CardData.Suit)` (see `BuildDeck`).
- **ScriptableObject at runtime:** use `ScriptableObject.CreateInstance<T>()` for procedural `CardData`/`EnemyTypeData` (not `new T()`). Runtime-created instances are not saved to disk, which is fine here.
- **Input System:** with the new Input System, `Input.GetMouseButton(0)` may be disabled unless *Active Input Handling* is "Both". Prefer `PointerEventData` passed into handlers (no `Input` calls) for portability.
- **Event subscriptions:** prefer `event Action` over `UnityEvent` for hot paths; unsubscribe in `OnDisable` for UI listeners to avoid leaks across scene loads.
- **Deferred calls:** Godot's `call_deferred("start_run")` maps to `StartCoroutine(... yield return null)` (or `UnityEngine.Object.Destroy` for deferred destruction).

---

## 13. Script inventory (source → target)

| Godot | Unity |
|---|---|
| `card_manager.gd` | `CardManager.cs` |
| `combat_manager.gd` | `CombatManager.cs` |
| `run_manager.gd` | `RunManager.cs` |
| `game.gd` | `GameController.cs` |
| `card/card_data.gd` | `CardData.cs` (ScriptableObject) |
| (relics in `card_manager.gd`) | `RelicData.cs` (ScriptableObject) |
| `card/card.gd` + `state_machine/*` | `CardView.cs` (+ optional `CardState.cs` classes) |
| `field/field.gd` | `Field.cs` |
| `enemy/enemy_type_data.gd` | `EnemyTypeData.cs` (ScriptableObject) |
| `enemy/enemy_data.gd` | `EnemyRuntime.cs` (plain class) |
| `enemy/enemy_ability.gd` | `EnemyAbility.cs` (abstract ScriptableObject) |
| `enemy/enemy_display.gd` | `EnemyDisplayUI.cs` |
| `action_buttons/action_buttons.gd` | `ActionButtonsUI.cs` |
| `deck_viewer/deck_viewer.gd` | `DeckViewerUI.cs` |
| `path_screen/path_screen.gd` | `PathScreenUI.cs` |
| `shop/shop.gd` | `ShopUI.cs` |
| `main_menu/main_menu.gd` | `MainMenuUI.cs` |

Data assets (`example/data/enemies/*.tres`) → ScriptableObject `.asset` instances: `Goblin`, `Thief`, `Knight` (bosses are generated at runtime via `BuildBossDeck`).

---

## 14. Suggested build order

1. Project + Canvas + EventSystem (§3).
2. `CardData`, `EnemyTypeData`, `EnemyRuntime`, `EnemyAbility`, `RelicData` (§4) + the four relic `.asset` files.
3. Singleton managers `CardManager`/`CombatManager`/`RunManager` (§5, 6, 8, 9).
4. `Field` + `CardView` prefabs and drag-and-drop FSM (§7) — verify reordering works standalone.
5. `GameController` + hand dealing.
6. Combat flow + `EnemyDisplayUI` + `ActionButtonsUI`.
7. `PathScreenUI` + `ShopUI` + `DeckViewerUI` + `MainMenuUI`.
8. Boss deck + path generation, then end-to-end polish.
