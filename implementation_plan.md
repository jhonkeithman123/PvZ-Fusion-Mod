# Implementation Plan v2 — PvZ Fusion 4.0.5 "Blooms Mods" Hub (menu + cheats)

## 1. Objective

A standalone MelonLoader mod that provides:

- **Mod menu (hub)** — an on-screen IMGUI window, shown/hidden with **F3**, listing our mods and their features with **clickable toggles**.
- **Blooms Cheats module** — Unlimited Sun (`F1`) and No Cooldown (`F2`), toggleable **both** from the menu and from hotkeys, persisted across restarts.
- **Extensible registry** — future mod DLLs can register themselves into the hub.
- Lists the existing **Blooms_QOL** mod and exposes its toggles.

> v1 (cheat-only) is superseded by this document. The cheat logic is unchanged; the menu is added on top.

---

## 2. Target environment (verified on disk)

| Item | Value |
|---|---|
| Game | `PlantsVsZombiesRH.exe` 4.0.5 (Multi-lang, "by Blooms") |
| Game path | `C:\Users\131fgh\Desktop\PvZ.Fusion.Multi-lang.4.0.5.2.Alpha.by.Blooms\Game Files` |
| Engine | Unity 2022.3.62f1, IL2CPP x64 |
| Mod loader | MelonLoader 0.7.3 Open-Beta (`Runtime Type: net6`) |
| Mod folder | `Game Files\Mods\` (DLLs auto-loaded) |
| Reference assemblies | `Game Files\MelonLoader\Il2CppAssemblies\` (already generated) |
| Build SDK | Windows .NET SDK 10.0.403 (targets net6.0) |

Existing mods in `Mods\`: `Blooms_QOL.dll`, `PvZ_Fusion_Translator.dll`, `AudioImportLib.dll`, `CustomizeLib.MelonLoader.dll`.

---

## 3. Architecture

### 3.1 Recommended: one DLL — `Blooms_Mods.dll`

A single assembly that is both the **hub** and the first **module** ("Blooms Cheats"). No cross-DLL load-order or assembly-resolution risk.

```
Blooms_Mods.dll
├── Core.cs          MelonMod entry: banner, prefs, hotkeys, OnUpdate, OnGUI
├── Registry.cs      BloomsModEntry / BloomsFeature / BloomsMods.Register()  (public API)
├── Menu.cs          IMGUI window rendering
├── BloomsCheats.cs  registers the "Blooms Cheats" module + its patches
└── Patches.cs       Harmony patches (sun / cooldown)
```

Future mods either merge into this DLL or reference it and call `BloomsMods.Register(...)`.

**Alternative (if preferred):** three DLLs — `Blooms_API.dll` (registry contract), `Blooms_ModMenu.dll` (hub), `Blooms_Cheats.dll` (cheat). More files, needs cross-DLL registration; not recommended for a first build.

### 3.2 Registry contract

```csharp
public sealed class BloomsFeature
{
    public string Name;
    public string Description;
    public KeyCode Hotkey;                       // 0 = none
    public MelonPreferences_Entry<bool> Pref;    // persisted backing store
    public Action<bool> OnChanged;               // optional side effect
}

public sealed class BloomsModEntry
{
    public string Name;
    public string Version;
    public string Author;
    public List<BloomsFeature> Features;
}

