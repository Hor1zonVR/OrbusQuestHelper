using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

// READ-ONLY: inspects already generated BepInEx IL2CPP interop *assembly metadata*
// and QuestHelper's plugin/config/log. Does not load any game assemblies,
// change files, attach a debugger, or invoke any game/UI code.
if (args.Length != 2 || args[0] != "--game")
{
    Console.Error.WriteLine("Usage: dotnet run --project tools/QuestClientProbe -- --game \"C:\\path\\to\\RebornManager instance\"");
    Environment.ExitCode = 2;
    return;
}

string game = Path.GetFullPath(Environment.ExpandEnvironmentVariables(args[1]));
string interop = Path.Combine(game, "BepInEx", "interop");
string plugins = Path.Combine(game, "BepInEx", "plugins");
string config = Path.Combine(game, "BepInEx", "config", "com.horizon.orbus.questhelper.cfg");
string logFile = Path.Combine(game, "BepInEx", "LogOutput.log");

Write("QuestClientProbe v1 — METADATA-ONLY compatibility report");
Write("DateUTC: " + DateTime.UtcNow.ToString("u"));
Write("Game directory: " + game);
Write("Game instance exists: " + Directory.Exists(game));
Write("Interop directory exists: " + Directory.Exists(interop));
Write("No files will be modified.");

if (!Directory.Exists(interop))
{
    Write("ERROR: No BepInEx/interop folder. Launch the instance once, then rerun.");
    Environment.ExitCode = 1;
    return;
}

Write("");
Write("=== QuestHelper installed DLLs ===");
if (Directory.Exists(plugins))
{
    try
    {
        string[] qh = Directory.GetFiles(plugins, "OrbusQuestHelper.dll",
            SearchOption.AllDirectories);
        Write("Count: " + qh.Length);
        foreach (string path in qh.Take(8))
        {
            var file = new FileInfo(path);
            Write(" - " + Path.GetRelativePath(game, path) +
                ", bytes=" + file.Length + ", SHA256=" +
                Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant());
        }
    }
    catch (Exception ex) { Write("DLL scan unavailable: " + ex.GetType().Name); }
}
else Write("BepInEx/plugins missing");

Write("");
Write("=== Relevant config settings ===");
try
{
    if (!File.Exists(config)) Write("QuestHelper config not created yet");
    else
    {
        bool inMissions = false;
        foreach (string original in File.ReadLines(config))
        {
            string line = original.Trim();
            if (line.StartsWith("[", StringComparison.Ordinal))
            {
                inMissions = line == "[Missions]";
                continue;
            }
            if (inMissions && Regex.IsMatch(line,
                    @"^(EnableHtmlRegionTaskGrouping|EnableRegionTaskGrouping|GroupingView)\s*=",
                    RegexOptions.IgnoreCase))
                Write(line);
        }
    }
}
catch (Exception ex) { Write("Config read unavailable: " + ex.GetType().Name); }

Write("");
Write("=== Relevant BepInEx log entries (last 35) ===");
try
{
    if (!File.Exists(logFile)) Write("BepInEx/LogOutput.log not found");
    else
    {
        var matched = File.ReadLines(logFile)
            .Where(line => line.Contains("OrbusQuestHelper", StringComparison.OrdinalIgnoreCase) ||
                           line.Contains("[QuestHelper]", StringComparison.OrdinalIgnoreCase))
            .TakeLast(35);
        foreach (string line in matched)
            Write(line.Length > 360 ? line.Substring(0, 360) : line);
    }
}
catch (Exception ex) { Write("Log read unavailable: " + ex.GetType().Name); }

