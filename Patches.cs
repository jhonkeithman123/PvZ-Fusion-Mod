using HarmonyLib;
using Il2Cpp;

namespace Blooms_Mods.Patches
{
    /// <summary>
    /// Unlimited sun + native free-cooldown flag.
    /// </summary>
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

    /// <summary>
    /// No plant seed cooldown.
    /// </summary>
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

    /// <summary>
    /// No tool cooldown (shovel / glove / hammer / wheel all inherit InGameTool).
    /// </summary>
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
