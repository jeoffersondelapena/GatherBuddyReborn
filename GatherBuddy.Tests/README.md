# Tests for the fork's own logic

```bash
dotnet test GatherBuddy.Tests/GatherBuddy.Tests.csproj
```

Runs on the Mac in under a second, outside the game: nothing here can touch a running game window.

The plugin itself cannot be loaded by a test. It targets Windows and reads the game's memory, so the fork keeps its
rules in `GatherBuddy/ForkLogic/`, in files that use no game or plugin types, and this project compiles those same
files. The plugin's code calls them with the game's data; the tests call them with made-up data.

What is covered:

- `ListStateRules`: which gather lists are on and which items are unticked, per character; a character's file is only
  written from the state that was loaded for that character.
- `QueueRules`: putting a recipe off to the end of a run (the loop that once froze the game), skipping within a bound,
  queueing every producer of a material before its consumer, and which class makes a shared material.
- `CharacterSettingsRules`: a character's own mount beside the shared setting.
- `TextRules`: the chat lists, the solver's "no solution" against a real failure, and the XIV Doctor note line.
- `SafeFile`: a file shared by two game windows is swapped in whole, never read half-written.

What is not covered: anything that needs the game (reading levels, the logs, inventory, driving the crafting window).
That is still checked by running it, and by the fork's per-character trace file.

A new fork rule goes into `ForkLogic` with a test beside it here. The upstream-sync workflow runs this suite after
each rebase and publishes nothing if it fails.
