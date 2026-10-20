using System;
using System.Collections.Generic;
using OrbusQuestHelper;

namespace Il2CppInterop.Runtime
{
    // Stub only: native IL2CPP delegate conversion is tested on the user's
    // Spades installation, never emulated as though this is the game.
    public static class DelegateSupport
    {
        public static T ConvertDelegate<T>(Delegate callback)
            where T : class => (T)(object)callback;
    }
}

namespace Dom
{
    public class Event { }
    public class MouseEvent : Event { }
    public class KeyboardEvent : Event { }
}

namespace OrbusQuestHelper
{
    internal sealed class TestLog
    {
        public void LogInfo(string value) => Console.WriteLine("INFO " + value);
        public void LogWarning(string value) => Console.WriteLine("WARN " + value);
    }
    internal static class Plugin
    {
        public static TestLog Logger = new();
    }
}

internal sealed class TwoArgDomNode
{
    private readonly Dictionary<string, Action<Dom.Event>> callbacks = new();
    public void addEventListener(string eventName, Action<Dom.Event> handler) =>
        callbacks[eventName] = handler;
    public void Fire(string name) => callbacks[name](new Dom.Event());
    public int ListenerCount => callbacks.Count;
}

internal sealed class ThreeArgDomNode
{
    private readonly Dictionary<string, Action<Dom.Event>> callbacks = new();
    public void addEventListener(string eventName, Action<Dom.Event> handler,
        bool useCapture)
    {
        if (useCapture) throw new Exception("Unexpected useCapture=True");
        callbacks[eventName] = handler;
    }
    public void Fire(string name) => callbacks[name](new Dom.Event());
    public int ListenerCount => callbacks.Count;
}

internal sealed class LegacyNode
{
    private Action<Dom.Event>? callback;
    public void add_OnMouseDown(Action<Dom.Event> handler) =>
        callback = handler;
    public void Fire() => callback?.Invoke(new Dom.Event());
}

internal sealed class TypedDomNode
{
    private Action<Dom.MouseEvent>? callback;
    public void addEventListener(string eventName,
        Action<Dom.KeyboardEvent> handler) =>
        throw new Exception("Wrong event overload selected");
    public void addEventListener(string eventName,
        Action<Dom.MouseEvent> handler)
    {
        if (eventName == "click") callback = handler;
    }
    public void Fire() => callback?.Invoke(new Dom.MouseEvent());
}

internal sealed class MissingEvents { }

internal static class Program
{
    private static void Main()
    {
        {
            var node = new TwoArgDomNode();
            int clicks = 0;
            var keepAlive = new List<object>();
            Require(PowerUiRuntime.BindMouseDown(node, () => clicks++,
                keepAlive, out string reason),
                "2-arg listener failed: " + reason);
            Require(node.ListenerCount == 2,
                "Expected both click and mousedown registrations");
            node.Fire("click");
            node.Fire("mousedown");
            Require(clicks == 2 && keepAlive.Count == 3,
                "2-arg callbacks or lifetime references failed");
            Console.WriteLine("PASS modern two-argument Dom.Event callbacks");
        }
        {
            var node = new ThreeArgDomNode();
            int clicks = 0;
            Require(PowerUiRuntime.BindMouseDown(node, () => clicks++,
                new List<object>(), out string reason),
                "3-arg listener failed: " + reason);
            Require(node.ListenerCount == 2,
                "Expected both modern event types");
            node.Fire("click");
            Require(clicks == 1, "3-arg native click did not invoke callback");
            Console.WriteLine("PASS modern three-argument Dom.Event callbacks");
        }
        {
            var node = new LegacyNode();
            int count = 0;
            Require(PowerUiRuntime.BindMouseDown(node, () => count++,
                new List<object>(), out string reason),
                "Legacy event failed: " + reason);
            node.Fire();
            Require(count == 1, "Legacy OnMouseDown broke");
            Console.WriteLine("PASS legacy PowerUI OnMouseDown unchanged");
        }
        {
            var node = new TypedDomNode();
            int count = 0;
            Require(PowerUiRuntime.BindMouseDown(node, () => count++,
                new List<object>(), out string reason),
                "MouseEvent callback failed: " + reason);
            node.Fire();
            Require(count == 1, "MouseEvent listener did not activate");
            Console.WriteLine("PASS select MouseEvent but never KeyboardEvent overload");
        }
        {
            Require(!PowerUiRuntime.BindMouseDown(
                new MissingEvents(), () => { }, new List<object>(),
                out string detail),
                "Missing event methods must safely refuse");
            Require(detail.Contains("No compatible", StringComparison.Ordinal),
                "Missing API must produce actionable diagnostic reason");
            Console.WriteLine("PASS fail closed with diagnostic when no event API exists");
        }
        Console.WriteLine("5/5 managed native-selector binder smoke checks passed.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
