using System;
using System.Linq;
using System.Text;
using UnityEngine;

namespace OrbusQuestHelper;

/// <summary>
/// Pinned mission HUD. Uses the game's existing TopHud.levelLabel as its
/// preferred spatial anchor, NOT as the text renderer. Local player
/// nameplates can be disabled/invisible: in that case Auto uses an
/// explicit small VR camera anchor. Original level/XP UI is NEVER moved.
/// </summary>
internal static class MissionHudPresenter
{
    private static GameObject? _root;
    private static TextMesh? _text;
    private static Transform? _anchorParent;
    private static bool _usingHeadset;
    private static float _nextTick;
    private static bool _warnedNoAnchor;
    private static bool _reportedRealCounters;
    private static string _lastAnchorLog = "";

    internal static void Tick()
    {
        if (Time.realtimeSinceStartup < _nextTick) return;
        _nextTick = Time.realtimeSinceStartup + 0.85f;

        if (Plugin.ShowPinnedHud == null || !Plugin.ShowPinnedHud.Value ||
            MissionPinStore.PinnedIds.Count == 0)
        {
            if (_root != null) _root.SetActive(false);
            return;
        }

        try
        {
            Component? label = MissionProgressReader.CurrentLevelLabel() as Component;
            Camera? eye = FindEyeCamera();
            // The original level/XP bar is the location the player wants,
            // NOT a floating headset popup. Renderer.isVisible and the
            // currently active stereo camera are not reliable availability
            // tests when a desktop spectator/creator camera is enabled.
            // If the game's level label exists, anchor to it first.
            bool levelUsable = label != null &&
                label.gameObject.activeInHierarchy &&
                label.transform.parent != null;

            TrackerAnchor requested = Plugin.HudAnchor?.Value ??
                TrackerAnchor.Auto;
            bool useHeadset = requested == TrackerAnchor.Headset ||
                (requested == TrackerAnchor.Auto && !levelUsable);
            // Explicit Headset remains available in Mods settings, but Auto
            // stays attached to the native level/XP HUD whenever possible.
            Transform? parent = useHeadset
                ? eye?.transform : levelUsable ? label!.transform.parent : null;

            if (parent == null)
            {
                if (_root != null) _root.SetActive(false);
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
                _usingHeadset != useHeadset;
            if (needsCreation)
            {
                if (_root != null) UnityEngine.Object.Destroy(_root);
                _root = null;
                _text = null;
                _anchorParent = null;
                CreateHud(label, eye, parent, useHeadset);
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
            if (_root != null) _root.SetActive(false);
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
        _root.transform.SetParent(parent, false);

        _text = _root.AddComponent<TextMesh>();
        _text.fontSize = 48;
        // v0.4.9 displayed a full-sized world TextMesh (0.037m) and
        // looked enormous in the headset. Keep the same crisp font but
        // scale the actual glyph size to roughly one-quarter.
        _text.characterSize = 0.009f;
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
        float x = Mathf.Clamp(Plugin.HudOffsetX?.Value ?? 0f, -0.2f, 0.2f);
        float y = Mathf.Clamp(Plugin.HudOffsetY?.Value ?? 0f, -0.2f, 0.2f);
        if (_usingHeadset)
        {
            // Last-resort fallback for clients without a TopHud levelLabel.
            // This is deliberately small, pushed into the top-left-ish
            // view rather than covering the middle of the screen.
            _root.transform.localPosition = new Vector3(
                -0.34f + x, 0.24f + y, 1.3f);
            _root.transform.localRotation = Quaternion.identity;
            _root.transform.localScale = Vector3.one * size;
        }
        else if (label != null)
        {
            // A TopHud can use a tiny parent transform scale. Compensate
            // only that parent's world scale, not the original label's own
            // local scale. Our TextMesh size is independently calibrated.
            Vector3 scale = _anchorParent.lossyScale;
            _root.transform.localScale = new Vector3(
                size / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
                size / Mathf.Max(0.0001f, Mathf.Abs(scale.y)),
                size / Mathf.Max(0.0001f, Mathf.Abs(scale.z)));
            _root.transform.rotation = label.transform.rotation;

            Renderer? reference = label.gameObject.GetComponent<Renderer>();
            float nativeWidth = reference == null ? 0.06f :
                Mathf.Clamp(reference.bounds.size.x, 0.025f, 0.35f);

            // Align at the right-hand side of the actual native Level
            // label, NOT a camera-relative arbitrary point. It tracks
            // every movement of the game's own level/XP UI.
            float right = Mathf.Clamp(nativeWidth + 0.012f,
                0.055f, 0.36f);
            _root.transform.position = label.transform.position +
                label.transform.right * (right + x) +
                label.transform.up * y;
        }
    }
}
