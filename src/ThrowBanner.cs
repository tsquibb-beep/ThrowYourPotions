using System;
using Godot;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes;
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

        var label = new Label
        {
            Name = NodeName,
            Text = config.Text,
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
            tween.TweenCallback(Callable.From(Impact)).SetDelay(ImpactAtSeconds);
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
