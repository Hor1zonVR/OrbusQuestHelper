using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Il2CppInterop.Runtime;

namespace OrbusQuestHelper;

/// <summary>
/// PowerUI compatibility adapter: the generic build has PowerUI.Document/Element
/// in Assembly-CSharp; the modified build uses HtmlDocument/HtmlElement in
/// Assembly-CSharp-firstpass. Never declare a PowerUI type at compile time.
/// </summary>
internal static class PowerUiRuntime
{
    private const BindingFlags Flags =
        BindingFlags.Public | BindingFlags.NonPublic |
        BindingFlags.Instance | BindingFlags.Static;

    internal static Type? FindType(string fullName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                Type? type = assembly.GetType(fullName, false);
                if (type != null) return type;
            }
            catch { }
        }
        return null;
    }

    internal static object? Get(object? owner, string name)
    {
        if (owner == null) return null;
        bool isStatic = owner is Type;
        Type type = isStatic ? (Type)owner : owner.GetType();
        object? instance = isStatic ? null : owner;
        try
        {
            PropertyInfo? property = type.GetProperty(name, Flags);
            if (property != null) return property.GetValue(instance);
            FieldInfo? field = type.GetField(name, Flags);
            if (field != null) return field.GetValue(instance);
            MethodInfo? getter = type.GetMethod("get_" + name, Flags,
                null, Type.EmptyTypes, null);
            return getter?.Invoke(instance, null);
        }
        catch { return null; }
    }

    internal static string Text(object? owner, string name)
    {
        object? value = Get(owner, name);
        return value?.ToString() ?? string.Empty;
    }

    internal static bool Set(object? owner, string name, object? value)
    {
        if (owner == null) return false;
        try
        {
            Type type = owner.GetType();
            PropertyInfo? property = type.GetProperty(name, Flags);
            if (property != null && property.CanWrite)
            {
                property.SetValue(owner, value);
                return true;
            }
            MethodInfo? setter = type.GetMethods(Flags).FirstOrDefault(
                m => m.Name == "set_" + name &&
                     m.GetParameters().Length == 1 &&
                     (value == null || m.GetParameters()[0].ParameterType.IsInstanceOfType(value)));
            if (setter != null)
            {
                setter.Invoke(owner, new[] { value });
                return true;
            }
        }
        catch { }
        return false;
    }

    internal static object? Call(object? owner, string method, params object?[] args)
    {
        if (owner == null) return null;
        Type type = owner.GetType();
        var methods = type.GetMethods(Flags)
            .Where(m => m.Name == method &&
                m.GetParameters().Length == args.Length)
            .OrderByDescending(m => MatchScore(m.GetParameters(), args))
            .ToArray();
        foreach (MethodInfo candidate in methods)
        {
            if (MatchScore(candidate.GetParameters(), args) < 0) continue;
            try { return candidate.Invoke(owner, args); }
            catch (TargetInvocationException) { }
            catch (ArgumentException) { }
        }
        return null;
    }

    private static int MatchScore(ParameterInfo[] p, object?[] args)
    {
        int score = 0;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == null)
            {
                if (p[i].ParameterType.IsValueType) return -1;
                continue;
            }
            Type a = args[i]!.GetType();
            if (p[i].ParameterType == a) score += 3;
            else if (p[i].ParameterType.IsAssignableFrom(a)) score += 1;
            else return -1;
        }
        return score;
    }

    internal static List<object> Enumerate(object? source, int limit = 512)
    {
        var results = new List<object>();
        if (source == null) return results;
        try
        {
            if (source is IEnumerable enumerable)
            {
                foreach (object? item in enumerable)
                {
                    if (item != null) results.Add(item);
                    if (results.Count >= limit) break;
                }
                return results;
            }

            object? countValue = Get(source, "Count") ?? Get(source, "Length")
                ?? Get(source, "length") ?? Get(source, "count");
            int count = countValue == null ? 0 : Convert.ToInt32(countValue);
            for (int i = 0; i < Math.Min(count, limit); i++)
            {
                object? item = Call(source, "get_Item", i) ??
                    Call(source, "item", i) ?? Call(source, "get", i);
                if (item != null) results.Add(item);
            }
        }
        catch { }
        return results;
    }

    /// <summary>
    /// Prefer native PowerUI selector APIs, but handle generated IL2CPP
    /// collection wrappers that cannot be enumerated by System.Collections.
    /// The legacy Element wrapper also exposes firstChild/nextSibling, which
    /// gives us a way to find original mission nodes without guessing IDs.
    /// </summary>
    internal static List<object> ByClass(object root, string className)
    {
        var result = Enumerate(Call(root, "getElementsByClassName", className));
        if (result.Count != 0) return result;

        // Check the document-wide selector too; some PowerUI versions only
        // implement the full selector operation on Document.
        object? doc = Get(root, "document");
        if (doc != null)
        {
            var all = Enumerate(Call(doc, "getElementsByClassName", className));
            result.AddRange(all.Where(node => IsDescendant(node, root)));
            if (result.Count != 0) return result;
        }

        // Last resort: bounded DOM traversal. Unlike scene scanners, this
        // touches only the specific UI subtree and never game-world objects.
        foreach (object node in Descendants(root, 2500))
        {
            string classes = Text(node, "className");
            if ((" " + classes + " ").Contains(" " + className + " ",
                    StringComparison.Ordinal))
                result.Add(node);
        }
        return result;
    }

    internal static List<object> Descendants(object root, int maxNodes = 2500)
    {
        var result = new List<object>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<object>();
        queue.Enqueue(root);
        while (queue.Count > 0 && result.Count < maxNodes)
        {
            object node = queue.Dequeue();
            string identity = Get(node, "Pointer")?.ToString()
                ?? node.GetHashCode().ToString();
            if (!seen.Add(identity)) continue;
            result.Add(node);

            // Element.firstChild/nextSibling are real PowerUI APIs in the
            // legacy game. They also work where childNodes exposes an
            // IL2CPP list that doesn't implement managed IEnumerable.
            object? child = Get(node, "firstChild");
            int siblings = 0;
            while (child != null && siblings++ < 700 &&
                result.Count + queue.Count < maxNodes)
            {
                queue.Enqueue(child);
                object? next = Get(child, "nextSibling");
                if (Same(next, child)) break;
                child = next;
            }
            if (siblings == 0)
            {
                foreach (object item in Enumerate(Get(node, "childNodes"),
                    Math.Min(700, maxNodes - result.Count - queue.Count)))
                    queue.Enqueue(item);
            }
        }
        return result;
    }

    internal static string Summary(object root)
    {
        try
        {
            var nodes = Descendants(root, 300);
            var previews = nodes.Select(node => Text(node, "className"))
                .Where(classes => !string.IsNullOrWhiteSpace(classes))
                .Distinct(StringComparer.Ordinal)
                .Take(10)
                .ToArray();
            string html = Text(root, "innerHTML");
            return "DOM nodes sampled=" + nodes.Count +
                "; native class query type=" +
                (Call(root, "getElementsByClassName", "vendor-mission")
                    ?.GetType().FullName ?? "null") +
                "; HTML contains 'vendor-mission'=" +
                html.Contains("vendor-mission", StringComparison.Ordinal) +
                "; class examples=[" + string.Join(" | ", previews) + "]";
        }
        catch (Exception ex)
        {
            return "DOM summary unavailable: " + ex.GetType().Name;
        }
    }

    internal static object? ById(object doc, string id) =>
        Call(doc, "getElementById", id);

    internal static object? Make(object doc, string tagName) =>
        Call(doc, "createElement", tagName);

    internal static bool Append(object parent, object element)
    {
        try
        {
            MethodInfo? method = parent.GetType().GetMethods(Flags)
                .Where(m => m.Name == "appendChild" &&
                    m.GetParameters().Length == 1 &&
                    m.GetParameters()[0].ParameterType.IsInstanceOfType(element))
                .OrderBy(m => m.GetParameters()[0].ParameterType == element.GetType() ? 0 : 1)
                .FirstOrDefault();
            if (method == null) return false;
            method.Invoke(parent, new[] { element });
            return true;
        }
        catch { return false; }
    }

    internal static bool Same(object? a, object? b)
    {
        if (a == null || b == null) return false;
        object? pointerA = Get(a, "Pointer"), pointerB = Get(b, "Pointer");
        if (pointerA != null && pointerB != null)
            return pointerA.Equals(pointerB);
        return ReferenceEquals(a, b);
    }

    internal static bool IsDescendant(object node, object ancestor)
    {
        object? current = node;
        for (int i = 0; current != null && i < 40; i++)
        {
            if (Same(current, ancestor)) return true;
            current = Get(current, "parentNode");
        }
        return false;
    }

    internal static bool Display(object node, string value)
    {
        // Set only display. Never overwrite native CSS or the game's font.
        object? style = Get(node, "style");
        return Set(style, "display", value);
    }

    /// <summary>
    /// Bind a managed click callback to the OLD PowerUI's OnMouseDown event.
    /// In the newer HTML API, event attachment varies. A failure leaves
    /// headings expanded rather than creating a non-functional VR button.
    /// </summary>
    internal static bool BindMouseDown(object node, Action handler,
        IList<object> keepAlive)
    {
        try
        {
            // In generated IL2CPP wrappers OnMouseDown is represented as an
            // add_ method whose argument is an IL2CPP delegate *object*,
            // NOT a System.Delegate. EventInfo.AddEventHandler would reject it.
            MethodInfo? add = node.GetType().GetMethods(Flags)
                .FirstOrDefault(m => m.Name == "add_OnMouseDown" &&
                    m.GetParameters().Length == 1);
            MethodInfo? modernAdd = null;
            if (add == null)
            {
                // Modern HtmlElement inherits Dom.EventTarget, whose
                // addEventListener("mousedown", Action<Dom.Event>) API replaces
                // the older PowerUI.Element.OnMouseDown native event.
                modernAdd = node.GetType().GetMethods(Flags)
                    .Where(m => m.Name == "addEventListener" &&
                        m.GetParameters().Length == 2 &&
                        m.GetParameters()[0].ParameterType == typeof(string))
                    .FirstOrDefault(m =>
                        m.GetParameters()[1].ParameterType.FullName != null &&
                        m.GetParameters()[1].ParameterType.FullName!.Contains(
                            "Dom.Event"));
                if (modernAdd == null) return false;
            }
            Type nativeType = add != null
                ? add.GetParameters()[0].ParameterType
                : modernAdd!.GetParameters()[1].ParameterType;
            MethodInfo? invoke = nativeType.GetMethod("Invoke");
            if (invoke == null) return false;
            ParameterInfo[] nativeParams = invoke.GetParameters();
            if (nativeParams.Length != 1) return false;

            Type actionType = typeof(Action<>).MakeGenericType(
                nativeParams[0].ParameterType);
            ParameterExpression arg = Expression.Parameter(
                nativeParams[0].ParameterType, "e");
            Expression callback = Expression.Call(
                Expression.Constant(handler), typeof(Action).GetMethod("Invoke")!);
            Delegate managed = Expression.Lambda(actionType, callback, arg).Compile();
            MethodInfo? convert = typeof(DelegateSupport)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "ConvertDelegate" &&
                    m.IsGenericMethodDefinition && m.GetParameters().Length == 1);
            if (convert == null) return false;

            object? converted = convert.MakeGenericMethod(nativeType)
                .Invoke(null, new object[] { managed });
            if (converted == null || !nativeType.IsInstanceOfType(converted))
                return false;

            if (add != null)
                add.Invoke(node, new[] { converted });
            else
                modernAdd!.Invoke(node, new object[] { "mousedown", converted });
            keepAlive.Add(managed);
            keepAlive.Add(converted);
            keepAlive.Add(handler);
            return true;
        }
        catch { return false; }
    }

    internal static object? PlayerMenuDocument()
    {
        Type? menu = FindType("PlayerMenu");
        return menu == null ? null : Get(menu, "document");
    }

    internal static string CurrentInventoryPage()
    {
        Type? menu = FindType("PlayerMenu");
        return menu == null ? string.Empty : Text(menu, "currentInventoryPage");
    }
}