public static class BloomsMods
{
    public static List<BloomsModEntry> Entries;
    public static void Register(BloomsModEntry entry);
}
```

---

## 4. Menu design (IMGUI via `OnGUI`)

F3 toggles visibility. Window is drawn with `GUI.Window` (draggable, title bar).

```
┌─ Blooms Mods v1.0.0 ─────────────────────────┐
│                                                │
│  Blooms Cheats v1.0.0 — by Blooms              │
│    [✓] Unlimited Sun              [F1]         │
│    [✓] No Cooldown                [F2]         │
│                                                │
│  Blooms QOL v1.0.0 — by cassidy                │
│    [✓] Show Plant HP              [F4]         │
│    [✓] Show Zombie HP             [F5]         │
│    [✓] Hide Metal Objects         [F6]         │
│                                                │
│  Press F3 to close                             │
└────────────────────────────────────────────────┘
```

- Each row is a `GUI.Toggle` bound to the feature's `MelonPreferences_Entry<bool>` — clicking writes the pref immediately (persisted).
- Hotkeys are shown as labels; pressing the hotkey flips the same pref.
- The menu does not pause the game.

---

## 5. Preferences

Category `Blooms_Mods`:

| Entry | Type | Default | Purpose |
|---|---|---|---|
| `UnlimitedSun` | bool | `true` | sun cheat state |
| `NoCooldown` | bool | `true` | cooldown cheat state |
| `SunKey` | KeyCode | `F1` | sun toggle |
| `CooldownKey` | KeyCode | `F2` | cooldown toggle |
| `MenuKey` | KeyCode | `F3` | menu show/hide |

Blooms_QOL features are bound to that mod's existing entries (category `BloomsQOL`: `ShowPlantHP` int, `ShowZombieHP` bool, `HideMetalObjects` bool) so the hub toggles them in place. Caveat: `ShowPlantHP` / `ShowZombieHP` are read at `Board.Start`, so those two apply on the next level/restart; `HideMetalObjects` is read live.

---

## 6. Cheat patches (unchanged from v1)

| Game class | Member | Patch |
|---|---|---|
| `Board` | `Update()` | postfix: `theSun = 99999` (if sun on); `freeCD = true` (if cooldown on) |
| `CardUI` | `Update()` | postfix: `CD = fullCD; isAvailable = true;` (if cooldown on) |
| `InGameTool` | `Update()` | postfix: `CD = fullCD; avaliable = true;` (if cooldown on — covers shovel/glove/hammer/wheel) |

Both the hotkeys and the menu write the same prefs, so all routes stay in sync.

---

## 7. Project layout (new folder)

```
C:\Users\131fgh\Desktop\PvZ_Fusion_Mod\
├── implementation_plan.md      ← this document
├── Blooms_Mods.csproj          (net6.0, AllowUnsafeBlocks, references Il2CppAssemblies + MelonLoader net6)
├── Core.cs
├── Registry.cs
├── Menu.cs
├── BloomsCheats.cs
└── Patches.cs
```

No game files, no existing mod files, and no game data are touched.

---

## 8. Build & install

1. `dotnet build -c Release` (Windows SDK 10.0.403; invoked from WSL as `/mnt/c/Program Files/dotnet/dotnet.exe`).
2. Copy `bin\Release\net6.0\Blooms_Mods.dll` → `Game Files\Mods\`.
3. Launch the game with the existing **Launch Game (Multilang).bat** / `PlantsVsZombiesRH.exe`.

---

## 9. Verification

1. Build succeeds with no errors.
2. Game launches; `MelonLoader\Latest.log` shows the `Blooms Mods v1.0.0` banner and **no Harmony patch errors**.
3. Press **F3** → the menu window appears; drag it; F3 again hides it.
4. Click **Unlimited Sun** in the menu → sun stops being pinned; click again → sun back to 99999.
5. Click **No Cooldown** → seed packets gain their cooldown overlay again; click again → instant replants.
6. Hotkeys F1/F2 still work and the menu checkboxes update to match.
7. Restart the game → toggle states are restored from `MelonPreferences`.
8. Blooms_QOL toggles in the menu flip its features (Hide Metal Objects live; HP toggles on next level).

---

## 10. Risks / notes

- Harmony patching IL2CPP methods is the established pattern for this game (your `Blooms_QOL` does the same).
- If a future game update renames `Board.Update` / `CardUI.Update` / `InGameTool.Update` or the fields, patches fail with a Harmony error in the log (the game keeps running).
- The menu uses IMGUI (`OnGUI`), the same approach as PvZ-Fusion-Helper's Utilities addon — simple and dependency-free.
- Toggling Blooms_QOL's prefs from the hub is convenient but relies on that mod's preference names staying stable.
- The game folder sits on the Desktop (the game's own readme advises against this, but it currently runs fine from there).
- Client-side / single-player only.

---

## 11. Open decisions (confirm or adjust)

- **Assembly name:** proposed `Blooms_Mods.dll` (hub + first module).
- **Author string:** proposed `Blooms` (your existing mod says "by cassidy").
- **Menu position:** proposed top-left, draggable via `GUI.Window`.
- **Blooms_QOL integration:** proposed read/write of its existing prefs; can be reduced to a read-only info listing if you prefer.
