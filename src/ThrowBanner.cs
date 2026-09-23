using System;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;
using MegaCrit.Sts2.addons.mega_text;

namespace ThrowYourPotions;

/// <summary>
/// The big text. Built imperatively from a stock Godot Label: the mod assembly gets no Godot
/// source generators, so it must not subclass node types, and the game's own MegaRichTextLabel
/// asserts a theme font override in _Ready and throws without one.
/// </summary>
internal static class ThrowBanner
{
    private const string NodeName = "ThrowYourPotionsBanner";

    private const float StartScale = 3.2f;
    private const float StartRotationDegrees = -22f;
    private const double SlamSeconds = 0.40;
    private const double SpinSeconds = 0.50;
    private const double FadeInSeconds = 0.12;
    private const double ImpactAtSeconds = 0.30;
    private const double FadeOutSeconds = 0.45;
    private const float ExitScale = 1.15f;

    /// <summary>Shake is re-kicked this often so it runs for as long as the text is up.</summary>
    private const double ShakeTickSeconds = 0.18;

    /// <summary>Backstop for freeing a splat if its own particles never report finished.</summary>
    private const double VfxCleanupSeconds = 8.0;

    /// <summary>The Foul Potion's own splat — the one effect proven to render in a merchant room.</summary>
    public const string SplatPath = "vfx/vfx_slime_impact";

    private static readonly Random _random = new();

    private static bool _loggedFirstBanner;

    public static void Show(ThrowConfig config)
    {
        Control? container = NRun.Instance?.GlobalUi?.AboveTopBarVfxContainer;
        if (container == null || !GodotObject.IsInstanceValid(container) || !container.IsInsideTree())
        {
            // Half a second has passed since we decided to fire; the run may be over.
            Log.Warn("[ThrowYourPotions] No VFX container (run ended before the banner?); skipping the text.");
            return;
        }

        // Two banners can never stack, however fast the rooms change.
        container.GetNodeOrNull<Label>(NodeName)?.QueueFreeSafely();

        // Splats first, so they land behind the text rather than over it.
        if (config.Splats)
        {
            SplatStorm(container, config);
        }

        var label = new Label
        {
            Name = NodeName,
            Text = config.WackyCase ? WackyCase(config.Text) : config.Text,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.Off,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 100,
            Modulate = new Color(1f, 1f, 1f, 0f),
        };

        Font? font = container.GetThemeFont(ThemeConstants.Label.Font, "Label");
        if (font != null)
        {
            label.AddThemeFontOverride(ThemeConstants.Label.Font, font);
        }

        label.AddThemeFontSizeOverride(ThemeConstants.Label.FontSize, config.Size);
        label.AddThemeColorOverride(ThemeConstants.Label.FontColor, config.TextColor);
        label.AddThemeColorOverride(ThemeConstants.Label.FontOutlineColor, config.OutlineColor);
        label.AddThemeConstantOverride(ThemeConstants.Label.OutlineSize, config.Outline);

        container.AddChildSafely(label);

        // AddChildSafely may defer to the next idle frame, and CreateTween needs the node in the
        // tree, so everything that depends on that waits a frame.
        Callable.From(() => Animate(label, font, config)).CallDeferred();
    }

    private static void Animate(Label label, Font? font, ThrowConfig config)
    {
        if (!GodotObject.IsInstanceValid(label) || !label.IsInsideTree())
        {
            return;
        }

        // Explicit size and position: SetAnchorsPreset only adjusts offsets to preserve the
        // control's current rect, which on a fresh node is 0x0, so it would render nothing.
        Rect2 viewport = label.GetViewportRect();
        label.Size = viewport.Size;
        label.GlobalPosition = viewport.Position;
        label.PivotOffset = label.Size / 2f;

        // Godot 4 has no tweenable "rotation_degrees" property; set degrees, tween radians.
        label.RotationDegrees = StartRotationDegrees;

        if (!_loggedFirstBanner)
        {
            _loggedFirstBanner = true;
            Log.Info($"[ThrowYourPotions] Banner: viewport={viewport.Size}, fontSize={config.Size}, "
                + $"font={(font != null ? "theme" : "Godot default")}.");
        }

        Tween tween = label.CreateTween().SetParallel();

        tween.TweenProperty(label, "scale", Vector2.One, SlamSeconds)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Expo)
            .From(Vector2.One * StartScale);