Write("");
Write("=== IL2CPP interop TypeDef and MethodDef metadata ===");
int valid = 0;
foreach (string file in new[]
{
    Path.Combine(interop, "Assembly-CSharp.dll"),
    Path.Combine(interop, "Assembly-CSharp-firstpass.dll")
})
{
    Write("");
    Write("Assembly: " + Path.GetFileName(file));
    if (!File.Exists(file))
    {
        Write("MISSING");
        continue;
    }

    try
    {
        using var stream = File.OpenRead(file);
        using var pe = new PEReader(stream, PEStreamOptions.LeaveOpen);
        if (!pe.HasMetadata)
        {
            Write("Assembly has no ECMA-335 metadata.");
            continue;
        }

        MetadataReader md = pe.GetMetadataReader();
        valid++;
        Write("Bytes=" + stream.Length +
            "; Types=" + md.TypeDefinitions.Count +
            "; Methods=" + md.MethodDefinitions.Count);

        var types = md.TypeDefinitions
            .Select(h =>
            {
                TypeDefinition t = md.GetTypeDefinition(h);
                string ns = md.GetString(t.Namespace);
                string name = md.GetString(t.Name);
                string full = string.IsNullOrEmpty(ns) ? name : ns + "." + name;
                return (Handle: h, Name: name, Full: full);
            }).ToList();

        var relevant = types.Where(t =>
            t.Full is "PlayerMenu" or
                "PowerUI.Element" or "PowerUI.Document" or
                "PowerUI.HtmlElement" or "PowerUI.HtmlDocument" or
                "Dom.Element" or "Dom.Document" or
                "Dom.EventTarget" or "Dom.HtmlElement" ||
            t.Full.EndsWith(".HtmlDocument", StringComparison.Ordinal) ||
            t.Full.EndsWith(".HtmlElement", StringComparison.Ordinal))
            .ToList();

        Write("Relevant types found: " + relevant.Count);
        foreach (var target in relevant)
        {
            var td = md.GetTypeDefinition(target.Handle);
            Write("");
            Write("TYPE " + target.Full);
            int shown = 0;
            foreach (var methodHandle in td.GetMethods())
            {
                MethodDefinition method = md.GetMethodDefinition(methodHandle);
                string name = md.GetString(method.Name);
                bool show = target.Full == "PlayerMenu"
                    ? ContainsAny(name, "mission", "inventory", "document", "menu", "tab", "quest")
                    : ContainsAny(name, "html", "elementbyid", "child", "event",
                        "content", "document", "id", "load", "inner", "query", "mission",
                        "text", "attribute");

                if (!show) continue;
                if (++shown > 95)
                {
                    Write("   ... additional matching members omitted");
                    break;
                }

                int pCount = 0;
                var paramNames = new List<string>();
                foreach (var pHandle in method.GetParameters())
                {
                    Parameter parameter = md.GetParameter(pHandle);
                    if (parameter.SequenceNumber == 0) continue; // return parameter
                    pCount++;
                    string pn = md.GetString(parameter.Name);
                    if (!string.IsNullOrEmpty(pn)) paramNames.Add(pn);
                }

                string signature = Convert.ToHexString(
                    md.GetBlobBytes(method.Signature).Take(24).ToArray());
                string visibility = method.Attributes.HasFlag(MethodAttributes.Static)
                    ? "static" : "instance";
                Write("  " + visibility + " " + name + " (" + pCount +
                    " args" + (paramNames.Count > 0 ?
                        ": " + string.Join(", ", paramNames.Take(8)) : "") +
                    ") sig=" + signature);
            }
            if (shown == 0) Write("   (no matching methods)");
        }

        // Useful for client builds whose target classes differ in name.
        var alternative = types.Where(t =>
            t.Full.Contains("PowerUI", StringComparison.OrdinalIgnoreCase) &&
            ContainsAny(t.Full, "document", "element", "html"))
            .Select(t => t.Full)
            .Distinct()
            .Take(45)
            .ToArray();
        Write("");
        Write("Other PowerUI document/element type names: " +
            string.Join(", ", alternative));
    }
    catch (Exception ex)
    {
        Write("Metadata inspection failed: " +
            ex.GetType().Name + ": " + ex.Message);
    }
}

Write("");
Write("=== End report ===");
if (valid == 0) Environment.ExitCode = 1;

static bool ContainsAny(string haystack, params string[] needles) =>
    needles.Any(n => haystack.Contains(n, StringComparison.OrdinalIgnoreCase));

static void Write(string text) => Console.WriteLine(text);
