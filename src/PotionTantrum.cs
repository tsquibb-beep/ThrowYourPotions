using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;

namespace ThrowYourPotions;

/// <summary>
/// Decides whether to kick off, and schedules the racket. Nothing here is allowed to throw:
/// it runs inside the game's room-entry path.
/// </summary>
internal static class PotionTantrum
{
    /// <summary>
    /// Rooms already shouted at. Keyed on the room instance so it cannot leak or carry across
    /// runs — arriving is one room object, and re-entering the same merchant later is another.
    /// </summary>
    private static readonly ConditionalWeakTable<AbstractRoom, object> _handled = new();

    private static readonly object _marker = new();

    private static bool _loggedSoundDiagnostics;

    public static void OnRoomEntered(IRunState? runState, AbstractRoom? room)
    {
        try
        {
            ThrowConfig config = ThrowConfig.Current;
            if (!config.Enabled || room == null || runState == null)
            {
                return;
            }

            if (!IsMerchant(room, out bool fakeMerchant))
            {
                return;
            }

            // Loading a save made inside a shop builds a fresh room object, so it fires again.
            // That is intentional: you have just arrived, and you may well have forgotten again.
            if (_handled.TryGetValue(room, out _))
            {
                return;
            }

            _handled.Add(room, _marker);

            Player? me = LocalPlayer(runState);
            if (me == null)
            {
                return;
            }

            int foul = me.Potions.Count(p => p is FoulPotion);
            if (foul == 0)
            {
                Log.Info("[ThrowYourPotions] Merchant entered with no Foul Potion; staying quiet.");
                return;
            }

            Log.Info($"[ThrowYourPotions] Merchant entered holding {foul} Foul Potion(s); firing in "
                + $"{config.Delay:0.00}s (text={config.ShowText}, sounds={config.Sounds}, fake={fakeMerchant}).");

            FireAfterDelay(config, fakeMerchant, config.Delay);
        }
        catch (Exception ex)
        {
            Log.Error($"[ThrowYourPotions] Failed on room entry: {ex}");
        }
    }

    /// <summary>Everything at once, right now — the console command's entry point.</summary>
    public static void FireNow(ThrowConfig config, bool fakeMerchant) => FireAfterDelay(config, fakeMerchant, 0f);

    private static void FireAfterDelay(ThrowConfig config, bool fakeMerchant, float delay)
    {
        SceneTree? tree = Tree();
        if (tree == null)
        {
            Log.Warn("[ThrowYourPotions] No scene tree to schedule against; skipping.");
            return;
        }

        if (config.ShowText)
        {
            ScheduleOnce(tree, delay, () => ThrowBanner.Show(config));
        }

        ScheduleOnce(tree, delay, () => FireSounds(config, fakeMerchant));
    }

    /// <summary>
    /// Kicks off the noise. Sounds are scheduled from here rather than from the banner so that
    /// they still play with the text turned off.
    /// </summary>
    public static void FireSounds(ThrowConfig config, bool fakeMerchant)
    {
        SceneTree? tree = Tree();
        if (tree == null || config.Sounds <= 0)
        {
            return;
        }

        if (!_loggedSoundDiagnostics)
        {
            _loggedSoundDiagnostics = true;
            Log.Info($"[ThrowYourPotions] Sound check — {Diagnostics()}");
        }

        IReadOnlyList<string> events = config.SoundEvents(fakeMerchant);
        for (int i = 0; i < config.Sounds; i++)
        {
            string sfx = events[i % events.Count];
            ScheduleOnce(tree, i * config.Gap, () => Play(sfx, config.Volume));
        }
    }

    /// <summary>State that would explain silence, printed by `tyt diag` and once per session.</summary>
    public static string Diagnostics()
    {
        string audio;
        try
        {
            audio = NAudioManager.Instance == null ? "NULL" : "ok";
        }
        catch (Exception ex)
        {
            audio = $"threw {ex.GetType().Name}";
        }

        string combat;
        try
        {
            combat = CombatManager.Instance == null ? "NULL" : $"ending={CombatManager.Instance.IsEnding}";
        }
        catch (Exception ex)
        {
            combat = $"threw {ex.GetType().Name}";
        }

        return $"audioManager={audio}, combatManager={combat}, testMode={TestMode.IsOn}, "
            + $"nonInteractive={NonInteractiveMode.IsActive}, tree={(Tree() == null ? "NULL" : "ok")}, "
            + $"volume={ThrowConfig.Current.Volume:0.00}";
    }

    private static SceneTree? Tree()
    {
        try
        {
            return NRun.Instance?.GetTree() ?? NGame.Instance?.GetTree();
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Mirrors the game's own test in <c>FoulPotion.PassesCustomUsabilityCheck</c>: the potion is
    /// throwable at a real merchant, and at the Fake Merchant event. Note the fake one lives in an
    /// EventRoom, so checking RoomType == Shop would miss it.
    /// </summary>
    private static bool IsMerchant(AbstractRoom room, out bool fakeMerchant)
    {
        fakeMerchant = room is EventRoom eventRoom && eventRoom.CanonicalEvent is FakeMerchant;
        return fakeMerchant || room is MerchantRoom;
    }

    /// <summary>
    /// The local player, so co-op shouts at whoever is actually holding the potion. GetMe returns
    /// null without a net id and throws if the player is not in the collection; both mean "not us".
    /// </summary>
    private static Player? LocalPlayer(IRunState runState)
    {
        try
        {
            return LocalContext.GetMe(runState);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Fires an action once, later, on the main thread.
    ///
    /// A SceneTreeTimer rather than async/Task.Delay: Godot node and audio calls must happen on the
    /// main loop, and an exception escaping an async void can take the process down. processAlways
    /// keeps it ticking if a screen pauses the tree, and ignoreTimeScale keeps the gap honest if the
    /// game is running slowed.
    /// </summary>
    private static void ScheduleOnce(SceneTree tree, double seconds, Action action)
    {
        if (seconds <= 0)
        {
            Callable.From(() => Run(action)).CallDeferred();
            return;
        }

        tree.CreateTimer(seconds, processAlways: true, processInPhysics: false, ignoreTimeScale: true)
            .Timeout += () => Run(action);
    }

    private static void Run(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Log.Error($"[ThrowYourPotions] Scheduled step failed: {ex}");
        }
    }

    private static void Play(string sfx, float volume)
    {
        // Go straight to the audio manager. SfxCmd.Play reaches through CombatManager on its way
        // there and silently drops the sound in some states.
        NAudioManager? audio = NAudioManager.Instance;
        if (audio == null)
        {
            Log.Warn("[ThrowYourPotions] No audio manager; sound skipped.");
            return;
        }

        audio.PlayOneShot(sfx, volume);
    }
}
