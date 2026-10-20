// Standalone, read-only .NET 8 inspection. No BepInEx, no Unity or
// third-party dependencies, no native code execution and no network calls.
using System.Buffers.Binary;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;

var gameDir = "";
var metadataPath = "";
var output = Path.Combine(Environment.CurrentDirectory, "QuestUiInvestigation.txt");
var dlls = new List<string>();
for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--game" when i + 1 < args.Length:
            gameDir = args[++i];
            break;
        case "--metadata" when i + 1 < args.Length:
            metadataPath = args[++i];
            break;
        case "--out" when i + 1 < args.Length:
            output = args[++i];
            break;
        default:
            if (args[i].EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                dlls.Add(args[i]);
            else
            {
                Console.Error.WriteLine("Unknown argument: " + args[i]);
                Environment.Exit(2);
            }
            break;
    }
}

if (!string.IsNullOrWhiteSpace(gameDir))
{
    var interop = Path.Combine(gameDir, "BepInEx", "interop");
    foreach (string name in new[] { "Assembly-CSharp.dll", "Assembly-CSharp-firstpass.dll" })
    {
        string candidate = Path.Combine(interop, name);
        if (File.Exists(candidate)) dlls.Add(candidate);
    }
    if (string.IsNullOrWhiteSpace(metadataPath))
    {
        var data = Directory.Exists(gameDir)
            ? Directory.GetDirectories(gameDir, "*_Data").FirstOrDefault()
            : null;
        if (data != null)
        {
            string native = Path.Combine(data, "il2cpp_data", "Metadata", "global-metadata.dat");
            if (File.Exists(native)) metadataPath = native;
        }
    }
}
dlls = dlls.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
if (dlls.Count == 0 && (metadataPath.Length == 0 || !File.Exists(metadataPath)))
{
    Console.Error.WriteLine("Usage: dotnet run --project tools/OrbusUiInspector -- " +
        "--game \"C:\\path\\to\\OrbusVR\" [--out \"C:\\path\\to\\report.txt\"]");
    Console.Error.WriteLine("Or supply Assembly-CSharp.dll and Assembly-CSharp-firstpass.dll paths directly.");
    Environment.Exit(2);
}

var report = new StringBuilder();
report.AppendLine("QuestHelper: read-only original Missions UI inspection");
report.AppendLine("This output contains technical metadata only and must not be published with game assemblies.");
report.AppendLine("No game process launched; no game files modified; no native methods executed.");
report.AppendLine();

foreach (string dll in dlls)
{
    try { InspectDll(dll, report); }
    catch (Exception ex)
    {
        report.AppendLine("[DLL ERROR] " + Path.GetFileName(dll) +
            ": " + ex.GetType().Name + " " + ex.Message);
    }
}
if (metadataPath.Length != 0 && File.Exists(metadataPath))
{
    try { InspectStringLiterals(metadataPath, report); }
    catch (Exception ex)
    {
        report.AppendLine("[METADATA ERROR] " + ex.GetType().Name + " " + ex.Message);
    }
}
report.AppendLine();
report.AppendLine("INTERPRETATION: Generated interop DLLs contain CLR method definitions");
report.AppendLine("and callable wrappers, not the original native C++ bodies.");
report.AppendLine("A matching PlayerMenu.UpdateMissions and Element.set_innerHTML");
report.AppendLine("DO NOT prove that UpdateMissions invokes that setter.");
report.AppendLine("No changes should be made to game UI based solely on this report.");

string? parentDir = Path.GetDirectoryName(Path.GetFullPath(output));
if (parentDir != null) Directory.CreateDirectory(parentDir);
File.WriteAllText(output, report.ToString(), new UTF8Encoding(false));
Console.WriteLine("Read-only investigation saved: " + Path.GetFullPath(output));

