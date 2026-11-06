using System;
using System.Linq;
using System.Text;
using UnityEngine;

namespace OrbusQuestHelper;

/// <summary>
/// Pinned mission HUD. Uses the game's existing TopHud.levelLabel as its
/// preferred spatial anchor, NOT as the text renderer. In-game headset
/// placement is optional, but all modes REQUIRE an active gameplay TopHud.
/// No tracker is displayed in the main menu or during loading.
/// Original level/XP UI is NEVER moved.
/// </summary>
internal static class MissionHudPresenter
{
    private static GameObject? _root;
    private static TextMesh? _text;
    private static Transform? _anchorParent;
    private static Component? _boundTopHud;
    private static Component? _boundLevelLabel;
    private static bool _usingHeadset;
    private static float _nextTick;
    private static bool _warnedNoAnchor;
    private static bool _reportedRealCounters;
    private static bool _loggedNoGameplayHud;
    private static string _lastAnchorLog = "";

    internal static void Shutdown() => ReleaseTracker();

    private static bool IsGameplayHudActive(Component? hud, Component? level)
    {
        if (hud == null || level == null) return false;
        if (!hud.gameObject.activeInHierarchy ||
            !level.gameObject.activeInHierarchy)
            return false;
        if (hud is Behaviour behaviour && !behaviour.isActiveAndEnabled)
            return false;
        if (level.transform.parent == null) return false;
        return true;
    }

    private static void ReleaseTracker()
    {
        // Disable immediately, even though Unity.Destroy takes effect at
        // the end of the frame. This prevents one-frame lobby flashes.
        if (_root != null)
        {
            _root.SetActive(false);
            UnityEngine.Object.Destroy(_root);
        }
        _root = null;
        _text = null;
        _anchorParent = null;
        _boundTopHud = null;
        _boundLevelLabel = null;
        _usingHeadset = false;
    }

