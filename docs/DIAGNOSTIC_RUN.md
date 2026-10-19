# QuestHelper v0.2.0 — first functional Missions navigation

The main objective is to verify a small, safe quality-of-life feature in the real PowerUI Missions page.

## Changes

- Accesses PlayerMenu.document directly through generated Assembly-CSharp interop.
- Inserts a visual QUESTHELPER bar with Weekly / Regular / Top controls inside the Missions page.
- Attempts to place it next to an existing weekly header, with a tab-level fallback.
- Uses PowerUI OnMouseDown on only the three newly created buttons.
- Scrolls toward the appropriate weekly/regular heading without rebuilding, removing or reordering native mission entries.
- Accents original section headings. Never changes game mission models, rewards, or official hover/click handlers.
- Automatically recreates the extra navigation node if the game reloads its Missions HTML.
- The broad diagnostic scanner is disabled; there is no giant automatic log file.

## First build and test

1. Exit Orbus. From your local clone run git pull origin main followed by .\release.ps1.
2. Update v0.2.0 in RebornManager with Orbus closed.
3. Open Inventory > Missions and find the QUESTHELPER bar near the weekly missions. If the known weekly header doesn't exist as a DOM ID, the bar may appear at the bottom of that tab.
4. Try Weekly, Regular, and Top with your normal VR controller cursor. Verify that the original mission icons and tooltips still work.
5. Send a screenshot and BepInEx/LogOutput.log if the UI is missing, clicks don't register, or the scroll goes to the wrong place. We can fix those using focused PowerUI code instead of going back to broad scanners.

This new event bridge (managed delegate to native PowerUI OnDomEvent) and live DOM section placement have not yet been compiled or runtime-validated by the assistant.

## Disable

Edit BepInEx/config/com.horizon.orbus.questhelper.cfg and set EnableQuickNavigation = false under [Missions], then restart Orbus.
