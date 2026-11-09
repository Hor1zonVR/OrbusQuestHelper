# QuestHelper 0.4.14 — short mission list and server-switch recovery

## Diagnosis from the actual 2026-11-09 Spades BepInEx log

- BepInEx loaded **`OrbusQuestHelper 0.4.12`**, not `0.4.13`, even though the newer source compiled earlier that day. **The installed DLL had not yet updated.**
- The plugin found **0 favourites and 0 HUD pins** on startup. Without a pinned mission, the always-on HUD has nothing to draw. This does not prove a broken native player HUD.
- On a private server with a one-mission page, Spades wrote **646 chars and 1 native mission tile** into `missions-container`.
- The pure transformer rejected the page with **"Unexpected trailing wrapper text or insufficient mission icons"**. The source's hard condition was `tiles.Count < 2`. The Harmony prefix then marked **`_failed=true` for the remainder of the process**.
- After switching to Community with many missions, the prefix immediately returned because `_failed` remained set; therefore the Region/Activity/pin UI never regenerated, even if the new server page was otherwise valid.

**There is no QuestHelper exception in the provided ErrorLog.** Separate Oculus compositor errors and one AutoMount ENet sending exception were recorded. Those do not explain this deterministic QuestHelper organiser lockout.

## Code changes

1. Allow a **single mission tile** while preserving its source HTML byte-for-byte. Zero tiles and unknown wrapper content still fail closed.
2. Distinguish a missing tile from genuinely unsafe trailing markup in the diagnostic reason.
3. Never permanently disable the entire HTML organiser because of one unsupported/empty/partial mission page. Leave that page's original HTML untouched, clear stale cached replacement, and attempt the next native Missions write again.
4. Bound warning logs (first four ordinary skips, first three unexpected exceptions), so repeated incomplete page writes cannot spam the VR log.
5. Protect previous Pin/Star and Region/Activity markup semantics; no native mission rewards or network traffic modified.
6. Bump to **0.4.14**, distinct from older releases.

### Offline verification

The pure HTML test harness includes an explicit one-mission test with both a simple wrapper and nested `vendor-reputation` wrapper, for both views. It asserts a **single exact original mission tile**, native **`qh-pin-hunter1`** button, view toggles, and marker. A server-switch test rejects malformed input without modifying it, accepts a one-mission page, then validates a separate **62-tile Community-style** page. This tests the pure transformer, not the Harmony prefix against the real Spades runtime.

### Testing in game

1. Run `release.ps1` on **main** after merging, publish the verified `v0.4.14` stable DLL and update through RebornManager **with Orbus closed**.
2. On startup, check `BepInEx/LogOutput.log` says **`Loading [OrbusQuestHelper 0.4.14]`**. If it still says 0.4.12 or 0.4.13, the client is loading an outdated copy of the plugin or a different RebornManager instance.
3. On the small private server, open Missions. A one-mission list should still get the Star/Pin controls; if the actual page differs in unknown wrapper content, it stays vanilla and reports a bounded skip rather than disabling QuestHelper.
4. Switch to the Community server **in the same game process**. Open Missions: the 62+ native tiles should be reorganized again. Pin up to three accepted missions and verify the `[Pinned Missions] HudPins` value persists.
5. Once there is at least one pin, test the tracker near the actual level/XP HUD using `TrackerAnchor=Auto`; objective labels and tier totals should use the 0.4.13 research. **Live server progress without opening the Missions tab is still unverified.**

To diagnose unsupported 1-mission markup if a new source template is encountered, enable `[Investigation] CaptureMissionHtmlOnce=true` for one client startup and provide the resulting original `*_SpadesMissionsOriginal.html` **privately**, not in public GitHub.
