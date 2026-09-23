# Throw Your Potions

A joke mod for **Slay the Spire 2** that is also, annoyingly, useful.

The Foul Potion has exactly one use outside combat: throw it at the merchant for 100 gold. It is
very easy to walk into a shop, buy your cards, leave, and only then remember you were carrying one.

So this mod shouts at you. Walk into a merchant holding one or more Foul Potions and
**THROW YOUR POTIONS** spins onto the middle of the screen, the screen kicks, and the merchant's
purchase noise plays ten times on top of itself until you get the message.

It also fires at the Fake Merchant, because the potion works on him too.

Purely cosmetic. It changes nothing about how the game plays, it just makes a scene.

## Installation

Install with Vortex, or copy the `ThrowYourPotions` folder into `Slay the Spire 2/mods/` so you end
up with `mods/ThrowYourPotions/ThrowYourPotions.dll`.

## Settings

You do not need any. If you must, edit `ThrowYourPotions.config.jsonc` in the mod folder and restart
the game. To keep your settings through mod updates, copy the file to `%APPDATA%\SlayTheSpire2\`;
that copy is read in preference to the one in the mod folder.

| Setting | Default | What it does |
|---|---|---|
| `enabled` | `true` | Master switch. |
| `showText` | `true` | The big text. Off gives you noise only. |
| `text` | `"THROW YOUR POTIONS"` | Say something else if you like. |
| `fontSize`, `textColor`, `outlineColor`, `outlineSize` | 140, cream, near-black, 24 | How the text looks. `textColor` applies to the `plain` style only. |
| `textStyle` | `"gold"` | `"gold"`, `"slime"` (Foul Potion green), `"rainbow"` (every letter a different colour), or `"plain"`. |
| `wordStepSeconds`, `wordTiltDegrees` | 0.2, 6 | The words land one at a time, stacked and crooked. |
| `strobe`, `strobeSeconds`, `strobeWidth`, `strobeColor` | true, 0.055, 2, white | A light chasing through the letters. |
| `sizeJitter`, `sizeJitterAmount`, `sizeJitterSeconds` | true, 0.06, 0.32 | Each word breathing in and out of size. |
| `textMotion` | `"jitter"` | `"jitter"` shakes the letters, `"sine"` bobs them, `"none"` holds still. |
| `flash`, `flashColor` | true, slime green | A full-screen colour wash as the text lands. |
| `holdSeconds` | `1.4` | How long it stays before fading. |
| `screenShake` | `true` | Rumbles the screen for as long as the text is up. |
| `splats`, `splatCount`, `splatScaleMin`, `splatScaleMax` | true, 14, 1.4, 3.2 | Slime splats going off around the text. |
| `wackyCase` | `true` | rAnDoM cAsE, re-rolled every time. |
| `soundCount` | `10` | How many merchant noises. This is the point of the mod. |
| `soundGapSeconds` | `0.12` | Gap between them. Small, so they overlap. |
| `soundVolume` | `1.0` | Quieter, if you are a coward. |
| `soundSet` | `"mix"` | `"buy"` is his purchase noise alone; `"mix"` cycles every noise he has, which is denser. |
| `soundWaveSeconds` | `1.3` | How long before the same noise can play again. Lower it for a faster racket. |
| `delaySeconds` | `0.6` | Wait after arriving, so it does not play under the room's fade-in. |

Values are clamped, so a daft number tones the mod down rather than breaking anything.

## Notes

- Loading a save made while standing in a shop sets it off again. That is deliberate: you have just
  arrived, and you have probably forgotten again.
- In co-op it only shouts at whoever is actually carrying a Foul Potion.

## Building from source

Requires the .NET 9 SDK and a local install of the game.

```bash
./deploy.sh ["/path/to/Slay the Spire 2"]   # build and install into the game's mods folder
./package.sh ["/path/to/Slay the Spire 2"]  # build a release ZIP into dist/
```

## Licence

MIT. See [LICENSE](LICENSE).
