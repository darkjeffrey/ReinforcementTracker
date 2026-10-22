# Reinforcement Tracker

A Mount & Blade II: Bannerlord mod (v1.4.8) that shows how big each army really is, reinforcements included, whenever you hold **Tab** for the battle scoreboard.

The vanilla scoreboard only counts troops that have already spawned. With a battle size of 500 and two big armies, most of the fight is still waiting off the map. This mod adds a panel on each side of the scoreboard: your side on the left, the enemy on the right.

| Row | Meaning |
|---|---|
| Total troops | On the field plus everyone still to come |
| At the start | How many this side brought into the battle |
| On the field | Spawned and still fighting |
| Still to come | Reinforcements that haven't spawned yet |
| Next wave | How many troops the next reinforcement wave brings |
| Waves left | How many waves are still to come (campaign battles) |

Under the numbers is a line that says when the next wave comes:

- **Next wave after 12 more casualties**: the side has to lose that many more troops first.
- **Next wave in 2s**: the wave is due and will be called at the next reinforcement check.
- **Reinforcements arriving now**, **Next wave is waiting for room on the battlefield** (the engine's agent limit is full), **Reinforcements are paused**, or **No reinforcements left**.

## Hideouts

Hideout assaults have no normal reinforcement waves, but the bandits' **second stage**, the boss and the bandits who come with him, works like one. The enemy panel shows it as the next wave:

- **Classic assault:** only some bandits are out at first. Once every bandit on the field is down, the boss cutscene plays and all the remaining bandits spawn with the boss. The panel shows how many are still waiting and "Second stage after 7 more casualties", i.e. the bandits still standing.
- **Stealth hideout:** the second stage comes when no bandits are left on the field. It brings the rest of the bandit pool, at least half the hideout's starting population (4 to 20), plus the boss.
- **Your side:** in a classic assault only the troops you picked fight, so there's nothing to wait for. In a stealth hideout your troops wait outside and come in, area by area, when you use a signal point. The panel shows how many are still waiting.

The halfway and arrival alerts work in hideouts too: "The bandits are halfway to their second stage", "The second stage has begun: 12 more bandits" and "Your reinforcements have arrived".

## Alerts

You don't have to keep checking the scoreboard. The mod can also tell you, in the chat log or as a banner at the top of the screen:

- **When a side is partway to its next wave**, once per wave: "Enemies are halfway to getting reinforcements (50 more casualties)". The point is configurable (10% to 90%, default halfway).
- **When reinforcements arrive**: "Enemy reinforcements have arrived: 100 troops".

Alerts for your side are green and enemy alerts are red. Each side can be switched off separately. The "partway" alert only applies to battles with real waves, which covers campaign field battles and sieges. In custom battles, where troops trickle in, it only shows while a full batch is still to come.

## How the numbers are worked out

The mod only reads the game's own reinforcement logic and never changes it. The rules below were read from the game code (v1.4.8):

- **Campaign field battles and sieges** use waves. Every 3 seconds the game checks each side. When a side has lost at least half of its starting strength on the field (50% of its initial spawn), a wave of that size is called, or whatever is left if that's fewer. Even the last small wave needs the full loss before it is called.
- The game's **Reinforcement waves** option limits how many waves come at all. Troops beyond that limit don't take part in the battle, so they aren't counted here either.
- **Custom battles** use the balanced method: troops trickle in as slots free up, released in batches (5% of the battle size with the default settings).
- Reinforcements spawn one per frame once called, so a wave arrives over a second or two.

## Options (Mod Options, MCM)

| Option | Default | What it does |
|---|---|---|
| Enable | On | Turns the panels and the alerts on or off. |
| Show the enemy panel | On | Turn off if you'd rather not know how many enemies are still coming. |
| Alert on the way to reinforcements | On | Message once per wave when a side is partway to its next reinforcements. |
| Alert at | 50% | How far towards the next wave (share of the casualties it needs) before that alert shows. |
| Alert when reinforcements arrive | On | Message each time a wave arrives, with its size. |
| Alerts for your side | On | Alerts about your own side and allies. |
| Alerts for the enemy | On | Alerts about the enemy. |
| Show alerts as a banner | Off | Off: coloured chat-log lines. On: the large notification at the top of the screen. |
| Debug messages | Off | Prints the raw numbers to the chat log each time you open the scoreboard, and a message each time a wave arrives showing the size that was predicted. |

## Requirements

Bannerlord.Harmony, ButterLib, UIExtenderEx and Mod Configuration Menu v5. Load it after them.

## Building

From the `src` folder run `dotnet build -c Release`. The build copies the dll, `SubModule.xml`, `GUI` and `ModuleData` into `Modules\ReinforcementTracker` in the game folder. If the game is installed somewhere else, pass `/p:GameFolder="..."`.

## Code overview

| File | What it does |
|---|---|
| `SubModule.cs` | Adds the mission logic to battles and hideout assaults. |
| `ReinforcementTrackerMissionLogic.cs` | Creates the overlay, shows it while the scoreboard is open, and feeds the alerts. |
| `ReinforcementForecast.cs` | Predicts reinforcements in battles by re-applying the game's own spawn rules. |
| `HideoutForecast.cs` | The same for hideout assaults (classic and stealth). |
| `SideForecast.cs` | The per-side snapshot both forecasts produce. |
| `TroopAlerts.cs` | The "halfway" and "arrived" messages. |
| `ReinforcementTrackerVM.cs` and `Module/GUI/Prefabs/ReinforcementTracker.xml` | The two panels. |
| `ReinforcementTrackerMcmSettings.cs`, `RtSettings.cs` | The Mod Options page and read access to it. |

The game exposes most of what's needed, but a few values (the reinforcement timer, the hideout stage state) are private fields read by reflection. If a game update renames them, the panels fall back to counting the troops on the field, and Debug messages says which fields weren't found.

## Translations

All English text is in the code as `{=id}English text`. `Module/ModuleData/Languages/std_ReinforcementTracker.xml` lists every string and is the template for translations.

To add a language, for example German:

1. Copy `std_ReinforcementTracker.xml` to `Module/ModuleData/Languages/DE/std_ReinforcementTracker-de.xml`, change `<tag language="English" />` to `<tag language="Deutsch" />` and translate each `text` attribute. Keep the ids and every `{VARIABLE}` unchanged.
2. Add `Module/ModuleData/Languages/DE/language_data.xml`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<LanguageData id="Deutsch">
  <LanguageFile xml_path="DE/std_ReinforcementTracker-de.xml" />
</LanguageData>
```

Don't put comments before `<base>` in a translation file: the game's loader expects `<base>` straight after the XML declaration.

## Support

If you enjoy the mod, you can support me on [Ko-fi](https://ko-fi.com/darkjeffrey).
