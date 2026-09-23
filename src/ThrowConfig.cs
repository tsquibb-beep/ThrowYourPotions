using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using MegaCrit.Sts2.Core.Audio;
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
    public string Text { get; set; } = "THROW YOUR POTIONS";

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
    public float HoldSeconds { get; set; } = 1.4f;

    [JsonPropertyName("screenShake")]
    public bool ScreenShake { get; set; } = true;

    /// <summary>Burst of gold coins behind the text — he pays you 100 gold for the throw.</summary>
    [JsonPropertyName("coinExplosion")]
    public bool CoinExplosion { get; set; } = true;

    /// <summary>Random upper/lower case, re-rolled every time, so it never looks the same twice.</summary>
    [JsonPropertyName("wackyCase")]
    public bool WackyCase { get; set; } = true;

    /// <summary>How many merchant noises to play. Ten is the point of the mod.</summary>
    [JsonPropertyName("soundCount")]
    public int SoundCount { get; set; } = 10;

    /// <summary>Gap between noises. Small enough that they pile on top of each other.</summary>
    [JsonPropertyName("soundGapSeconds")]
    public float SoundGapSeconds { get; set; } = 0.12f;

    [JsonPropertyName("soundVolume")]
    public float SoundVolume { get; set; } = 1.0f;

    /// <summary>"buy" repeats his purchase noise; "mix" cycles every noise he has.</summary>
    [JsonPropertyName("soundSet")]
    public string SoundSet { get; set; } = "buy";

    /// <summary>
    /// Wait this long after arriving before going off. Room entry happens behind a fade to black
    /// (0.8s normally, shorter in Fast mode), so firing immediately would play under it.
    /// </summary>
    [JsonPropertyName("delaySeconds")]
    public float DelaySeconds { get; set; } = 0.6f;

    private static ThrowConfig? _current;

    public static ThrowConfig Current => _current ??= Load();

    // Clamped accessors: a silly edit should tone the mod down, never wedge the game.
    public int Sounds => Math.Clamp(SoundCount, 0, 50);

    public float Gap => Math.Clamp(SoundGapSeconds, 0.02f, 2f);

    public float Volume => Math.Clamp(SoundVolume, 0f, 1f);

    public float Delay => Math.Clamp(DelaySeconds, 0f, 10f);

    public float Hold => Math.Clamp(HoldSeconds, 0f, 10f);

    public int Size => Math.Clamp(FontSize, 8, 400);

    public int Outline => Math.Clamp(OutlineSize, 0, 64);

    public Color TextColor => ParseColor(TextColorHex, "#fff1c9");

    public Color OutlineColor => ParseColor(OutlineColorHex, "#1a0f06");

    public bool MixSounds => string.Equals(SoundSet?.Trim(), "mix", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The noises, in the order they are played. "buy" is his purchase vocalisation, which is a
    /// random-variation event in FMOD, so repeating it still gives a different noise each time.
    /// </summary>
    public IReadOnlyList<string> SoundEvents(bool fakeMerchant)
    {
        if (!MixSounds)
        {
            return fakeMerchant
                ? new[] { FmodSfx.fakeMerchantLaugh, FmodSfx.merchantThankYou }
                : new[] { FmodSfx.merchantThankYou };
        }

        return fakeMerchant
            ? new[] { FmodSfx.fakeMerchantLaugh, FmodSfx.merchantThankYou, FmodSfx.merchantWelcome, FmodSfx.merchantPassive, FmodSfx.merchantDisappointment }
            : new[] { FmodSfx.merchantThankYou, FmodSfx.merchantWelcome, FmodSfx.merchantPassive, FmodSfx.merchantDisappointment };
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
