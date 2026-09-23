using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;
using MegaCrit.Sts2.addons.mega_text;

namespace ThrowYourPotions;

/// <summary>
/// The big text: one label per word, slamming in one after another down the screen, each sitting
/// at its own crooked angle.
///
/// Everything is built imperatively — the mod assembly gets no Godot source generators, so it must
/// not subclass node types. It may instantiate the game's own classes, which is how the rich label
/// (and its per-letter colour and jitter effects) is used here.
/// </summary>
internal static class ThrowBanner
{
    private const string NodeName = "ThrowYourPotionsBanner";

    /// <summary>Each word lands from this much oversized.</summary>
    private const float StartScale = 2.4f;

    private const double SlamSeconds = 0.32;
    private const double FadeInSeconds = 0.10;
    private const double FadeOutSeconds = 0.45;
    private const float ExitScale = 1.12f;

    /// <summary>Shake is re-kicked this often so it runs for as long as the text is up.</summary>
    private const double ShakeTickSeconds = 0.18;

    /// <summary>Backstop for freeing a splat if its own particles never report finished.</summary>
    private const double VfxCleanupSeconds = 8.0;

    /// <summary>The Foul Potion's own splat — the one effect proven to render in a merchant room.</summary>
    public const string SplatPath = "vfx/vfx_slime_impact";

    private static readonly Random _random = new();

    /// <summary>
    /// The size-pulse and strobe tweens for the banner currently on screen. They drive the same
    /// properties as the exit animation, so they are killed before it starts rather than left to
    /// fight it. Only one banner exists at a time, so one list is enough.
    /// </summary>
    private static readonly List<Tween> _loops = new();

    /// <summary>The words as laid out, so the strobe can run a light through the whole phrase.</summary>
    private static readonly List<(Control Label, string Text, int FirstLetter)> _lit = new();

    private static bool _loggedFirstBanner;

    /// <summary>
    /// Roughly how long the whole banner lasts, so the noise can be made to last the same.
    /// </summary>
    public static double DurationSeconds(ThrowConfig config)
    {
        int words = Words(config.Text).Length;
        return (Math.Max(0, words - 1) * config.WordStep) + SlamSeconds + config.Hold + FadeOutSeconds;
    }

    public static void Show(ThrowConfig config)
    {
        Control? container = NRun.Instance?.GlobalUi?.AboveTopBarVfxContainer;
        if (container == null || !GodotObject.IsInstanceValid(container) || !container.IsInsideTree())
        {
            // Time has passed since we decided to fire; the run may be over.
            Log.Warn("[ThrowYourPotions] No VFX container (run ended before the banner?); skipping the text.");
            return;
        }

        // Nothing can stack, however fast the rooms change.
        container.GetNodeOrNull<Control>(NodeName)?.QueueFreeSafely();

        // Splats and flash first, so they sit behind the words rather than over them.
        if (config.Splats)
        {
            SplatStorm(container, config);
        }

        if (config.Flash)
        {
            ShowFlash(container, config);
        }

        // One parent holding every word, so the whole lot is freed and replaced as a unit.
        var stack = new Control
        {
            Name = NodeName,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 100,
        };

        container.AddChildSafely(stack);
        Callable.From(() => BuildWords(stack, container, config)).CallDeferred();
    }