    internal static void Tick()
    {
        // Validate the previous gameplay HUD on EVERY frame, not just
        // every 0.85-second checklist refresh. In particular, a Headset-
        // anchored tracker must not survive the transition to main menu.
        if (_root != null)
        {
            bool stillInWorld = false;
            try
            {
                stillInWorld = IsGameplayHudActive(
                    _boundTopHud, _boundLevelLabel);
            }
            catch
            {
                // Native HUD components may be destroyed mid-scene-load.
                // Fail closed: hide the tracker instead of risking an
                // Update exception while the main menu is opening.
            }
            if (!stillInWorld) ReleaseTracker();
            else if (_root != null && !_usingHeadset)
            {
                // A detached level-anchored tracker follows the level HUD
                // without adding a huge-scale child to Orbus's own layout.
                try { PositionHud(_boundLevelLabel, null); }
                catch { ReleaseTracker(); }
            }
        }

        if (Time.realtimeSinceStartup < _nextTick) return;
        _nextTick = Time.realtimeSinceStartup + 0.85f;

        if (Plugin.ShowPinnedHud == null || !Plugin.ShowPinnedHud.Value ||
            MissionPinStore.PinnedIds.Count == 0)
        {
            ReleaseTracker();
            return;
        }

        try
        {
            // Crucial lifecycle gate: TopHud and its level label must be
            // active inside the playable world. Missing/deactivated HUD
            // means loading or lobby, NOT permission to spawn a new
            // camera-attached label in front of the user's face.
            Component? hud = MissionProgressReader.CurrentTopHud() as Component;
            Component? label = MissionProgressReader.CurrentLevelLabel() as Component;
            if (!IsGameplayHudActive(hud, label))
            {
                ReleaseTracker();
                if (!_loggedNoGameplayHud)
                {
                    _loggedNoGameplayHud = true;
                    Plugin.Logger.LogInfo("[QuestHelper/HUD] Gameplay level " +
                        "HUD unavailable (lobby/loading). Tracker hidden; " +
                        "saved pinned missions retained.");
                }
                return;
            }

            if (_loggedNoGameplayHud)
            {
                _loggedNoGameplayHud = false;
                Plugin.Logger.LogInfo("[QuestHelper/HUD] Gameplay level " +
                    "HUD restored; pinned tracker may resume.");
            }

            Camera? eye = FindEyeCamera();
            // The gameplay HUD is still required as a lifecycle check, but
            // Auto now prefers a small headset-relative element. Orbus may
            // rescale the native level-label hierarchy when XP/reward tiers
            // update; following that parent made QuestHelper gigantic.
            // Explicit LevelHud remains supported as a detached overlay.
            TrackerAnchor requested = Plugin.HudAnchor?.Value ??
                TrackerAnchor.Auto;
            bool useHeadset = eye != null &&
                (requested == TrackerAnchor.Headset ||
                 (requested == TrackerAnchor.Auto && eye.stereoEnabled));
            Transform? parent = useHeadset ? eye?.transform :
                label!.transform.parent;

            if (parent == null)
            {
                ReleaseTracker();
                if (!_warnedNoAnchor)
                {
                    _warnedNoAnchor = true;
                    Plugin.Logger.LogWarning("[QuestHelper/HUD] No visible " +
                        "native level anchor or active headset camera. " +
                        "Pinned data saved; no HUD elements changed.");
                }
                return;
            }

            bool needsCreation = _root == null || _text == null ||
                _anchorParent == null || _anchorParent != parent ||
                _boundTopHud != hud || _boundLevelLabel != label ||
                _usingHeadset != useHeadset;
            if (needsCreation)
            {
                ReleaseTracker();
                CreateHud(label, eye, parent, useHeadset);
                _boundTopHud = hud;
                _boundLevelLabel = label;
            }

            if (_root == null || _text == null) return;

            var rows = MissionProgressReader.ReadPinnedObjectives(
                MissionPinStore.PinnedIds, out int resolved);
            if (!_reportedRealCounters && rows.Count > 0)
            {
                _reportedRealCounters = true;
                Plugin.Logger.LogInfo("[QuestHelper/HUD] Verified native " +
                    "ChecklistItem counts for " + resolved +
                    " pinned mission(s); rendering " + rows.Count +
                    " real objective line(s).");
            }

            var display = new StringBuilder();
            if (rows.Count > 0)
            {
                foreach (MissionProgressReader.ObjectiveLine objective
                    in rows.Take(5))
                {
                    if (display.Length > 0) display.Append('\n');
                    display.Append(objective.Name).Append(": ")
                        .Append(objective.Current.ToString("N0",
                            System.Globalization.CultureInfo.InvariantCulture))
                        .Append('/')
                        .Append(objective.Required.ToString("N0",
                            System.Globalization.CultureInfo.InvariantCulture))
                        .Append(" - ")
                        .Append(objective.Remaining.ToString("N0",
                            System.Globalization.CultureInfo.InvariantCulture))
                        .Append(objective.GoalLabel.Length > 0
                            ? " to " + objective.GoalLabel : " left");
                }
            }
            else
            {
                display.Append(resolved > 0
                    ? "Pinned mission: no count objectives"
                    : "Pinned mission progress unavailable");
            }

            string value = display.ToString();
            if (_text.text != value) _text.text = value;
            PositionHud(label, eye);
            _root.SetActive(true);
        }
        catch (Exception ex)
        {
            ReleaseTracker();
            if (!_warnedNoAnchor)
            {
                _warnedNoAnchor = true;
                Plugin.Logger.LogWarning("[QuestHelper/HUD] Safe failure " +
                    "(no original HUD edits): " + ex.GetType().Name +
                    " " + ex.Message);
            }
        }
    }

    private static Camera? FindEyeCamera()
    {
        // Prefer stereo-enabled VR cameras; the game can also have a
        // RebornCamera desktop camera which must NOT steal the HUD.
        Camera? eyeNamed = null;
        try
        {
            foreach (Camera candidate in Camera.allCameras)
            {
                if (candidate == null || !candidate.isActiveAndEnabled)
                    continue;
                if (candidate.stereoEnabled) return candidate;
                if (candidate.targetTexture != null) continue;
                string name = candidate.gameObject.name.ToLowerInvariant();
                if (name.Contains("better") || name.Contains("freecam") ||
                    name.Contains("creator") || name.Contains("spectator") ||
                    name.Contains("replay"))
                    continue;
                if (eyeNamed == null && (name.Contains("head") ||
                    name.Contains("eye") || name.Contains("main") ||
                    name.Contains("vr")))
                    eyeNamed = candidate;
            }
        }
        catch { }
        if (eyeNamed != null) return eyeNamed;
        Camera? main = Camera.main;
        if (main == null || !main.isActiveAndEnabled ||
            main.targetTexture != null) return null;
        string fallbackName = main.gameObject.name.ToLowerInvariant();
        return fallbackName.Contains("better") ||
            fallbackName.Contains("freecam") ||
            fallbackName.Contains("creator") ? null : main;
    }

