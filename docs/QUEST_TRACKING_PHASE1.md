# QuestHelper — Tracking Phase 1 (v0.4.6)

## Current status

The Missions organiser already works on the current Spades client, including Region / Activity switching. This phase does not implement a live tracker yet; it tests the game mission-data API needed before displaying reliable objective progress.

The roadmap is to provide a native-looking Track / Untrack control, an optional small in-world tracking HUD, and later verified compass/navigation, fishing-lure information and relevant public-event alerts.

We cannot infer a quest's numeric progress from Bronze/Silver/Gold icon styling. The native per-objective status, progress and checklist fields need runtime verification, particularly on Spades, where generated classes may be renamed.

## What was implemented

MissionTrackingProbe runs on the Unity main thread once per session after QuestHelper has successfully received valid original Missions HTML. It samples up to three distinct regular mission IDs; generated timestamp-like weekly IDs are skipped.

It checks for the known MagicalActual.PlayerCharacter / PlayerCharacter and MagicalActual.Mission types. If available, it reads PlayerCharacter.Instance, the existing GetMission(string) getter, and the shapes/types of status, progress, checklist and bounded first-objective progress fields.

It never prints mission description text, usernames, chat, world coordinates, or inventory data. It never invokes quest purchase, submission, reward or progress setters. It never changes PowerUI, game DOM, XR rigs or scene objects. If Spades classes cannot be resolved, it logs that fact and refuses to guess.

No config editing is required. The probe is automatically one-shot, with no repeated per-frame reflection scans.

## How to test

Run the usual release script with -GameDir pointing at the My Orbus Renewed Spades instance. Install version 0.4.6 in RebornManager, launch Spades, open Inventory > Missions, wait five seconds and exit. Send the [QuestHelper.TrackProbe] lines from the game's BepInEx/LogOutput.log privately.

If PlayerCharacter and GetMission resolve, we can implement Track/Untrack and poll actual quest progress on a moderate interval. If those Spades APIs are obfuscated, the next step is read-only metadata inspection rather than patching random methods or inventing numbers.

## Next development

After analysing the live Spades data, build a native Track / Untrack interaction independent of existing mission icon hover/click handlers. Implement a compact in-VR tracker, preserving original native mission HTML byte-for-byte, and without adjusting the XR rig. Verify the selected mission's real-time progress before enabling a public counter. Do not reintroduce old DOM reparenting.