    private static string[] Words(string text) =>
        text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// Lays the words out as a stack and animates them in one at a time. Runs deferred, because
    /// AddChildSafely may not have put the parent in the tree yet and CreateTween needs it there.
    /// </summary>
    private static void BuildWords(Control stack, Control container, ThrowConfig config)
    {
        if (!GodotObject.IsInstanceValid(stack) || !stack.IsInsideTree())
        {
            return;
        }

        Rect2 viewport = stack.GetViewportRect();
        stack.Size = viewport.Size;
        stack.GlobalPosition = viewport.Position;

        string[] words = Words(config.WackyCase ? WackyCase(config.Text) : config.Text);
        if (words.Length == 0)
        {
            return;
        }

        // Anything still looping belongs to a banner that is on its way out.
        StopLoops();
        _lit.Clear();

        Font? font = container.GetThemeFont(ThemeConstants.Label.Font, "Label");
        float lineHeight = config.Size * 1.12f;
        float firstLineY = (viewport.Size.Y / 2f) - (((words.Length - 1) * lineHeight) / 2f);

        if (!_loggedFirstBanner)
        {
            _loggedFirstBanner = true;
            Log.Info($"[ThrowYourPotions] Banner: viewport={viewport.Size}, fontSize={config.Size}, "
                + $"words={words.Length}, step={config.WordStep:0.00}s, style={config.Style}, "
                + $"font={(font != null ? "theme" : "Godot default")}.");
        }

        int letters = 0;

        for (int i = 0; i < words.Length; i++)
        {
            Control word = BuildRichWord(container, words[i], font, config)
                ?? BuildPlainWord(words[i], font, config);

            // Where this word's letters sit in the phrase, so the light can run straight through.
            _lit.Add((word, words[i], letters));
            letters += words[i].Length;

            // A band one line tall, centred horizontally, stacked down the screen. The rich label
            // lays its text out from the top of its rect and has no vertical alignment of its own,
            // so the band is what positions it.
            word.Size = new Vector2(viewport.Size.X, lineHeight);
            word.Position = new Vector2(0f, firstLineY + (i * lineHeight) - (lineHeight / 2f));
            word.PivotOffset = word.Size / 2f;
            word.Modulate = new Color(1f, 1f, 1f, 0f);

            // Crooked, alternating side to side so the stack looks hand-thrown rather than tidy.
            float tilt = config.WordTilt * (i % 2 == 0 ? 1f : -1f);
            word.RotationDegrees = tilt + ((float)(_random.NextDouble() - 0.5) * config.WordTilt);

            stack.AddChildSafely(word);

            double at = i * config.WordStep;
            bool last = i == words.Length - 1;
            Control captured = word;
            Callable.From(() => AnimateWord(captured, config, at, last)).CallDeferred();
        }

        if (config.Strobe && _lit.Count > 0 && _lit[0].Label is RichTextLabel)
        {
            StartStrobe(stack, config, letters);
        }
    }

    /// <summary>
    /// Runs a light through the letters, left to right, wrapping round for as long as the banner
    /// is up. The text is static markup, so the movement comes from rewriting each word's markup
    /// on a tick with a different letter picked out — cheap enough for three short words.
    /// </summary>
    private static void StartStrobe(Control stack, ThrowConfig config, int letters)
    {
        if (letters <= 0)
        {
            return;
        }

        int head = 0;
        Tween strobe = stack.CreateTween().SetLoops();
        strobe.TweenCallback(Callable.From(() =>
        {
            head = (head + 1) % letters;

            foreach ((Control label, string text, int firstLetter) in _lit)
            {
                if (!GodotObject.IsInstanceValid(label) || label is not RichTextLabel rich)
                {
                    continue;
                }

                rich.Text = Markup(text, config, head - firstLetter);
            }
        })).SetDelay(config.StrobeStep);

        _loops.Add(strobe);
    }

    /// <summary>Stops the looping strobe and size pulses so they cannot fight the exit animation.</summary>
    private static void StopLoops()
    {
        foreach (Tween loop in _loops)
        {
            if (loop != null && loop.IsValid())
            {
                loop.Kill();
            }
        }

        _loops.Clear();
    }

    /// <summary>
    /// One word: a beat of nothing, then it slams in oversized and settles. The last word to land
    /// owns the hold and takes the whole stack out with it.
    /// </summary>
    private static void AnimateWord(Control word, ThrowConfig config, double delay, bool last)
    {
        if (!GodotObject.IsInstanceValid(word) || !word.IsInsideTree())
        {
            return;
        }

        Tween tween = word.CreateTween().SetParallel();

        tween.TweenProperty(word, "scale", Vector2.One, SlamSeconds)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back)
            .From(Vector2.One * StartScale)
            .SetDelay(delay);