    private static void CreateHud(Component? label, Camera? eye,
        Transform parent, bool headset)
    {
        _root = new GameObject("QuestHelper Pinned Objectives");
        // Normal world layer is rendered by the local VR camera. For
        // level-anchored presentation use the same layer as original text.
        _root.layer = headset ? 0 : label!.gameObject.layer;
        // NEVER parent a custom tracker under the native XP/level HUD.
        // Reward transitions may replace/re-scale the native hierarchy.
        // Detach LevelHud tracking and calculate its world pose explicitly.
        if (headset) _root.transform.SetParent(parent, false);

        _text = _root.AddComponent<TextMesh>();
        _text.fontSize = 48;
        // Consistent, small glyph sizing independent of native XP HUD
        // transform scale. All user scaling remains a bounded multiplier.
        _text.characterSize = 0.008f;
        _text.color = new Color(1f, 0.96f, 0.82f, 1f);
        _text.anchor = TextAnchor.UpperLeft;
        _text.alignment = TextAlignment.Left;
        _text.richText = false;

        object? originalFont = label == null ? null :
            PowerUiRuntime.Get(label, "font") ??
            PowerUiRuntime.Get(label, "pym");
        Font? font = originalFont as Font;
        if (font == null)
        {
            try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
            catch { }
        }
        if (font != null)
        {
            _text.font = font;
            MeshRenderer? renderer = _root.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = font.material;
                MeshRenderer? sourceRenderer =
                    label!.gameObject.GetComponent<MeshRenderer>();
                if (sourceRenderer != null)
                {
                    renderer.sortingLayerID = sourceRenderer.sortingLayerID;
                    renderer.sortingOrder = sourceRenderer.sortingOrder + 1;
                }
            }
        }

        _anchorParent = parent;
        _usingHeadset = headset;

        PositionHud(label, eye);

        // Bounded metadata only: NO quest IDs, player names or world
        // coordinates. This gives us actual runtime scale/visibility
        // instead of assuming the local avatar's label is visible.
        Renderer? source = label == null ? null :
            label.gameObject.GetComponent<Renderer>();
        string report = "anchor=" + (headset ? "headset" : "level") +
            ",hudLevelFound=" + (label != null) +
            ",nativeLevelActive=" +
                (label != null && label.gameObject.activeInHierarchy) +
            ",nativeLevelRenderer=" + (source != null &&
                source.enabled) +
            ",nativeLevelVisible=" + (source != null &&
                source.isVisible) +
            ",nativeLevelLayer=" + (label?.gameObject.layer.ToString() ?? "-") +
            ",levelLayerInCamera=" + (label != null && eye != null &&
                (eye.cullingMask & (1 << label.gameObject.layer)) != 0) +
            ",parentScale=" + Rounded(parent.lossyScale) +
            ",camera=" + (eye?.name ?? "<none>");
        if (_lastAnchorLog != report)
        {
            _lastAnchorLog = report;
            Plugin.Logger.LogInfo("[QuestHelper/HUD] Tracker created: " +
                report + ". Original level label unchanged.");
        }
    }

    private static string Rounded(Vector3 v) =>
        v.x.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
        "," + v.y.ToString("0.###",
            System.Globalization.CultureInfo.InvariantCulture) +
        "," + v.z.ToString("0.###",
            System.Globalization.CultureInfo.InvariantCulture);

    private static void PositionHud(Component? label, Camera? eye)
    {
        if (_root == null || _anchorParent == null) return;
        float size = Mathf.Clamp(Plugin.HudSize?.Value ?? 1f, 0.4f, 1.5f);
        float x = Mathf.Clamp(Plugin.HudOffsetX?.Value ?? 0f, -1f, 1f);
        float y = Mathf.Clamp(Plugin.HudOffsetY?.Value ?? 0f, -0.75f, 0.75f);
        if (_usingHeadset)
        {
            // Headset attachment is separate from the game's level UI,
            // with a stable local transform and modest text size.
            _root.transform.localPosition = new Vector3(
                -0.35f + x, 0.25f + y, 1.45f);
            _root.transform.localRotation = Quaternion.identity;
            _root.transform.localScale = Vector3.one * (size * 0.85f);
        }
        else if (label != null)
        {
            // Detached world-space placement: never invert the tiny
            // native parent lossyScale. That made the custom text grow
            // dramatically as the game rebuilt the XP label at Silver.
            Renderer? reference = label.gameObject.GetComponent<Renderer>();
            float nativeWidth = reference == null ? 0.06f :
                Mathf.Clamp(reference.bounds.size.x, 0.025f, 0.35f);
            float right = Mathf.Clamp(nativeWidth + 0.012f, 0.055f, 0.36f);

            _root.transform.position = label.transform.position +
                label.transform.right * (right + x) +
                label.transform.up * y;
            _root.transform.rotation = label.transform.rotation;
            // Because our transform has NO parent in LevelHud mode,
            // world-scale stays fixed despite native HUD scene changes.
            _root.transform.localScale = Vector3.one * (size * 0.65f);
        }
    }
}
