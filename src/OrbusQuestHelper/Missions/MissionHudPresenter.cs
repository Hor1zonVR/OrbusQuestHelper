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
    private static Component? _sourceLevelLabel;
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
            bool levelUsable = label != null &&
                label.gameObject.activeInHierarchy &&
                label.transform.parent != null;

            Renderer? nativeRenderer = levelUsable
                ? label!.GetComponent<Renderer>() : null;
            if (nativeRenderer != null && !nativeRenderer.enabled)
                levelUsable = false;

            // The local avatar's overhead nameplate can exist and be
            // "active" but excluded from the stereo camera's culling mask.
            // That was the untested assumption in v0.4.8.
            if (levelUsable && eye != null &&
                (eye.cullingMask & (1 << label!.gameObject.layer)) == 0)
                levelUsable = false;

            TrackerAnchor requested = Plugin.HudAnchor?.Value ??
                TrackerAnchor.Auto;
            bool useHeadset = requested == TrackerAnchor.Headset ||
                (requested == TrackerAnchor.Auto && !levelUsable);
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
                    in rows.Take(7))
                {
                    if (display.Length > 0) display.Append('\n');
                    display.Append(objective.Name).Append("  ")
                        .Append(objective.Current).Append('/')
                        .Append(objective.Required);
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
        try
        {
            foreach (Camera candidate in Camera.allCameras)
                if (candidate != null && candidate.isActiveAndEnabled &&
                    candidate.stereoEnabled)
                    return candidate;
        }
        catch { }
        Camera? main = Camera.main;
        return main != null && main.isActiveAndEnabled ? main : null;
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
        _text.characterSize = 0.037f;
        _text.color = new Color(1f, 0.96f, 0.82f, 1f);
        _text.anchor = TextAnchor.UpperLeft;
        _text.alignment = TextAlignment.Left;

        object? originalFont = label == null ? null :
            PowerUiRuntime.Get(label, "font") ??
            PowerUiRuntime.Get(label, "pym");
        if (originalFont is Font font)
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
        _sourceLevelLabel = label;

        PositionHud(label, eye);

        // Bounded metadata only: NO quest IDs, player names or world
        // coordinates. This gives us actual runtime scale/visibility
        // instead of assuming the local avatar's label is visible.
        string report = "anchor=" + (headset ? "headset" : "level") +
            ",hudLevelFound=" + (label != null) +
            ",nativeLevelActive=" +
                (label != null && label.gameObject.activeInHierarchy) +
            ",nativeLevelLayer=" + (label?.gameObject.layer.ToString() ?? "-") +
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
        float size = Mathf.Clamp(Plugin.HudSize?.Value ?? 1f, 0.5f, 2f);
        if (_usingHeadset)
        {
            // Up in the user's view, like the level/XP HUD, rather than
            // floating in the world. Does NOT touch player movement.
            _root.transform.localPosition = new Vector3(-0.10f, 0.31f, 1.12f);
            _root.transform.localRotation = Quaternion.identity;
            _root.transform.localScale = Vector3.one * size;
        }
        else if (label != null)
        {
            // A TopHud often has a tiny parent scale. Multiplying a
            // regular TextMesh by the levelLabel's localScale (v0.4.8)
            // caused tiny/invisible text. Compensate its world scale.
            Vector3 scale = _anchorParent.lossyScale;
            _root.transform.localScale = new Vector3(
                size / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
                size / Mathf.Max(0.0001f, Mathf.Abs(scale.y)),
                size / Mathf.Max(0.0001f, Mathf.Abs(scale.z)));
            _root.transform.rotation = label.transform.rotation;
            Renderer? reference = label.gameObject.GetComponent<Renderer>();
            float width = reference == null ? 0.09f :
                Mathf.Clamp(reference.bounds.size.x, 0.03f, 0.25f);
            _root.transform.position = label.transform.position +
                label.transform.right * (width * 0.65f + 0.04f) -
                label.transform.up * 0.015f;
        }
    }
}
