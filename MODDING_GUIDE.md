# PvZ Fusion 4.0.5 — MelonLoader Modding Guide (Step by Step)

A complete walkthrough of how the **Blooms Mods** mod was built for *Plants vs. Zombies Fusion 4.0.5 (Multi-lang)*: from a fresh game folder, through decompiling the IL2CPP assemblies, finding the right methods to patch, implementing the mod, building it, and debugging it in a live game.

This guide is written against the exact environment used, so every command, path and version is reproducible.

---

## Table of contents

1. [What we built](#1-what-we-built)
2. [Target environment](#2-target-environment)
3. [Toolchain setup](#3-toolchain-setup)
4. [Understanding the modding surface](#4-understanding-the-modding-surface)
5. [Step 1 — Reconnaissance with `strings`](#5-step-1--reconnaissance-with-strings)
6. [Step 2 — Decompiling with ilspycmd](#6-step-2--decompiling-with-ilspycmd)
7. [Step 3 — Finding which class owns a member (dnfile)](#7-step-3--finding-which-class-owns-a-member-dnfile)
8. [Step 4 — Learning from existing mods](#8-step-4--learning-from-existing-mods)
9. [Step 5 — Choosing the patch targets](#9-step-5--choosing-the-patch-targets)
10. [Step 6 — Project scaffolding](#10-step-6--project-scaffolding)
11. [Step 7 — Implementation (full source)](#11-step-7--implementation-full-source)
12. [Step 8 — Building](#12-step-8--building)
13. [Step 9 — Installing and launching](#13-step-9--installing-and-launching)
14. [Step 10 — Debugging and verification](#14-step-10--debugging-and-verification)
15. [Pitfalls and lessons learned](#15-pitfalls-and-lessons-learned)
16. [Command cheat sheet](#16-command-cheat-sheet)

---

## 1. What we built

A standalone MelonLoader mod (`Blooms_Mods.dll`) that adds:

| Feature | Detail |
|---|---|
| **Mod menu (hub)** | IMGUI window, draggable, toggled with **F3** |
| **Unlimited Sun** | Pins the sun counter to **99999**, toggle **F1** |
| **No Cooldown** | Plant seed packets and tools (shovel/glove/hammer/wheel) never go on cooldown, toggle **F2** |
| **Extensible registry** | Other mods can register themselves into the hub |
| **Blooms QOL integration** | Lists the existing `Blooms_QOL` mod and exposes its *Hide Metal Objects* toggle |
| **Persistence** | All toggles persist via `MelonPreferences` |

The mod is a *separate DLL* — no game files or existing mods were modified.

---

## 2. Target environment

| Item | Value |
|---|---|
| Game | `PlantsVsZombiesRH.exe` 4.0.5 (Multi-lang, "by Blooms") |
| Game folder | `C:\Users\131fgh\Desktop\PvZ.Fusion.Multi-lang.4.0.5.2.Alpha.by.Blooms\Game Files` |
| Engine | Unity **2022.3.62f1c1**, **IL2CPP** x64 |
| Mod loader | **MelonLoader 0.7.3 Open-Beta** (`Runtime Type: net6`) |
| Mod folder | `Game Files\Mods\` (every `.dll` is auto-loaded) |
| Reference assemblies | `Game Files\MelonLoader\Il2CppAssemblies\` (auto-generated on first run) |
| Mod target framework | **.NET 6** (`net6.0`) |
| Build SDK | Windows .NET SDK **10.0.403** at `C:\Program Files\dotnet\dotnet.exe` |
| Working environment | WSL2 (Debian), driving Windows tools via interop |

Everything below assumes the game folder path above; adjust if you move it.

---

## 3. Toolchain setup

### 3.1 .NET SDK (already present)

```
C:\Program Files\dotnet\dotnet.exe --list-sdks
# 10.0.403 [C:\Program Files\dotnet\sdk]
```

The SDK 10 can target `net6.0` — the reference assemblies are pulled from NuGet on first build.

### 3.2 ilspycmd (decompiler)

```
"/mnt/c/Program Files/dotnet/dotnet.exe" tool install -g ilspycmd
# Tool 'ilspycmd' (version '11.2.0.10050') was successfully installed.
```

> **Important:** the `.cmd` shim (`~/.dotnet/tools/ilspycmd.cmd`) does **not** run under WSL bash (`@echo: command not found`). Call the real executable directly:
>
> ```
> ILSPY="/mnt/c/Users/131fgh/.dotnet/tools/.store/ilspycmd/11.2.0.10050/ilspycmd.win-x64/11.2.0.10050/tools/any/win-x64/ilspycmd.exe"
> ```

### 3.3 dnfile (Python .NET metadata reader)

Used to answer "which class declares this method/field?" without decompiling everything.

```
pip install --break-system-packages dnfile      # 0.18.0
```

> The default `python3` in this environment is a Hermes venv **without pip**; `pip` belongs to `/usr/bin/python3`. Use `/usr/bin/python3` for the scripts below.

### 3.4 Reference material

Two open-source mods were used as proven recipes:

- **PVZ-Hyper-Fusion-Mod** — `https://github.com/ArifRios1st/PVZ-Hyper-Fusion-Mod` (`ModFeatures.cs`)
- **PvZ-Fusion-Helper** — `https://github.com/Dynamixus/PvZ-Fusion-Helper` (`Utilities/Patches.cs`, `Utilities/Core.cs`)

---

## 4. Understanding the modding surface

Before writing any code, establish the facts:

```
Game Files\
├── PlantsVsZombiesRH.exe
├── GameAssembly.dll                     ← native IL2CPP binary (59 MB)
├── MelonLoader\
│   ├── net6\                            ← MelonLoader.dll, 0Harmony.dll, Il2CppInterop.*.dll
│   ├── Il2CppAssemblies\                ← generated C# "dummy" assemblies
│   │   ├── Assembly-CSharp.dll          ← ALL game code lives here
│   │   ├── Il2Cppmscorlib.dll
│   │   ├── UnityEngine.*Module.dll
│   │   └── ...
│   └── Latest.log                       ← the log we read while debugging
├── Mods\                                ← drop your mod DLL here
└── UserData\MelonPreferences.cfg        ← persisted mod settings
```

Key facts confirmed from `MelonLoader/Latest.log`:

```
MelonLoader v0.7.3 Open-Beta
Game Type: Il2cpp
Unity Version: 2022.3.62f1c1
Game Version: 4.0.5
Runtime Type: net6
```

**What the "dummy" assemblies are:** MelonLoader's Il2CppAssemblyGenerator runs Cpp2IL over `GameAssembly.dll` and emits C# assemblies with the *correct type, field and method signatures* but **empty method bodies** (each body is a stub that forwards to native code via `il2cpp_runtime_invoke`). That is enough to write Harmony patches against — you get exact names, types and access modifiers — but you cannot read the original game *logic* from them.

---

## 5. Step 1 — Reconnaissance with `strings`

The fastest way to find candidate classes is to grep the metadata of `Assembly-CSharp.dll` for the concept you care about.

```
cd "/mnt/c/Users/131fgh/Desktop/PvZ.Fusion.Multi-lang.4.0.5.2.Alpha.by.Blooms/Game Files/MelonLoader/Il2CppAssemblies"

strings -n 4 Assembly-CSharp.dll | grep -i "sun"   | sort -u
strings -n 4 Assembly-CSharp.dll | grep -i "cooldown" | sort -u
strings -n 4 Assembly-CSharp.dll | grep -i "seed"   | sort -u
```

This produced (abridged):

```
# --- sun ---
get_maxSun
get_startSun
get_theSun
get_SetSun
SetSun_Public_Void_Int32_0
UseSun_Public_Void_Single_0
GetSun_Public_Void_Single_Boolean_0
SunUpdate_Private_Void_0
Board ...

# --- cooldown ---
ResolveCooldown
SetEventCooldown
UpdateCooldownTimer
get_cooldownTimer
get_eventCooldown
CD ...

# --- seed ---
SeedLibrary
CardUI
get_SeedBank
get_seedPool
...
```

`strings` gives you names, not ownership. The next step is to decompile.

---

## 6. Step 2 — Decompiling with ilspycmd

### 6.1 List every class

```
"$ILSPY" -l c Assembly-CSharp.dll > classes.txt
wc -l classes.txt          # 4290 classes
grep -i "Il2Cpp\.\(Board\|CardUI\|InGameTool\|SeedLibrary\)" classes.txt
```

### 6.2 Decompile one type

```
"$ILSPY" -t Il2Cpp.Board Assembly-CSharp.dll   > Board.cs
"$ILSPY" -t Il2Cpp.CardUI Assembly-CSharp.dll  > CardUI.cs
"$ILSPY" -t Il2Cpp.InGameTool Assembly-CSharp.dll > InGameTool.cs
```

A decompiled type looks like this (abridged from `Board`):

```csharp
namespace Il2Cpp;

public class Board : MonoBehaviour
{
    private static readonly IntPtr NativeFieldInfoPtr_theSun;
    private static readonly IntPtr NativeFieldInfoPtr_freeCD;
    private static readonly IntPtr NativeMethodInfoPtr_UseSun_Public_Void_Single_0;

    public unsafe int theSun
    {
        get { ... }
        set { ... }
    }

    public unsafe bool freeCD { get { ... } set { ... } }

    public unsafe void UseSun(float count) { ... }
    public unsafe void SetSun(int count) { ... }
    public unsafe void GetSun(float count, bool save = true) { ... }
    public unsafe void SunUpdate() { ... }
}
```

That is exactly what we need: **exact member names, types, and whether they are public/private, virtual/non-virtual**.

> **Path gotcha:** ilspycmd is a *Windows* process. `-o /mnt/c/...` silently produces **zero files** (WSL does not translate output paths). Always pass a Windows path:
>
> ```
> "$ILSPY" -o 'C:\Users\131fgh\AppData\Local\Temp\pvz_decomp' -p Mods\Blooms_QOL.dll
> ```

---

## 7. Step 3 — Finding which class owns a member (dnfile)

`strings` showed `ResolveCooldown`, `SetCooldown`, `UpdateCooldownTimer`, `cooldown`, `CD` — but not *which class* declares them. Rather than decompile all 4290 classes, enumerate the metadata directly:

```python
# /usr/bin/python3
import dnfile

DLL = r"C:\Users\131fgh\Desktop\PvZ.Fusion.Multi-lang.4.0.5.2.Alpha.by.Blooms\Game Files\MelonLoader\Il2CppAssemblies\Assembly-CSharp.dll"
pe = dnfile.dnPE(DLL)

hits = {}
for td in pe.net.mdtables.TypeDef:
    tname = str(td.TypeName)
    try:
        methods = list(td.MethodList)
    except Exception:
        methods = []
    for m in methods:
        try:
            mname = str(m.row.Name)
        except Exception:
            continue
        low = mname.lower()
        if "cooldown" in low or "sun" in low:
            hits.setdefault(tname, []).append(mname)

for t, ms in sorted(hits.items()):
    print(t)
    for m in sorted(set(ms)):
        print("   ", m)
```

This produced the decisive table (abridged):

```
Board               GetSun, OnGetSun, SetSun, SunUpdate, UseSun,
                    get_theSun, get_maxSun, get_extraSun, set_theSun, ...
BoardData           get_theSun, set_theSun, ...
GameLose            SetCooldown, UpdateCooldownTimer, get_cooldownTimer, ...
InGameTool          ResolveCooldown, SetEventCooldown, get_eventCooldown, ...
InGameUI            UpdateSunDisplay, get_sun, set_sun, ...
LevelData           get_theSun, set_theSun
...
```

`GameLose` turned out to be the *game-over* logic (its `cooldownTimer` is the lose-condition timer) — a good reminder that **names can lie**; always confirm by decompiling.

### 7.1 Finding inheritance

To know whether patching a base class covers subclasses, dump the `Extends` table:

```python
for td in pe.net.mdtables.TypeDef:
    ext = td.Extends
    if ext is None: continue
    base = str(ext.row.TypeName)
    if base in {"InGameTool", "CardUI", "Board"}:
        print(f"{base}  <-  {td.TypeName}")
```

Result:

```
CardUI     <-  DroppedCard
CardUI     <-  IZECard
CardUI     <-  SpecialCard
InGameTool <-  Glove
InGameTool <-  Hammer
InGameTool <-  Shovel
InGameTool <-  Wheel
```

So one patch on `InGameTool.Update` covers **all four tools**.

---

## 8. Step 4 — Learning from existing mods

Decompile the mods that are already in `Mods\` — they show the exact idioms that work with this loader:

```
"$ILSPY" -o 'C:\Users\131fgh\AppData\Local\Temp\decomp_tmp' -p Mods\Blooms_QOL.dll
```

`Blooms_QOL` uses `[HarmonyPatch(typeof(ItemManager))]`, `[HarmonyPatch("SetBucket")]`, `[HarmonyPostfix]` — and **never calls `Harmony.PatchAll()`**. That is the key discovery:

> **MelonLoader automatically applies `[HarmonyPatch]` attributes.** Confirmed by decompiling `MelonLoader.dll` (`HarmonyPatchAll`, `TryPatchAll` are present in the loader). No manual patch call is needed.

The two reference mods from GitHub gave the community-proven recipes:

```csharp
// PVZ-Hyper-Fusion-Mod / ModFeatures.cs  (older 2.1.x version)
[HarmonyPatch(typeof(CardUI))]
public static class CardUI_Patch
{
    [HarmonyPostfix]
    [HarmonyPatch("Update")]
    private static void Update(CardUI __instance)
    {
        if (noCooldown)
        {
            __instance.CD = __instance.fullCD;
            __instance.isAvailable = true;
        }
    }
}

[HarmonyPatch(typeof(Board))]
public static class Board_Patch
{
    [HarmonyPostfix]
    [HarmonyPatch("Update")]
    private static void Update(Board __instance)
    {
        if (unlimitedSun)
            __instance.theSun = 10000;
        __instance.freeCD = noCooldown;
    }
}

// Dynamixus/PvZ-Fusion-Helper / Utilities/Patches.cs  (current version)
//   Board postfix:   theSun = 99999;  freeCD = NoCooldown;
//   CardUI postfix:  CD = fullCD;    isAvailable = true;
//   GloveMgr/HammerMgr prefix on CDUpdate (older class names)
```

We adapted these to the class names that actually exist in 4.0.5 (`GloveMgr`/`HammerMgr` do not exist; `InGameTool` is the tool base class).

---

## 9. Step 5 — Choosing the patch targets

Final verified target table (all members confirmed present in `Assembly-CSharp.dll`):

| Game class | Member | Kind | Purpose |
|---|---|---|---|
| `Board` | `Update()` | public, non-virtual | per-frame hook |
| `Board` | `theSun` (int) | field | current sun |
| `Board` | `maxSun` (int) | field | sun cap |
| `Board` | `freeCD` (bool) | field | **native** "no cooldown" debug flag |
| `Board` | `UseSun(float)`, `SetSun(int)`, `GetSun(float,bool)` | methods | sun economy |
| `CardUI` | `Update()` | public **virtual** | plant seed packet per-frame hook |
| `CardUI` | `CD`, `fullCD` (float) | fields | cooldown remaining / total |
| `CardUI` | `isAvailable` (bool) | field | whether the packet can be used |
| `InGameTool` | `Update()` | public, non-virtual | tool base class per-frame hook |
| `InGameTool` | `CD`, `fullCD` (float) | fields | tool cooldown |
| `InGameTool` | `avaliable` (bool) | field | tool availability *(sic — misspelled in game code)* |

**Semantics note:** in this game `CD` behaves as *progress towards* `fullCD` (the reference mods set `CD = fullCD` to mean "ready"), and `isAvailable`/`avaliable` are the authoritative "can I use this" flags. Setting both is what makes the packet instantly reusable.

---

## 10. Step 6 — Project scaffolding

Create `C:\Users\131fgh\Desktop\PvZ_Fusion_Mod\` with this layout:

```
PvZ_Fusion_Mod\
├── Blooms_Mods.csproj
├── Core.cs
├── Registry.cs
├── Menu.cs
├── BloomsCheats.cs
└── Patches.cs
```

### 10.1 `Blooms_Mods.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <ImplicitUsings>disable</ImplicitUsings>
    <Nullable>disable</Nullable>
    <RootNamespace>Blooms_Mods</RootNamespace>
    <AssemblyName>Blooms_Mods</AssemblyName>
    <LangVersion>latest</LangVersion>
    <IsPackable>false</IsPackable>
    <Version>1.0.0</Version>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <EnableDefaultCompileItems>true</EnableDefaultCompileItems>
    <GamePath Condition="'$(GamePath)' == ''">C:\Users\131fgh\Desktop\PvZ.Fusion.Multi-lang.4.0.5.2.Alpha.by.Blooms\Game Files</GamePath>
  </PropertyGroup>

  <ItemGroup>
    <Reference Include="Assembly-CSharp">
      <HintPath>$(GamePath)\MelonLoader\Il2CppAssemblies\Assembly-CSharp.dll</HintPath>
    </Reference>
    <Reference Include="Il2Cppmscorlib">
      <HintPath>$(GamePath)\MelonLoader\Il2CppAssemblies\Il2Cppmscorlib.dll</HintPath>
    </Reference>
    <Reference Include="Il2CppSystem">
      <HintPath>$(GamePath)\MelonLoader\Il2CppAssemblies\Il2CppSystem.dll</HintPath>
    </Reference>
    <Reference Include="Il2CppSystem.Core">
      <HintPath>$(GamePath)\MelonLoader\Il2CppAssemblies\Il2CppSystem.Core.dll</HintPath>
    </Reference>
    <Reference Include="UnityEngine">
      <HintPath>$(GamePath)\MelonLoader\Il2CppAssemblies\UnityEngine.dll</HintPath>
    </Reference>
    <Reference Include="UnityEngine.CoreModule">
      <HintPath>$(GamePath)\MelonLoader\Il2CppAssemblies\UnityEngine.CoreModule.dll</HintPath>
    </Reference>
    <Reference Include="UnityEngine.IMGUIModule">
      <HintPath>$(GamePath)\MelonLoader\Il2CppAssemblies\UnityEngine.IMGUIModule.dll</HintPath>
    </Reference>
    <Reference Include="UnityEngine.InputLegacyModule">
      <HintPath>$(GamePath)\MelonLoader\Il2CppAssemblies\UnityEngine.InputLegacyModule.dll</HintPath>
    </Reference>
    <Reference Include="MelonLoader">
      <HintPath>$(GamePath)\MelonLoader\net6\MelonLoader.dll</HintPath>
    </Reference>
    <Reference Include="0Harmony">
      <HintPath>$(GamePath)\MelonLoader\net6\0Harmony.dll</HintPath>
    </Reference>
    <Reference Include="Il2CppInterop.Runtime">
      <HintPath>$(GamePath)\MelonLoader\net6\Il2CppInterop.Runtime.dll</HintPath>
    </Reference>
    <Reference Include="Il2CppInterop.Common">
      <HintPath>$(GamePath)\MelonLoader\net6\Il2CppInterop.Common.dll</HintPath>
    </Reference>
  </ItemGroup>

</Project>
```

Only the references you actually use are required; these are the ones this mod needs.

---

## 11. Step 7 — Implementation (full source)

### 11.1 `Core.cs` — entry point, preferences, hotkeys

```csharp
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(Blooms_Mods.Core), "Blooms Mods", "1.0.0", "Blooms", null)]
[assembly: MelonGame("LanPiaoPiao", "PlantsVsZombiesRH")]

namespace Blooms_Mods
{
    public class Core : MelonMod
    {
        public static MelonPreferences_Category Prefs;

        // Blooms Cheats
        public static MelonPreferences_Entry<bool> UnlimitedSun;
        public static MelonPreferences_Entry<bool> NoCooldown;

        // Keybinds
        public static MelonPreferences_Entry<KeyCode> SunKey;
        public static MelonPreferences_Entry<KeyCode> CooldownKey;
        public static MelonPreferences_Entry<KeyCode> MenuKey;
        public static MelonPreferences_Entry<bool> DebugLog;

        public static bool MenuVisible;

        public override void OnInitializeMelon()
        {
            Prefs = MelonPreferences.CreateCategory("Blooms_Mods");

            UnlimitedSun = Prefs.CreateEntry("UnlimitedSun", true);
            NoCooldown   = Prefs.CreateEntry("NoCooldown", true);

            SunKey      = Prefs.CreateEntry("SunKey", KeyCode.F1);
            CooldownKey = Prefs.CreateEntry("CooldownKey", KeyCode.F2);
            MenuKey     = Prefs.CreateEntry("MenuKey", KeyCode.F3);
            DebugLog    = Prefs.CreateEntry("DebugLog", false, "Debug logging");

            BloomsCheats.Register();

            MelonLogger.Msg("Blooms Mods v1.0.0 loaded.");
        }

        public override void OnLateInitializeMelon()
        {
            BloomsCheats.RegisterQol();
            MelonLogger.Msg("Blooms Mods: hub entries = " + BloomsMods.Entries.Count);
        }

        public override void OnUpdate()
        {
            if (Input.GetKeyDown(MenuKey.Value))
                MenuVisible = !MenuVisible;

            foreach (BloomsModEntry mod in BloomsMods.Entries)
            {
                foreach (BloomsFeature feature in mod.Features)
                {
                    if (feature == null || feature.HotkeyPref == null || feature.HotkeyPref.Value == KeyCode.None)
                        continue;

                    if (Input.GetKeyDown(feature.HotkeyPref.Value))
                    {
                        bool newValue = !feature.IsEnabled();
                        feature.SetEnabled(newValue);
                        MelonLogger.Msg(feature.Name + ": " + (newValue ? "ON" : "OFF"));
                    }
                }
            }
        }

        public override void OnGUI()
        {
            if (MenuVisible)
                Menu.Render();
        }
    }
}
```

### 11.2 `Registry.cs` — the hub contract

```csharp
using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;

namespace Blooms_Mods
{
    public sealed class BloomsFeature
    {
        public string Name;
        public string Description;
        public Func<bool> IsEnabled;
        public Action<bool> SetEnabled;
        public MelonPreferences_Entry<KeyCode> HotkeyPref;

        public BloomsFeature(string name, string description, Func<bool> isEnabled,
                             Action<bool> setEnabled, MelonPreferences_Entry<KeyCode> hotkeyPref = null)
        {
            Name = name; Description = description;
            IsEnabled = isEnabled; SetEnabled = setEnabled; HotkeyPref = hotkeyPref;
        }

        public string HotkeyLabel =>
            (HotkeyPref == null || HotkeyPref.Value == KeyCode.None) ? string.Empty : " [" + HotkeyPref.Value + "]";
    }

    public sealed class BloomsModEntry
    {
        public string Name;
        public string Version;
        public string Author;
        public List<BloomsFeature> Features = new List<BloomsFeature>();

        public BloomsModEntry(string name, string version, string author)
        { Name = name; Version = version; Author = author; }
    }

    public static class BloomsMods
    {
        public static readonly List<BloomsModEntry> Entries = new List<BloomsModEntry>();

        public static void Register(BloomsModEntry entry)
        {
            if (entry == null) return;
            if (!Entries.Contains(entry)) Entries.Add(entry);
        }
    }
}
```

### 11.3 `Patches.cs` — the actual cheats

```csharp
using HarmonyLib;
using Il2Cpp;

namespace Blooms_Mods.Patches
{
    [HarmonyPatch(typeof(Board))]
    public static class BoardPatch
    {
        private static int logCounter;

        [HarmonyPostfix]
        [HarmonyPatch("Update")]
        public static void Post_Update(Board __instance)
        {
            if (Core.UnlimitedSun.Value)
                __instance.theSun = 99999;

            __instance.freeCD = Core.NoCooldown.Value;

            if (Core.DebugLog.Value && logCounter++ % 300 == 0)
                MelonLoader.MelonLogger.Msg("[Blooms Mods] Board: theSun=" + __instance.theSun + " freeCD=" + __instance.freeCD);
        }
    }

    [HarmonyPatch(typeof(CardUI))]
    public static class CardUIPatch
    {
        private static int logCounter;

        [HarmonyPostfix]
        [HarmonyPatch("Update")]
        public static void Post_Update(CardUI __instance)
        {
            if (Core.NoCooldown.Value)
            {
                __instance.CD = __instance.fullCD;
                __instance.isAvailable = true;
            }

            if (Core.DebugLog.Value && logCounter++ % 300 == 0)
                MelonLoader.MelonLogger.Msg("[Blooms Mods] CardUI: CD=" + __instance.CD + " fullCD=" + __instance.fullCD + " isAvailable=" + __instance.isAvailable);
        }
    }

    [HarmonyPatch(typeof(InGameTool))]
    public static class InGameToolPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch("Update")]
        public static void Post_Update(InGameTool __instance)
        {
            if (Core.NoCooldown.Value)
            {
                __instance.CD = __instance.fullCD;
                __instance.avaliable = true;
            }
        }
    }
}
```

### 11.4 `BloomsCheats.cs` — module registration

```csharp
using MelonLoader;

namespace Blooms_Mods
{
    public static class BloomsCheats
    {
        public static void Register()
        {
            BloomsMods.Register(new BloomsModEntry("Blooms Cheats", "1.0.0", "Blooms")
            {
                Features =
                {
                    new BloomsFeature("Unlimited Sun", "Pin sun to 99999",
                        () => Core.UnlimitedSun.Value,
                        value => Core.UnlimitedSun.Value = value,
                        Core.SunKey),

                    new BloomsFeature("No Cooldown", "No plant or tool cooldowns",
                        () => Core.NoCooldown.Value,
                        value => Core.NoCooldown.Value = value,
                        Core.CooldownKey),
                }
            });
        }

        // Runs in OnLateInitializeMelon, AFTER every mod has created its preferences.
        public static void RegisterQol()
        {
            try
            {
                if (!MelonPreferences.HasEntry("BloomsQOL", "HideMetalObjects"))
                    return;                                   // Blooms_QOL not installed

                MelonPreferences_Entry<bool> hideMetal =
                    MelonPreferences.GetEntry<bool>("BloomsQOL", "HideMetalObjects");

                BloomsMods.Register(new BloomsModEntry("Blooms QOL", "1.0.0", "cassidy")
                {
                    Features =
                    {
                        new BloomsFeature("Hide Metal Objects", "",
                            () => hideMetal.Value,
                            value => hideMetal.Value = value),
                    }
                });
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning("Blooms Mods: could not hook Blooms QOL - " + ex.Message);
            }
        }
    }
}
```

### 11.5 `Menu.cs` — IMGUI panel

```csharp
using UnityEngine;

namespace Blooms_Mods
{
    public static class Menu
    {
        private const float Width = 360f;
        private const float RowHeight = 20f;
        private const float TitleHeight = 24f;

        private static Rect windowRect = new Rect(20f, 20f, Width, 220f);
        private static bool dragging;
        private static Vector2 dragOffset;

        public static void Render()
        {
            float height = TitleHeight + 6f;
            foreach (BloomsModEntry mod in BloomsMods.Entries)
            {
                if (mod == null) continue;
                height += RowHeight;
                if (mod.Features != null) height += mod.Features.Count * RowHeight;
                height += 6f;
            }
            height += RowHeight;

            Rect rect = new Rect(windowRect.x, windowRect.y, Width, height);

            GUI.Box(rect, string.Empty, GUI.skin.box);
            GUI.Label(new Rect(rect.x + 8f, rect.y + 3f, rect.width - 16f, 20f),
                      "Blooms Mods v1.0.0", GUI.skin.label);

            float y = rect.y + TitleHeight;

            foreach (BloomsModEntry mod in BloomsMods.Entries)
            {
                if (mod == null || mod.Name == null) continue;

                GUI.Label(new Rect(rect.x + 8f, y, rect.width - 16f, 18f),
                          mod.Name + " v" + mod.Version + "  -  " + mod.Author, GUI.skin.label);
                y += RowHeight;

                if (mod.Features == null) continue;

                foreach (BloomsFeature feature in mod.Features)
                {
                    if (feature == null || feature.Name == null) continue;

                    bool value = feature.IsEnabled();
                    bool newValue = GUI.Toggle(new Rect(rect.x + 8f, y, rect.width - 16f, 18f),
                                               value, feature.Name + feature.HotkeyLabel);
                    if (newValue != value)
                        feature.SetEnabled(newValue);
                    y += RowHeight;
                }
                y += 6f;
            }

            GUI.Label(new Rect(rect.x + 8f, y, rect.width - 16f, 18f),
                      "Press F3 to close", GUI.skin.label);

            HandleDrag(rect);
        }

        private static void HandleDrag(Rect rect)
        {
            Event e = Event.current;
            if (e == null) return;

            Rect titleRect = new Rect(rect.x, rect.y, rect.width, TitleHeight);

            if (e.type == EventType.MouseDown && titleRect.Contains(e.mousePosition))
            {
                dragging = true;
                dragOffset = new Vector2(e.mousePosition.x - rect.x, e.mousePosition.y - rect.y);
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && dragging)
            {
                windowRect.x = e.mousePosition.x - dragOffset.x;
                windowRect.y = e.mousePosition.y - dragOffset.y;
                e.Use();
            }
            else if (e.type == EventType.MouseUp)
            {
                dragging = false;
            }
        }
    }
}
```

---

## 12. Step 8 — Building

```
cd "/mnt/c/Users/131fgh/Desktop/PvZ_Fusion_Mod"
"/mnt/c/Program Files/dotnet/dotnet.exe" build -c Release
```

Expected output:

```
Blooms_Mods -> C:\Users\131fgh\Desktop\PvZ_Fusion_Mod\bin\Release\net6.0\Blooms_Mods.dll
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

If `net6.0` targeting fails, the SDK restores `Microsoft.NETCore.App.Ref` from NuGet automatically (network required on first build).

---

## 13. Step 9 — Installing and launching

### 13.1 Install

```
cp "/mnt/c/Users/131fgh/Desktop/PvZ_Fusion_Mod/bin/Release/net6.0/Blooms_Mods.dll" \
   "/mnt/c/Users/131fgh/Desktop/PvZ.Fusion.Multi-lang.4.0.5.2.Alpha.by.Blooms/Game Files/Mods/"
```

MelonLoader loads every DLL in `Mods\` at startup — no registration, no config.

### 13.2 Launch

From WSL, `cmd.exe /c start ...` **hangs** (it does not return). Use PowerShell instead:

```powershell
powershell.exe -NoProfile -Command "Start-Process -FilePath 'C:\Users\131fgh\Desktop\PvZ.Fusion.Multi-lang.4.0.5.2.Alpha.by.Blooms\Game Files\PlantsVsZombiesRH.exe' -WorkingDirectory 'C:\Users\131fgh\Desktop\PvZ.Fusion.Multi-lang.4.0.5.2.Alpha.by.Blooms\Game Files'"
```

### 13.3 Read the log

```
tail -60 "Game Files/MelonLoader/Latest.log"
grep -n -i "blooms mods\|error\|exception" "Game Files/MelonLoader/Latest.log"
```

You are looking for:

```
Blooms Mods v1.0.0
by Blooms
Assembly: Blooms_Mods.dll
[Blooms Mods] Blooms Mods v1.0.0 loaded.
[Blooms Mods] Blooms Mods: hub entries = 2
```

…and **no** Harmony patch errors.

---

## 14. Step 10 — Debugging and verification

### 14.1 The three log sources

| Log | Path | What it shows |
|---|---|---|
| MelonLoader log | `Game Files\MelonLoader\Latest.log` | mod loading, mod messages, Harmony errors |
| Unity player log | `%USERPROFILE%\AppData\LocalLow\LanPiaoPiao\PlantsVsZombiesRH\Player.log` | game's own `Debug.Log` output, game state |
| Preferences | `Game Files\UserData\MelonPreferences.cfg` | persisted mod settings |

### 14.2 Prove the patches fire (debug logging)

Add a **throttled** log inside the patch itself, gated by a preference:

```csharp
private static int logCounter;

// inside BoardPatch.Post_Update
if (Core.DebugLog.Value && logCounter++ % 300 == 0)
    MelonLogger.Msg("[Blooms Mods] Board: theSun=" + __instance.theSun + " freeCD=" + __instance.freeCD);
```

Set `DebugLog = true` in `MelonPreferences.cfg`, launch, and watch:

```
[Blooms Mods] Board: theSun=99999 freeCD=True
[Blooms Mods] CardUI: CD=7.5 fullCD=7.5 isAvailable=True
[Blooms Mods] CardUI: CD=300 fullCD=300 isAvailable=True
```

That is direct evidence that (a) the patch is applied, (b) it runs every frame, and (c) it has the intended effect.

> **Throttle carefully.** `CardUI.Update` runs once per card; a shared static counter means the log fires every `300 / cardCount` frames. With ~100 seed packets that is still ~20 lines/second. Never log unconditionally in a per-frame patch.

### 14.3 Isolating "is it my mod?"

When something misbehaves:

1. **Read the log first** — Harmony errors and exceptions land there.
2. **Check whether the main loop is alive.** If the game's per-frame debug log keeps printing every 5 s, the main thread is *not* blocked.
3. **Check the process:** `Get-Process PlantsVsZombiesRH | Select Id,CPU,Responding`.
   - `Responding = True` + rising CPU → main thread is running.
   - `Responding = False` → the window message loop is blocked.
4. **Bisect by disabling.** Move `Blooms_Mods.dll` out of `Mods\`, relaunch, reproduce. If the problem persists → not your mod. If it disappears → your mod.
5. **Narrow further** by building a variant with only the menu (no Harmony patches), then only the patches (no menu).

### 14.4 The hang we investigated

Symptom: the game appeared to freeze when starting *Survival Endless*; the user reported that sending **Ctrl+C in the game's MelonLoader console** made it continue.

What we checked:

| Check | Result |
|---|---|
| MelonLoader log | `Board: theSun=99999 freeCD=True` every 5 s, **no gaps** → main thread was running |
| Unity `Player.log` | game kept loading level instances and saving; no exceptions |
| Process | `Responding = True`, CPU rising |
| Console input in MelonLoader | none — decompiled `MelonLoader.dll` has **no `Console.ReadLine`** |
| Console input in the game | none — no `ReadLine` in `Assembly-CSharp.dll` metadata |

Conclusion: the game loop was **not** blocked; the "hang" was most likely a console/QuickEdit artifact or a coincidence. The reproducible detector we established is: *if the 5-second `Board:` debug line stops appearing, the main thread is genuinely blocked* — capture a dump at that moment.

---

## 15. Pitfalls and lessons learned

| # | Pitfall | What happened | Fix |
|---|---|---|---|
| 1 | **`MelonPreferences.CreateEntry` throws if the entry exists** | Our mod created `BloomsQOL` prefs first; `Blooms_QOL` then threw `Calling CreateEntry for ShowPlantHP when it Already Exists` and its init aborted | Never create another mod's prefs. Call `MelonPreferences.HasEntry(...)` first and use `GetEntry<T>(...)`, ideally in `OnLateInitializeMelon` after the owner has initialized |
| 2 | **`GUI.Window` needs an Il2Cpp delegate** | `GUI.WindowFunction` is an `Il2CppSystem.MulticastDelegate`; a C# method group cannot convert to it → CS1503 | Draw the panel manually with `GUI.Box` / `GUI.Label` / `GUI.Toggle` and implement dragging via `Event.current` |
| 3 | **`GUIStyle` copy constructor is missing** | `new GUIStyle(GUI.skin.label)` failed (only `GUIStyle()` and `GUIStyle(IntPtr)` exist) | Use `GUI.skin.label` / `GUI.skin.box` directly |
| 4 | **ilspycmd output path** | `-o /mnt/c/...` produced **0 files** | Pass a Windows path: `-o 'C:\Users\...\Temp\out'` |
| 5 | **ilspycmd `.cmd` shim** | Running `ilspycmd.cmd` under WSL bash fails | Call the `.exe` inside `.dotnet/tools/.store/...` directly |
| 6 | **`cmd.exe /c start` hangs from WSL** | The launch command never returned | Use PowerShell `Start-Process` |
| 7 | **`pip` vs `python3` mismatch** | `python3` is a Hermes venv without pip; `dnfile` installed for a different interpreter | Use `/usr/bin/python3` for dnfile scripts |
| 8 | **Dummy assemblies have no method bodies** | Cannot read game logic, only signatures | Fine for Harmony patches; for real logic you would need Il2CppDumper + Ghidra |
| 9 | **Names can lie** | `GameLose` has a `cooldownTimer` (lose condition, not card cooldown); `InGameTool.avaliable` is misspelled | Always decompile the candidate before patching |
| 10 | **Virtual vs non-virtual targets** | Patching a virtual method only intercepts the base implementation, not overrides | Patch non-virtual hooks (`InGameTool.Update`) that subclasses inherit, or patch each override |
| 11 | **Per-frame logging spam** | Card count × frame rate can produce thousands of lines/second | Always throttle with a static counter and gate behind a debug pref |

---

## 16. Command cheat sheet

```
# --- paths ---
GAME  ="/mnt/c/Users/131fgh/Desktop/PvZ.Fusion.Multi-lang.4.0.5.2.Alpha.by.Blooms/Game Files"
PROJ  ="/mnt/c/Users/131fgh/Desktop/PvZ_Fusion_Mod"
ILSPY ="/mnt/c/Users/131fgh/.dotnet/tools/.store/ilspycmd/11.2.0.10050/ilspycmd.win-x64/11.2.0.10050/tools/any/win-x64/ilspycmd.exe"
DOTNET="/mnt/c/Program Files/dotnet/dotnet.exe"

# --- decompile ---
"$ILSPY" -l c    "$GAME/MelonLoader/Il2CppAssemblies/Assembly-CSharp.dll" > classes.txt
"$ILSPY" -t Il2Cpp.Board      "$GAME/MelonLoader/Il2CppAssemblies/Assembly-CSharp.dll" > Board.cs
"$ILSPY" -t Il2Cpp.CardUI     "$GAME/MelonLoader/Il2CppAssemblies/Assembly-CSharp.dll" > CardUI.cs
"$ILSPY" -o 'C:\Users\131fgh\AppData\Local\Temp\decomp' -p "$GAME/Mods/Blooms_QOL.dll"

# --- string reconnaissance ---
strings -n 4 "$GAME/MelonLoader/Il2CppAssemblies/Assembly-CSharp.dll" | grep -i "sun" | sort -u

# --- build ---
cd "$PROJ" && "$DOTNET" build -c Release

# --- install ---
cp "$PROJ/bin/Release/net6.0/Blooms_Mods.dll" "$GAME/Mods/"

# --- launch ---
powershell.exe -NoProfile -Command "Start-Process -FilePath '$GAME/PlantsVsZombiesRH.exe' -WorkingDirectory '$GAME'"

# --- logs ---
tail -60 "$GAME/MelonLoader/Latest.log"
tail -60 "/mnt/c/Users/131fgh/AppData/LocalLow/LanPiaoPiao/PlantsVsZombiesRH/Player.log"
```

---

*Document generated from the actual session that produced `Blooms_Mods.dll` v1.0.0. All commands and outputs are real, not illustrative.*
