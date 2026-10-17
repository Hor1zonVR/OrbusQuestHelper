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
/// Read-only, bounded mission/journal UI hierarchy inspector.
/// Reads Unity component types and visible, non-input text; never invokes quest handlers.
/// </summary>
public sealed class QuestUiWatcher : MonoBehaviour
{
    public QuestUiWatcher(IntPtr pointer) : base(pointer) { }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int key);

    private const int Ctrl = 0x11;
    private const int Alt = 0x12;
    private const int K = 0x4B;
    private const int MaxScan = 60000;
    private const int MaxAnchors = 12;
    private const int MaxNodes = 1200;
    private const int MaxDepth = 10;
    private const int MaxReports = 8;

    private float _next = 6f;
    private float _lastMissions = -999f;
    private float _lastJournal = -999f;
    private int _missionReports;
    private int _journalReports;
    private string _missionSignature = "";
    private string _journalSignature = "";
    private string _lastScene = "";
    private bool _previousChord;

    private static bool Held(int key) => (GetAsyncKeyState(key) & 0x8000) != 0;

    private void Update()
    {
        bool chord = Application.isFocused && Held(Ctrl) && Held(Alt) && Held(K);
        if (chord && !_previousChord)
            Scan(true);
        _previousChord = chord;

        if (Time.realtimeSinceStartup >= _next)
        {
            _next = Time.realtimeSinceStartup + 5f;
            Scan(false);
        }
    }

    private static string Clean(string? text, int maximum = 140)
    {
        if (string.IsNullOrEmpty(text)) return "";
        text = text.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ').Trim();
        return text.Length > maximum ? text.Substring(0, maximum) + "..." : text;
    }

    private static bool Live(Transform? transform)
    {
        try
        {
            return transform != null &&
                transform.gameObject.scene.IsValid() &&
                transform.gameObject.scene.isLoaded;
        }
        catch { return false; }
    }

    private static bool Mission(string value) =>
        value.Contains("mission", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("quest", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("objective", StringComparison.OrdinalIgnoreCase);

    private static bool Journal(string value) =>
        value.Contains("journal", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("story", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("logbook", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("questbook", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("bookpage", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("bookui", StringComparison.OrdinalIgnoreCase);

    private static string Path(Transform? t)
    {
        var pieces = new List<string>();
        int n = 0;
        while (t != null && n++ < 18)
        {
            pieces.Add(Clean(t.gameObject.name, 55));
            t = t.parent;
        }
        pieces.Reverse();
        return (t != null ? ".../" : "") + string.Join("/", pieces);
    }

    private static void Add(List<Transform> anchors, HashSet<int> seen, Transform? t)
    {
        if (t == null || !Live(t) || !t.gameObject.activeInHierarchy ||
            anchors.Count >= MaxAnchors) return;
        if (seen.Add(t.GetInstanceID())) anchors.Add(t);
    }

    private static void VisibleText(
        List<Transform> missions, HashSet<int> missionSeen,
        List<Transform> journals, HashSet<int> journalSeen,
        List<string> labels)
    {
        foreach (TextMesh text in UnityEngine.Object.FindObjectsOfType<TextMesh>())
        {
            if (text == null || !Live(text.transform) || !text.gameObject.activeInHierarchy)
                continue;
            string label;
            try { label = Clean(text.text, 120); } catch { continue; }
            if (!Mission(label) && !Journal(label)) continue;
            if (labels.Count < 40) labels.Add("TextMesh: " + label);
            var parent = text.transform.parent ?? text.transform;
            if (Mission(label)) Add(missions, missionSeen, parent);
            if (Journal(label)) Add(journals, journalSeen, parent);
        }
#if QUESTDIAG_UI
        foreach (Text text in UnityEngine.Object.FindObjectsOfType<Text>())
        {
            if (text == null || !Live(text.transform) || !text.gameObject.activeInHierarchy)
                continue;
            string label;
            try { label = Clean(text.text, 120); } catch { continue; }
            if (!Mission(label) && !Journal(label)) continue;
            if (labels.Count < 40) labels.Add("Unity UI Text: " + label);
            var parent = text.transform.parent ?? text.transform;
            if (Mission(label)) Add(missions, missionSeen, parent);
            if (Journal(label)) Add(journals, journalSeen, parent);
        }
#endif
    }

    private void Scan(bool manual)
    {
        try
        {
            string scene = Clean(SceneManager.GetActiveScene().name);
            if (_lastScene != scene)
            {
                _lastScene = scene;
                _missionSignature = "";
                _journalSignature = "";
                Plugin.Logger.LogInfo("[QuestDiag] Scene: " + scene);
            }

            var missions = new List<Transform>();
            var journals = new List<Transform>();
            var missionIds = new HashSet<int>();
            var journalIds = new HashSet<int>();
            var labels = new List<string>();
            int visited = 0;

            foreach (Transform t in UnityEngine.Object.FindObjectsOfType<Transform>())
            {
                if (!Live(t)) continue;
                if (++visited > MaxScan) break;
                if (!t.gameObject.activeInHierarchy) continue;
                string name = t.gameObject.name;
                if (Mission(name)) Add(missions, missionIds, t);
                if (Journal(name)) Add(journals, journalIds, t);
            }

            VisibleText(missions, missionIds, journals, journalIds, labels);
            float now = Time.realtimeSinceStartup;
            string ms = Signature(missions, labels, true);
            string js = Signature(journals, labels, false);

            if (manual)
            {
                Save("Manual", missions, journals, labels, visited);
                return;
            }

            if (missions.Count == 0) _missionSignature = "";
            else if (_missionReports < MaxReports &&
                     (ms != _missionSignature || now - _lastMissions >= 30f) &&
                     now - _lastMissions > 3f)
            {
                _missionSignature = ms;
                _lastMissions = now;
                _missionReports++;
                Save("Missions", missions, journals, labels, visited);
            }

            if (journals.Count == 0) _journalSignature = "";
            else if (_journalReports < MaxReports &&
                     (js != _journalSignature || now - _lastJournal >= 30f) &&
                     now - _lastJournal > 3f)
            {
                _journalSignature = js;
                _lastJournal = now;
                _journalReports++;
                Save("Journal", missions, journals, labels, visited);
            }
        }
        catch (Exception e)
        {
            Plugin.Logger.LogWarning("[QuestDiag] Scan failed: " + Clean(e.ToString(), 320));
        }
    }

    private static string Signature(List<Transform> anchors, List<string> labels, bool forMission)
    {
        var s = new StringBuilder();
        foreach (Transform t in anchors)
        {
            if (t == null) continue;
            s.Append(t.GetInstanceID()).Append(':').Append(Clean(t.gameObject.name, 60)).Append(';');
        }
        foreach (var label in labels)
            if ((forMission && Mission(label)) || (!forMission && Journal(label)))
                s.Append(label).Append(';');
        return s.ToString();
    }

    private static string NativeType(Component component)
    {
        try
        {
            IntPtr cls = IL2CPP.il2cpp_object_get_class(component.Pointer);
            if (cls != IntPtr.Zero)
            {
                string name = IL2CPP.il2cpp_class_get_name_(cls) ?? "";
                string ns = IL2CPP.il2cpp_class_get_namespace_(cls) ?? "";
                if (!string.IsNullOrEmpty(name))
                    return (string.IsNullOrEmpty(ns) ? "" : ns + ".") + name;
            }
        }
        catch { }
        return component.GetType().FullName ?? component.GetType().Name;
    }

    private static string Components(GameObject go)
    {
        var parts = new List<string>();
        try
        {
            foreach (Component component in go.GetComponents<Component>())
            {
                if (component == null) continue;
                if (parts.Count >= 14) { parts.Add("..."); break; }
                string type = NativeType(component);
                if (component is Renderer renderer)
                    type += "[enabled=" + renderer.enabled + "]";
                if (component is TextMesh label)
                    type += "[text='" + Clean(label.text, 220) + "']";
#if QUESTDIAG_UI
                if (component is Text uiText)
                    type += "[text='" + Clean(uiText.text, 220) + "']";
                if (component is Selectable selectable)
                    type += "[interactable=" + selectable.IsInteractable() + "]";
#endif
                parts.Add(type);
            }
        }
        catch (Exception ex) { parts.Add("inspection-error: " + Clean(ex.Message)); }
        return string.Join(", ", parts);
    }

    private static void Hierarchy(StringBuilder output, Transform anchor, HashSet<int> seen, ref int count)
    {
        Transform root = anchor.parent ?? anchor;
        var stack = new Stack<(Transform Node, int Depth)>();
        stack.Push((root, 0));

        while (stack.Count > 0 && count < MaxNodes)
        {
            var (t, depth) = stack.Pop();
            if (t == null || !Live(t) || depth > MaxDepth) continue;
            if (!seen.Add(t.GetInstanceID())) continue;
            count++;
            try
            {
                GameObject go = t.gameObject;
                Vector3 p = t.localPosition;
                output.Append(' ', Math.Min(depth, 12) * 2)
                    .Append("- ").Append(Clean(go.name, 90))
                    .Append(" active=").Append(go.activeInHierarchy)
                    .Append(" local=(").Append(p.x.ToString("0.00"))
                    .Append(",").Append(p.y.ToString("0.00"))
                    .Append(",").Append(p.z.ToString("0.00"))
                    .Append(") components: ").Append(Components(go)).AppendLine();

                int n = Math.Min(t.childCount, 200);
                for (int i = n - 1; i >= 0; i--) stack.Push((t.GetChild(i), depth + 1));
            }
            catch (Exception e)
            {
                output.AppendLine("  [object unreadable: " + Clean(e.Message) + "]");
            }
        }
    }

    private static void Save(string reason, List<Transform> missions,
        List<Transform> journals, List<string> labels, int visited)
    {
        try
        {
            var output = new StringBuilder(100_000);
            output.AppendLine("ORBUS QUESTHELPER 0.1.0 - READ-ONLY DIAGNOSTICS");
            output.AppendLine("Captured: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            output.AppendLine("Reason: " + reason);
            output.AppendLine("Visible labels may contain personal text. Review before sharing.");
            output.AppendLine("Scene: " + SceneManager.GetActiveScene().name);
            output.AppendLine("Scene count: " + SceneManager.sceneCount);
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                output.AppendLine("  " + Clean(scene.name) + " loaded=" + scene.isLoaded);
            }
            output.AppendLine("Scene transforms scanned: " + visited);
            output.AppendLine("Mission anchors: " + missions.Count);
            output.AppendLine("Journal anchors: " + journals.Count);
            output.AppendLine("Text components: TextMesh" +
#if QUESTDIAG_UI
                " and UnityEngine.UI.Text"
#else
                " (UnityEngine.UI.Text interop unavailable)"
#endif
            );
            output.AppendLine();
            output.AppendLine("=== VISIBLE RELEVANT LABELS ===");
            foreach (string label in labels) output.AppendLine(label);
            output.AppendLine();
            output.AppendLine("=== MISSION OBJECT PATHS ===");
            foreach (Transform t in missions) output.AppendLine(Path(t));
            output.AppendLine();
            output.AppendLine("=== JOURNAL OBJECT PATHS ===");
            foreach (Transform t in journals) output.AppendLine(Path(t));
            output.AppendLine();
            output.AppendLine("=== OBJECT HIERARCHY AND NATIVE COMPONENT TYPES ===");
            var seen = new HashSet<int>();
            int count = 0;
            foreach (Transform t in missions) Hierarchy(output, t, seen, ref count);
            foreach (Transform t in journals) Hierarchy(output, t, seen, ref count);
            output.AppendLine("Hierarchy nodes: " + count);
            if (count >= MaxNodes) output.AppendLine("Report truncated at node safety limit.");
            if (missions.Count == 0 && journals.Count == 0)
                output.AppendLine("No active targets found; keep menu open or use Ctrl+Alt+K.");

            string dir = System.IO.Path.Combine(Paths.ConfigPath, "OrbusQuestHelper", "Diagnostics");
            Directory.CreateDirectory(dir);
            string filename = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "_" + reason + ".txt";
            string path = System.IO.Path.Combine(dir, filename);
            File.WriteAllText(path, output.ToString(), Encoding.UTF8);
            Plugin.Logger.LogInfo("[QuestDiag] Saved " + reason + " snapshot to " + path);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogWarning("[QuestDiag] Report failed: " + Clean(e.ToString(), 400));
        }
    }
}
