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
                ?? Get(source, "length");
            int count = countValue == null ? 0 : Convert.ToInt32(countValue);
            for (int i = 0; i < Math.Min(count, limit); i++)
            {
                object? item = Call(source, "get_Item", i);
                if (item != null) results.Add(item);
            }
        }
        catch { }
        return results;
    }

    internal static List<object> ByClass(object root, string className)
    {
        // Old and new PowerUI both expose this method on the document/Element
        // hierarchy, though collection types differ.
        return Enumerate(Call(root, "getElementsByClassName", className));
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
            EventInfo? evt = node.GetType().GetEvent("OnMouseDown", Flags);
            if (evt?.EventHandlerType == null) return false;
            Type nativeType = evt.EventHandlerType;
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
            if (converted is not Delegate nativeDelegate) return false;
            evt.AddEventHandler(node, nativeDelegate);
            keepAlive.Add(managed);
            keepAlive.Add(nativeDelegate);
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
