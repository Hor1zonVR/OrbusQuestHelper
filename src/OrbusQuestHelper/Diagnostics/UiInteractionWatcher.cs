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
using UnityEngine.EventSystems;
using UnityEngine.UI;
#endif

namespace OrbusQuestHelper;

/// <summary>
/// Read-only VR pointer and UI selection observer.
/// No input consumption, hooks, game callbacks, movement, or quest state changes.
/// Polls physical laser-pointer rays and Unity's selected UI object.
/// </summary>
public sealed class UiInteractionWatcher : MonoBehaviour
{
    public UiInteractionWatcher(IntPtr pointer) : base(pointer) { }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int key);

    private const int Ctrl = 0x11, Alt = 0x12, K = 0x4B;
    private const float PollInterval = 0.15f;
    private const float DiscoverInterval = 3f;
    private const float MaximumRayDistance = 16f;
    private const int MaxEvents = 2200;
    private const int MaxManualScans = 30;
    private const int MaxManualCanvases = 125;
    private const int MaxManualSelectables = 180;

    private readonly Transform?[] _pointers = new Transform?[2];
    private readonly int[] _lastRayTargets = { int.MinValue, int.MinValue };
    private readonly bool[] _lastReverse = new bool[2];
    private float _nextPoll;
    private float _nextDiscovery;
    private float _lastWrite;
    private float _nextNoHitStatus;
    private int _lastSelection = int.MinValue;
    private int _events;
    private int _manualScans;
    private bool _previousHotkey;
    private string _path = "";
    private readonly StringBuilder _buffer = new(100_000);

    private static bool Held(int key) => (GetAsyncKeyState(key) & 0x8000) != 0;

    private void Start()
    {
        try
        {
            string dir = Path.Combine(Paths.ConfigPath, "OrbusQuestHelper", "Diagnostics");
            Directory.CreateDirectory(dir);
            _path = Path.Combine(dir,
                DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "_UiInteractions.txt");
            File.WriteAllText(_path,
                "ORBUS QUESTHELPER v0.1.5 - READ-ONLY VR UI INTERACTION SCANNER" +
                Environment.NewLine +
                "Session started: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") +
                Environment.NewLine +
                "Sources: VR pointer physics raycast, EventSystem selection, manual UI survey." +
                Environment.NewLine +
                "A raycast candidate is NOT proof of a UI hover or click; canvas-only UI may be missed." +
                Environment.NewLine +
                "No inputs are consumed. Do not share a log containing private text." +
                Environment.NewLine + Environment.NewLine, Encoding.UTF8);
            Plugin.Logger.LogInfo("[QuestUI] Interaction scanner ready: " + _path);
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning("[QuestUI] Could not create report: " + Safe(ex.Message));
        }

        _nextPoll = Time.realtimeSinceStartup + 1f;
        _nextDiscovery = Time.realtimeSinceStartup + 1f;
        _lastWrite = Time.realtimeSinceStartup;
    }

    private void Update()
    {
        float now = Time.realtimeSinceStartup;
        if (now >= _nextDiscovery)
        {
            _nextDiscovery = now + DiscoverInterval;
            DiscoverPointers();
        }

        if (now >= _nextPoll)
        {
            _nextPoll = now + PollInterval;
            if (_events < MaxEvents)
            {
                InspectRay(0);
                InspectRay(1);
                InspectEventSelection();
            }
        }

        bool hotkey = Application.isFocused && Held(Ctrl) && Held(Alt) && Held(K);
        if (hotkey && !_previousHotkey && _manualScans < MaxManualScans)
        {
            _manualScans++;
            ManualUiSurvey();
        }
        _previousHotkey = hotkey;

        if (_buffer.Length > 0 && now - _lastWrite >= 1.5f)
            Flush();

        if (now >= _nextNoHitStatus)
        {
            _nextNoHitStatus = now + 25f;
            if (_events == 0)
                Log("STATUS: no detected selection or ray target changes yet. " +
                    "Use Ctrl+Alt+K to capture active canvases; confirm pointers are found.");
        }
    }

    private void OnDisable() => Flush();
    private void OnDestroy() => Flush();

    private static string Safe(string? value, int limit = 145)
    {
        if (string.IsNullOrEmpty(value)) return "(none)";
        var s = value.Replace('\n', ' ').Replace('\r', ' ').Replace('\t', ' ').Trim();
        return s.Length <= limit ? s : s.Substring(0, limit) + "...";
    }

    private static bool IsSceneObject(Transform t)
    {
        try
        {
            Scene s = t.gameObject.scene;
            return s.IsValid() && s.isLoaded;
        }
        catch { return false; }
    }

    private static string NativeType(Component component)
    {
        try
        {
            IntPtr klass = IL2CPP.il2cpp_object_get_class(component.Pointer);
            if (klass != IntPtr.Zero)
            {
                var name = IL2CPP.il2cpp_class_get_name_(klass) ?? "";
                var ns = IL2CPP.il2cpp_class_get_namespace_(klass) ?? "";
                if (name.Length > 0) return ns.Length == 0 ? name : ns + "." + name;
            }
        }
        catch { }
        return component.GetType().FullName ?? component.GetType().Name;
    }

    private static string Components(GameObject gameObject)
    {
        var text = new StringBuilder();
        int count = 0;
        try
        {
            foreach (Component c in gameObject.GetComponents<Component>())
            {
                if (c == null) continue;
                if (count++ > 0) text.Append(", ");
                text.Append(Safe(NativeType(c), 110));
                if (count == 18) { text.Append(", ..."); break; }
            }
        }
        catch (Exception ex) { text.Append(" [error: ").Append(Safe(ex.Message)).Append(']'); }
        return text.ToString();
    }

    private static string HierarchyPath(Transform t)
    {
        var levels = new List<string>();
        Transform? cur = t;
        while (cur != null && levels.Count < 24)
        {
            levels.Add(Safe(cur.gameObject.name, 72));
            cur = cur.parent;
        }
        levels.Reverse();
        return (cur != null ? ".../" : "") + string.Join("/", levels);
    }

    private static bool Has(string s, string value) =>
        s.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;

    private void DiscoverPointers()
    {
        if (_pointers[0] != null && _pointers[1] != null) return;
        int inspected = 0;
        try
        {
            foreach (Transform t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (t == null || !IsSceneObject(t)) continue;
                if (++inspected > 25000) break;
                if (t.gameObject.name != "UIPointer") continue;
                var parent = t.parent;
                if (parent == null || parent.gameObject.name != "Grabber") continue;
                var controller = parent.parent;
                if (controller == null) continue;
                int hand = Has(controller.gameObject.name, "left") ? 0 :
                    Has(controller.gameObject.name, "right") ? 1 : -1;
                if (hand < 0 || _pointers[hand] != null) continue;
                _pointers[hand] = t;
                Log("POINTER DISCOVERED: " + (hand == 0 ? "LEFT" : "RIGHT") +
                    " path=" + HierarchyPath(t) +
                    " components=[" + Components(t.gameObject) + "]");
            }
        }
        catch (Exception ex) { Log("POINTER DISCOVERY ERROR: " + Safe(ex.Message)); }
    }

    private sealed class HitTarget
    {
        public Transform Node = null!;
        public float Distance;
        public bool Reversed;
        public int UiScore;
    }

    private static bool IsPrivateTextPath(string path) =>
        Has(path, "chat") || Has(path, "password") ||
        Has(path, "login") || Has(path, "keyboard") ||
        Has(path, "inputfield") || Has(path, "username");

    private static int UiScore(Transform target)
    {
        int score = 0;
        Transform? cur = target;
        for (int depth = 0; depth < 9 && cur != null; depth++, cur = cur.parent)
        {
            string n = cur.gameObject.name;
            if (Has(n, "inventory") || Has(n, "mission") || Has(n, "quest") ||
                Has(n, "menu") || Has(n, "weekly") || Has(n, "tooltip") ||
                Has(n, "ui") || Has(n, "button") || Has(n, "icon") ||
                Has(n, "canvas") || Has(n, "scroll") || Has(n, "panel") ||
                Has(n, "content") || Has(n, "grid") || Has(n, "journal"))
                score += Math.Max(1, 10 - depth);
            // Hover target components can be more informative than their names.
            if (depth <= 3)
            {
                string types = Components(cur.gameObject);
                if (Has(types, "Canvas") || Has(types, "Selectable") ||
                    Has(types, "InventoryIcon") || Has(types, "JournalButton") ||
                    Has(types, "ScrollRect") || Has(types, "GraphicRaycaster"))
                    score += 20;
            }
        }
        return score;
    }

    private static HitTarget? Cast(Transform pointer, bool reversed)
    {
        Vector3 position = pointer.position;
        Vector3 direction = reversed ? -pointer.forward : pointer.forward;
        var ray = new Ray(position, direction);
        HitTarget? bestUi = null, nearest = null;
        try
        {
            foreach (RaycastHit hit in Physics.RaycastAll(ray,
                MaximumRayDistance, ~0, QueryTriggerInteraction.Collide))
            {
                Collider collider = hit.collider;
                if (collider == null || collider.transform == null) continue;
                Transform t = collider.transform;
                if (!IsSceneObject(t)) continue;
                if (t == pointer || t.IsChildOf(pointer)) continue;
                string n = t.gameObject.name;
                if (n == "Cube" || n == "Sphere" || Has(n, "Hand Target")) continue;
                var found = new HitTarget
                {
                    Node = t,
                    Distance = hit.distance,
                    Reversed = reversed,
                    UiScore = UiScore(t)
                };
                if (nearest == null || found.Distance < nearest.Distance) nearest = found;
                if (found.UiScore > 0 &&
                    (bestUi == null || found.UiScore > bestUi.UiScore ||
                     (found.UiScore == bestUi.UiScore && found.Distance < bestUi.Distance)))
                    bestUi = found;
            }
        }
        catch { return null; }
        return bestUi ?? nearest;
    }

    private void InspectRay(int hand)
    {
        Transform? pointer = _pointers[hand];
        if (pointer == null) return;

        HitTarget? hit = Cast(pointer, false);
        if (hit == null || hit.UiScore == 0)
        {
            HitTarget? reverse = Cast(pointer, true);
            if (reverse != null && reverse.UiScore > (hit?.UiScore ?? 0))
                hit = reverse;
        }

        int id = hit?.Node.GetInstanceID() ?? 0;
        bool reverseDirection = hit?.Reversed ?? false;
        if (id == _lastRayTargets[hand] &&
            reverseDirection == _lastReverse[hand]) return;
        bool previousHadTarget = _lastRayTargets[hand] > 0;
        _lastRayTargets[hand] = id;
        _lastReverse[hand] = reverseDirection;

        if (hit == null)
        {
            if (previousHadTarget)
                Log((hand == 0 ? "LEFT" : "RIGHT") + " RAY CLEARED");
            return;
        }

        // Change-only logging: every newly hit object is potentially useful,
        // even if this build does not yet know its native UI class.
        string source = (hand == 0 ? "LEFT" : "RIGHT") +
            (hit.Reversed ? " reverse pointer ray" : " forward pointer ray") +
            " dist=" + hit.Distance.ToString("0.00") +
            " uiHintScore=" + hit.UiScore;
        RecordTarget("VR RAY TARGET", source, hit.Node);
    }

    private void InspectEventSelection()
    {
#if QUESTDIAG_UI
        try
        {
            EventSystem system = EventSystem.current;
            if (system == null) return;
            GameObject selected = system.currentSelectedGameObject;
            int id = selected == null ? 0 : selected.GetInstanceID();
            if (id == _lastSelection) return;
            _lastSelection = id;
            if (selected != null)
                RecordTarget("EVENTSYSTEM SELECTED OBJECT",
                    "selection changed (not proof of button click)",
                    selected.transform);
        }
        catch (Exception ex) { Log("EVENTSYSTEM QUERY ERROR: " + Safe(ex.Message)); }
#endif
    }

    private void RecordTarget(string reason, string source, Transform target)
    {
        if (_events >= MaxEvents) return;
        try
        {
            string path = HierarchyPath(target);
            var s = new StringBuilder(4000);
            s.AppendLine();
            s.AppendLine("=================================================");
            s.Append("EVENT ").Append(++_events).Append(" | ")
                .Append(DateTime.Now.ToString("HH:mm:ss.fff"))
                .Append(" | ").AppendLine(reason);
            s.Append("Source: ").AppendLine(source);
            s.Append("Target: ").AppendLine(path);
            s.Append("Active: ").Append(target.gameObject.activeInHierarchy)
                .Append(" | Components: [").Append(Components(target.gameObject))
                .AppendLine("]");
            s.AppendLine("=== ANCESTORS (target -> root) ===");
            Transform? cur = target;
            for (int depth = 0; depth < 12 && cur != null; depth++, cur = cur.parent)
            {
                s.Append(depth).Append(": ").Append(Safe(cur.gameObject.name, 90))
                    .Append(" children=").Append(cur.childCount)
                    .Append(" active=").Append(cur.gameObject.activeInHierarchy)
                    .Append(" [" ).Append(Components(cur.gameObject)).AppendLine("]");
                if (depth < 3 && cur.childCount > 1)
                {
                    s.Append("   sibling examples: ");
                    int count = Math.Min(cur.childCount, 10);
                    for (int n = 0; n < count; n++)
                    {
                        if (n > 0) s.Append(" | ");
                        s.Append(Safe(cur.GetChild(n).gameObject.name, 45));
                    }
                    if (cur.childCount > count)
                        s.Append(" | ... ").Append(cur.childCount - count).Append(" more");
                    s.AppendLine();
                }
            }
            s.AppendLine("=== TARGET DESCENDANTS (2 levels max) ===");
            var queue = new Queue<(Transform Node, int Level)>();
            queue.Enqueue((target, 0));
            int descendants = 0;
            while (queue.Count > 0 && descendants < 36)
            {
                var (node, level) = queue.Dequeue();
                s.Append(' ', level * 2).Append(Safe(node.gameObject.name, 60))
                    .Append(" [").Append(Components(node.gameObject)).AppendLine("]");
                descendants++;
                if (level >= 2) continue;
                for (int i = 0; i < Math.Min(node.childCount, 20); i++)
                    queue.Enqueue((node.GetChild(i), level + 1));
            }
#if QUESTDIAG_UI
            // Only labels adjacent to the target, not arbitrary UI/chat globally.
            if (!IsPrivateTextPath(path))
            {
                int labels = 0;
                foreach (Text text in target.gameObject.GetComponentsInChildren<Text>(true))
                {
                    if (text == null || labels++ >= 12) break;
                    s.Append("UI Text: ").Append(Safe(text.text, 190)).AppendLine();
                }
            }
#endif
            Log(s.ToString());
        }
        catch (Exception ex) { Log("TARGET RECORD ERROR: " + Safe(ex.Message)); }
    }

    private void ManualUiSurvey()
    {
        try
        {
            var report = new StringBuilder(25000);
            report.AppendLine();
            report.AppendLine("=================================================");
            report.Append("MANUAL UI SURVEY ").Append(_manualScans)
                .Append(" | ").AppendLine(DateTime.Now.ToString("HH:mm:ss.fff"));
            report.AppendLine("Hold inventory Missions open for this snapshot.");
            report.AppendLine("=== LOADED SCENES ===");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                report.AppendLine(SceneManager.GetSceneAt(i).name);

#if QUESTDIAG_UI
            report.AppendLine("=== ALL ACTIVE NATIVE CANVASES (not CanvasRenderer) ===");
            int canvasCount = 0;
            foreach (Canvas c in UnityEngine.Object.FindObjectsOfType<Canvas>())
            {
                if (c == null || !IsSceneObject(c.transform) ||
                    !c.gameObject.activeInHierarchy) continue;
                if (++canvasCount > MaxManualCanvases) break;
                report.Append(canvasCount).Append(" ")
                    .Append(HierarchyPath(c.transform)).Append(" [")
                    .Append(Components(c.gameObject)).AppendLine("]");
                int childCount = Math.Min(c.transform.childCount, 18);
                for (int i = 0; i < childCount; i++)
                {
                    Transform child = c.transform.GetChild(i);
                    report.Append("   child: ").Append(Safe(child.gameObject.name, 85))
                        .Append(" children=").Append(child.childCount)
                        .Append(" [").Append(Components(child.gameObject)).AppendLine("]");
                }
            }
            report.AppendLine("=== ACTIVE UNITY UI SELECTABLE OBJECTS ===");
            int selectableCount = 0;
            foreach (Selectable item in UnityEngine.Object.FindObjectsOfType<Selectable>())
            {
                if (item == null || !IsSceneObject(item.transform) ||
                    !item.gameObject.activeInHierarchy) continue;
                if (++selectableCount > MaxManualSelectables) break;
                report.Append(selectableCount).Append(" ")
                    .Append(HierarchyPath(item.transform)).Append(" [")
                    .Append(Components(item.gameObject)).AppendLine("]");
            }
            report.AppendLine("=== CURRENT EVENT SYSTEM ===");
            EventSystem es = EventSystem.current;
            if (es == null) report.AppendLine("No Unity EventSystem.current.");
            else
            {
                report.AppendLine("EventSystem: " + HierarchyPath(es.transform) +
                    " [" + Components(es.gameObject) + "]");
                GameObject current = es.currentSelectedGameObject;
                report.AppendLine("Selection: " +
                    (current == null ? "(none)" : HierarchyPath(current.transform)));
            }
#else
            report.AppendLine("Unity UI interop missing: cannot enumerate Canvas or EventSystem.");
#endif
            report.AppendLine("=== VR POINTER TRANSFORMS ===");
            for (int hand = 0; hand < 2; hand++)
            {
                Transform? pointer = _pointers[hand];
                report.Append((hand == 0 ? "LEFT: " : "RIGHT: "))
                    .AppendLine(pointer == null ? "(not detected)" :
                        HierarchyPath(pointer) + " [" + Components(pointer.gameObject) + "]");
            }
            Log(report.ToString());
            Flush();
        }
        catch (Exception ex)
        {
            Log("MANUAL SCAN ERROR: " + Safe(ex.ToString(), 350));
        }
    }

    private void Log(string entry)
    {
        if (_path.Length == 0) return;
        _buffer.AppendLine(entry);
        if (_buffer.Length > 150_000) Flush();
    }

    private void Flush()
    {
        if (_buffer.Length == 0 || _path.Length == 0) return;
        try
        {
            File.AppendAllText(_path, _buffer.ToString(), Encoding.UTF8);
            _buffer.Clear();
            _lastWrite = Time.realtimeSinceStartup;
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning("[QuestUI] Log append failed: " + Safe(ex.Message));
        }
    }
}