        tween.TweenProperty(word, "modulate:a", 1f, FadeInSeconds)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad)
            .From(0f)
            .SetDelay(delay);

        if (config.ScreenShake)
        {
            // A kick as each word lands, then a rolling rumble for as long as the text is up.
            double until = last ? SlamSeconds + config.Hold : SlamSeconds;
            for (double at = delay; at < delay + until; at += ShakeTickSeconds)
            {
                tween.TweenCallback(Callable.From(Impact)).SetDelay(at);
            }
        }

        // Once it has landed, let it breathe: a size wobble at its own speed, so the three words
        // are never in step with each other.
        if (config.JitterAmount > 0f && config.SizeJitter)
        {
            tween.Chain();
            tween.TweenCallback(Callable.From(() => StartSizePulse(word, config)));
        }

        if (!last)
        {
            return;
        }

        // Take the whole stack out together, once the last word has had its hold.
        tween.Chain();
        tween.TweenInterval(config.Hold);

        tween.Chain();
        tween.TweenCallback(Callable.From(StopLoops));
        tween.Chain();
        Node? stack = word.GetParent();
        foreach (Control sibling in Siblings(stack))
        {
            tween.TweenProperty(sibling, "modulate:a", 0f, FadeOutSeconds)
                .SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Cubic);
            tween.TweenProperty(sibling, "scale", Vector2.One * ExitScale, FadeOutSeconds)
                .SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
        }

        tween.Chain();
        tween.TweenCallback(Callable.From(() =>
        {
            if (stack != null && GodotObject.IsInstanceValid(stack))
            {
                stack.QueueFreeSafely();
            }
        }));
    }

    /// <summary>
    /// A word breathing in and out of size, forever, until the exit kills it. Each word gets a
    /// slightly different period so the stack never pulses as one block.
    /// </summary>
    private static void StartSizePulse(Control word, ThrowConfig config)
    {
        if (!GodotObject.IsInstanceValid(word) || !word.IsInsideTree())
        {
            return;
        }

        double period = config.JitterSeconds * (0.75 + (_random.NextDouble() * 0.5));
        float big = 1f + config.JitterAmount;
        float small = 1f - (config.JitterAmount * 0.6f);

        Tween pulse = word.CreateTween().SetLoops();
        pulse.TweenProperty(word, "scale", Vector2.One * big, period / 2f)
            .SetEase(Tween.EaseType.InOut).SetTrans(Tween.TransitionType.Sine);
        pulse.TweenProperty(word, "scale", Vector2.One * small, period / 2f)
            .SetEase(Tween.EaseType.InOut).SetTrans(Tween.TransitionType.Sine);

        _loops.Add(pulse);
    }

    private static IEnumerable<Control> Siblings(Node? parent) =>
        parent == null ? Enumerable.Empty<Control>() : parent.GetChildren().OfType<Control>();

    /// <summary>
    /// The game's own rich label, which gives us its per-letter effects: colour tags plus
    /// [jitter]/[sine]. It asserts a theme font override in _Ready and throws without one, so the
    /// font goes on before it ever enters the tree. Returns null if anything about it misbehaves,
    /// and the caller falls back to a stock Label.
    /// </summary>
    private static Control? BuildRichWord(Control container, string word, Font? font, ThrowConfig config)
    {
        if (config.Style == "plain")
        {
            return null;
        }

        try
        {
            Font? richFont = container.GetThemeFont(ThemeConstants.RichTextLabel.NormalFont, "RichTextLabel") ?? font;
            if (richFont == null)
            {
                return null;
            }

            var label = new MegaRichTextLabel
            {
                BbcodeEnabled = true,
                AutoSizeEnabled = false,
                FitContent = false,
                ScrollActive = false,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };

            // Must be set before the node enters the tree, or _Ready throws.
            label.AddThemeFontOverride(ThemeConstants.RichTextLabel.NormalFont, richFont);
            label.AddThemeFontSizeOverride(ThemeConstants.RichTextLabel.NormalFontSize, config.Size);
            label.AddThemeColorOverride(ThemeConstants.RichTextLabel.FontOutlineColor, config.OutlineColor);
            label.AddThemeConstantOverride("outline_size", config.Outline);
            label.Text = Markup(word, config);
            return label;
        }
        catch (Exception ex)
        {
            Log.Warn($"[ThrowYourPotions] Rich text unavailable ({ex.Message}); falling back to plain text.");
            return null;
        }
    }

    private static Label BuildPlainWord(string word, Font? font, ThrowConfig config)
    {
        var label = new Label
        {
            Text = word,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.Off,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        if (font != null)
        {
            label.AddThemeFontOverride(ThemeConstants.Label.Font, font);
        }

        label.AddThemeFontSizeOverride(ThemeConstants.Label.FontSize, config.Size);
        label.AddThemeColorOverride(ThemeConstants.Label.FontColor, config.TextColor);
        label.AddThemeColorOverride(ThemeConstants.Label.FontOutlineColor, config.OutlineColor);
        label.AddThemeConstantOverride(ThemeConstants.Label.OutlineSize, config.Outline);
        return label;
    }

    /// <summary>
    /// Wraps the word in the game's own BBCode effects. Each letter is tagged individually rather
    /// than nesting one big [jitter] around a coloured run, so every character reliably picks up
    /// both its colour and its motion.
    /// </summary>
    private static string Markup(string word, ThrowConfig config, int litIndex = -1)
    {
        string[] rainbow = { "red", "orange", "gold", "green", "aqua", "blue", "purple", "pink" };
        string motion = config.Motion is "jitter" or "sine" ? config.Motion : "";
        string litHex = config.StrobeColor.ToHtml(false);

        var built = new StringBuilder("[center]");
        int letter = 0;

        for (int i = 0; i < word.Length; i++)
        {
            char c = word[i];
            bool lit = litIndex >= 0 && i >= litIndex && i < litIndex + config.StrobeLetters;

            // A plain [color] tag for the lit letters, so the light reads as brightness rather
            // than as another hue; everything else takes the chosen style's own tag.
            string open, close;
            if (lit)
            {
                open = $"[color=#{litHex}]";
                close = "[/color]";
            }
            else
            {
                string colour = config.Style switch
                {
                    "rainbow" => rainbow[letter % rainbow.Length],
                    "gold" => "gold",
                    "slime" => "green",
                    _ => "",
                };

                open = colour.Length > 0 ? $"[{colour}]" : "";
                close = colour.Length > 0 ? $"[/{colour}]" : "";
            }

            if (char.IsLetter(c))
            {
                letter++;
            }

            if (motion.Length > 0)
            {
                open += $"[{motion}]";
                close = $"[/{motion}]" + close;
            }

            built.Append(open).Append(c).Append(close);
        }

        return built.Append("[/center]").ToString();
    }

    /// <summary>
    /// The game's full-screen smoky vignette, as used for big AoE cards. It fades itself in and
    /// out and frees itself, so there is nothing to clean up.
    /// </summary>
    private static void ShowFlash(Control container, ThrowConfig config)
    {
        try
        {
            Color tint = config.FlashColor;
            tint.A = 0.45f;
            Color highlight = config.FlashColor.Lightened(0.35f);
            highlight.A = 0.33f;

            NSmokyVignetteVfx? vignette = NSmokyVignetteVfx.Create(tint, highlight);
            if (vignette != null)
            {
                container.AddChildSafely(vignette);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"[ThrowYourPotions] Flash failed: {ex.Message}");
        }
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

        double window = Math.Max(0.1, DurationSeconds(config) - FadeOutSeconds);
        int count = config.SplatCount;

        for (int i = 0; i < count; i++)
        {
            // Spread evenly over the window, then jitter, so none of it looks scheduled.
            double at = (window * i / Math.Max(1, count)) + (_random.NextDouble() * 0.12);

            double angle = _random.NextDouble() * Math.Tau;
            float radiusX = (float)(viewport.Size.X * (0.16 + (_random.NextDouble() * 0.28)));
            float radiusY = (float)(viewport.Size.Y * (0.14 + (_random.NextDouble() * 0.30)));
            var where = new Vector2(
                centre.X + (radiusX * (float)Math.Cos(angle)),
                centre.Y + (radiusY * (float)Math.Sin(angle)));

            float scale = config.SplatScaleMin
                + ((float)_random.NextDouble() * Math.Max(0f, config.SplatScaleMax - config.SplatScaleMin));

            ScheduleOnce(tree, at, () => ShowVfx(container, SplatPath, where, scale, log: false));
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
                    Log.Warn($"[ThrowYourPotions] Scheduled step failed: {ex.Message}");
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
