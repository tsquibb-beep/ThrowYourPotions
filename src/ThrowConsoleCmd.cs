using System.Linq;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;

namespace ThrowYourPotions;

/// <summary>
/// Fires the alert on demand, so it can be tuned without walking into a shop each time.
///
/// The dev console discovers commands in loaded mods by reflection, so simply shipping this
/// public class with a parameterless constructor is enough to register `tyt`.
/// </summary>
public class ThrowConsoleCmd : AbstractConsoleCmd
{
    public override string CmdName => "tyt";

    public override string Args => "[all|text|sound|diag|reload|vfx <path>]";

    public override string Description => "Throw Your Potions: fire the alert here and now.";

    /// <summary>Purely local noise and pixels — nothing to synchronise to other players.</summary>
    public override bool IsNetworked => false;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        string mode = args.FirstOrDefault()?.ToLowerInvariant() ?? "all";
        ThrowConfig config = ThrowConfig.Current;

        switch (mode)
        {
            case "reload":
            {
                ThrowConfig fresh = ThrowConfig.Reload();
                return new CmdResult(success: true,
                    $"Config reloaded: text={fresh.ShowText}, sounds={fresh.Sounds} in waves of {fresh.SoundEvents(false).Count} "
                    + $"every {fresh.Wave:0.00}s, hold={fresh.Hold:0.00}s, coins={fresh.CoinExplosion}, wacky={fresh.WackyCase}.");
            }

            case "diag":
                return new CmdResult(success: true, PotionTantrum.Diagnostics());

            case "text":
                ThrowBanner.Show(config);
                return new CmdResult(success: true, "Banner shown.");

            case "sound":
                PotionTantrum.FireSounds(config, fakeMerchant: false);
                return new CmdResult(success: true, $"Playing {config.Sounds} merchant noise(s) ({config.SoundSet}).");

            case "vfx":
            {
                string path = args.Length > 1 ? args[1] : MegaCrit.Sts2.Core.Commands.VfxCmd.coinExplosionJumboPath;
                Godot.Control? container = MegaCrit.Sts2.Core.Nodes.NRun.Instance?.GlobalUi?.AboveTopBarVfxContainer;
                if (container == null)
                {
                    return new CmdResult(success: false, "No run UI to spawn into.");
                }

                ThrowBanner.ShowVfx(container, path);
                return new CmdResult(success: true, $"Spawned '{path}' centre screen — see the log for where it landed.");
            }

            case "all":
                PotionTantrum.FireNow(config, fakeMerchant: false);
                return new CmdResult(success: true, "Throwing a tantrum.");

            default:
                return new CmdResult(success: false, $"Unknown mode '{mode}'. Use: all, text, sound, diag, reload, vfx.");
        }
    }

    public override CompletionResult GetArgumentCompletions(Player? player, string[] args)
    {
        if (args.Length <= 1)
        {
            return CompleteArgument(new[] { "all", "text", "sound", "diag", "reload", "vfx" }, System.Array.Empty<string>(), args.FirstOrDefault() ?? "");
        }

        return new CompletionResult { Type = CompletionType.Argument, ArgumentContext = CmdName };
    }
}
