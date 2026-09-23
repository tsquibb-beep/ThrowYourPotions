using HarmonyLib;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace ThrowYourPotions;

/// <summary>
/// Catches the moment the player arrives in a room.
///
/// This is a PREFIX on purpose. <see cref="Hook.AfterRoomEntered"/> is an async method, so a
/// postfix would run the instant the Task is handed back — near the first await, not when the
/// work finishes. A prefix runs synchronously before the body, which is exactly once per arrival.
/// It returns void and swallows everything downstream, so it cannot alter or break room entry.
/// </summary>
[HarmonyPatch(typeof(Hook), nameof(Hook.AfterRoomEntered))]
internal static class MerchantRoomPatches
{
    [HarmonyPrefix]
    private static void Prefix(IRunState runState, AbstractRoom room)
    {
        PotionTantrum.OnRoomEntered(runState, room);
    }
}
