using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace ThrowYourPotions;

/// <summary>
/// Decides whether to kick off, and schedules the racket. Nothing here is allowed to throw:
/// this runs inside the game's room-entry path.
/// </summary>
internal static class PotionTantrum
{
    /// <summary>
    /// Rooms already shouted at. Keyed on the room instance so it cannot leak or carry across
    /// runs — arriving is one room object, and re-entering the same merchant later is another.
    /// </summary>
    private static readonly ConditionalWeakTable<AbstractRoom, object> _handled = new();

    private static readonly object _marker = new();

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

            SceneTree? tree = NRun.Instance?.GetTree();
            if (tree == null)
            {
                Log.Warn("[ThrowYourPotions] No run node to schedule against; skipping.");
                return;
            }

            Log.Info($"[ThrowYourPotions] Merchant entered holding {foul} Foul Potion(s); firing in "
                + $"{config.Delay:0.00}s (text={config.ShowText}, sounds={config.Sounds}, fake={fakeMerchant}).");

            if (config.ShowText)
            {
                ScheduleOnce(tree, config.Delay, () => ThrowBanner.Show(config));
            }

            IReadOnlyList<string> events = config.SoundEvents(fakeMerchant);
            for (int i = 0; i < config.Sounds; i++)
            {
                string sfx = events[i % events.Count];
                ScheduleOnce(tree, config.Delay + (i * config.Gap), () => Play(sfx, config.Volume));
            }
        }
        catch (Exception ex)
        {
            Log.Error($"[ThrowYourPotions] Failed on room entry: {ex}");
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
        try
        {
            SfxCmd.Play(sfx, volume);
        }
        catch (Exception)
        {
            // SfxCmd reaches through CombatManager on its way to the audio manager; if that is not
            // ready, go direct rather than losing the noise.
            NAudioManager.Instance?.PlayOneShot(sfx, volume);
        }
    }
}
