using System;
using System.Linq;
using System.Text;
using UnityEngine;

namespace OrbusQuestHelper;

/// <summary>
/// Optional lightweight tracker ATTACHED to the game's own TopHud level
/// label parent. Never edits, replaces, hides, or moves the original
/// level/Xp/HUD components. The tracker only exists while pins are active.
/// </summary>
internal static class MissionHudPresenter
{
    private static GameObject? _root;
    private static TextMesh? _text;
    private static int _levelLabelInstanceId;
    private static float _nextTick;
    private static bool _loggedAnchor;
    private static bool _warnedMissingAnchor;

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
            // The Community dump identifies:
            // PlayerAvatar.topHud.levelLabel. In the actual Spades client:
            // ceh.betm.bebt.bhch (ceh=PlayerCharacter, cbn=PlayerAvatar,
            // clf=TopHud, ft=SuperTextMesh).
            object? original = MissionProgressReader.CurrentLevelLabel();
            if (original is not Component label)
            {
                if (!_warnedMissingAnchor)
                {
                    _warnedMissingAnchor = true;
                    Plugin.Logger.LogInfo("[QuestHelper/HUD] Waiting for " +
                        "TopHud.levelLabel; original HUD untouched.");
                }
                if (_root != null) _root.SetActive(false);
                return;
            }

            int instanceId = label.GetInstanceID();
            if (_root == null || _text == null ||
                instanceId != _levelLabelInstanceId)
            {
                if (_root != null)
                    UnityEngine.Object.Destroy(_root);
                _root = null;
                _text = null;
                _levelLabelInstanceId = 0;
                CreateHud(label);
            }

            if (_text == null || _root == null) return;

            var rows = MissionProgressReader.ReadPinnedObjectives(
                MissionPinStore.PinnedIds, out int resolved);
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
                // Unknown counters stay unknown. No fabricated 0/35.
                display.Append(resolved > 0
                    ? "Pinned mission: no count objectives"
                    : "Pinned mission progress unavailable");
            }

            string value = display.ToString();
            if (_text.text != value) _text.text = value;
            _root.SetActive(true);
        }
        catch (Exception ex)
        {
            if (_root != null) _root.SetActive(false);
            if (!_warnedMissingAnchor)
            {
                _warnedMissingAnchor = true;
                Plugin.Logger.LogWarning("[QuestHelper/HUD] Tracker " +
                    "unavailable without affecting original HUD: " +
                    ex.GetType().Name);
            }
        }
    }

    private static void CreateHud(Component levelLabel)
    {
        Transform? parent = levelLabel.transform.parent;
        if (parent == null) return;

        _root = new GameObject("QuestHelper Pinned Objectives");
        // The top HUD uses a dedicated render layer on some Orbus clients.
        // Reusing that layer lets the text render in the same VR camera.
        _root.layer = levelLabel.gameObject.layer;
        _root.transform.SetParent(parent, false);
        // Position relative to the existing LEVEL label rather than
        // creating a head-locked popup or guessing world coordinates.
        _root.transform.localPosition = levelLabel.transform.localPosition +
            new Vector3(0f, -0.18f, 0f);
        _root.transform.localRotation = levelLabel.transform.localRotation;
        _root.transform.localScale = levelLabel.transform.localScale * 0.65f;

        _text = _root.AddComponent<TextMesh>();
        _text.fontSize = 48;
        _text.characterSize = 0.034f;
        _text.color = new Color(1f, 0.96f, 0.82f, 0.98f);
        _text.anchor = TextAnchor.UpperCenter;
        _text.alignment = TextAlignment.Center;
        _text.text = "";

        // Borrow ONLY the existing font asset, never manipulate the
        // level text, its backing renderer, or any game font files.
        object? existingFont =
            PowerUiRuntime.Get(levelLabel, "font") ??
            PowerUiRuntime.Get(levelLabel, "pym");
        if (existingFont is Font font)
        {
            _text.font = font;
            MeshRenderer? renderer = _root.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = font.material;
                MeshRenderer? originalRenderer =
                    levelLabel.gameObject.GetComponent<MeshRenderer>();
                if (originalRenderer != null)
                {
                    renderer.sortingLayerID = originalRenderer.sortingLayerID;
                    renderer.sortingOrder = originalRenderer.sortingOrder + 1;
                }
            }
        }

        _levelLabelInstanceId = levelLabel.GetInstanceID();
        if (!_loggedAnchor)
        {
            _loggedAnchor = true;
            Plugin.Logger.LogInfo("[QuestHelper/HUD] Pinned mission tracker " +
                "attached beside the game's existing TopHud level label.");
        }
    }
}
