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
    // Frequent captures are useful while navigating VR menus, but keep an upper bound.
    private const int AutoCaptures = 40;
    private const int ManualCaptures = 30;

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
    }

    private static bool Held(int key) => (GetAsyncKeyState(key) & 0x8000) != 0;

    private void Start()
    {
        _nextAutomatic = Time.realtimeSinceStartup + 20f;
        Plugin.Logger.LogInfo("[QuestDiag] One combined report per game session, with timestamped captures.");
        Plugin.Logger.LogInfo("[QuestDiag] Auto snapshot every 20 seconds in the Overworld (max 40), plus Ctrl+Alt+K manual captures.");
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
            _nextAutomatic = Time.realtimeSinceStartup + 20f;
            if (IsWorldLoaded())
            {
                _automaticCount++;
                Capture("Auto" + _automaticCount);
            }
        }
    }

    private static bool IsWorldLoaded()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (scene.isLoaded && scene.name.IndexOf("Overworld", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
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
        HashSet<int> seen, ref int total)
    {
        // Two ancestors expose sibling entries such as mission icons and journal pages.
        Transform root = focus;
        for (int i = 0; i < 2 && root.parent != null; i++) root = root.parent;
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

    private void Capture(string reason)
    {
        _busy = true;
        try
        {
            var candidates = new List<Candidate>();
            var report = new StringBuilder(120_000);
            report.AppendLine("==================================================================");
            report.AppendLine("QUESTHELPER UI SNAPSHOT " + (_snapshotCount + 1) + " - " + reason);
            report.AppendLine("Captured: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            report.AppendLine("==================================================================");
            report.AppendLine("ORBUS QUESTHELPER v0.1.1 - UI DISCOVERY (READ ONLY)");
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
                if (superText) superTextCount++;
                if (gameScript) gameScriptCount++;

                // v0.1.0 falsely treated terrain meshes ending in _Quest as mission UI.
                if (name.EndsWith("_Quest", StringComparison.OrdinalIgnoreCase) && onlyMeshes)
                    continue;

                int score = NameScore(name) + scriptScore;
                if (score < 25) continue;
                if (go.activeInHierarchy) score += 30;
                if (superText) score += 15;

                candidates.Add(new Candidate
                {
                    Node = t,
                    Types = types,
                    Score = score,
                    IsSuperText = superText,
                    IsGameScript = gameScript
                });
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
                    "ORBUS QUESTHELPER v0.1.1 - COMBINED UI DIAGNOSTICS" + Environment.NewLine +
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
