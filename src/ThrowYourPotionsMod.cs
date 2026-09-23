using System;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace ThrowYourPotions;

/// <summary>
/// Entry point. The loader finds this via <see cref="ModInitializerAttribute"/> and calls
/// <see cref="Init"/> once at startup.
/// </summary>
[ModInitializer(nameof(Init))]
internal static class ThrowYourPotionsMod
{
    private const string HarmonyId = "tomasapan.ThrowYourPotions";

    private static void Init()
    {
        try
        {
            new Harmony(HarmonyId).PatchAll(Assembly.GetExecutingAssembly());

            // Touch the config here so a bad file shows up in the log at startup rather than
            // halfway through a run.
            ThrowConfig config = ThrowConfig.Current;
            Log.Info($"[ThrowYourPotions] Initialised (enabled={config.Enabled}, text={config.ShowText}, "
                + $"sounds={config.Sounds} @ {config.Gap:0.00}s, set={config.SoundSet}, delay={config.Delay:0.00}s).");
        }
        catch (Exception ex)
        {
            Log.Error($"[ThrowYourPotions] Failed to initialise: {ex}");
        }
    }
}
