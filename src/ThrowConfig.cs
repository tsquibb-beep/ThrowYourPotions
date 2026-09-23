using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using MegaCrit.Sts2.Core.Audio;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Logging;

namespace ThrowYourPotions;

/// <summary>
/// Settings, read once at startup from ThrowYourPotions.config.jsonc. The defaults are the full
/// silly experience; this file exists only so anyone who wants to tone it down can. Missing or
/// malformed files fall back to these values, so the mod never fails to load because of a bad config.
/// </summary>
internal sealed class ThrowConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>Show the big text. Turn off for sound only.</summary>
    [JsonPropertyName("showText")]
    public bool ShowText { get; set; } = true;

    [JsonPropertyName("text")]
    public string Text { get; set; } = "THROW YOUR POTIONS!!";

    /// <summary>A light running through the letters, left to right, over and over.</summary>
    [JsonPropertyName("strobe")]
    public bool Strobe { get; set; } = true;

    /// <summary>How long the light spends on each letter. Lower is faster.</summary>
    [JsonPropertyName("strobeSeconds")]
    public float StrobeSeconds { get; set; } = 0.055f;

    /// <summary>How many letters light up at once as it passes.</summary>
    [JsonPropertyName("strobeWidth")]
    public int StrobeWidth { get; set; } = 2;

    [JsonPropertyName("strobeColor")]
    public string StrobeColorHex { get; set; } = "#ffffff";

    /// <summary>Each word breathing in and out of size while it sits there.</summary>
    [JsonPropertyName("sizeJitter")]
    public bool SizeJitter { get; set; } = true;

    /// <summary>How much bigger and smaller, as a fraction. 0.06 is a 6% wobble.</summary>
    [JsonPropertyName("sizeJitterAmount")]
    public float SizeJitterAmount { get; set; } = 0.06f;

    /// <summary>Seconds for one breath. Each word is given its own speed around this.</summary>
    [JsonPropertyName("sizeJitterSeconds")]
    public float SizeJitterSeconds { get; set; } = 0.32f;

    /// <summary>Each word rocking back and forth around its crooked angle.</summary>
    [JsonPropertyName("rock")]
    public bool Rock { get; set; } = true;

    /// <summary>How far it rocks either way, in degrees.</summary>
    [JsonPropertyName("rockDegrees")]
    public float RockDegrees { get; set; } = 3f;

    /// <summary>Seconds for one rock back and forth. Each word gets its own speed around this.</summary>
    [JsonPropertyName("rockSeconds")]
    public float RockSeconds { get; set; } = 0.55f;

    [JsonPropertyName("fontSize")]
    public int FontSize { get; set; } = 140;

    [JsonPropertyName("textColor")]
    public string TextColorHex { get; set; } = "#fff1c9";

    [JsonPropertyName("outlineColor")]
    public string OutlineColorHex { get; set; } = "#1a0f06";

    [JsonPropertyName("outlineSize")]
    public int OutlineSize { get; set; } = 24;

    /// <summary>How long the text sits there before it fades out.</summary>
    [JsonPropertyName("holdSeconds")]
    public float HoldSeconds { get; set; } = 1.1f;

    [JsonPropertyName("screenShake")]
    public bool ScreenShake { get; set; } = true;

    /// <summary>Slime splats going off around the text — the Foul Potion's own splat effect.</summary>
    [JsonPropertyName("splats")]
    public bool Splats { get; set; } = true;

    /// <summary>How many splats over the life of the banner.</summary>
    [JsonPropertyName("splatCount")]
    public int SplatCount { get; set; } = 14;

    /// <summary>Size range for the splats. The effect is small at 1, so these scale it up.</summary>
    [JsonPropertyName("splatScaleMin")]
    public float SplatScaleMin { get; set; } = 1.4f;

    [JsonPropertyName("splatScaleMax")]
    public float SplatScaleMax { get; set; } = 3.2f;

    /// <summary>
    /// "rainbow" gives every letter its own colour, "gold" the game's gold treatment, "slime"
    /// Foul Potion green, "plain" a single flat colour with no per-letter effects.
    /// </summary>
    [JsonPropertyName("textStyle")]
    public string TextStyle { get; set; } = "gold";

    /// <summary>Per-letter motion: "jitter" shakes, "sine" bobs, "none" holds still.</summary>
    [JsonPropertyName("textMotion")]
    public string TextMotion { get; set; } = "jitter";

    /// <summary>Gap between each word landing, so they stack up one at a time.</summary>
    [JsonPropertyName("wordStepSeconds")]
    public float WordStepSeconds { get; set; } = 0.15f;

    /// <summary>How crooked the words sit, in degrees, alternating side to side.</summary>
    [JsonPropertyName("wordTiltDegrees")]
    public float WordTiltDegrees { get; set; } = 6f;

    /// <summary>Full-screen colour wash as the text lands.</summary>
    [JsonPropertyName("flash")]
    public bool Flash { get; set; } = true;

    [JsonPropertyName("flashColor")]
    public string FlashColorHex { get; set; } = "#4fd14f";

    /// <summary>Random upper/lower case, re-rolled every time, so it never looks the same twice.</summary>
    [JsonPropertyName("wackyCase")]
    public bool WackyCase { get; set; } = true;

    /// <summary>How many merchant noises to play. Ten is the point of the mod.</summary>
    [JsonPropertyName("soundCount")]
    public int SoundCount { get; set; } = 20;

    /// <summary>Gap between the noises within one wave, so they pile on top of each other.</summary>
    [JsonPropertyName("soundGapSeconds")]
    public float SoundGapSeconds { get; set; } = 0.08f;

    /// <summary>
    /// How long before the same noise can be used again. The sound engine will not play a voice
    /// line over itself, so repeats inside this window are silently dropped — hence waves: every
    /// noise he has, all at once, then the whole lot again.
    /// </summary>
    [JsonPropertyName("soundWaveSeconds")]
    public float SoundWaveSeconds { get; set; } = 1.3f;

    [JsonPropertyName("soundVolume")]
    public float SoundVolume { get; set; } = 1.0f;

    /// <summary>Mix the game's gold sounds in with his noises — he is paying you, after all.</summary>
    [JsonPropertyName("moneySounds")]
    public bool MoneySounds { get; set; } = true;

    /// <summary>
    /// "buy" is his purchase noise alone; "mix" cycles every noise he has.
    ///
    /// The sound engine refuses a second instance of the same voice event while the first is still
    /// playing, so "buy" cannot overlap itself — it repeats once per wave instead, and the total is
    /// capped to what fits in the banner's lifetime. "mix" is the denser option because five
    /// distinct noises can sound at once; lower soundWaveSeconds to make "buy" repeat faster.
    /// </summary>
    [JsonPropertyName("soundSet")]
    public string SoundSet { get; set; } = "mix";

    /// <summary>
    /// Wait this long after arriving before going off. Room entry happens behind a fade to black
    /// (0.8s normally, shorter in Fast mode), so firing immediately would play under it.
    /// </summary>
    [JsonPropertyName("delaySeconds")]
    public float DelaySeconds { get; set; } = 0.6f;

    private static ThrowConfig? _current;

    public static ThrowConfig Current => _current ??= Load();

    /// <summary>Re-reads the file, so numbers can be tuned without restarting the game.</summary>
    public static ThrowConfig Reload()
    {
        _current = Load();
        return _current;
    }

    // Clamped accessors: a silly edit should tone the mod down, never wedge the game.
    public int Sounds => Math.Clamp(SoundCount, 0, 50);

    public float Gap => Math.Clamp(SoundGapSeconds, 0.02f, 2f);

    public float Volume => Math.Clamp(SoundVolume, 0f, 1f);

    public float Delay => Math.Clamp(DelaySeconds, 0f, 10f);

    public float Hold => Math.Clamp(HoldSeconds, 0f, 10f);

    public float Wave => Math.Clamp(SoundWaveSeconds, 0.2f, 5f);

    public int Size => Math.Clamp(FontSize, 8, 400);

    public int Outline => Math.Clamp(OutlineSize, 0, 64);

    public Color TextColor => ParseColor(TextColorHex, "#fff1c9");

    public Color OutlineColor => ParseColor(OutlineColorHex, "#1a0f06");

    public Color FlashColor => ParseColor(FlashColorHex, "#4fd14f");

    public string Style => (TextStyle ?? "gold").Trim().ToLowerInvariant();

    public string Motion => (TextMotion ?? "jitter").Trim().ToLowerInvariant();

    public float WordStep => Math.Clamp(WordStepSeconds, 0f, 2f);

    public float WordTilt => Math.Clamp(WordTiltDegrees, 0f, 45f);

    public float StrobeStep => Math.Clamp(StrobeSeconds, 0.01f, 1f);

    public int StrobeLetters => Math.Clamp(StrobeWidth, 1, 12);

    public Color StrobeColor => ParseColor(StrobeColorHex, "#ffffff");

    public float JitterAmount => Math.Clamp(SizeJitterAmount, 0f, 0.5f);

    public float JitterSeconds => Math.Clamp(SizeJitterSeconds, 0.05f, 3f);

    public float RockAmount => Math.Clamp(RockDegrees, 0f, 30f);

    public float RockPeriod => Math.Clamp(RockSeconds, 0.05f, 3f);

    public bool MixSounds => string.Equals(SoundSet?.Trim(), "mix", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The noises, in the order they are played. Cycling different events matters: FMOD will not
    /// play a second instance of the same voice event over itself, so "buy" alone is nearly silent.
    /// </summary>
    public IReadOnlyList<string> SoundEvents(bool fakeMerchant)
    {
        if (!MixSounds)
        {
            return new[] { fakeMerchant ? FmodSfx.fakeMerchantLaugh : FmodSfx.merchantThankYou };
        }

        // Every noise he has, plus the game's three gold sounds. The laugh belongs to the fake
        // merchant but is funny anywhere. Each distinct event is another voice that can sound at
        // the same time as the others, so a wider pool is a denser racket as well as a richer one.
        var pool = new List<string>
        {
            FmodSfx.merchantThankYou,
            FmodSfx.merchantWelcome,
            FmodSfx.merchantPassive,
            FmodSfx.merchantDisappointment,
            FmodSfx.fakeMerchantLaugh,
        };

        if (MoneySounds)
        {
            // Interleaved rather than appended, so the coins land among the gibbering instead of
            // all arriving together at the end of a wave.
            pool.Insert(1, PlayerCmd.goldLargeSfx);
            pool.Insert(3, PlayerCmd.goldMediumSfx);
            pool.Insert(6, PlayerCmd.goldSmallSfx);
        }

        return pool;
    }

    private static Color ParseColor(string value, string fallback)
    {
        try
        {
            return new Color(value);
        }
        catch (Exception)
        {
            Log.Warn($"[ThrowYourPotions] '{value}' is not a valid colour, falling back to {fallback}.");
            return new Color(fallback);
        }
    }

    private const string FileName = "ThrowYourPotions.config.jsonc";

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// Looks for the config next to the game's save data first, then beside the mod DLL.
    ///
    /// The save-data copy wins so that settings survive a Vortex update, which replaces the whole
    /// mod folder. The file is .jsonc rather than .json deliberately: the game scans mods/
    /// recursively for *.json and tries to parse every one as a mod manifest.
    /// </summary>
    private static ThrowConfig Load()
    {
        foreach (string path in CandidatePaths())
        {
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                ThrowConfig? loaded = JsonSerializer.Deserialize<ThrowConfig>(File.ReadAllText(path), _jsonOptions);
                if (loaded != null)
                {
                    Log.Info($"[ThrowYourPotions] Loaded config from {path}.");
                    return loaded;
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"[ThrowYourPotions] Could not read {path} ({ex.Message}), trying the next location.");
            }
        }

        Log.Info("[ThrowYourPotions] No config file found, using defaults.");
        return new ThrowConfig();
    }

    private static IEnumerable<string> CandidatePaths()
    {
        string? userDir = null;
        try
        {
            userDir = OS.GetUserDataDir();
        }
        catch (Exception)
        {
            // Not fatal: fall back to the mod folder.
        }

        if (!string.IsNullOrEmpty(userDir))
        {
            yield return Path.Combine(userDir, FileName);
        }

        string? modDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (!string.IsNullOrEmpty(modDir))
        {
            yield return Path.Combine(modDir, FileName);
        }
    }
}
