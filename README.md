# Throw Your Potions

A joke mod for **Slay the Spire 2** that is also, annoyingly, useful.

The Foul Potion has exactly one use outside combat: throw it at the merchant for 100 gold. It is
very easy to walk into a shop, buy your cards, leave, and only then remember you were carrying one.

So this mod shouts at you. Walk into a merchant holding one or more Foul Potions and
**THROW YOUR POTIONS!!** slams onto the screen a word at a time, crooked, shaking, rocking,
breathing, with a light chasing through the letters and slime splattering around it, while the
screen rumbles and the merchant gibbers over himself with his coins jingling.

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
| `text` | `"THROW YOUR POTIONS!!"` | Say something else if you like. Each word becomes its own line. |
| `fontSize`, `textColor`, `outlineColor`, `outlineSize` | 210, cream, near-black, 54 | How the text looks. `textColor` applies to the `plain` style only. |
| `textStyle` | `"slime"` | `"slime"` (Foul Potion green), `"gold"`, `"rainbow"` (every letter a different colour), or `"plain"`. |
| `wordStepSeconds`, `wordTiltDegrees` | 0.15, 6 | The words land one at a time, stacked and crooked. |
| `strobe`, `strobeSeconds`, `strobeWidth`, `strobeColor` | true, 0.055, 2, white | A light chasing through the letters. |
| `sizeJitter`, `sizeJitterAmount`, `sizeJitterSeconds` | true, 0.16, 0.32 | Each word breathing in and out of size. |
| `rock`, `rockDegrees`, `rockSeconds` | true, 3, 0.55 | Each word rocking back and forth. |
| `textMotion` | `"jitter"` | `"jitter"` shakes the letters, `"sine"` bobs them, `"none"` holds still. |
| `flash`, `flashColor` | true, slime green | A full-screen colour wash as the text lands. |
| `holdSeconds` | `1.1` | How long it stays before fading. |
| `screenShake` | `true` | Rumbles the screen for as long as the text is up. |
| `splats`, `splatCount`, `splatScaleMin`, `splatScaleMax` | true, 24, 8.4, 16.2 | Slime splats going off around the text. |
| `wackyCase` | `true` | rAnDoM cAsE, re-rolled every time. |
| `soundCount` | `20` | How many noises. Capped to what fits while the text is up. |
| `soundGapSeconds` | `0.14` | Gap between the noises within one wave, so they pile up. |
| `soundVolume` | `1.0` | Quieter, if you are a coward. |
| `soundSet` | `"mix"` | `"buy"` is his purchase noise alone; `"mix"` cycles every noise he has, which is denser. |
| `moneySounds` | `true` | Mixes the game's gold sounds in with his noises. |
| `soundWaveSeconds` | `1.3` | How long before the same noise can play again. Lower it for a faster racket. |
| `delaySeconds` | `0.6` | Wait after arriving, so it does not play under the room's fade-in. |

Values are clamped, so a daft number tones the mod down rather than breaking anything.

## Notes

- Loading a save made while standing in a shop sets it off again. That is deliberate: you have just
  arrived, and you have probably forgotten again.
- In co-op it only shouts at whoever is actually carrying a Foul Potion.
- The noises come in waves rather than one long stream. The sound engine will not play a voice line
  on top of itself, so a wave is every distinct noise at once, then the whole set again once they
  have finished. That is why `"mix"` sounds fuller than `"buy"`.
- Your settings are never overwritten by an update, and any setting missing from your file simply
  uses its default, so new ones can be added without touching what you have tuned.

## Building from source

Requires the .NET 9 SDK and a local install of the game.

```bash
./deploy.sh ["/path/to/Slay the Spire 2"]   # build and install into the game's mods folder
./package.sh ["/path/to/Slay the Spire 2"]  # build a release ZIP into dist/
```

## Licence

MIT. See [LICENSE](LICENSE).
