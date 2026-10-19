using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Il2CppInterop.Runtime;
using UnityEngine;
#if QUESTDIAG_UI
using UnityEngine.EventSystems;
#endif

namespace OrbusQuestHelper;

/// <summary>
/// Read-only IL2CPP metadata inspection of VR input component types.
/// Reports signatures only; does not read field values or invoke game methods.
/// </summary>
internal static class UiInputMetadataProbe
{
    private static readonly Dictionary<string, MethodInfo?> Api = new();

    private static MethodInfo? Resolve(string name, int argCount)
    {
        string key = name + "/" + argCount;
        if (Api.TryGetValue(key, out MethodInfo? cached)) return cached;
        foreach (MethodInfo m in typeof(IL2CPP).GetMethods(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
        {
            if ((m.Name == name || m.Name == name + "_") &&
                m.GetParameters().Length == argCount)
            {
                Api[key] = m;
                return m;
            }
        }
        Api[key] = null;
        return null;
    }

    private static object? Call(string name, params object?[] args)
    {
        MethodInfo? m = Resolve(name, args.Length);
        if (m == null) return null;
        try
        {
            ParameterInfo[] ps = m.GetParameters();
            for (int i = 0; i < args.Length; i++)
            {
                Type t = ps[i].ParameterType;
                if (t.IsByRef) t = t.GetElementType()!;
                if (args[i] is uint u && t == typeof(int)) args[i] = (int)u;
                else if (args[i] is int n && t == typeof(uint)) args[i] = (uint)n;
            }
            return m.Invoke(null, args);
        }
        catch { return null; }
    }

    private static IntPtr Pointer(object? obj) =>
        obj is IntPtr pointer ? pointer : IntPtr.Zero;

    private static string NativeString(object? obj)
    {
        if (obj is string text) return text;
        if (obj is IntPtr pointer && pointer != IntPtr.Zero)
        {
            try { return Marshal.PtrToStringAnsi(pointer) ?? "?"; }
            catch { }
        }
        return "?";
    }

    private static string TypeName(IntPtr ptr) =>
        ptr == IntPtr.Zero ? "?" : NativeString(Call("il2cpp_type_get_name", ptr));

    private static bool Relevant(string name)
    {
        string n = name.ToLowerInvariant();
        return n.Contains("pointer") || n.Contains("raycast") ||
            n.Contains("hover") || n.Contains("input") ||
            n.Contains("target") || n.Contains("hit") ||
            n.Contains("cursor") || n.Contains("laser") ||
            n.Contains("trigger") || n.Contains("click") ||
            n.Contains("select") || n.Contains("process") ||
            n.Contains("event") || n.Contains("press") ||
            n.Contains("release") || n.Contains("button");
    }

    private static void DumpClass(StringBuilder report, Component component)
    {
        IntPtr klass = IL2CPP.il2cpp_object_get_class(component.Pointer);
        if (klass == IntPtr.Zero) return;
        string type = (IL2CPP.il2cpp_class_get_namespace_(klass) ?? "") +
            "." + (IL2CPP.il2cpp_class_get_name_(klass) ?? "");
        report.AppendLine();
        report.AppendLine("Class: " + type);
        report.AppendLine("Assembly: " + IL2CPP.il2cpp_class_get_assemblyname_(klass));

        IntPtr iterator = IntPtr.Zero;
        int seenMethods = 0, writtenMethods = 0;
        for (int i = 0; i < 220; i++)
        {
            object?[] parameters = { klass, iterator };
            IntPtr method = Pointer(Call("il2cpp_class_get_methods", parameters));
            if (method == IntPtr.Zero) break;
            iterator = Pointer(parameters[1]);
            seenMethods++;
            string name = NativeString(Call("il2cpp_method_get_name", method));
            if (!Relevant(name) || writtenMethods >= 65) continue;
            writtenMethods++;
            int count = -1;
            try
            {
                object? value = Call("il2cpp_method_get_param_count", method);
                if (value != null) count = Convert.ToInt32(value);
            }
            catch { }
            report.Append("  Method ")
                .Append(TypeName(Pointer(Call("il2cpp_method_get_return_type", method))))
                .Append(' ').Append(name).Append('(');
            if (count >= 0 && count < 20)
            {
                for (int j = 0; j < count; j++)
                {
                    if (j > 0) report.Append(", ");
                    report.Append(TypeName(Pointer(Call(
                        "il2cpp_method_get_param", method, (uint)j))));
                }
            }
            else report.Append("?");
            report.AppendLine(")");
        }
        report.AppendLine("Methods inspected=" + seenMethods +
            ", relevant signatures shown=" + writtenMethods);
        iterator = IntPtr.Zero;
        int fields = 0;
        for (int i = 0; i < 140; i++)
        {
            object?[] parameters = { klass, iterator };
            IntPtr field = Pointer(Call("il2cpp_class_get_fields", parameters));
            if (field == IntPtr.Zero) break;
            iterator = Pointer(parameters[1]);
            fields++;
            string name = NativeString(Call("il2cpp_field_get_name", field));
            if (!Relevant(name)) continue;
            report.AppendLine("  Field " +
                TypeName(Pointer(Call("il2cpp_field_get_type", field))) +
                " " + name + " [type only]");
        }
        report.AppendLine("Fields inspected=" + fields + "; values never read.");
    }

    public static string Capture(Transform?[] pointers)
    {
        var report = new StringBuilder(8000);
        report.AppendLine();
        report.AppendLine("=== NATIVE VR POINTER / INPUT MODULE METADATA ===");
        report.AppendLine("Names and method signatures only; no game methods invoked.");
        report.AppendLine("Metadata exports available: methods=" +
            (Resolve("il2cpp_class_get_methods", 2) != null) +
            ", fields=" + (Resolve("il2cpp_class_get_fields", 2) != null));

        int classes = 0;
        var found = new HashSet<string>(StringComparer.Ordinal);
        void Inspect(Component component)
        {
            if (component == null) return;
            try
            {
                IntPtr klass = IL2CPP.il2cpp_object_get_class(component.Pointer);
                if (klass == IntPtr.Zero) return;
                string name = (IL2CPP.il2cpp_class_get_namespace_(klass) ?? "") +
                    "." + (IL2CPP.il2cpp_class_get_name_(klass) ?? "");
                if (!found.Add(name)) return;
                DumpClass(report, component);
                classes++;
            }
            catch (Exception ex)
            {
                report.AppendLine("Metadata error: " + ex.GetType().Name +
                    " " + ex.Message);
            }
        }
        foreach (Transform? pointer in pointers)
        {
            if (pointer == null) continue;
            foreach (Component c in pointer.gameObject.GetComponents<Component>())
            {
                if (c == null) continue;
                try
                {
                    string name = IL2CPP.il2cpp_class_get_name_(
                        IL2CPP.il2cpp_object_get_class(c.Pointer)) ?? "";
                    if (Relevant(name)) Inspect(c);
                }
                catch { }
            }
        }
#if QUESTDIAG_UI
        try
        {
            foreach (BaseInputModule module in
                UnityEngine.Object.FindObjectsOfType<BaseInputModule>())
            {
                if (module != null && module.gameObject.activeInHierarchy)
                    Inspect(module);
            }
            EventSystem es = EventSystem.current;
            if (es != null) Inspect(es);
        }
        catch (Exception ex)
        {
            report.AppendLine("Event module discovery error: " + ex.Message);
        }
#endif
        report.AppendLine("Input-related classes inspected=" + classes);
        return classes > 0 ? report.ToString() : "";
    }
}
