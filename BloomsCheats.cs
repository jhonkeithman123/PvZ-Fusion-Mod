using MelonLoader;

namespace Blooms_Mods
{
    /// <summary>
    /// Registers the mods we are creating into the hub.
    /// </summary>
    public static class BloomsCheats
    {
        public static void Register()
        {
            BloomsMods.Register(new BloomsModEntry("Blooms Cheats", "1.0.0", "Blooms")
            {
                Features =
                {
                    new BloomsFeature(
                        "Unlimited Sun",
                        "Pin sun to 99999",
                        () => Core.UnlimitedSun.Value,
                        value => Core.UnlimitedSun.Value = value,
                        Core.SunKey),

                    new BloomsFeature(
                        "No Cooldown",
                        "No plant or tool cooldowns",
                        () => Core.NoCooldown.Value,
                        value => Core.NoCooldown.Value = value,
                        Core.CooldownKey),
                }
            });
        }

        /// <summary>
        /// Lists the existing Blooms_QOL mod in the hub and exposes its Hide Metal Objects toggle.
        /// Runs after every mod has initialized, so Blooms_QOL owns its preference entries and
        /// this mod only reads them (creating them first would make Blooms_QOL throw).
        /// </summary>
        public static void RegisterQol()
        {
            try
            {
                if (!MelonPreferences.HasEntry("BloomsQOL", "HideMetalObjects"))
                    return;

                MelonPreferences_Entry<bool> hideMetal =
                    MelonPreferences.GetEntry<bool>("BloomsQOL", "HideMetalObjects");

                BloomsMods.Register(new BloomsModEntry("Blooms QOL", "1.0.0", "cassidy")
                {
                    Features =
                    {
                        new BloomsFeature(
                            "Hide Metal Objects",
                            "",
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