        // Back/Out overshoots past zero and settles back, so the wobble comes for free.
        tween.TweenProperty(label, "rotation", 0f, SpinSeconds)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);

        tween.TweenProperty(label, "modulate:a", 1f, FadeInSeconds)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad)
            .From(0f);

        if (config.ScreenShake)
        {
            // One shake is over in a moment, so re-kick it on a tick for as long as the text is
            // on screen. The game's own shakes are short by design; this keeps the rumble going
            // from the slam right through to the fade-out.
            double shakeUntil = SpinSeconds + config.Hold + FadeOutSeconds;
            for (double at = ImpactAtSeconds; at < shakeUntil; at += ShakeTickSeconds)
            {
                tween.TweenCallback(Callable.From(Impact)).SetDelay(at);
            }
        }

        tween.Chain();
        tween.TweenInterval(config.Hold);

        tween.Chain();
        tween.TweenProperty(label, "modulate:a", 0f, FadeOutSeconds)
            .SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(label, "scale", Vector2.One * ExitScale, FadeOutSeconds)
            .SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);

        tween.Chain();
        tween.TweenCallback(Callable.From(() => label.QueueFreeSafely()));
    }

    /// <summary>
    /// Slime splats going off around the text for as long as it is up.
    ///
    /// They are scattered on a ring around the centre so they frame the words rather than cover
    /// them, at random sizes, and the timings are jittered so it reads as a barrage rather than a
    /// metronome. The Foul Potion's own splat is used deliberately: it is what the game itself
    /// fires in a merchant room, and it shows the player exactly what they are being told to do.
    /// </summary>
    private static void SplatStorm(Control container, ThrowConfig config)
    {
        SceneTree? tree = container.GetTree();
        if (tree == null)
        {
            return;
        }

        Rect2 viewport = container.GetViewportRect();
        Vector2 centre = viewport.Position + (viewport.Size / 2f);

        // Keep them going from the slam until the text starts to fade.
        double window = Math.Max(0.1, SpinSeconds + config.Hold);
        int count = config.SplatCount;

        for (int i = 0; i < count; i++)
        {
            // Spread evenly over the window, then jitter, so none of it looks scheduled.
            double at = (window * i / Math.Max(1, count)) + (_random.NextDouble() * 0.12);

            double angle = _random.NextDouble() * Math.Tau;
            float radiusX = (float)(viewport.Size.X * (0.16 + (_random.NextDouble() * 0.28)));
            float radiusY = (float)(viewport.Size.Y * (0.14 + (_random.NextDouble() * 0.30)));
            var at2 = new Vector2(
                centre.X + (radiusX * (float)Math.Cos(angle)),
                centre.Y + (radiusY * (float)Math.Sin(angle)));

            float scale = config.SplatScaleMin
                + ((float)_random.NextDouble() * Math.Max(0f, config.SplatScaleMax - config.SplatScaleMin));

            ScheduleOnce(tree, at, () => ShowVfx(container, SplatPath, at2, scale, log: false));
        }
    }

    /// <summary>
    /// Drops a VFX scene on the screen.
    ///
    /// Parented to the merchant room rather than the UI container: that is where the game puts its
    /// own merchant-room effect (FoulPotion's slime splat), and a world-space particle scene does
    /// not reliably render as the child of a UI Control — the coin burst was invisible for exactly
    /// that reason.
    /// </summary>
    public static void ShowVfx(Control container, string vfxPath, Vector2? position = null, float scale = 1f, bool log = true)
    {
        try
        {
            Rect2 viewport = container.GetViewportRect();
            Vector2 at = position ?? viewport.Position + (viewport.Size / 2f);
            Node parent = (Node?)NMerchantRoom.Instance ?? container;
            Node2D? vfx = VfxCmd.PlayNonCombatVfx(parent, at, vfxPath);
            if (vfx == null)
            {
                return;
            }

            // AddChildSafely may defer to the next idle frame, and a GlobalPosition written before
            // the node is in the tree is measured against nothing. Place it once it is in.
            Callable.From(() =>
            {
                if (!GodotObject.IsInstanceValid(vfx) || !vfx.IsInsideTree())
                {
                    return;
                }

                vfx.GlobalPosition = at;
                vfx.Scale = Vector2.One * scale;

                if (log)
                {
                    Log.Info($"[ThrowYourPotions] Vfx '{vfxPath}': at={vfx.GlobalPosition}, wanted={at}, "
                        + $"visible={vfx.Visible}, scale={vfx.Scale}, zIndex={vfx.ZIndex}, "
                        + $"parent={vfx.GetParent()?.Name}, children={vfx.GetChildCount()}.");
                }
            }).CallDeferred();

            // Particle scenes usually free themselves when they finish, but not every one does,
            // so put a backstop on it rather than leaving nodes parked on the room.
            ScheduleOnce(container.GetTree(), VfxCleanupSeconds, () =>
            {
                if (GodotObject.IsInstanceValid(vfx))
                {
                    vfx.QueueFreeSafely();
                }
            });
        }
        catch (Exception ex)
        {
            Log.Warn($"[ThrowYourPotions] Vfx '{vfxPath}' failed: {ex.Message}");
        }
    }

    private static void ScheduleOnce(SceneTree? tree, double seconds, Action action)
    {
        if (tree == null)
        {
            return;
        }

        tree.CreateTimer(Math.Max(0.001, seconds), processAlways: true, processInPhysics: false, ignoreTimeScale: true)
            .Timeout += () =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Log.Warn($"[ThrowYourPotions] Splat step failed: {ex.Message}");
                }
            };
    }

    /// <summary>
    /// rAnDoM cAsE, re-rolled on every showing so it never looks the same twice. Runs of three
    /// identical cases are broken up, otherwise the coin flips clump and it just looks like a typo.
    /// </summary>
    private static string WackyCase(string text)
    {
        char[] chars = text.ToCharArray();
        bool lastUpper = false;
        int run = 0;

        for (int i = 0; i < chars.Length; i++)
        {
            if (!char.IsLetter(chars[i]))
            {
                continue;
            }

            bool upper = _random.Next(2) == 0;
            if (run >= 2 && upper == lastUpper)
            {
                upper = !upper;
            }

            run = upper == lastUpper ? run + 1 : 0;
            lastUpper = upper;
            chars[i] = upper ? char.ToUpperInvariant(chars[i]) : char.ToLowerInvariant(chars[i]);
        }

        return new string(chars);
    }

    private static void Impact()
    {
        try
        {
            NGame.Instance?.ScreenShake(ShakeStrength.Medium, ShakeDuration.Short);
        }
        catch (Exception ex)
        {
            Log.Warn($"[ThrowYourPotions] Screen shake failed: {ex.Message}");
        }
    }
}
