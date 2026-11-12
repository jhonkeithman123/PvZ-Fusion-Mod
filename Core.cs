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
            NoCooldown = Prefs.CreateEntry("NoCooldown", true);

            SunKey = Prefs.CreateEntry("SunKey", KeyCode.F1);
            CooldownKey = Prefs.CreateEntry("CooldownKey", KeyCode.F2);
            MenuKey = Prefs.CreateEntry("MenuKey", KeyCode.F3);
            DebugLog = Prefs.CreateEntry("DebugLog", false, "Debug logging");

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