static void InspectDll(string file, StringBuilder sb)
{
    if (!File.Exists(file))
    {
        sb.AppendLine("[MISSING] " + Path.GetFileName(file));
        return;
    }
    using var stream = File.OpenRead(file);
    using var pe = new PEReader(stream);
    if (!pe.HasMetadata)
    {
        sb.AppendLine("[NO .NET METADATA] " + Path.GetFileName(file));
        return;
    }
    MetadataReader md = pe.GetMetadataReader();
    sb.AppendLine("=== ASSEMBLY: " + Path.GetFileName(file) + " ===");
    sb.AppendLine("Size: " + stream.Length + " bytes");
    sb.AppendLine("TypeDefinitions: " + md.TypeDefinitions.Count);
    sb.AppendLine("MethodDefinitions: " + md.MethodDefinitions.Count);

    var targetTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "PlayerMenu",
        "PowerUI.Document",
        "PowerUI.Element",
        "PowerUI.Node",
        "PowerUI.Css.ElementStyle",
        "PowerUI.HtmlDocument",
        "PowerUI.HtmlElement",
        "MagicalActual.Mission",
        "MagicalActual.MissionMonster"
    };
    string[] methodKeywords =
    {
        "mission", "innerhtml", "getelement", "append", "remove", "insert",
        "layout", "scroll", "child", "style", "class", "setattribute",
        "display", "height", "clear", "write", "createelement"
    };
    string[] fieldKeywords = { "mission", "document", "zone", "checklist", "description", "type" };

    foreach (TypeDefinitionHandle h in md.TypeDefinitions)
    {
        TypeDefinition def = md.GetTypeDefinition(h);
        string ns = md.GetString(def.Namespace);
        string name = md.GetString(def.Name);
        string fullName = ns.Length == 0 ? name : ns + "." + name;
        if (!targetTypes.Contains(fullName)) continue;
        sb.AppendLine();
        sb.AppendLine("TYPE " + fullName);
        foreach (FieldDefinitionHandle fh in def.GetFields())
        {
            string field = md.GetString(md.GetFieldDefinition(fh).Name);
            if (fieldKeywords.Any(k =>
                    field.Contains(k, StringComparison.OrdinalIgnoreCase)))
                sb.AppendLine("  FIELD " + field);
        }
        foreach (MethodDefinitionHandle mh in def.GetMethods())
        {
            MethodDefinition method = md.GetMethodDefinition(mh);
            string methodName = md.GetString(method.Name);
            if (!methodKeywords.Any(k =>
                    methodName.Contains(k, StringComparison.OrdinalIgnoreCase)))
                continue;
            byte[] signature = md.GetBlobBytes(method.Signature);
            int paramCount = -1;
            try
            {
                BlobReader signatureReader = md.GetBlobReader(method.Signature);
                byte flags = signatureReader.ReadByte();
                if ((flags & 0x10) != 0) signatureReader.ReadCompressedInteger();
                paramCount = signatureReader.ReadCompressedInteger();
            }
            catch { }
            sb.AppendLine("  METHOD " + methodName +
                " | parameterCount=" + paramCount +
                " | ecmaSignature=" + Convert.ToHexString(signature));
        }
    }
    sb.AppendLine();
}

static void InspectStringLiterals(string path, StringBuilder sb)
{
    // IL2CPP global metadata has a stable 24-byte prefix in the versions
    // used here. Parse bounds defensively, without executing any game code.
    // This is only a string pool, NOT proof of the owning native function.
    byte[] bytes = File.ReadAllBytes(path);
    ReadOnlySpan<byte> b = bytes;
    if (bytes.Length < 256 || ReadU32(b, 0) != 0xFAB11BAF)
    {
        sb.AppendLine("[METADATA] Not a recognised IL2CPP global-metadata.dat");
        return;
    }
    uint version = ReadU32(b, 4);
    uint indexOffset = ReadU32(b, 8);
    uint indexBytes = ReadU32(b, 12);
    uint dataOffset = ReadU32(b, 16);
    uint dataBytes = ReadU32(b, 20);
    sb.AppendLine("=== IL2CPP STRING LITERALS (v" + version + ") ===");
    if (indexBytes > 20_000_000 || dataBytes > 40_000_000 ||
        (ulong)indexOffset + indexBytes > (ulong)b.Length ||
        (ulong)dataOffset + dataBytes > (ulong)b.Length)
    {
        sb.AppendLine("[METADATA] String literal bounds invalid; skipped.");
        return;
    }

    int emitted = 0;
    int total = (int)indexBytes / 8;
    for (int n = 0; n < total && emitted < 65; n++)
    {
        int entry = (int)indexOffset + n * 8;
        uint length = ReadU32(b, entry);
        uint offset = ReadU32(b, entry + 4);
        if (length == 0 || length > 2048 || (ulong)offset + length > dataBytes)
            continue;
        string value = Encoding.UTF8.GetString(
            bytes, (int)(dataOffset + offset), (int)length);
        bool relevant =
            value.Contains("missions-container", StringComparison.Ordinal) ||
            value.Contains("missions-tab", StringComparison.Ordinal) ||
            value.Contains("PlayerMenu-Your", StringComparison.Ordinal) ||
            value.Contains("PlayerMenu-Available", StringComparison.Ordinal) ||
            value.Contains("vendor-mission", StringComparison.Ordinal) ||
            value.Contains("data-missionid-", StringComparison.Ordinal) ||
            value.Contains("updateMissionTab()", StringComparison.Ordinal) ||
            value.Contains("class='vendor'", StringComparison.Ordinal);
        if (!relevant) continue;
        // Only report a short local snippet; don't copy raw metadata / assets
        // or entire game UI templates into the GitHub repository.
        string preview = value.Length > 160 ? value[..160] + "..." : value;
        sb.AppendLine("LITERAL " + n + " " +
            preview.Replace('\n', ' ').Replace('\r', ' '));
        emitted++;
    }
    sb.AppendLine("Relevant short literal fragments: " + emitted);
}

static uint ReadU32(ReadOnlySpan<byte> bytes, int index) =>
    BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(index, 4));
