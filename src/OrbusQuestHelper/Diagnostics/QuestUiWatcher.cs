using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using BepInEx;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;
#if QUESTDIAG_UI
using UnityEngine.UI;
#endif

namespace OrbusQuestHelper;

/// <summary>
/// Bounded read-only survey of the actual VR UI and native IL2CPP component names.
/// No quest methods, inputs, progress state, or gameplay objects are changed.
/// </summary>
public sealed class QuestUiWatcher : MonoBehaviour
{
    public QuestUiWatcher(IntPtr pointer) : base(pointer) { }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int key);

    private const int Ctrl = 0x11, Alt = 0x12, K = 0x4B;
    private const int MaxObjects = 22000;
    private const int MaxCandidates = 220;
    private const int MaxHierarchyNodes = 650;
    private const int MaxHierarchyDepth = 4;
    private const int MaxMissionUi = 160;
    private const int MaxInventoryUi = 200;
    private const int MaxCanvasUi = 100;
    private const int MaxUiText = 160;
    private const int MaxNativeCanvases = 120;
    private const int MaxInventoryIcons = 200;
    private const int MaxGameplayTypes = 125;
    // Frequent captures are useful while navigating VR menus, but keep an upper bound.
    private const int AutoCaptures = 120;
    private const int ManualCaptures = 30;
    private const float CaptureIntervalSeconds = 5f;

    private bool _previousKey;
    private bool _busy;
    private float _nextAutomatic;
    private int _automaticCount;
    private int _manualCount;
    private int _snapshotCount;
    private string _sessionReportPath = "";

    private sealed class Candidate
    {
        public Transform Node = null!;
        public string Types = "";
        public int Score;
        public bool IsSuperText;
        public bool IsGameScript;
        public string Path = "";
    }

    private static bool Held(int key) => (GetAsyncKeyState(key) & 0x8000) != 0;

    private void Start()
    {
        _nextAutomatic = Time.realtimeSinceStartup + CaptureIntervalSeconds;
        Plugin.Logger.LogInfo("[QuestDiag] One combined report per game session, with timestamped captures.");
        Plugin.Logger.LogInfo("[QuestDiag] Auto snapshot every 5 seconds regardless of current scene (max 120), plus Ctrl+Alt+K manual captures.");
        Plugin.Logger.LogInfo("[QuestDiag] The session report is appended to after each successful snapshot.");
    }

    private void Update()
    {
        bool pressed = Application.isFocused && Held(Ctrl) && Held(Alt) && Held(K);
        if (pressed && !_previousKey && !_busy && _manualCount < ManualCaptures)
        {
            _manualCount++;
            Capture("Manual");
        }
        _previousKey = pressed;

        if (!_busy && _automaticCount < AutoCaptures &&
            Time.realtimeSinceStartup >= _nextAutomatic)
        {
            _nextAutomatic = Time.realtimeSinceStartup + CaptureIntervalSeconds;
            // Community Edition does not always load a scene named "Overworld".
            // Highsteppe, player house, character select and instanced areas all use
            // distinct scene names. Never gate read-only diagnostics on one scene name.
            _automaticCount++;
            Capture("Auto" + _automaticCount);
        }
    }

    private static string Clean(string? value, int max = 100)
    {
        if (string.IsNullOrWhiteSpace(value)) return "(empty)";
        string s = value.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ').Trim();
        return s.Length > max ? s.Substring(0, max) + "..." : s;
    }

    private static bool Live(Transform t)
    {
        try
        {
            var scene = t.gameObject.scene;
            return scene.IsValid() && scene.isLoaded;
        }
        catch { return false; }
    }

    private static string PathOf(Transform t)
    {
        var parts = new List<string>();
        Transform? cursor = t;
        while (cursor != null && parts.Count < 18)
        {
            parts.Add(Clean(cursor.gameObject.name, 65));
            cursor = cursor.parent;
        }
        parts.Reverse();
        return (cursor != null ? ".../" : "") + string.Join("/", parts);
    }

    private static string NativeType(Component component)
    {
        try
        {
            IntPtr klass = IL2CPP.il2cpp_object_get_class(component.Pointer);
            if (klass != IntPtr.Zero)
            {
                string type = IL2CPP.il2cpp_class_get_name_(klass) ?? "";
                string ns = IL2CPP.il2cpp_class_get_namespace_(klass) ?? "";
                if (type.Length > 0) return ns.Length == 0 ? type : ns + "." + type;
            }
        }
        catch { }
        return component.GetType().FullName ?? component.GetType().Name;
    }

    private static bool RelevantName(string value)
    {
        string s = value.ToLowerInvariant();
        return s.Contains("mission") || s.Contains("journal") ||
            s.Contains("quest") || s.Contains("objective") ||
            s.Contains("tooltip") || s.Contains("reward") ||
            s.Contains("inventory") || s.Contains("menu") ||
            s.Contains("book") || s.Contains("page") ||
            s.Contains("hover") || s.Contains("ui_") ||
            s.Contains("panel") || s.Contains("canvas");
    }

    private static int NameScore(string value)
    {
        string s = value.ToLowerInvariant();
        if (s.Contains("mission") || s.Contains("journal") ||
            s.Contains("quest") || s.Contains("objective"))
            return 40;
        if (s.Contains("inventory") || s.Contains("tooltip") ||
            s.Contains("reward") || s.Contains("book") ||
            s.Contains("page"))
            return 26;
        if (RelevantName(value)) return 14;
        return 0;
    }

    private static string Types(GameObject go, out bool superText, out bool gameScript,
        out int scriptScore, out bool hasOnlyMeshes)
    {
        superText = false;
        gameScript = false;
        scriptScore = 0;
        hasOnlyMeshes = true;
        var parts = new List<string>();
        try
        {
            foreach (Component c in go.GetComponents<Component>())
            {
                if (c == null) continue;
                string type = NativeType(c);
                if (parts.Count < 16) parts.Add(Clean(type, 100));

                if (type.IndexOf("SuperTextMesh", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    type.IndexOf("TextMeshPro", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    superText = true;
                    scriptScore = Math.Max(scriptScore, 72);
                }
                if (type.StartsWith("MagicalActual.", StringComparison.Ordinal) ||
                    (type.IndexOf("UnityEngine.", StringComparison.Ordinal) < 0 &&
                     type.IndexOf(".", StringComparison.Ordinal) < 0 &&
                     !type.StartsWith("qb_", StringComparison.Ordinal)))
                {
                    gameScript = true;
                    hasOnlyMeshes = false;
                    if (RelevantName(type)) scriptScore = Math.Max(scriptScore, 100);
                    else scriptScore = Math.Max(scriptScore, 12);
                }
                if (c is Collider || c is Renderer || c is MeshFilter ||
                    c is Transform || c is LODGroup)
                {
                    // Standard scenery components alone do not indicate quest UI.
                }
                else
                {
                    hasOnlyMeshes = false;
                }
#if QUESTDIAG_UI
                if (c is Canvas || c is Selectable || c is GraphicRaycaster)
                {
                    hasOnlyMeshes = false;
                    scriptScore = Math.Max(scriptScore, 40);
                }
#endif
                if (c is TextMesh)
                {
                    hasOnlyMeshes = false;
                    scriptScore = Math.Max(scriptScore, 40);
                }
            }
        }
        catch (Exception ex) { parts.Add("error:" + Clean(ex.Message, 70)); }
        return string.Join(", ", parts);
    }

    private static string Location(Transform t)
    {
        try
        {
            Vector3 p = t.position;
            return "(" + p.x.ToString("0.0") + "," + p.y.ToString("0.0") +
                "," + p.z.ToString("0.0") + ")";
        }
        catch { return "(unreadable)"; }
    }

    private static void AppendHierarchy(StringBuilder report, Transform focus,
        HashSet<int> seen, ref int total, int ancestors = 2)
    {
        // Default: include nearby siblings; focused reports: inspect selected root.
        Transform root = focus;
        for (int i = 0; i < ancestors && root.parent != null; i++) root = root.parent;
        var stack = new Stack<(Transform Node, int Depth)>();
        stack.Push((root, 0));

        while (stack.Count > 0 && total < MaxHierarchyNodes)
        {
            var (t, depth) = stack.Pop();
            if (t == null || !Live(t) || depth > MaxHierarchyDepth) continue;
            if (!seen.Add(t.GetInstanceID())) continue;
            total++;
            try
            {
                string types = Types(t.gameObject, out _, out _, out _, out _);
                report.Append(' ', depth * 2).Append(Clean(t.gameObject.name, 80))
                    .Append(" active=").Append(t.gameObject.activeInHierarchy)
                    .Append(" children=").Append(t.childCount)
                    .Append(" components=[").Append(types).AppendLine("]");
                int n = Math.Min(t.childCount, 35);
                for (int i = n - 1; i >= 0; i--)
                    stack.Push((t.GetChild(i), depth + 1));
                if (n != t.childCount)
                    report.AppendLine("  ... more children omitted");
            }
            catch (Exception ex)
            {
                report.AppendLine("  inspection-error: " + Clean(ex.Message));
            }
        }
    }


    private static bool Has(string value, string fragment) =>
        value.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0;

    private static bool ContainsNativeType(string nativeNames, string target)
    {
        foreach (string part in nativeNames.Split(','))
            if (string.Equals(part.Trim(), target, StringComparison.Ordinal))
                return true;
        return false;
    }

    private static void AddGameplayTypes(Dictionary<string, int> counts,
        string nativeNames)
    {
        foreach (string item in nativeNames.Split(','))
        {
            string type = item.Trim();
            if (!type.StartsWith("MagicalActual.", StringComparison.Ordinal) &&
                !type.StartsWith("ExtUI.", StringComparison.Ordinal) &&
                !type.StartsWith("PlayfulSystems.", StringComparison.Ordinal) &&
                !type.StartsWith("Wacki.", StringComparison.Ordinal))
                continue;
            counts.TryGetValue(type, out int previous);
            counts[type] = previous + 1;
        }
    }

    private static void WriteGameplayTypes(StringBuilder report,
        Dictionary<string, int> counts)
    {
        report.AppendLine();
        report.AppendLine("=== GAME UI / BEHAVIOUR COMPONENT TYPE INVENTORY ===");
        report.AppendLine("This lists native component type names, NOT quest instances.");
        var entries = new List<KeyValuePair<string, int>>(counts);
        entries.Sort((a, b) =>
        {
            bool ar = Has(a.Key, "inventory") || Has(a.Key, "mission") ||
                Has(a.Key, "quest") || Has(a.Key, "menu") || Has(a.Key, "ui");
            bool br = Has(b.Key, "inventory") || Has(b.Key, "mission") ||
                Has(b.Key, "quest") || Has(b.Key, "menu") || Has(b.Key, "ui");
            if (ar != br) return br.CompareTo(ar);
            int difference = b.Value.CompareTo(a.Value);
            return difference != 0 ? difference : string.CompareOrdinal(a.Key, b.Key);
        });
        for (int i = 0; i < Math.Min(entries.Count, MaxGameplayTypes); i++)
            report.Append(entries[i].Value).Append(" x ")
                .AppendLine(entries[i].Key);
        report.AppendLine("Different UI may also be rendered without a MonoBehaviour.");
    }

    private static void ListFocused(StringBuilder report, string heading,
        List<Candidate> nodes, int limit)
    {
        report.AppendLine();
        report.AppendLine("=== " + heading + " ===");
        nodes.Sort((a, b) => b.Score.CompareTo(a.Score));
        report.AppendLine("Total matches: " + nodes.Count + ", showing up to " + limit);
        for (int i = 0; i < Math.Min(limit, nodes.Count); i++)
        {
            var c = nodes[i];
            try
            {
                report.Append("[").Append(i + 1).Append("] score=").Append(c.Score)
                    .Append(" active=").Append(c.Node.gameObject.activeInHierarchy)
                    .Append(" children=").Append(c.Node.childCount)
                    .AppendLine();
                report.Append("  ").AppendLine(c.Path);
                report.Append("  types: ").AppendLine(c.Types);
            }
            catch (Exception e) { report.AppendLine("  skipped: " + Clean(e.Message)); }
        }
    }

    private static void CaptureVisibleUiText(StringBuilder report)
    {
        report.AppendLine();
        report.AppendLine("=== ACTIVE UI TEXT: MISSION/INVENTORY KEYWORDS ===");
        int count = 0;
#if QUESTDIAG_UI
        foreach (Text label in UnityEngine.Object.FindObjectsOfType<Text>())
        {
            if (label == null || !label.gameObject.activeInHierarchy) continue;
            try
            {
                string value = Clean(label.text, 200);
                if (value == "(empty)") continue;
                string path = PathOf(label.transform);
                // Keep text relevant to menu discovery; do not collect unrelated chat.
                if (!Has(value, "mission") && !Has(value, "quest") &&
                    !Has(value, "reward") && !Has(value, "talent") &&
                    !Has(value, "achievement") && !Has(value, "weekly") &&
                    !Has(value, "progress") && !Has(path, "inventory") &&
                    !Has(path, "mission") && !Has(path, "quest") &&
                    !Has(path, "tooltip")) continue;
                report.Append("[").Append(++count).Append("] ")
                    .Append(path).Append(" : '").Append(value).AppendLine("'");
                if (count >= MaxUiText) break;
            }
            catch (Exception e) { report.AppendLine("  Text read error: " + Clean(e.Message)); }
        }
#else
        report.AppendLine("UnityEngine.UI.Text interop unavailable in this build.");
#endif
        report.AppendLine("Active relevant UI text found: " + count);
        report.AppendLine("Custom SuperTextMesh body text is not read by this build.");
    }

    private static void CaptureFocusedHierarchies(StringBuilder report,
        List<Candidate> mission, List<Candidate> inventory)
    {
        report.AppendLine();
        report.AppendLine("=== FOCUSED MISSION / INVENTORY UI HIERARCHIES ===");
        int total = 0;
        int roots = 0;
        var seen = new HashSet<int>();
        var rootsSeen = new HashSet<int>();
        foreach (var group in new[] { mission, inventory })
        {
            foreach (Candidate c in group)
            {
                if (roots >= 12 || total >= MaxHierarchyNodes) break;
                if (c.Node == null) continue;
                if (!c.Node.gameObject.activeInHierarchy && roots >= 7) continue;
                Transform root = c.Node;
                // For individual UI buttons/icons, inspect their immediate parent.
                if (c.Node.childCount == 0 && root.parent != null) root = root.parent;
                if (!rootsSeen.Add(root.GetInstanceID())) continue;
                report.AppendLine("ROOT " + PathOf(root));
                AppendHierarchy(report, root, seen, ref total, ancestors: 0);
                roots++;
            }
        }
        report.AppendLine("Focused hierarchy roots: " + roots + "; nodes: " + total);
    }

    private void Capture(string reason)
    {
        _busy = true;
        try
        {
            var candidates = new List<Candidate>();
            var missionUi = new List<Candidate>();
            var inventoryUi = new List<Candidate>();
            var activeCanvases = new List<Candidate>();
            var inventoryIcons = new List<Candidate>();
            var gameplayTypes = new Dictionary<string, int>(StringComparer.Ordinal);
            int allRealCanvases = 0, allInventoryIcons = 0, wardrobeIcons = 0;
            var report = new StringBuilder(120_000);
            report.AppendLine("==================================================================");
            report.AppendLine("QUESTHELPER UI SNAPSHOT " + (_snapshotCount + 1) + " - " + reason);
            report.AppendLine("Captured: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            report.AppendLine("==================================================================");
            report.AppendLine("ORBUS QUESTHELPER v0.1.4 - UI DISCOVERY (READ ONLY)");
            report.AppendLine("No quest data was modified. Custom SuperTextMesh text is NOT read yet.");
            report.AppendLine("Paths can expose game-visible labels; review before sharing.");
            report.AppendLine();
            report.AppendLine("=== LOADED SCENES ===");
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                report.AppendLine(" " + Clean(scene.name) + " loaded=" + scene.isLoaded);
            }
            int visited = 0;
            int superTextCount = 0;
            int gameScriptCount = 0;
            foreach (Transform t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (t == null || !Live(t)) continue;
                if (++visited > MaxObjects) break;

                GameObject go;
                try { go = t.gameObject; }
                catch { continue; }
                string name = go.name;
                string types = Types(go, out bool superText, out bool gameScript,
                    out int scriptScore, out bool onlyMeshes);
                AddGameplayTypes(gameplayTypes, types);
                if (superText) superTextCount++;
                if (gameScript) gameScriptCount++;

                // v0.1.0 falsely treated terrain meshes ending in _Quest as mission UI.
                if (name.EndsWith("_Quest", StringComparison.OrdinalIgnoreCase) && onlyMeshes)
                    continue;

                int score = NameScore(name) + scriptScore;
                if (go.activeInHierarchy) score += 30;
                if (superText) score += 15;

                // Dedicated inventories are NEVER excluded by the general ranked limit.
                // In v0.1.4, library books and wardrobe objects displaced the real UI.
                string path = "";
                bool realCanvas = ContainsNativeType(types, "UnityEngine.Canvas");
                bool inventoryIcon = ContainsNativeType(types, "MagicalActual.InventoryIcon");
                bool missionRelated = Has(name, "mission") || Has(name, "quest") ||
                    Has(name, "objective") || Has(types, "mission") ||
                    Has(types, "quest") || Has(types, "objective");
                bool inventoryRelated = Has(name, "inventory") || Has(name, "tooltip") ||
                    Has(name, "mission") || Has(name, "quest") ||
                    Has(types, "inventory") || Has(types, "menu") ||
                    Has(types, "tooltip") || Has(types, "quest") ||
                    Has(types, "mission");
                bool likelyScenery = Has(types, "QuestCollider") ||
                    (Has(name, "TutorialQuest") && !Has(types, "Canvas"));
                if (missionRelated || inventoryRelated ||
                    realCanvas || inventoryIcon)
                {
                    path = PathOf(t);
                    bool houseWardrobe = Has(path, "/Wardrobe/") ||
                        Has(path, "/PlayerWardrobe/") ||
                        Has(path, "/TransmogStation");
                    var record = new Candidate
                    {
                        Node = t,
                        Types = types,
                        Score = score + (Has(path, "PlayerRigv4") ? 35 : 0) +
                            (Has(types, "MagicalActual.") ? 15 : 0),
                        Path = path,
                        IsSuperText = superText,
                        IsGameScript = gameScript
                    };
                    if (missionRelated && !likelyScenery && !houseWardrobe)
                        missionUi.Add(record);
                    if (inventoryRelated && !houseWardrobe && !likelyScenery)
                        inventoryUi.Add(record);
                    if (realCanvas)
                    {
                        allRealCanvases++;
                        if (go.activeInHierarchy && !houseWardrobe)
                            activeCanvases.Add(record);
                    }
                    if (inventoryIcon)
                    {
                        allInventoryIcons++;
                        if (houseWardrobe) wardrobeIcons++;
                        else inventoryIcons.Add(record);
                    }
                }
                if (score >= 25)
                {
                    candidates.Add(new Candidate
                    {
                        Node = t,
                        Types = types,
                        Score = score,
                        Path = path,
                        IsSuperText = superText,
                        IsGameScript = gameScript
                    });
                }
            }
            candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
            report.AppendLine();
            report.AppendLine("=== SURVEY SUMMARY ===");
            report.AppendLine("Loaded scene transforms inspected: " + visited);
            report.AppendLine("Objects with SuperTextMesh/TextMeshPro: " + superTextCount);
            report.AppendLine("Objects with custom game components: " + gameScriptCount);
            report.AppendLine("UI/quest candidates ranked: " + candidates.Count);
            report.AppendLine("Active and inactive objects included.");
            report.AppendLine("A candidate is not proof of an active mission or journal.");
            report.AppendLine("Targeted UI matches: mission=" + missionUi.Count +
                ", inventory=" + inventoryUi.Count +
                ", real active canvases outside wardrobe=" + activeCanvases.Count);
            report.AppendLine("Real Unity Canvas components: " + allRealCanvases +
                " (CanvasRenderer is NOT counted as a Canvas)");
            report.AppendLine("MagicalActual.InventoryIcon components: " + allInventoryIcons +
                ", wardrobe excluded=" + wardrobeIcons +
                ", elsewhere=" + inventoryIcons.Count);
            report.AppendLine();

            // Independent sections: never drown out mission UI behind wardrobe/books.
            ListFocused(report, "MISSION / QUEST UI COMPONENTS", missionUi, MaxMissionUi);
            ListFocused(report, "INVENTORY / MENU COMPONENTS (WARDROBE EXCLUDED)",
                inventoryUi, MaxInventoryUi);
            ListFocused(report, "REAL ACTIVE UNITY CANVASES OUTSIDE WARDROBE",
                activeCanvases, MaxNativeCanvases);
            ListFocused(report, "INVENTORY ICONS OUTSIDE WARDROBE",
                inventoryIcons, MaxInventoryIcons);
            WriteGameplayTypes(report, gameplayTypes);
            CaptureVisibleUiText(report);
            CaptureFocusedHierarchies(report, missionUi, inventoryUi);

            report.AppendLine();
            report.AppendLine("=== TOP CANDIDATES: NATIVE TYPES AND FULL PATHS ===");
            int shown = Math.Min(MaxCandidates, candidates.Count);
            for (int i = 0; i < shown; i++)
            {
                var c = candidates[i];
                try
                {
                    report.Append("[").Append(i + 1).Append("] score=").Append(c.Score)
                        .Append(" active=").Append(c.Node.gameObject.activeInHierarchy)
                        .Append(" SuperText=").Append(c.IsSuperText)
                        .Append(" script=").Append(c.IsGameScript)
                        .Append(" position=").Append(Location(c.Node))
                        .AppendLine();
                    report.Append("  ").AppendLine(PathOf(c.Node));
                    report.Append("  components: ").AppendLine(c.Types);
                }
                catch (Exception ex) { report.AppendLine("  candidate-error: " + Clean(ex.Message)); }
            }
            if (shown < candidates.Count)
                report.AppendLine("Remaining candidates omitted to keep report readable.");

            report.AppendLine();
            report.AppendLine("=== CANDIDATE NEIGHBOURHOOD HIERARCHIES ===");
            var selected = new HashSet<int>();
            var seen = new HashSet<int>();
            int nodes = 0;
            int anchors = 0;
            // Prioritize custom rendering and game scripts; skip ordinary terrain.
            for (int i = 0; i < candidates.Count && anchors < 14; i++)
            {
                var c = candidates[i];
                if (c.Score < 65 || (!c.IsSuperText && !c.IsGameScript)) continue;
                Transform focus = c.Node;
                Transform root = focus;
                for (int p = 0; p < 2 && root.parent != null; p++) root = root.parent;
                if (!selected.Add(root.GetInstanceID())) continue;
                report.AppendLine("FOCUS " + PathOf(focus));
                AppendHierarchy(report, focus, seen, ref nodes);
                anchors++;
            }
            report.AppendLine("Neighbourhood anchors: " + anchors +
                "; hierarchy nodes: " + nodes + " (cap " + MaxHierarchyNodes + ")");
            report.AppendLine();

            // Keep all automatic and manual snapshots in one file per game run.
            // Create the path only after a snapshot succeeds, never once per capture.
            if (_sessionReportPath.Length == 0)
            {
                string dir = Path.Combine(Paths.ConfigPath, "OrbusQuestHelper", "Diagnostics");
                Directory.CreateDirectory(dir);
                _sessionReportPath = Path.Combine(dir,
                    DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "_QuestHelperSession.txt");
                File.WriteAllText(_sessionReportPath,
                    "ORBUS QUESTHELPER v0.1.4 - COMBINED UI DIAGNOSTICS" + Environment.NewLine +
                    "Session started: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + Environment.NewLine +
                    "Contains up to " + AutoCaptures + " automatic and " + ManualCaptures +
                    " manual snapshots. No gameplay state is modified." + Environment.NewLine +
                    Environment.NewLine, Encoding.UTF8);
            }
            report.AppendLine();
            File.AppendAllText(_sessionReportPath, report.ToString(), Encoding.UTF8);
            _snapshotCount++;
            Plugin.Logger.LogInfo("[QuestDiag] Appended snapshot " + _snapshotCount +
                " (" + reason + ") to " + _sessionReportPath);
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning("[QuestDiag] Capture failed: " + Clean(ex.ToString(), 350));
        }
        finally { _busy = false; }
    }
}
