using System.Buffers.Binary;
using System.Text.Json;
using UAssetAPI;
using UAssetAPI.CustomVersions;
using UAssetAPI.ExportTypes;
using UAssetAPI.FieldTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;

if (args.Length == 4 && args[0] == "--restore-container-index")
{
    var referenceUtoc = Path.GetFullPath(args[1]);
    var generatedUtoc = Path.GetFullPath(args[2]);
    var outputUtoc = Path.GetFullPath(args[3]);
    IoStoreToc.RestoreReferenceIndex(referenceUtoc, generatedUtoc, outputUtoc);
    Console.WriteLine($"Restored reference IoStore index in {outputUtoc}");
    return 0;
}

if (args.Length == 3 && args[0] == "--verify-container-index")
{
    var referenceUtoc = Path.GetFullPath(args[1]);
    var candidateUtoc = Path.GetFullPath(args[2]);
    IoStoreToc.VerifyReferenceIndex(referenceUtoc, candidateUtoc);
    Console.WriteLine($"Verified reference IoStore index in {candidateUtoc}");
    return 0;
}

if (args.Length == 3 && args[0] == "--verify-conversion-control")
{
    var referenceRoot = Path.GetFullPath(args[1]);
    var candidateRoot = Path.GetFullPath(args[2]);
    VerifyConversionControl(referenceRoot, candidateRoot);
    Console.WriteLine($"Verified semantically equivalent conversion-control assets in {candidateRoot}");
    return 0;
}

if (args.Length == 3 && args[0] == "--direct-hud-control")
{
    var directSourceRoot = Path.GetFullPath(args[1]);
    var directOutputRoot = Path.GetFullPath(args[2]);
    if (!Directory.Exists(directSourceRoot))
    {
        throw new DirectoryNotFoundException($"Direct HUD source directory not found: {directSourceRoot}");
    }

    CopyDirectory(directSourceRoot, directOutputRoot);
    PatchDirectHudControlWidget(Path.Combine(directOutputRoot, "Stalker2", "Content", "Mods", "ShowDMG",
        "wbp_ShowDMG.uasset"));
    VerifyDirectHudControlAssets(directSourceRoot, directOutputRoot);
    Console.WriteLine($"Patched length-preserving direct HUD control assets in {directOutputRoot}");
    return 0;
}

if (args.Length == 3 && args[0] == "--verify-direct-hud-control")
{
    var directSourceRoot = Path.GetFullPath(args[1]);
    var directCandidateRoot = Path.GetFullPath(args[2]);
    VerifyDirectHudControlAssets(directSourceRoot, directCandidateRoot);
    Console.WriteLine($"Verified length-preserving direct HUD control assets in {directCandidateRoot}");
    return 0;
}

if (args.Length == 3 && args[0] == "--direct-release")
{
    var directSourceRoot = Path.GetFullPath(args[1]);
    var directOutputRoot = Path.GetFullPath(args[2]);
    if (!Directory.Exists(directSourceRoot))
    {
        throw new DirectoryNotFoundException($"Direct release source directory not found: {directSourceRoot}");
    }

    CopyDirectory(directSourceRoot, directOutputRoot);
    var directModRoot = Path.Combine(directOutputRoot, "Stalker2", "Content", "Mods", "ShowDMG");
    PatchDirectReleaseWidget(Path.Combine(directModRoot, "wbp_ShowDMG.uasset"));
    PatchDirectReleaseSpawner(Path.Combine(directModRoot, "bpac_dmgWidgetSpawner.uasset"));
    VerifyDirectReleaseAssets(directSourceRoot, directOutputRoot);
    Console.WriteLine($"Patched length-preserving direct release assets in {directOutputRoot}");
    return 0;
}

if (args.Length == 3 && args[0] == "--verify-direct-release")
{
    var directSourceRoot = Path.GetFullPath(args[1]);
    var directCandidateRoot = Path.GetFullPath(args[2]);
    VerifyDirectReleaseAssets(directSourceRoot, directCandidateRoot);
    Console.WriteLine($"Verified length-preserving direct release assets in {directCandidateRoot}");
    return 0;
}

if (args.Length == 5 && args[0] == "--transplant-legacy-exports")
{
    var referenceRawRoot = Path.GetFullPath(args[1]);
    var transplantSourceRoot = Path.GetFullPath(args[2]);
    var transplantPatchedRoot = Path.GetFullPath(args[3]);
    var transplantOutputRoot = Path.GetFullPath(args[4]);
    TransplantLegacyExports(referenceRawRoot, transplantSourceRoot, transplantPatchedRoot, transplantOutputRoot);
    Console.WriteLine($"Transplanted length-preserving exports into original Zen chunks in {transplantOutputRoot}");
    return 0;
}

if (args.Length == 5 && args[0] == "--verify-transplanted-exports")
{
    var referenceRawRoot = Path.GetFullPath(args[1]);
    var transplantSourceRoot = Path.GetFullPath(args[2]);
    var transplantPatchedRoot = Path.GetFullPath(args[3]);
    var transplantCandidateRoot = Path.GetFullPath(args[4]);
    VerifyTransplantedExports(referenceRawRoot, transplantSourceRoot, transplantPatchedRoot,
        transplantCandidateRoot);
    Console.WriteLine($"Verified direct Zen export transplant in {transplantCandidateRoot}");
    return 0;
}

if (args.Length == 2 && args[0] is "--verify" or "--verify-bootstrap")
{
    var verificationRoot = Path.GetFullPath(args[1]);
    if (args[0] == "--verify-bootstrap") VerifyBootstrapAssets(verificationRoot);
    else VerifyPatchedAssets(verificationRoot);
    Console.WriteLine($"Verified HitMarkers cooked assets in {verificationRoot}");
    return 0;
}

var diagnostics = args.Length == 3 && args[0] == "--diagnostics";
var bootstrapDiagnostics = args.Length == 3 && args[0] == "--bootstrap-diagnostics";
if (diagnostics || bootstrapDiagnostics) args = args[1..];

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: HitMarkersPatcher [--diagnostics|--bootstrap-diagnostics] <legacy-source> <legacy-output> | --verify|--verify-bootstrap <legacy-root> | --direct-hud-control|--direct-release <legacy-source> <legacy-output> | --verify-direct-hud-control|--verify-direct-release <legacy-source> <legacy-candidate> | --transplant-legacy-exports <reference-raw> <legacy-source> <legacy-patched> <output-raw> | --verify-transplanted-exports <reference-raw> <legacy-source> <legacy-patched> <candidate-raw> | --restore-container-index <reference.utoc> <generated.utoc> <output.utoc> | --verify-container-index <reference.utoc> <candidate.utoc> | --verify-conversion-control <reference-root> <candidate-root>");
    return 2;
}

var sourceRoot = Path.GetFullPath(args[0]);
var outputRoot = Path.GetFullPath(args[1]);

if (!Directory.Exists(sourceRoot))
{
    Console.Error.WriteLine($"Source directory not found: {sourceRoot}");
    return 2;
}

CopyDirectory(sourceRoot, outputRoot);

PatchDamageWidget(Path.Combine(outputRoot, "Stalker2", "Content", "Mods", "ShowDMG", "wbp_ShowDMG.uasset"));
PatchDamageArea(Path.Combine(outputRoot, "Stalker2", "Content", "Mods", "ShowDMG", "wbp_ShowDMGArea.uasset"));
PatchHolder(Path.Combine(outputRoot, "Stalker2", "Content", "Mods", "ShowDMG", "bp_dmgActorHolder.uasset"),
    diagnostics || bootstrapDiagnostics);
PatchSpawnerDisplay(Path.Combine(outputRoot, "Stalker2", "Content", "Mods", "ShowDMG", "bpac_dmgWidgetSpawner.uasset"));
if (bootstrapDiagnostics)
{
    PatchBootstrapRunner(Path.Combine(outputRoot, "Stalker2", "Content", "Mods", "ShowDMG", "BP_Run_ModActor.uasset"));
    VerifyBootstrapAssets(outputRoot);
}
else
{
    PatchSpawner(Path.Combine(outputRoot, "Stalker2", "Content", "Mods", "ShowDMG", "bpac_dmgWidgetSpawner.uasset"), diagnostics);
    PatchRunner(Path.Combine(outputRoot, "Stalker2", "Content", "Mods", "ShowDMG", "BP_Run_ModActor.uasset"), diagnostics);
    VerifyPatchedAssets(outputRoot);
}

Console.WriteLine($"Patched HitMarkers assets in {outputRoot}");
return 0;

static UAsset LoadAsset(string path)
{
    if (!File.Exists(path))
    {
        throw new FileNotFoundException("Required cooked asset is missing.", path);
    }

    return new UAsset(path, EngineVersion.VER_UE5_1);
}

static void VerifyConversionControl(string referenceRoot, string candidateRoot)
{
    if (!Directory.Exists(referenceRoot))
    {
        throw new DirectoryNotFoundException($"Conversion-control reference directory not found: {referenceRoot}");
    }
    if (!Directory.Exists(candidateRoot))
    {
        throw new DirectoryNotFoundException($"Conversion-control candidate directory not found: {candidateRoot}");
    }

    static Dictionary<string, string> EnumerateFiles(string root) => Directory
        .EnumerateFiles(root, "*", SearchOption.AllDirectories)
        .ToDictionary(path => Path.GetRelativePath(root, path), path => path, StringComparer.OrdinalIgnoreCase);

    var referenceFiles = EnumerateFiles(referenceRoot);
    var candidateFiles = EnumerateFiles(candidateRoot);
    if (referenceFiles.Count != candidateFiles.Count ||
        !referenceFiles.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase)
            .SetEquals(candidateFiles.Keys))
    {
        throw new InvalidDataException("Conversion-control file set differs after the legacy round trip.");
    }

    var normalizedHeaders = new List<string>();
    foreach (var (relativePath, referencePath) in referenceFiles.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
    {
        var candidatePath = candidateFiles[relativePath];
        var referenceBytes = File.ReadAllBytes(referencePath);
        var candidateBytes = File.ReadAllBytes(candidatePath);
        if (referenceBytes.AsSpan().SequenceEqual(candidateBytes)) continue;

        if (!Path.GetExtension(relativePath).Equals(".uasset", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Conversion control changed cooked payload {relativePath}.");
        }
        if (referenceBytes.Length != candidateBytes.Length)
        {
            throw new InvalidDataException($"Conversion control changed the size of asset header {relativePath}.");
        }

        VerifyLegacyAssetSemantics(referencePath, candidatePath, relativePath);
        normalizedHeaders.Add(relativePath);
    }

    Console.WriteLine(normalizedHeaders.Count == 0
        ? "Conversion-control legacy files are byte-identical."
        : $"Accepted serializer-only header normalization in {normalizedHeaders.Count} asset(s): {string.Join(", ", normalizedHeaders)}");
}

static void VerifyLegacyAssetSemantics(string referencePath, string candidatePath, string relativePath)
{
    var reference = LoadAsset(referencePath);
    var candidate = LoadAsset(candidatePath);

    RequireEquivalent(reference.GetEngineVersion() == candidate.GetEngineVersion(), "engine version");
    RequireEquivalent(reference.IsUnversioned == candidate.IsUnversioned, "unversioned flag");
    RequireEquivalent(reference.UseSeparateBulkDataFiles == candidate.UseSeparateBulkDataFiles,
        "separate bulk-data flag");

    var referenceNames = reference.GetNameMapIndexList().Select(name => name.Value).ToArray();
    var candidateNames = candidate.GetNameMapIndexList().Select(name => name.Value).ToArray();
    RequireEquivalent(referenceNames.SequenceEqual(candidateNames), "name map");

    RequireEquivalent(reference.Imports.Count == candidate.Imports.Count, "import count");
    for (var index = 0; index < reference.Imports.Count; index++)
    {
        var left = reference.Imports[index];
        var right = candidate.Imports[index];
        RequireEquivalent(left.ClassPackage.Value.Value == right.ClassPackage.Value.Value &&
            left.ClassName.Value.Value == right.ClassName.Value.Value &&
            left.ObjectName.Value.Value == right.ObjectName.Value.Value &&
            left.OuterIndex.Index == right.OuterIndex.Index,
            $"import {index}");
    }

    RequireEquivalent(reference.Exports.Count == candidate.Exports.Count, "export count");
    for (var index = 0; index < reference.Exports.Count; index++)
    {
        var left = reference.Exports[index];
        var right = candidate.Exports[index];
        RequireEquivalent(left.GetType() == right.GetType() &&
            left.ObjectName.Value.Value == right.ObjectName.Value.Value &&
            left.ClassIndex.Index == right.ClassIndex.Index &&
            left.SuperIndex.Index == right.SuperIndex.Index &&
            left.TemplateIndex.Index == right.TemplateIndex.Index &&
            left.OuterIndex.Index == right.OuterIndex.Index &&
            left.SerialSize == right.SerialSize,
            $"export {index} metadata");

        if (left is not RawExport leftRaw || right is not RawExport rightRaw)
        {
            throw new InvalidDataException(
                $"Conversion control changed {relativePath}, whose export {index} is not a verifiable raw export.");
        }
        RequireEquivalent(leftRaw.Data.AsSpan().SequenceEqual(rightRaw.Data), $"export {index} payload");
    }

    void RequireEquivalent(bool condition, string field)
    {
        if (!condition)
        {
            throw new InvalidDataException($"Conversion control changed {relativePath} {field}.");
        }
    }
}

static void PatchDirectHudControlWidget(string path)
{
    var asset = LoadAsset(path);
    var function = FunctionScript.Parse(asset, "ShowDamage");
    var functionExport = asset.Exports.OfType<RawExport>()
        .Single(export => export.ObjectName.Value.Value == "ShowDamage");
    var originalFunctionLength = functionExport.Data.Length;

    foreach (var offset in new[] { 99, 344, 401 })
    {
        SetLinearColor(function.At(offset).Expression, asset, 1f, 1f, 1f, 1f);
    }

    var allyJump = (EX_Jump)function.At(396).Expression;
    var enemyJump = (EX_Jump)function.At(453).Expression;
    function.PendingTargets[allyJump] = "old:245";
    function.PendingTargets[enemyJump] = "old:245";
    function.FinalizeScript();
    if (functionExport.Data.Length != originalFunctionLength)
    {
        throw new InvalidDataException("Direct HUD patch changed the ShowDamage export length.");
    }

    var textBlock = asset.Exports.OfType<RawExport>()
        .Single(export => export.ObjectName.Value.Value == "TXT_DMG");
    ReplaceSingle(textBlock.Data, new byte[] { 0x31, 0x30, 0x30, 0x00 }, new byte[] { 0x20, 0x58, 0x20, 0x00 });
    asset.Write(path);
}

static void PatchDirectReleaseWidget(string path)
{
    var asset = LoadAsset(path);
    var function = FunctionScript.Parse(asset, "ShowDamage");
    var functionExport = asset.Exports.OfType<RawExport>()
        .Single(export => export.ObjectName.Value.Value == "ShowDamage");
    var originalFunctionLength = functionExport.Data.Length;

    SetLinearColor(function.At(99).Expression, asset, 1f, 0f, 0f, 1f);
    SetLinearColor(function.At(344).Expression, asset, 1f, 1f, 1f, 0f);
    SetLinearColor(function.At(401).Expression, asset, 1f, 1f, 1f, 1f);

    var skipNumericText = new EX_Jump();
    function.At(151).Expression = skipNumericText;
    function.PendingTargets[(EX_JumpIfNot)function.At(35).Expression] = "old:344";
    function.PendingTargets[(EX_JumpIfNot)function.At(85).Expression] = "old:401";
    function.PendingTargets[skipNumericText] = "old:245";
    function.PendingTargets[(EX_Jump)function.At(339).Expression] = "old:458";
    function.PendingTargets[(EX_Jump)function.At(396).Expression] = "old:245";
    function.PendingTargets[(EX_Jump)function.At(453).Expression] = "old:245";

    FinalizeWithStoragePadding(function, functionExport, function.At(245), originalFunctionLength,
        "direct_release_widget_padding");

    var textBlock = asset.Exports.OfType<RawExport>()
        .Single(export => export.ObjectName.Value.Value == "TXT_DMG");
    ReplaceSingle(textBlock.Data, new byte[] { 0x31, 0x30, 0x30, 0x00 }, new byte[] { 0x20, 0x58, 0x20, 0x00 });
    asset.Write(path);
}

static void PatchDirectReleaseSpawner(string path)
{
    var asset = LoadAsset(path);
    var tickTarget = ReadEntrypoint(asset, "ReceiveTick");
    var tickExport = asset.Exports.OfType<RawExport>()
        .Single(export => export.ObjectName.Value.Value == "ReceiveTick");
    var originalTickLength = tickExport.Data.Length;
    var graph = FunctionScript.Parse(asset, "ExecuteUbergraph_bpac_dmgWidgetSpawner");
    var graphExport = asset.Exports.OfType<RawExport>()
        .Single(export => export.ObjectName.Value.Value == "ExecuteUbergraph_bpac_dmgWidgetSpawner");
    var originalGraphLength = graphExport.Data.Length;

    var damageComparison = (EX_LetBool)graph.At(123).Expression;
    var greaterCall = (EX_CallMath)damageComparison.AssignmentExpression;
    var currentHp = Local(asset, "CallFunc_Greater_DoubleDouble_B_ImplicitCast", graph.ExportIndex);
    var subtractCall = (EX_CallMath)((EX_Let)graph.At(241).Expression).Expression;
    subtractCall.Parameters[1] = currentHp;
    ((EX_Let)graph.At(422).Expression).Expression = currentHp;

    foreach (var offset in new[] { 175, 212, 356, 393 })
    {
        graph.Statements.Remove(graph.At(offset));
    }

    var aliveTest = new EX_LetBool
    {
        VariableExpression = damageComparison.VariableExpression,
        AssignmentExpression = new EX_CallMath
        {
            StackNode = greaterCall.StackNode,
            Parameters = new KismetExpression[] { currentHp, new EX_DoubleConst { Value = 0d } }
        }
    };
    var killJump = new EX_JumpIfNot
    {
        BooleanExpression = damageComparison.VariableExpression
    };
    var skipKill = new EX_Jump();
    var hitCallStatement = graph.At(324);
    var hitCall = (EX_LocalVirtualFunction)hitCallStatement.Expression;
    var killCall = new EX_LocalVirtualFunction
    {
        VirtualFunctionName = hitCall.VirtualFunctionName,
        Parameters = new KismetExpression[]
        {
            hitCall.Parameters[0], new EX_StringConst { Value = "Kill" }
        }
    };

    graph.InsertBefore(hitCallStatement, new ScriptStatement(null, "direct_alive_test", aliveTest));
    graph.InsertBefore(hitCallStatement, new ScriptStatement(null, "direct_kill_jump", killJump));
    graph.InsertAfter(hitCallStatement, new ScriptStatement(null, "direct_skip_kill", skipKill));
    graph.InsertBefore(graph.At(422), new ScriptStatement(null, "direct_kill_call", killCall));

    graph.PendingTargets[(EX_JumpIfNot)graph.At(43).Expression] = "old:459";
    graph.PendingTargets[(EX_JumpIfNot)graph.At(161).Expression] = "old:422";
    graph.PendingTargets[killJump] = "direct_kill_call";
    graph.PendingTargets[skipKill] = "old:422";
    graph.PendingTargets[(EX_Jump)graph.At(449).Expression] = "old:459";
    graph.PendingTargets[(EX_Jump)graph.At(454).Expression] = "old:10";

    var graphMap = FinalizeWithStoragePadding(graph, graphExport, graph.At(454), originalGraphLength,
        "direct_release_spawner_padding");
    UpdateEntrypoint(asset, "ReceiveTick", tickTarget, graphMap[tickTarget]);
    if (tickExport.Data.Length != originalTickLength)
    {
        throw new InvalidDataException("Direct release patch changed the ReceiveTick export length.");
    }

    asset.Write(path);
}

static Dictionary<int, uint> FinalizeWithStoragePadding(
    FunctionScript function,
    RawExport functionExport,
    ScriptStatement paddingAnchor,
    int requiredLength,
    string tagPrefix)
{
    function.FinalizeScript();
    var deficit = requiredLength - functionExport.Data.Length;
    if (deficit < 0)
    {
        throw new InvalidDataException(
            $"Length-preserving patch exceeded its export by {-deficit} byte(s).");
    }

    if (deficit > 0)
    {
        var insertion = function.Statements.IndexOf(paddingAnchor);
        function.Statements.InsertRange(insertion, Enumerable.Range(0, deficit)
            .Select(index => new ScriptStatement(null, $"{tagPrefix}_{index}", new EX_Nothing())));
    }

    var finalMap = function.FinalizeScript();
    if (functionExport.Data.Length != requiredLength)
    {
        throw new InvalidDataException(
            $"Length-preserving patch produced {functionExport.Data.Length} bytes; expected {requiredLength}.");
    }
    return finalMap;
}

static void VerifyDirectReleaseAssets(string sourceRoot, string candidateRoot)
{
    var patches = CollectLegacyExportPatches(sourceRoot, candidateRoot);
    var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        @"Stalker2\Content\Mods\ShowDMG\wbp_ShowDMG.uasset|ShowDamage",
        @"Stalker2\Content\Mods\ShowDMG\wbp_ShowDMG.uasset|TXT_DMG",
        @"Stalker2\Content\Mods\ShowDMG\bpac_dmgWidgetSpawner.uasset|ExecuteUbergraph_bpac_dmgWidgetSpawner",
        @"Stalker2\Content\Mods\ShowDMG\bpac_dmgWidgetSpawner.uasset|ReceiveTick"
    };
    var actual = patches.Select(patch => $"{patch.RelativeAssetPath}|{patch.ExportName}")
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
    if (!actual.SetEquals(expected))
    {
        throw new InvalidDataException(
            $"Direct release changed an unexpected export set: {string.Join(", ", actual.Order())}");
    }

    var modRoot = Path.Combine(candidateRoot, "Stalker2", "Content", "Mods", "ShowDMG");
    var widget = LoadAsset(Path.Combine(modRoot, "wbp_ShowDMG.uasset"));
    var showMarker = FunctionScript.Parse(widget, "ShowDamage");
    var markerJump = showMarker.At(151).Expression as EX_Jump
        ?? throw new InvalidDataException("Direct release does not bypass numeric text conversion.");
    var markerTarget = showMarker.Statements.SingleOrDefault(statement =>
        statement.OriginalOffset == checked((int)markerJump.CodeOffset));
    Require(markerTarget is not null && FlattenExpression(markerTarget.Expression, widget)
        .OfType<EX_FinalFunction>().Any(call => StackName(call.StackNode, widget) == "PlayAnimation"),
        "direct release marker jump targets the fade animation");

    var colors = showMarker.Statements
        .Select(statement => ReadLinearColor(statement.Expression, widget))
        .Where(color => color.Length == 4)
        .ToArray();
    Require(colors.Length == 3, "direct release retains exactly three marker color branches");
    Require(ColorEquals(colors[0], 1f, 0f, 0f, 1f), "kill marker is opaque red");
    Require(ColorEquals(colors[1], 1f, 1f, 1f, 0f), "friendly marker is transparent");
    Require(ColorEquals(colors[2], 1f, 1f, 1f, 1f), "enemy hit marker is opaque white");
    var textBlock = widget.Exports.OfType<RawExport>()
        .Single(export => export.ObjectName.Value.Value == "TXT_DMG");
    Require(textBlock.Data.AsSpan().IndexOf(new byte[] { 0x20, 0x58, 0x20, 0x00 }) >= 0,
        "direct release marker text exists");

    var spawner = LoadAsset(Path.Combine(modRoot, "bpac_dmgWidgetSpawner.uasset"));
    var tickTarget = ReadEntrypoint(spawner, "ReceiveTick");
    var graph = FunctionScript.Parse(spawner, "ExecuteUbergraph_bpac_dmgWidgetSpawner");
    var tickEntry = graph.Statements.SingleOrDefault(statement => statement.OriginalOffset == tickTarget);
    Require(tickEntry?.Expression is EX_Jump tickJump && tickJump.CodeOffset == 10,
        "direct release ReceiveTick targets the preserved polling flow");
    var graphNodes = Flatten(graph, spawner);
    Require(graphNodes.OfType<EX_CallMath>().Count(call => StackName(call.StackNode, spawner) == "ObjGetHP") == 1,
        "direct release polls HP once per tick");
    Require(graphNodes.OfType<EX_CallMath>().Count(call => StackName(call.StackNode, spawner) == "Greater_DoubleDouble") == 2,
        "direct release contains damage and alive comparisons");
    var markerCalls = graphNodes.OfType<EX_LocalVirtualFunction>()
        .Where(call => call.VirtualFunctionName.Value.Value == "showDamage").ToArray();
    Require(markerCalls.Length == 2, "direct release contains one hit and one kill display call");
    Require(markerCalls.Count(call => call.Parameters.ElementAtOrDefault(1) is EX_InstanceVariable relation &&
        relation.Variable.New.Path.Any(name => name.Value.Value == "Rel")) == 1,
        "direct release hit call preserves enemy relation classification");
    Require(markerCalls.Count(call => call.Parameters.ElementAtOrDefault(1) is EX_StringConst type &&
        type.Value == "Kill") == 1,
        "direct release lethal call selects the red marker branch");
    Require(graphNodes.OfType<EX_DoubleConst>().Any(value => Math.Abs(value.Value) < 0.0001d),
        "direct release lethal test compares current HP with zero");
}

static void VerifyDirectHudControlAssets(string sourceRoot, string candidateRoot)
{
    var patches = CollectLegacyExportPatches(sourceRoot, candidateRoot);
    var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        @"Stalker2\Content\Mods\ShowDMG\wbp_ShowDMG.uasset|ShowDamage",
        @"Stalker2\Content\Mods\ShowDMG\wbp_ShowDMG.uasset|TXT_DMG"
    };
    var actual = patches
        .Select(patch => $"{patch.RelativeAssetPath}|{patch.ExportName}")
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
    if (!actual.SetEquals(expected))
    {
        throw new InvalidDataException(
            $"Direct HUD control changed an unexpected export set: {string.Join(", ", actual.Order())}");
    }

    var widgetPath = Path.Combine(candidateRoot, "Stalker2", "Content", "Mods", "ShowDMG", "wbp_ShowDMG.uasset");
    var widget = LoadAsset(widgetPath);
    var function = FunctionScript.Parse(widget, "ShowDamage");
    Require(function.At(396).Expression is EX_Jump allyJump && allyJump.CodeOffset == 245,
        "direct HUD control skips Ally numeric text without shifting bytecode");
    Require(function.At(453).Expression is EX_Jump enemyJump && enemyJump.CodeOffset == 245,
        "direct HUD control skips Enemy numeric text without shifting bytecode");
    foreach (var offset in new[] { 99, 344, 401 })
    {
        Require(IsLinearColor(function.At(offset).Expression, widget, 1f, 1f, 1f, 1f),
            $"direct HUD control color at bytecode offset {offset} is opaque white");
    }

    var textBlock = widget.Exports.OfType<RawExport>()
        .Single(export => export.ObjectName.Value.Value == "TXT_DMG");
    Require(textBlock.Data.AsSpan().IndexOf(new byte[] { 0x20, 0x58, 0x20, 0x00 }) >= 0,
        "direct HUD control marker text exists");
    Require(textBlock.Data.AsSpan().IndexOf(new byte[] { 0x31, 0x30, 0x30, 0x00 }) < 0,
        "direct HUD control numeric placeholder was removed");
}

static List<LegacyExportPatch> CollectLegacyExportPatches(string sourceRoot, string candidateRoot)
{
    if (!Directory.Exists(sourceRoot) || !Directory.Exists(candidateRoot))
    {
        throw new DirectoryNotFoundException("Legacy source or candidate directory is missing.");
    }

    var sourceAssets = Directory.EnumerateFiles(sourceRoot, "*.uasset", SearchOption.AllDirectories)
        .ToDictionary(path => Path.GetRelativePath(sourceRoot, path), path => path, StringComparer.OrdinalIgnoreCase);
    var candidateAssets = Directory.EnumerateFiles(candidateRoot, "*.uasset", SearchOption.AllDirectories)
        .ToDictionary(path => Path.GetRelativePath(candidateRoot, path), path => path, StringComparer.OrdinalIgnoreCase);
    if (sourceAssets.Count != candidateAssets.Count ||
        !sourceAssets.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(candidateAssets.Keys))
    {
        throw new InvalidDataException("Legacy asset set changed during direct HUD patching.");
    }

    var patches = new List<LegacyExportPatch>();
    foreach (var (relativePath, sourcePath) in sourceAssets.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
    {
        var candidatePath = candidateAssets[relativePath];
        if (new FileInfo(sourcePath).Length != new FileInfo(candidatePath).Length)
        {
            throw new InvalidDataException($"Direct HUD patch changed the header length of {relativePath}.");
        }

        var sourceExportPath = Path.ChangeExtension(sourcePath, ".uexp");
        var candidateExportPath = Path.ChangeExtension(candidatePath, ".uexp");
        if (File.Exists(sourceExportPath) != File.Exists(candidateExportPath) ||
            (File.Exists(sourceExportPath) && new FileInfo(sourceExportPath).Length != new FileInfo(candidateExportPath).Length))
        {
            throw new InvalidDataException($"Direct HUD patch changed the export-file length of {relativePath}.");
        }

        var source = LoadAsset(sourcePath);
        var candidate = LoadAsset(candidatePath);
        RequireSameAssetStructure(source, candidate, relativePath);
        for (var index = 0; index < source.Exports.Count; index++)
        {
            if (source.Exports[index] is not RawExport sourceExport ||
                candidate.Exports[index] is not RawExport candidateExport)
            {
                throw new InvalidDataException($"Direct HUD asset {relativePath} export {index} is not raw.");
            }
            if (sourceExport.Data.Length != candidateExport.Data.Length)
            {
                throw new InvalidDataException($"Direct HUD patch changed {relativePath} export {index} length.");
            }
            if (!sourceExport.Data.AsSpan().SequenceEqual(candidateExport.Data))
            {
                patches.Add(new LegacyExportPatch(relativePath, sourceExport.ObjectName.Value.Value,
                    sourceExport.Data.ToArray(), candidateExport.Data.ToArray()));
            }
        }
    }

    return patches;
}

static void RequireSameAssetStructure(UAsset source, UAsset candidate, string relativePath)
{
    void Same(bool condition, string field)
    {
        if (!condition) throw new InvalidDataException($"Direct HUD patch changed {relativePath} {field}.");
    }

    Same(source.GetEngineVersion() == candidate.GetEngineVersion(), "engine version");
    Same(source.IsUnversioned == candidate.IsUnversioned, "unversioned flag");
    Same(source.UseSeparateBulkDataFiles == candidate.UseSeparateBulkDataFiles, "bulk-data flag");
    Same(source.GetNameMapIndexList().Select(name => name.Value)
        .SequenceEqual(candidate.GetNameMapIndexList().Select(name => name.Value)), "name map");
    Same(source.Imports.Count == candidate.Imports.Count, "import count");
    for (var index = 0; index < source.Imports.Count; index++)
    {
        var left = source.Imports[index];
        var right = candidate.Imports[index];
        Same(left.ClassPackage.Value.Value == right.ClassPackage.Value.Value &&
            left.ClassName.Value.Value == right.ClassName.Value.Value &&
            left.ObjectName.Value.Value == right.ObjectName.Value.Value &&
            left.OuterIndex.Index == right.OuterIndex.Index,
            $"import {index}");
    }

    Same(source.Exports.Count == candidate.Exports.Count, "export count");
    for (var index = 0; index < source.Exports.Count; index++)
    {
        var left = source.Exports[index];
        var right = candidate.Exports[index];
        Same(left.GetType() == right.GetType() &&
            left.ObjectName.Value.Value == right.ObjectName.Value.Value &&
            left.ClassIndex.Index == right.ClassIndex.Index &&
            left.SuperIndex.Index == right.SuperIndex.Index &&
            left.TemplateIndex.Index == right.TemplateIndex.Index &&
            left.OuterIndex.Index == right.OuterIndex.Index &&
            left.SerialSize == right.SerialSize,
            $"export {index} metadata");
    }
}

static bool IsLinearColor(KismetExpression expression, UAsset asset, params float[] expected)
{
    return ColorEquals(ReadLinearColor(expression, asset), expected);
}

static float[] ReadLinearColor(KismetExpression expression, UAsset asset)
{
    var values = new List<float>();
    Visit(expression, asset, node =>
    {
        if (node is EX_FloatConst value) values.Add(value.Value);
    });
    return values.ToArray();
}

static bool ColorEquals(float[] actual, params float[] expected)
{
    return actual.Length == expected.Length &&
        actual.Zip(expected).All(pair => Math.Abs(pair.First - pair.Second) < 0.0001f);
}

static void TransplantLegacyExports(
    string referenceRawRoot,
    string sourceRoot,
    string patchedRoot,
    string outputRawRoot)
{
    if (!Directory.Exists(referenceRawRoot))
    {
        throw new DirectoryNotFoundException($"Reference raw container directory not found: {referenceRawRoot}");
    }
    if (Path.GetFullPath(referenceRawRoot).Equals(Path.GetFullPath(outputRawRoot),
        StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("Direct Zen transplant requires a distinct output directory.");
    }

    CopyDirectory(referenceRawRoot, outputRawRoot);
    var expectedChunks = BuildTransplantedChunks(referenceRawRoot, sourceRoot, patchedRoot);
    var outputChunkRoot = Path.Combine(outputRawRoot, "chunks");
    foreach (var (chunkId, bytes) in expectedChunks)
    {
        var outputChunk = Path.Combine(outputChunkRoot, chunkId);
        if (!File.Exists(outputChunk))
        {
            throw new FileNotFoundException("Output raw chunk is missing.", outputChunk);
        }
        File.WriteAllBytes(outputChunk, bytes);
    }

    VerifyTransplantedExports(referenceRawRoot, sourceRoot, patchedRoot, outputRawRoot);
}

static void VerifyTransplantedExports(
    string referenceRawRoot,
    string sourceRoot,
    string patchedRoot,
    string candidateRawRoot)
{
    if (!Directory.Exists(referenceRawRoot) || !Directory.Exists(candidateRawRoot))
    {
        throw new DirectoryNotFoundException("Reference or candidate raw container directory is missing.");
    }

    var expectedChunks = BuildTransplantedChunks(referenceRawRoot, sourceRoot, patchedRoot);
    if (expectedChunks.Count == 0)
    {
        throw new InvalidDataException("Direct Zen transplant has no changed chunks.");
    }

    static Dictionary<string, string> FilesByRelativePath(string root) => Directory
        .EnumerateFiles(root, "*", SearchOption.AllDirectories)
        .ToDictionary(path => Path.GetRelativePath(root, path), path => path, StringComparer.OrdinalIgnoreCase);

    var referenceFiles = FilesByRelativePath(referenceRawRoot);
    var candidateFiles = FilesByRelativePath(candidateRawRoot);
    if (referenceFiles.Count != candidateFiles.Count ||
        !referenceFiles.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(candidateFiles.Keys))
    {
        throw new InvalidDataException("Direct Zen transplant changed the raw container file set.");
    }

    foreach (var (relativePath, referencePath) in referenceFiles)
    {
        var candidatePath = candidateFiles[relativePath];
        if (relativePath.Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
        {
            RequireEquivalentRawManifests(referencePath, candidatePath);
            continue;
        }
        var expected = relativePath.StartsWith($"chunks{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase) &&
            expectedChunks.TryGetValue(Path.GetFileName(relativePath), out var changedChunk)
                ? changedChunk
                : File.ReadAllBytes(referencePath);
        var candidate = File.ReadAllBytes(candidatePath);
        if (!expected.AsSpan().SequenceEqual(candidate))
        {
            throw new InvalidDataException($"Direct Zen transplant produced unexpected bytes in {relativePath}.");
        }
    }

    Console.WriteLine($"Direct Zen transplant changed exactly {expectedChunks.Count} raw chunk(s): " +
        string.Join(", ", expectedChunks.Keys.Order(StringComparer.Ordinal)));
}

static void RequireEquivalentRawManifests(string referencePath, string candidatePath)
{
    static (string Version, string MountPoint, Dictionary<string, string> Chunks) Read(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        var root = document.RootElement;
        var chunks = root.GetProperty("chunk_paths").EnumerateObject()
            .ToDictionary(property => property.Name,
                property => property.Value.GetString() ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);
        return (
            root.GetProperty("version").GetString() ?? string.Empty,
            root.GetProperty("mount_point").GetString() ?? string.Empty,
            chunks);
    }

    var reference = Read(referencePath);
    var candidate = Read(candidatePath);
    if (reference.Version != candidate.Version || reference.MountPoint != candidate.MountPoint ||
        reference.Chunks.Count != candidate.Chunks.Count ||
        reference.Chunks.Any(pair => !candidate.Chunks.TryGetValue(pair.Key, out var value) || value != pair.Value))
    {
        throw new InvalidDataException("Direct Zen transplant changed raw manifest semantics.");
    }
}

static Dictionary<string, byte[]> BuildTransplantedChunks(
    string referenceRawRoot,
    string sourceRoot,
    string patchedRoot)
{
    var exportPatches = CollectLegacyExportPatches(sourceRoot, patchedRoot);
    if (exportPatches.Count == 0)
    {
        throw new InvalidDataException("No changed legacy exports were supplied for direct Zen transplant.");
    }

    var manifestPath = Path.Combine(referenceRawRoot, "manifest.json");
    if (!File.Exists(manifestPath))
    {
        throw new FileNotFoundException("Reference raw manifest is missing.", manifestPath);
    }

    using var document = JsonDocument.Parse(File.ReadAllBytes(manifestPath));
    var chunkPaths = document.RootElement.GetProperty("chunk_paths")
        .EnumerateObject()
        .ToDictionary(
            property => NormalizeCookedPath(property.Value.GetString()
                ?? throw new InvalidDataException("Raw manifest contains a null chunk path.")),
            property => property.Name,
            StringComparer.OrdinalIgnoreCase);

    var chunks = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
    var occupiedRanges = new Dictionary<string, List<(int Start, int End)>>(StringComparer.OrdinalIgnoreCase);
    foreach (var patch in exportPatches)
    {
        var cookedPath = NormalizeCookedPath(patch.RelativeAssetPath);
        if (!chunkPaths.TryGetValue(cookedPath, out var chunkId))
        {
            throw new InvalidDataException($"Raw manifest has no package chunk for {patch.RelativeAssetPath}.");
        }

        if (!chunks.TryGetValue(chunkId, out var chunk))
        {
            var chunkPath = Path.Combine(referenceRawRoot, "chunks", chunkId);
            if (!File.Exists(chunkPath)) throw new FileNotFoundException("Reference raw chunk is missing.", chunkPath);
            chunk = File.ReadAllBytes(chunkPath);
            chunks.Add(chunkId, chunk);
            occupiedRanges.Add(chunkId, new List<(int Start, int End)>());
        }

        if (patch.SourceData.Length != patch.PatchedData.Length || patch.SourceData.Length == 0)
        {
            throw new InvalidDataException(
                $"Export {patch.RelativeAssetPath}:{patch.ExportName} is not length-preserving.");
        }

        var offset = FindUniqueSequence(chunk, patch.SourceData,
            $"{patch.RelativeAssetPath}:{patch.ExportName}");
        var end = checked(offset + patch.SourceData.Length);
        if (occupiedRanges[chunkId].Any(range => offset < range.End && end > range.Start))
        {
            throw new InvalidDataException($"Direct Zen export patches overlap in chunk {chunkId}.");
        }

        patch.PatchedData.CopyTo(chunk, offset);
        occupiedRanges[chunkId].Add((offset, end));
        Console.WriteLine($"Mapped {patch.RelativeAssetPath}:{patch.ExportName} to chunk {chunkId} at 0x{offset:X}.");
    }

    return chunks;
}

static int FindUniqueSequence(byte[] haystack, byte[] needle, string description)
{
    var first = haystack.AsSpan().IndexOf(needle);
    if (first < 0)
    {
        throw new InvalidDataException($"Original Zen chunk does not contain export {description}.");
    }

    var remainder = haystack.AsSpan(first + 1);
    if (remainder.IndexOf(needle) >= 0)
    {
        throw new InvalidDataException($"Original Zen chunk contains export {description} more than once.");
    }
    return first;
}

static string NormalizeCookedPath(string path)
{
    var normalized = path.Replace('\\', '/');
    while (normalized.StartsWith("../", StringComparison.Ordinal)) normalized = normalized[3..];
    return normalized.TrimStart('/');
}

static void PatchDamageWidget(string path)
{
    var asset = LoadAsset(path);
    var function = FunctionScript.Parse(asset, "ShowDamage");

    foreach (var statement in function.Statements)
    {
        Visit(statement.Expression, asset, expression =>
        {
            if (expression is EX_StringConst text)
            {
                text.Value = text.Value switch
                {
                    "Ally" => "Hit",
                    "Enemy" => "Kill",
                    _ => text.Value
                };
            }
        });
    }

    SetLinearColor(function.At(99).Expression, asset, 1f, 1f, 1f, 1f);
    SetLinearColor(function.At(344).Expression, asset, 1f, 1f, 1f, 1f);
    SetLinearColor(function.At(401).Expression, asset, 1f, 0f, 0f, 1f);

    function.At(151).Expression = new EX_Nothing();
    function.At(200).Expression = new EX_Nothing();
    function.FinalizeScript();

    var textBlock = asset.Exports.OfType<RawExport>()
        .Single(export => export.ObjectName.Value.Value == "TXT_DMG");
    ReplaceSingle(textBlock.Data, new byte[] { 0x31, 0x30, 0x30, 0x00 }, new byte[] { 0x20, 0x58, 0x20, 0x00 });

    asset.Write(path);
}

static void PatchDamageArea(string path)
{
    var asset = LoadAsset(path);
    var function = FunctionScript.Parse(asset, "showDamage");
    var gameplayStatics = EnsureClass(asset, "/Script/Engine", "GameplayStatics");
    var getPlayerController = EnsureObject(asset, gameplayStatics, "GetPlayerController");

    var create = (EX_FinalFunction)((EX_Context)((EX_LetObj)function.At(0).Expression).AssignmentExpression).ContextExpression;
    create.Parameters[2] = new EX_CallMath
    {
        StackNode = getPlayerController,
        Parameters = new KismetExpression[] { new EX_Self(), new EX_IntConst { Value = 0 } }
    };

    var marker = Local(asset, "CallFunc_Create_ReturnValue", function.ExportIndex);
    var visible = new ScriptStatement(null, "marker_visible", new EX_Context
    {
        ObjectExpression = marker,
        RValuePointer = EmptyProperty(),
        ContextExpression = new EX_VirtualFunction
        {
            VirtualFunctionName = new FName(asset, "SetVisibility"),
            Parameters = new KismetExpression[] { new EX_ByteConst { Value = 0 } }
        }
    });
    var opaque = new ScriptStatement(null, "marker_opaque", new EX_Context
    {
        ObjectExpression = marker,
        RValuePointer = EmptyProperty(),
        ContextExpression = new EX_VirtualFunction
        {
            VirtualFunctionName = new FName(asset, "SetRenderOpacity"),
            Parameters = new KismetExpression[] { new EX_FloatConst { Value = 1f } }
        }
    });
    function.Statements.InsertRange(function.Statements.IndexOf(function.At(53)), new[] { visible, opaque });

    foreach (var offset in new[] { 53, 99 })
    {
        Visit(function.At(offset).Expression, asset, expression =>
        {
            if (expression is EX_DoubleConst value)
            {
                value.Value = 0d;
            }
        });
    }

    function.FinalizeScript();
    asset.Write(path);
}

static void PatchHolder(string path, bool diagnostics)
{
    var asset = LoadAsset(path);
    var beginPlayTarget = ReadEntrypoint(asset, "ReceiveBeginPlay");
    var tickTarget = ReadEntrypoint(asset, "ReceiveTick");
    var graph = FunctionScript.Parse(asset, "ExecuteUbergraph_bp_dmgActorHolder");
    var gameplayStatics = EnsureClass(asset, "/Script/Engine", "GameplayStatics");
    var getPlayerController = EnsureObject(asset, gameplayStatics, "GetPlayerController");

    var showContext = (EX_Context)graph.At(81).Expression;
    var drawContext = (EX_Context)graph.At(502).Expression;
    drawContext.ObjectExpression = showContext.ObjectExpression;
    drawContext.RValuePointer = showContext.RValuePointer;
    drawContext.ContextExpression = new EX_VirtualFunction
    {
        VirtualFunctionName = new FName(asset, "AddToPlayerScreen"),
        Parameters = new KismetExpression[] { new EX_IntConst { Value = 10000 } }
    };

    var create = (EX_FinalFunction)((EX_Context)((EX_LetObj)graph.At(430).Expression).AssignmentExpression).ContextExpression;
    create.Parameters[2] = new EX_CallMath
    {
        StackNode = getPlayerController,
        Parameters = new KismetExpression[] { new EX_Self(), new EX_IntConst { Value = 0 } }
    };

    var killComparison = new EX_LetBool
    {
        VariableExpression = Local(asset, "CallFunc_NotEqual_StrStr_ReturnValue", graph.ExportIndex),
        AssignmentExpression = new EX_CallMath
        {
            StackNode = FPackageIndex.FromRawIndex(-22),
            Parameters = new KismetExpression[]
            {
                Instance(asset, "rel", FPackageIndex.FromExport(0)),
                new EX_StringConst { Value = "Kill" }
            }
        }
    };
    var killJump = new EX_JumpIfNot
    {
        BooleanExpression = Local(asset, "CallFunc_NotEqual_StrStr_ReturnValue", graph.ExportIndex)
    };
    var afterDisplay = new EX_Jump();
    var killDisplay = new EX_Context
    {
        ObjectExpression = showContext.ObjectExpression,
        RValuePointer = showContext.RValuePointer,
        ContextExpression = new EX_VirtualFunction
        {
            VirtualFunctionName = new FName(asset, "AddToPlayerScreen"),
            Parameters = new KismetExpression[] { new EX_IntConst { Value = 10001 } }
        }
    };
    var hitDisplayStatement = graph.At(502);
    graph.InsertBefore(hitDisplayStatement, new ScriptStatement(null, "kill_comparison", killComparison));
    graph.InsertBefore(hitDisplayStatement, new ScriptStatement(null, "kill_jump", killJump));
    graph.InsertAfter(hitDisplayStatement, new ScriptStatement(null, "after_display", afterDisplay));
    graph.InsertBefore(graph.At(566), new ScriptStatement(null, "kill_display", killDisplay));
    graph.PendingTargets[killJump] = "kill_display";
    graph.PendingTargets[afterDisplay] = "old:566";
    graph.At(566).Expression = showContext;

    var cleanupDelay = graph.At(135);
    graph.Statements.Remove(cleanupDelay);
    graph.InsertAfter(graph.At(566), cleanupDelay);
    if (diagnostics)
    {
        graph.InsertBefore(cleanupDelay, new ScriptStatement(null, "widget_diagnostic",
            PrintLog(asset, "[HitMarkers][Diagnostic] widget created; viewport inserted; visible; fade started")));
    }
    graph.At(81).Expression = new EX_Nothing();

    Visit(graph.At(135).Expression, asset, expression =>
    {
        if (expression is EX_FloatConst delay && Math.Abs(delay.Value - 5f) < 0.001f)
        {
            delay.Value = 1.1f;
        }
    });

    var destroy = graph.At(15);
    destroy.OriginalOffset = null;
    destroy.Tag = "destroy_actor";

    var removeContext = new EX_Context
    {
        ObjectExpression = showContext.ObjectExpression,
        RValuePointer = showContext.RValuePointer,
        PropertyType = showContext.PropertyType,
        ContextExpression = new EX_VirtualFunction
        {
            VirtualFunctionName = new FName(asset, "RemoveFromParent"),
            Parameters = Array.Empty<KismetExpression>()
        }
    };
    graph.InsertBefore(destroy, new ScriptStatement(15, "remove_area", removeContext));
    if (diagnostics)
    {
        graph.InsertBefore(destroy, new ScriptStatement(null, "fade_diagnostic",
            PrintLog(asset, "[HitMarkers][Diagnostic] fade completed; widget removed")));
    }

    var holderMap = graph.FinalizeScript();
    var disabledTick = graph.Statements.Last(statement => statement.Expression is EX_Return).OriginalOffset
        ?? throw new InvalidDataException("Holder terminal return has no original offset.");
    UpdateEntrypoint(asset, "ReceiveBeginPlay", beginPlayTarget, holderMap[beginPlayTarget]);
    UpdateEntrypoint(asset, "ReceiveTick", tickTarget, holderMap[disabledTick]);
    asset.Write(path);
}

static void PatchSpawnerDisplay(string path)
{
    var asset = LoadAsset(path);
    var showDamage = FunctionScript.Parse(asset, "showDamage");
    var damageAssignment = showDamage.At(354);
    var typeAssignment = showDamage.At(403);
    showDamage.Statements.Remove(damageAssignment);
    showDamage.Statements.Remove(typeAssignment);
    var finish = showDamage.At(316);
    var deferredOwner = Local(asset, "CallFunc_BeginDeferredActorSpawnFromClass_ReturnValue", showDamage.ExportIndex);
    ((EX_Context)((EX_Let)damageAssignment.Expression).Variable).ObjectExpression = deferredOwner;
    ((EX_Context)((EX_Let)typeAssignment.Expression).Variable).ObjectExpression = deferredOwner;
    showDamage.InsertBefore(finish, damageAssignment);
    showDamage.InsertBefore(finish, typeAssignment);
    showDamage.FinalizeScript();
    asset.Write(path);
}

static void PatchSpawner(string path, bool diagnostics)
{
    var asset = LoadAsset(path);
    var tickTarget = ReadEntrypoint(asset, "ReceiveTick");
    var graph = FunctionScript.Parse(asset, "ExecuteUbergraph_bpac_dmgWidgetSpawner");

    var stalkerPackage = EnsurePackage(asset, "/Script/Stalker2");
    var corePackage = EnsurePackage(asset, "/Script/CoreUObject");
    var enginePackage = EnsurePackage(asset, "/Script/Engine");
    var commonHitArgs = EnsureObject(asset, stalkerPackage, "CommonHitArgs");
    var bulletHitArgs = EnsureObject(asset, stalkerPackage, "BulletProjectileHitArgs");
    var guidType = EnsureObject(asset, corePackage, "Guid");
    var latentInfo = EnsureObject(asset, enginePackage, "LatentActionInfo");
    var cppMediator = EnsureClass(asset, "/Script/Stalker2", "CppMediator");
    var gameplayStatics = EnsureClass(asset, "/Script/Engine", "GameplayStatics");
    var guidLibrary = EnsureClass(asset, "/Script/Engine", "KismetGuidLibrary");
    var mathLibrary = EnsureClass(asset, "/Script/Engine", "KismetMathLibrary");
    var systemLibrary = EnsureClass(asset, "/Script/Engine", "KismetSystemLibrary");
    var getGuid = EnsureObject(asset, cppMediator, "GetGUID");
    var getPlayerCharacter = EnsureObject(asset, gameplayStatics, "GetPlayerCharacter");
    var equalGuid = EnsureObject(asset, guidLibrary, "EqualEqual_GuidGuid");
    var getFocusedEnemy = EnsureObject(asset, cppMediator, "GetFocusedEnemy");
    var booleanOr = EnsureObject(asset, mathLibrary, "BooleanOR");
    var equalObject = EnsureObject(asset, mathLibrary, "EqualEqual_ObjectObject");
    var delayFunction = EnsureObject(asset, systemLibrary, "Delay");

    graph.Properties.Add(StructProperty(asset, "K2Node_CustomEvent_Common", 336, commonHitArgs,
        EPropertyFlags.CPF_None));
    graph.Properties.Add(StructProperty(asset, "K2Node_CustomEvent_HitArgs", 12, bulletHitArgs,
        EPropertyFlags.CPF_None));

    var pendingCheck = (EX_CallMath)((EX_LetBool)graph.At(10).Expression).AssignmentExpression;
    ((EX_StringConst)pendingCheck.Parameters[1]).Value = "Pending";
    var pendingJump = (EX_JumpIfNot)graph.At(43).Expression;
    var baselineJump = new EX_Jump();
    graph.InsertAfter(graph.At(43), new ScriptStatement(null, "baseline_jump", baselineJump));
    graph.PendingTargets[pendingJump] = "old:57";
    graph.PendingTargets[baselineJump] = "old:356";
    graph.PendingTargets[(EX_JumpIfNot)graph.At(161).Expression] = "old:459";

    var originalComparison = (EX_LetBool)graph.At(123).Expression;
    var comparisonCall = (EX_CallMath)originalComparison.AssignmentExpression;
    var subtractCall = (EX_CallMath)((EX_Let)graph.At(241).Expression).Expression;
    var currentHp = subtractCall.Parameters[1];

    var aliveTest = new EX_LetBool
    {
        VariableExpression = originalComparison.VariableExpression,
        AssignmentExpression = new EX_CallMath
        {
            StackNode = comparisonCall.StackNode,
            Parameters = new KismetExpression[]
            {
                currentHp,
                new EX_DoubleConst { Value = 0d }
            }
        }
    };

    var killJump = new EX_JumpIfNot
    {
        BooleanExpression = originalComparison.VariableExpression
    };

    var hitCallStatement = graph.At(324);
    var hitCall = (EX_LocalVirtualFunction)hitCallStatement.Expression;
    hitCall.Parameters[1] = new EX_StringConst { Value = "Hit" };

    var skipKill = new EX_Jump();
    var killCall = new EX_LocalVirtualFunction
    {
        VirtualFunctionName = hitCall.VirtualFunctionName,
        Parameters = new KismetExpression[]
        {
            hitCall.Parameters[0],
            new EX_StringConst { Value = "Kill" }
        }
    };

    graph.InsertBefore(hitCallStatement, new ScriptStatement(null, "alive_test", aliveTest));
    graph.InsertBefore(hitCallStatement, new ScriptStatement(null, "kill_jump", killJump));
    graph.InsertAfter(hitCallStatement, new ScriptStatement(null, "skip_kill", skipKill));
    graph.InsertBefore(graph.At(356), new ScriptStatement(null, "kill_call", killCall));
    graph.PendingTargets[killJump] = "kill_call";
    graph.PendingTargets[skipKill] = "old:356";

    var clearPending = new EX_Let
    {
        Value = Property(asset, "Rel", FPackageIndex.FromExport(0)),
        Variable = Instance(asset, "Rel", FPackageIndex.FromExport(0)),
        Expression = new EX_StringConst { Value = "Enemy" }
    };
    graph.InsertAfter(graph.At(422), new ScriptStatement(null, "clear_pending", clearPending));

    var uidCheck = new EX_JumpIfNot
    {
        BooleanExpression = new EX_CallMath
        {
            StackNode = equalGuid,
            Parameters = new KismetExpression[]
            {
                new EX_StructMemberContext
                {
                    StructMemberExpression = Property(asset, "DamageDealerUID", commonHitArgs),
                    StructExpression = Local(asset, "K2Node_CustomEvent_Common", graph.ExportIndex)
                },
                new EX_CallMath
                {
                    StackNode = getGuid,
                    Parameters = new KismetExpression[]
                    {
                        new EX_CallMath
                        {
                            StackNode = getPlayerCharacter,
                            Parameters = new KismetExpression[] { new EX_Self(), new EX_IntConst { Value = 0 } }
                        }
                    }
                }
            }
        }
    };
    var hostilityCheck = new EX_JumpIfNot
    {
        BooleanExpression = new EX_CallMath
        {
            StackNode = booleanOr,
            Parameters = new KismetExpression[]
            {
                new EX_CallMath
                {
                    StackNode = FPackageIndex.FromRawIndex(-24),
                    Parameters = new KismetExpression[]
                    {
                        Instance(asset, "Rel", FPackageIndex.FromExport(0)),
                        new EX_StringConst { Value = "NA" }
                    }
                },
                new EX_CallMath
                {
                    StackNode = equalObject,
                    Parameters = new KismetExpression[]
                    {
                        Instance(asset, "refAgentOwner", FPackageIndex.FromExport(0)),
                        new EX_CallMath
                        {
                            StackNode = getFocusedEnemy,
                            Parameters = new KismetExpression[] { new EX_Self() }
                        }
                    }
                }
            }
        }
    };
    var setPending = new EX_Let
    {
        Value = Property(asset, "Rel", FPackageIndex.FromExport(0)),
        Variable = Instance(asset, "Rel", FPackageIndex.FromExport(0)),
        Expression = new EX_StringConst { Value = "Pending" }
    };
    var latentTarget = new EX_SkipOffsetConst();
    var confirmationDelay = new EX_CallMath
    {
        StackNode = delayFunction,
        Parameters = new KismetExpression[]
        {
            new EX_Self(),
            new EX_FloatConst { Value = 0.2f },
            new EX_StructConst
            {
                Struct = latentInfo,
                StructSize = 32,
                Value = new KismetExpression[]
                {
                    latentTarget,
                    new EX_IntConst { Value = 734210 },
                    new EX_NameConst { Value = new FName(asset, "ExecuteUbergraph_bpac_dmgWidgetSpawner") },
                    new EX_Self()
                }
            }
        }
    };
    var eventReturn = new EX_Return { ReturnExpression = new EX_Nothing() };
    var timeoutComparison = new EX_LetBool
    {
        VariableExpression = Local(asset, "CallFunc_NotEqual_StrStr_ReturnValue", graph.ExportIndex),
        AssignmentExpression = new EX_CallMath
        {
            StackNode = FPackageIndex.FromRawIndex(-24),
            Parameters = new KismetExpression[]
            {
                Instance(asset, "Rel", FPackageIndex.FromExport(0)),
                new EX_StringConst { Value = "Pending" }
            }
        }
    };
    var timeoutClear = new EX_JumpIfNot
    {
        BooleanExpression = Local(asset, "CallFunc_NotEqual_StrStr_ReturnValue", graph.ExportIndex)
    };
    var timeoutReturn = new EX_Jump();
    var finalReturn = graph.At(459);
    if (diagnostics)
    {
        graph.InsertBefore(finalReturn, new ScriptStatement(null, "bullet_event",
            PrintLog(asset, "[HitMarkers][Diagnostic] BulletProjectileHit callback received")));
        graph.InsertBefore(finalReturn, new ScriptStatement(null, "hostility_check", hostilityCheck));
    }
    else
    {
        graph.InsertBefore(finalReturn, new ScriptStatement(null, "bullet_event", hostilityCheck));
    }
    graph.InsertBefore(finalReturn, new ScriptStatement(null, "uid_check", uidCheck));
    graph.InsertBefore(finalReturn, new ScriptStatement(null, "set_pending", setPending));
    graph.InsertBefore(finalReturn, new ScriptStatement(null, "confirmation_delay", confirmationDelay));
    graph.InsertBefore(finalReturn, new ScriptStatement(null, "event_return", eventReturn));
    graph.InsertBefore(finalReturn, new ScriptStatement(null, "pending_timeout", timeoutComparison));
    graph.InsertBefore(finalReturn, new ScriptStatement(null, "timeout_clear", timeoutClear));
    graph.InsertBefore(finalReturn, new ScriptStatement(null, "timeout_return", timeoutReturn));
    graph.PendingTargets[hostilityCheck] = "old:459";
    graph.PendingTargets[uidCheck] = "old:459";
    graph.PendingTargets[latentTarget] = "pending_timeout";
    graph.PendingTargets[timeoutClear] = "old:356";
    graph.PendingTargets[timeoutReturn] = "old:459";
    if (diagnostics)
    {
        graph.InsertBefore(hitCallStatement, new ScriptStatement(null, "confirmed_hit_log",
            PrintLog(asset, "[HitMarkers][Diagnostic] confirmed enemy hit; displaying white marker")));
        var killStatement = graph.Statements.Single(item => item.Tag == "kill_call");
        graph.InsertBefore(killStatement, new ScriptStatement(null, "confirmed_kill_log",
            PrintLog(asset, "[HitMarkers][Diagnostic] confirmed enemy kill; displaying red marker")));
        graph.PendingTargets[killJump] = "confirmed_kill_log";
    }

    var spawnerMap = graph.FinalizeScript();
    UpdateEntrypoint(asset, "ReceiveTick", tickTarget, spawnerMap[tickTarget]);

    var handler = FunctionScript.Parse(asset, "setAgentOwner");
    var oldHandlerName = SerializeName(asset, "setAgentOwner");
    var newHandlerName = SerializeName(asset, "OnBulletProjectileHit");
    var classExport = asset.Exports.OfType<RawExport>()
        .Single(item => item.ObjectName.Value.Value == "bpac_dmgWidgetSpawner_C");
    ReplaceSingle(classExport.Data, oldHandlerName, newHandlerName);
    handler.Rename("OnBulletProjectileHit");
    handler.Properties.Clear();
    var parameterFlags = EPropertyFlags.CPF_BlueprintVisible |
        EPropertyFlags.CPF_BlueprintReadOnly | EPropertyFlags.CPF_Parm;
    handler.Properties.Add(StructProperty(asset, "Common", 336, commonHitArgs, parameterFlags));
    handler.Properties.Add(StructProperty(asset, "HitArgs", 12, bulletHitArgs, parameterFlags));
    handler.Statements.Clear();
    handler.Statements.Add(new ScriptStatement(null, "store_common", new EX_LetValueOnPersistentFrame
    {
        DestinationProperty = Property(asset, "K2Node_CustomEvent_Common", graph.ExportIndex),
        AssignmentExpression = Local(asset, "Common", handler.ExportIndex)
    }));
    handler.Statements.Add(new ScriptStatement(null, "store_hit_args", new EX_LetValueOnPersistentFrame
    {
        DestinationProperty = Property(asset, "K2Node_CustomEvent_HitArgs", graph.ExportIndex),
        AssignmentExpression = Local(asset, "HitArgs", handler.ExportIndex)
    }));
    handler.Statements.Add(new ScriptStatement(null, "invoke_event", new EX_LocalFinalFunction
    {
        StackNode = graph.ExportIndex,
        Parameters = new KismetExpression[] { new EX_IntConst { Value = checked((int)graph.OffsetOf("bullet_event")) } }
    }));
    handler.Statements.Add(new ScriptStatement(null, "handler_return", new EX_Return { ReturnExpression = new EX_Nothing() }));
    handler.Statements.Add(new ScriptStatement(null, "handler_end", new EX_EndOfScript()));
    handler.Rebuild();

    asset.Write(path);
}

static void PatchBootstrapRunner(string path)
{
    var asset = LoadAsset(path);
    var beginPlayTarget = ReadEntrypoint(asset, "ReceiveBeginPlay");
    var tickTarget = ReadEntrypoint(asset, "ReceiveTick");
    var graph = FunctionScript.Parse(asset, "ExecuteUbergraph_BP_Run_ModActor");

    const string canaryPropertyName = "HitMarkers_CanaryShown";
    graph.Properties.Add(BoolProperty(asset, canaryPropertyName));

    var gameplayStatics = EnsureClass(asset, "/Script/Engine", "GameplayStatics");
    var systemLibrary = EnsureClass(asset, "/Script/Engine", "KismetSystemLibrary");
    var mathLibrary = EnsureClass(asset, "/Script/Engine", "KismetMathLibrary");
    var getPlayerController = EnsureObject(asset, gameplayStatics, "GetPlayerController");
    var getPlayerCharacter = EnsureObject(asset, gameplayStatics, "GetPlayerCharacter");
    var isValid = EnsureObject(asset, systemLibrary, "IsValid");
    var booleanAnd = EnsureObject(asset, mathLibrary, "BooleanAND");
    var booleanNot = EnsureObject(asset, mathLibrary, "Not_PreBool");

    KismetExpression Controller() => new EX_CallMath
    {
        StackNode = getPlayerController,
        Parameters = new KismetExpression[] { new EX_Self(), new EX_IntConst { Value = 0 } }
    };

    KismetExpression ControllerValue(string functionName) => new EX_Context
    {
        ObjectExpression = Controller(),
        RValuePointer = EmptyProperty(),
        ContextExpression = new EX_VirtualFunction
        {
            VirtualFunctionName = new FName(asset, functionName),
            Parameters = Array.Empty<KismetExpression>()
        }
    };

    KismetExpression Valid(KismetExpression value) => new EX_CallMath
    {
        StackNode = isValid,
        Parameters = new[] { value }
    };

    KismetExpression And(KismetExpression left, KismetExpression right) => new EX_CallMath
    {
        StackNode = booleanAnd,
        Parameters = new[] { left, right }
    };

    void InsertCanaryAfter(ScriptStatement anchor, string prefix)
    {
        var component = Local(asset, "CallFunc_AddComponent_ReturnValue", graph.ExportIndex);
        var notShown = new EX_CallMath
        {
            StackNode = booleanNot,
            Parameters = new KismetExpression[] { Local(asset, canaryPropertyName, graph.ExportIndex) }
        };
        var skipShown = new EX_JumpIfNot { BooleanExpression = notShown };
        var readiness = And(
            Valid(new EX_CallMath
            {
                StackNode = getPlayerCharacter,
                Parameters = new KismetExpression[] { new EX_Self(), new EX_IntConst { Value = 0 } }
            }),
            And(Valid(Controller()), And(Valid(ControllerValue("GetLocalPlayer")),
                And(Valid(ControllerValue("GetHUD")), Valid(component)))));
        var skipNotReady = new EX_JumpIfNot { BooleanExpression = readiness };

        var additions = new List<ScriptStatement>
        {
            new(null, $"{prefix}_canary_not_shown", skipShown),
            new(null, $"{prefix}_canary_ready", skipNotReady),
            new(null, $"{prefix}_readiness_log",
                PrintLog(asset, "[HitMarkers][Diagnostic] controller, local player, HUD, and Agent ready")),
            new(null, $"{prefix}_canary_request_log",
                PrintLog(asset, "[HitMarkers][Diagnostic] startup white canary requested")),
            new(null, $"{prefix}_canary_display", new EX_Context
            {
                ObjectExpression = component,
                RValuePointer = EmptyProperty(),
                ContextExpression = new EX_LocalVirtualFunction
                {
                    VirtualFunctionName = new FName(asset, "showDamage"),
                    Parameters = new KismetExpression[]
                    {
                        new EX_IntConst { Value = 1 }, new EX_StringConst { Value = "Hit" }
                    }
                }
            }),
            new(null, $"{prefix}_canary_marked", new EX_LetBool
            {
                VariableExpression = Local(asset, canaryPropertyName, graph.ExportIndex),
                AssignmentExpression = new EX_True()
            }),
            new(null, $"{prefix}_after_canary", new EX_Nothing())
        };
        graph.Statements.InsertRange(graph.Statements.IndexOf(anchor) + 1, additions);
        graph.PendingTargets[skipShown] = $"{prefix}_after_canary";
        graph.PendingTargets[skipNotReady] = $"{prefix}_after_canary";
    }

    var beginPlayAnchor = graph.At(beginPlayTarget);
    graph.InsertBefore(beginPlayAnchor, new ScriptStatement(null, "bootstrap_begin_play_log",
        PrintLog(asset, "[HitMarkers][Diagnostic] runner BeginPlay reached")));
    InsertCanaryAfter(graph.At(1367), "enemy");
    InsertCanaryAfter(graph.At(1479), "neutral");

    var map = graph.FinalizeScript();
    UpdateEntrypoint(asset, "ReceiveBeginPlay", beginPlayTarget, graph.OffsetOf("bootstrap_begin_play_log"));
    UpdateEntrypoint(asset, "ReceiveTick", tickTarget, map[tickTarget]);
    asset.Write(path);
}

static void PatchRunner(string path, bool diagnostics)
{
    var asset = LoadAsset(path);
    var beginPlayTarget = ReadEntrypoint(asset, "ReceiveBeginPlay");
    var tickTarget = ReadEntrypoint(asset, "ReceiveTick");
    var graph = FunctionScript.Parse(asset, "ExecuteUbergraph_BP_Run_ModActor");

    var stalkerPackage = EnsurePackage(asset, "/Script/Stalker2");
    var enginePackage = EnsurePackage(asset, "/Script/Engine");
    var hittableClass = EnsureClass(asset, "/Script/Stalker2", "HittableComponent");
    var cppMediator = EnsureClass(asset, "/Script/Stalker2", "CppMediator");
    var mathLibrary = EnsureClass(asset, "/Script/Engine", "KismetMathLibrary");
    var systemLibrary = EnsureClass(asset, "/Script/Engine", "KismetSystemLibrary");
    var bulletSignature = EnsureObject(asset, stalkerPackage, "BulletProjectileHitSignature__DelegateSignature");
    var objGetHp = EnsureObject(asset, cppMediator, "ObjGetHP");
    var getFocusedEnemy = EnsureObject(asset, cppMediator, "GetFocusedEnemy");
    var booleanOr = EnsureObject(asset, mathLibrary, "BooleanOR");
    var equalObject = EnsureObject(asset, mathLibrary, "EqualEqual_ObjectObject");
    var equalInt = EnsureObject(asset, mathLibrary, "EqualEqual_IntInt");
    var delayFunction = EnsureObject(asset, systemLibrary, "Delay");
    var printString = EnsureObject(asset, systemLibrary, "PrintString");
    var setBroadcastHitPending = EnsureObject(asset, hittableClass, "SetBroadcastHitPending");
    var latentInfo = EnsureObject(asset, enginePackage, "LatentActionInfo");
    var linearColor = EnsureObject(asset, EnsurePackage(asset, "/Script/CoreUObject"), "LinearColor");
    var agentClass = FPackageIndex.FromImport(asset.Imports.FindIndex(item =>
        item.ClassName.Value.Value == "Class" && item.ObjectName.Value.Value == "Agent"));
    var spawnerClass = FPackageIndex.FromImport(asset.Imports.FindIndex(item =>
        item.ClassName.Value.Value == "BlueprintGeneratedClass" &&
        item.ObjectName.Value.Value == "bpac_dmgWidgetSpawner_C"));

    graph.Properties.Add(DelegateProperty(asset, "K2Node_CreateDelegate_OutputDelegate", bulletSignature));

    var focusedComparison = new EX_CallMath
    {
        StackNode = equalObject,
        Parameters = new KismetExpression[]
        {
            Local(asset, "CallFunc_Array_Get_Item", graph.ExportIndex),
            new EX_CallMath
            {
                StackNode = getFocusedEnemy,
                Parameters = new KismetExpression[] { new EX_Self() }
            }
        }
    };
    ((EX_JumpIfNot)graph.At(1294).Expression).BooleanExpression = new EX_CallMath
    {
        StackNode = booleanOr,
        Parameters = new KismetExpression[]
        {
            Local(asset, "CallFunc_func__IsEnemy_bEnemy", graph.ExportIndex),
            focusedComparison
        }
    };

    void ReplaceInitialization(ScriptStatement anchor, string relation, string prefix)
    {
        var component = Local(asset, "CallFunc_AddComponent_ReturnValue", graph.ExportIndex);
        var agent = Local(asset, "CallFunc_Array_Get_Item", graph.ExportIndex);

        EX_Context ComponentField(string name) => new()
        {
            ObjectExpression = component,
            RValuePointer = Property(asset, name, spawnerClass),
            ContextExpression = Instance(asset, name, spawnerClass)
        };

        anchor.Expression = new EX_LetObj
        {
            VariableExpression = ComponentField("refAgentOwner"),
            AssignmentExpression = agent
        };

        var setRelation = new ScriptStatement(null, $"{prefix}_relation", new EX_Let
        {
            Value = Property(asset, "Rel", spawnerClass),
            Variable = ComponentField("Rel"),
            Expression = new EX_StringConst { Value = relation }
        });
        var setHealth = new ScriptStatement(null, $"{prefix}_health", new EX_Let
        {
            Value = Property(asset, "OwnerHP", spawnerClass),
            Variable = ComponentField("OwnerHP"),
            Expression = new EX_PrimitiveCast
            {
                ConversionType = (ECastToken)4,
                Target = new EX_CallMath
                {
                    StackNode = objGetHp,
                    Parameters = new KismetExpression[] { agent }
                }
            }
        });
        var bind = new ScriptStatement(null, $"{prefix}_bind", new EX_BindDelegate
        {
            FunctionName = new FName(asset, "OnBulletProjectileHit"),
            Delegate = Local(asset, "K2Node_CreateDelegate_OutputDelegate", graph.ExportIndex),
            ObjectTerm = component
        });
        var hittable = new EX_Context
        {
            ObjectExpression = agent,
            RValuePointer = Property(asset, "HittableComponent", agentClass),
            ContextExpression = Instance(asset, "HittableComponent", agentClass)
        };
        var addDelegate = new ScriptStatement(null, $"{prefix}_add_delegate", new EX_AddMulticastDelegate
        {
            Delegate = new EX_Context
            {
                ObjectExpression = hittable,
                RValuePointer = Property(asset, "BulletProjectileHit", hittableClass),
                ContextExpression = Instance(asset, "BulletProjectileHit", hittableClass)
            },
            DelegateToAdd = Local(asset, "K2Node_CreateDelegate_OutputDelegate", graph.ExportIndex)
        });
        var enableBroadcast = new ScriptStatement(null, $"{prefix}_enable_broadcast", new EX_Context
        {
            ObjectExpression = hittable,
            RValuePointer = EmptyProperty(),
            ContextExpression = new EX_FinalFunction
            {
                StackNode = setBroadcastHitPending,
                Parameters = new KismetExpression[] { new EX_True() }
            }
        });

        var additions = new List<ScriptStatement> { setRelation, setHealth, enableBroadcast, bind, addDelegate };
        if (diagnostics)
        {
            var skipCanary = new EX_JumpIfNot
            {
                BooleanExpression = new EX_CallMath
                {
                    StackNode = equalInt,
                    Parameters = new KismetExpression[]
                    {
                        Local(asset, "Temp_int_Loop_Counter_Variable", graph.ExportIndex),
                        new EX_IntConst { Value = 0 }
                    }
                }
            };
            additions.Add(new ScriptStatement(null, $"{prefix}_canary_check", skipCanary));
            additions.Add(new ScriptStatement(null, $"{prefix}_canary", new EX_Context
            {
                ObjectExpression = component,
                RValuePointer = EmptyProperty(),
                ContextExpression = new EX_LocalVirtualFunction
                {
                    VirtualFunctionName = new FName(asset, "showDamage"),
                    Parameters = new KismetExpression[]
                    {
                        new EX_IntConst { Value = 1 }, new EX_StringConst { Value = "Hit" }
                    }
                }
            }));
            additions.Add(new ScriptStatement(null, $"{prefix}_after_canary", new EX_Nothing()));
            graph.PendingTargets[skipCanary] = $"{prefix}_after_canary";
        }

        var insertion = graph.Statements.IndexOf(anchor) + 1;
        graph.Statements.InsertRange(insertion, additions);
    }

    ReplaceInitialization(graph.At(1367), "Enemy", "enemy");
    ReplaceInitialization(graph.At(1479), "NA", "neutral");

    graph.At(397).Expression = new EX_Nothing();
    graph.At(1003).Expression = new EX_Nothing();

    var initLog = new EX_CallMath
    {
        StackNode = printString,
        Parameters = new KismetExpression[]
        {
            new EX_Self(),
            new EX_StringConst { Value = diagnostics
                ? "[HitMarkers][Diagnostic] initialization, HUD wait, and delegate scan started"
                : "[HitMarkers] initialized; BulletProjectileHit broadcasting enabled" },
            new EX_False(),
            new EX_True(),
            new EX_StructConst
            {
                Struct = linearColor,
                StructSize = 16,
                Value = new KismetExpression[]
                {
                    new EX_FloatConst { Value = 1f }, new EX_FloatConst { Value = 1f },
                    new EX_FloatConst { Value = 1f }, new EX_FloatConst { Value = 1f }
                }
            },
            new EX_FloatConst { Value = 1f },
            new EX_NameConst { Value = new FName(asset, "None") }
        }
    };
    graph.InsertBefore(graph.At(1531), new ScriptStatement(null, "init_log", initLog));

    var scanTarget = new EX_SkipOffsetConst();
    var scanDelay = new EX_CallMath
    {
        StackNode = delayFunction,
        Parameters = new KismetExpression[]
        {
            new EX_Self(),
            new EX_FloatConst { Value = 0.5f },
            new EX_StructConst
            {
                Struct = latentInfo,
                StructSize = 32,
                Value = new KismetExpression[]
                {
                    scanTarget,
                    new EX_IntConst { Value = 734211 },
                    new EX_NameConst { Value = new FName(asset, "ExecuteUbergraph_BP_Run_ModActor") },
                    new EX_Self()
                }
            }
        }
    };
    graph.InsertBefore(graph.At(1623), new ScriptStatement(null, "scan_delay", scanDelay));
    graph.PendingTargets[(EX_PushExecutionFlow)graph.At(0).Expression] = "scan_delay";
    graph.PendingTargets[scanTarget] = "old:1531";

    var runnerMap = graph.FinalizeScript();
    var terminalReturn = graph.Statements.Last(statement => statement.Expression is EX_Return).OriginalOffset
        ?? throw new InvalidDataException("Runner terminal return has no original offset.");
    UpdateEntrypoint(asset, "ReceiveBeginPlay", beginPlayTarget, graph.OffsetOf("init_log"));
    UpdateEntrypoint(asset, "ReceiveTick", tickTarget, runnerMap[terminalReturn]);
    asset.Write(path);
}

static int ReadEntrypoint(UAsset asset, string functionName)
{
    var function = FunctionScript.Parse(asset, functionName);
    var targets = Flatten(function, asset).OfType<EX_IntConst>().Select(value => value.Value).Distinct().ToArray();
    if (targets.Length != 1)
    {
        throw new InvalidDataException($"Expected one {functionName} ubergraph entrypoint, found {targets.Length}.");
    }
    return targets[0];
}

static void UpdateEntrypoint(UAsset asset, string functionName, int oldTarget, uint newTarget)
{
    var function = FunctionScript.Parse(asset, functionName);
    var replacements = 0;
    foreach (var statement in function.Statements)
    {
        Visit(statement.Expression, asset, expression =>
        {
            if (expression is EX_IntConst value && value.Value == oldTarget)
            {
                value.Value = checked((int)newTarget);
                replacements++;
            }
        });
    }

    if (replacements != 1)
    {
        throw new InvalidDataException($"Expected one {functionName} entrypoint {oldTarget}, found {replacements}.");
    }

    function.Rebuild();
}

static void SetLinearColor(KismetExpression expression, UAsset asset, params float[] rgba)
{
    var values = new List<EX_FloatConst>();
    Visit(expression, asset, node =>
    {
        if (node is EX_FloatConst value)
        {
            values.Add(value);
        }
    });

    if (values.Count != rgba.Length)
    {
        throw new InvalidDataException($"Expected {rgba.Length} color channels, found {values.Count}.");
    }

    for (var index = 0; index < rgba.Length; index++)
    {
        values[index].Value = rgba[index];
    }
}

static EX_CallMath PrintLog(UAsset asset, string message)
{
    var systemLibrary = EnsureClass(asset, "/Script/Engine", "KismetSystemLibrary");
    var printString = EnsureObject(asset, systemLibrary, "PrintString");
    var color = EnsureObject(asset, EnsurePackage(asset, "/Script/CoreUObject"), "LinearColor");
    return new EX_CallMath
    {
        StackNode = printString,
        Parameters = new KismetExpression[]
        {
            new EX_Self(), new EX_StringConst { Value = message }, new EX_False(), new EX_True(),
            new EX_StructConst
            {
                Struct = color, StructSize = 16,
                Value = new KismetExpression[]
                {
                    new EX_FloatConst { Value = 1f }, new EX_FloatConst { Value = 1f },
                    new EX_FloatConst { Value = 1f }, new EX_FloatConst { Value = 1f }
                }
            },
            new EX_FloatConst { Value = 1f }, new EX_NameConst { Value = new FName(asset, "None") }
        }
    };
}

static void Visit(KismetExpression expression, UAsset asset, Action<KismetExpression> visitor)
{
    uint offset = 0;
    expression.Visit(asset, ref offset, (node, _) => visitor(node));
}

static void ReplaceSingle(byte[] data, byte[] before, byte[] after)
{
    if (before.Length != after.Length)
    {
        throw new ArgumentException("Binary replacement lengths must match.");
    }

    var match = -1;
    for (var index = 0; index <= data.Length - before.Length; index++)
    {
        if (!data.AsSpan(index, before.Length).SequenceEqual(before))
        {
            continue;
        }

        if (match >= 0)
        {
            throw new InvalidDataException("Binary replacement pattern is not unique.");
        }

        match = index;
    }

    if (match < 0)
    {
        throw new InvalidDataException("Binary replacement pattern was not found.");
    }

    after.CopyTo(data, match);
}

static void VerifyPatchedAssets(string root)
{
    var modRoot = Path.Combine(root, "Stalker2", "Content", "Mods", "ShowDMG");
    var runnerAsset = LoadAsset(Path.Combine(modRoot, "BP_Run_ModActor.uasset"));
    var runner = FunctionScript.Parse(runnerAsset, "ExecuteUbergraph_BP_Run_ModActor");
    var runnerNodes = Flatten(runner, runnerAsset);
    Require(runner.Properties.OfType<FDelegateProperty>().Any(item =>
        item.Name.Value.Value == "K2Node_CreateDelegate_OutputDelegate"), "typed delegate local");
    Require(runnerNodes.OfType<EX_BindDelegate>().Count(item =>
        item.FunctionName.Value.Value == "OnBulletProjectileHit") == 2, "delegate bindings");
    Require(runnerNodes.OfType<EX_AddMulticastDelegate>().Count() == 2, "multicast delegate additions");
    Require(runnerNodes.OfType<EX_FinalFunction>().Count(item =>
        StackName(item.StackNode, runnerAsset) == "SetBroadcastHitPending") == 2,
        "native hit broadcast enablement");
    Require(runnerNodes.OfType<EX_FloatConst>().Any(item => Math.Abs(item.Value - 0.5f) < 0.001f),
        "0.5-second agent scan");
    Require(runnerNodes.OfType<EX_StringConst>().Any(item =>
        item.Value.StartsWith("[HitMarkers]", StringComparison.Ordinal)), "log-only initialization message");
    Require(!runnerNodes.OfType<EX_CallMath>().Any(item => StackName(item.StackNode, runnerAsset) == "PrintText"),
        "on-screen debug removal");

    var spawnerAsset = LoadAsset(Path.Combine(modRoot, "bpac_dmgWidgetSpawner.uasset"));
    var spawner = FunctionScript.Parse(spawnerAsset, "ExecuteUbergraph_bpac_dmgWidgetSpawner");
    var spawnerNodes = Flatten(spawner, spawnerAsset);
    var handler = FunctionScript.Parse(spawnerAsset, "OnBulletProjectileHit");
    Require(handler.Properties.OfType<FStructProperty>().Any(item =>
        item.Name.Value.Value == "Common" && item.ElementSize == 336), "CommonHitArgs handler parameter");
    Require(handler.Properties.OfType<FStructProperty>().Any(item =>
        item.Name.Value.Value == "HitArgs" && item.ElementSize == 12), "BulletProjectileHitArgs handler parameter");
    Require(spawnerNodes.OfType<EX_StructMemberContext>().Any(item =>
        item.StructMemberExpression.New.Path.Any(name => name.Value.Value == "DamageDealerUID")),
        "DamageDealerUID player filter");
    foreach (var functionName in new[] { "GetGUID", "GetPlayerCharacter", "EqualEqual_GuidGuid", "GetFocusedEnemy" })
    {
        Require(spawnerNodes.OfType<EX_CallMath>().Any(item => StackName(item.StackNode, spawnerAsset) == functionName),
            functionName);
    }
    Require(spawnerNodes.OfType<EX_StringConst>().Any(item => item.Value == "Hit"), "white hit path");
    Require(spawnerNodes.OfType<EX_StringConst>().Any(item => item.Value == "Kill"), "red kill path");
    Require(spawnerNodes.OfType<EX_FloatConst>().Any(item => Math.Abs(item.Value - 0.2f) < 0.001f),
        "0.2-second confirmation window");

    var showDamage = FunctionScript.Parse(spawnerAsset, "showDamage");
    var finishIndex = showDamage.Statements.FindIndex(statement =>
        FlattenExpression(statement.Expression, spawnerAsset).OfType<EX_CallMath>()
            .Any(item => StackName(item.StackNode, spawnerAsset) == "FinishSpawningActor"));
    var initializationIndices = showDamage.Statements
        .Select((statement, index) => (statement.Expression, index))
        .Where(item => item.Expression is EX_Let let && let.Value.New is not null &&
            let.Value.New.Path.Any(name => name.Value.Value is "dmg" or "rel"))
        .Select(item => item.index).ToArray();
    Require(finishIndex >= 0 && initializationIndices.Length == 2 &&
        initializationIndices.All(index => index < finishIndex),
        "pre-FinishSpawning marker initialization");

    var holderAsset = LoadAsset(Path.Combine(modRoot, "bp_dmgActorHolder.uasset"));
    var holder = FunctionScript.Parse(holderAsset, "ExecuteUbergraph_bp_dmgActorHolder");
    var holderNodes = Flatten(holder, holderAsset);
    var playerScreenCalls = holderNodes.OfType<EX_VirtualFunction>()
        .Where(item => item.VirtualFunctionName.Value.Value == "AddToPlayerScreen").ToArray();
    Require(playerScreenCalls.Any(item => item.Parameters.OfType<EX_IntConst>().Any(value => value.Value == 10000)),
        "hit marker Z-order");
    Require(playerScreenCalls.Any(item => item.Parameters.OfType<EX_IntConst>().Any(value => value.Value == 10001)),
        "kill marker Z-order");
    Require(holderNodes.OfType<EX_CallMath>().Any(item => StackName(item.StackNode, holderAsset) == "GetPlayerController"),
        "owning player widget creation");
    Require(holderNodes.OfType<EX_FloatConst>().Any(item => Math.Abs(item.Value - 1.1f) < 0.001f),
        "fade cleanup timing");

    var areaAsset = LoadAsset(Path.Combine(modRoot, "wbp_ShowDMGArea.uasset"));
    var area = FunctionScript.Parse(areaAsset, "showDamage");
    var areaNodes = Flatten(area, areaAsset);
    Require(areaNodes.OfType<EX_CallMath>().Any(item => StackName(item.StackNode, areaAsset) == "GetPlayerController"),
        "marker widget owning player");
    Require(areaNodes.OfType<EX_VirtualFunction>().Any(item => item.VirtualFunctionName.Value.Value == "SetVisibility"),
        "marker visibility initialization");
    Require(areaNodes.OfType<EX_VirtualFunction>().Any(item => item.VirtualFunctionName.Value.Value == "SetRenderOpacity"),
        "marker opacity initialization");
}

static void VerifyBootstrapAssets(string root)
{
    var modRoot = Path.Combine(root, "Stalker2", "Content", "Mods", "ShowDMG");
    var runnerAsset = LoadAsset(Path.Combine(modRoot, "BP_Run_ModActor.uasset"));
    var runner = FunctionScript.Parse(runnerAsset, "ExecuteUbergraph_BP_Run_ModActor");
    var runnerNodes = Flatten(runner, runnerAsset);
    Require(runner.Properties.OfType<FBoolProperty>().Count(item =>
        item.Name.Value.Value == "HitMarkers_CanaryShown") == 1, "persistent one-shot canary guard");
    Require(runnerNodes.OfType<EX_StringConst>().Any(item =>
        item.Value == "[HitMarkers][Diagnostic] runner BeginPlay reached"), "derived BeginPlay diagnostic");
    Require(runnerNodes.OfType<EX_StringConst>().Any(item =>
        item.Value == "[HitMarkers][Diagnostic] controller, local player, HUD, and Agent ready"),
        "runtime readiness diagnostic");
    Require(runnerNodes.OfType<EX_LocalVirtualFunction>().Count(item =>
        item.VirtualFunctionName.Value.Value == "showDamage") == 2, "one-shot canary branch coverage");
    Require(!runnerNodes.OfType<EX_BindDelegate>().Any(), "bootstrap mode excludes delegate binding");
    Require(!runnerNodes.OfType<EX_AddMulticastDelegate>().Any(), "bootstrap mode excludes multicast registration");
    Require(!runnerNodes.OfType<EX_FinalFunction>().Any(item =>
        StackName(item.StackNode, runnerAsset) == "SetBroadcastHitPending"),
        "bootstrap mode excludes hit broadcast changes");
    var beginPlayTarget = ReadEntrypoint(runnerAsset, "ReceiveBeginPlay");
    var tickTarget = ReadEntrypoint(runnerAsset, "ReceiveTick");
    Require(runner.Statements.Any(item => runner.OffsetOf(item.Tag) == beginPlayTarget &&
        FlattenExpression(item.Expression, runnerAsset).OfType<EX_StringConst>().Any(value =>
            value.Value == "[HitMarkers][Diagnostic] runner BeginPlay reached")),
        "ReceiveBeginPlay targets bootstrap diagnostic");
    Require(runner.Statements.Any(item => runner.OffsetOf(item.Tag) == tickTarget),
        "ReceiveTick targets valid remapped bytecode");

    var spawnerAsset = LoadAsset(Path.Combine(modRoot, "bpac_dmgWidgetSpawner.uasset"));
    Require(spawnerAsset.Exports.OfType<RawExport>().Any(item => item.ObjectName.Value.Value == "setAgentOwner"),
        "original spawner handler remains present");
    Require(!spawnerAsset.Exports.OfType<RawExport>().Any(item => item.ObjectName.Value.Value == "OnBulletProjectileHit"),
        "bootstrap mode excludes damage callback replacement");
    var showDamage = FunctionScript.Parse(spawnerAsset, "showDamage");
    var finishIndex = showDamage.Statements.FindIndex(statement =>
        FlattenExpression(statement.Expression, spawnerAsset).OfType<EX_CallMath>()
            .Any(item => StackName(item.StackNode, spawnerAsset) == "FinishSpawningActor"));
    var initializationIndices = showDamage.Statements
        .Select((statement, index) => (statement.Expression, index))
        .Where(item => item.Expression is EX_Let let && let.Value.New is not null &&
            let.Value.New.Path.Any(name => name.Value.Value is "dmg" or "rel"))
        .Select(item => item.index).ToArray();
    Require(finishIndex >= 0 && initializationIndices.Length == 2 &&
        initializationIndices.All(index => index < finishIndex),
        "bootstrap pre-FinishSpawning marker initialization");

    var holderAsset = LoadAsset(Path.Combine(modRoot, "bp_dmgActorHolder.uasset"));
    var holder = FunctionScript.Parse(holderAsset, "ExecuteUbergraph_bp_dmgActorHolder");
    var holderNodes = Flatten(holder, holderAsset);
    Require(holderNodes.OfType<EX_VirtualFunction>().Any(item =>
        item.VirtualFunctionName.Value.Value == "AddToPlayerScreen"), "bootstrap viewport insertion");
    foreach (var message in new[]
    {
        "[HitMarkers][Diagnostic] widget created; viewport inserted; visible; fade started",
        "[HitMarkers][Diagnostic] fade completed; widget removed"
    })
    {
        Require(holderNodes.OfType<EX_StringConst>().Any(item => item.Value == message), message);
    }

    var areaAsset = LoadAsset(Path.Combine(modRoot, "wbp_ShowDMGArea.uasset"));
    var area = FunctionScript.Parse(areaAsset, "showDamage");
    var areaNodes = Flatten(area, areaAsset);
    Require(areaNodes.OfType<EX_CallMath>().Any(item => StackName(item.StackNode, areaAsset) == "GetPlayerController"),
        "bootstrap owning-player marker creation");
    Require(areaNodes.OfType<EX_VirtualFunction>().Any(item => item.VirtualFunctionName.Value.Value == "SetVisibility"),
        "bootstrap marker visibility");
    Require(areaNodes.OfType<EX_VirtualFunction>().Any(item => item.VirtualFunctionName.Value.Value == "SetRenderOpacity"),
        "bootstrap marker opacity");
}

static List<KismetExpression> Flatten(FunctionScript function, UAsset asset)
{
    var result = new List<KismetExpression>();
    foreach (var statement in function.Statements)
    {
        Visit(statement.Expression, asset, result.Add);
    }
    return result;
}

static List<KismetExpression> FlattenExpression(KismetExpression expression, UAsset asset)
{
    var result = new List<KismetExpression>();
    Visit(expression, asset, result.Add);
    return result;
}

static string StackName(FPackageIndex index, UAsset asset)
{
    if (index.IsImport()) return index.ToImport(asset).ObjectName.Value.Value;
    if (index.IsExport()) return index.ToExport(asset).ObjectName.Value.Value;
    return string.Empty;
}

static void Require(bool condition, string postcondition)
{
    if (!condition)
    {
        throw new InvalidDataException($"HitMarkers patch postcondition failed: {postcondition}.");
    }
}

static void CopyDirectory(string source, string destination)
{
    Directory.CreateDirectory(destination);
    foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
    {
        Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
    }

    foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
    {
        var target = Path.Combine(destination, Path.GetRelativePath(source, file));
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(file, target, overwrite: true);
    }
}

static FPackageIndex EnsurePackage(UAsset asset, string packageName)
{
    var existing = asset.Imports.FindIndex(item =>
        item.ClassName.Value.Value == "Package" && item.ObjectName.Value.Value == packageName);
    if (existing >= 0)
    {
        return FPackageIndex.FromImport(existing);
    }

    var import = new Import("/Script/CoreUObject", "Package", FPackageIndex.FromRawIndex(0), packageName, false, asset)
    {
        PackageName = new FName(asset, "None")
    };
    asset.Imports.Add(import);
    return FPackageIndex.FromImport(asset.Imports.Count - 1);
}

static FPackageIndex EnsureClass(UAsset asset, string packageName, string className)
{
    var package = EnsurePackage(asset, packageName);
    var existing = asset.Imports.FindIndex(item =>
        item.ClassName.Value.Value == "Class" && item.ObjectName.Value.Value == className &&
        item.OuterIndex.Index == package.Index);
    if (existing >= 0)
    {
        return FPackageIndex.FromImport(existing);
    }

    var import = new Import("/Script/CoreUObject", "Class", package, className, false, asset)
    {
        PackageName = new FName(asset, "None")
    };
    asset.Imports.Add(import);
    return FPackageIndex.FromImport(asset.Imports.Count - 1);
}

static FPackageIndex EnsureObject(UAsset asset, FPackageIndex owner, string objectName)
{
    var existing = asset.Imports.FindIndex(item =>
        item.ClassName.Value.Value == "Object" && item.ObjectName.Value.Value == objectName &&
        item.OuterIndex.Index == owner.Index);
    if (existing >= 0)
    {
        return FPackageIndex.FromImport(existing);
    }

    var import = new Import("/Script/CoreUObject", "Object", owner, objectName, false, asset)
    {
        PackageName = new FName(asset, "None")
    };
    asset.Imports.Add(import);
    return FPackageIndex.FromImport(asset.Imports.Count - 1);
}

static KismetPropertyPointer Property(UAsset asset, string name, FPackageIndex owner) =>
    new(new FFieldPath(new[] { new FName(asset, name) }, owner));

static KismetPropertyPointer EmptyProperty() =>
    new(new FFieldPath(Array.Empty<FName>(), FPackageIndex.FromRawIndex(0)));

static EX_LocalVariable Local(UAsset asset, string name, FPackageIndex owner) =>
    new() { Variable = Property(asset, name, owner) };

static EX_InstanceVariable Instance(UAsset asset, string name, FPackageIndex owner) =>
    new() { Variable = Property(asset, name, owner) };

static FStructProperty StructProperty(
    UAsset asset,
    string name,
    int size,
    FPackageIndex structType,
    EPropertyFlags flags)
{
    return new FStructProperty
    {
        SerializedType = new FName(asset, "StructProperty"),
        Name = new FName(asset, name),
        Flags = EObjectFlags.RF_Public,
        ArrayDim = EArrayDim.TArray,
        ElementSize = size,
        PropertyFlags = flags,
        RepIndex = 0,
        RepNotifyFunc = new FName(asset, "None"),
        BlueprintReplicationCondition = ELifetimeCondition.COND_None,
        Struct = structType
    };
}

static FDelegateProperty DelegateProperty(
    UAsset asset,
    string name,
    FPackageIndex signatureFunction)
{
    return new FDelegateProperty
    {
        SerializedType = new FName(asset, "DelegateProperty"),
        Name = new FName(asset, name),
        Flags = EObjectFlags.RF_Public,
        ArrayDim = EArrayDim.TArray,
        ElementSize = 20,
        PropertyFlags = EPropertyFlags.CPF_None,
        RepIndex = 0,
        RepNotifyFunc = new FName(asset, "None"),
        BlueprintReplicationCondition = ELifetimeCondition.COND_None,
        SignatureFunction = signatureFunction
    };
}

static FBoolProperty BoolProperty(UAsset asset, string name)
{
    return new FBoolProperty
    {
        SerializedType = new FName(asset, "BoolProperty"),
        Name = new FName(asset, name),
        Flags = EObjectFlags.RF_Public,
        ArrayDim = EArrayDim.TArray,
        ElementSize = 1,
        PropertyFlags = EPropertyFlags.CPF_None,
        RepIndex = 0,
        RepNotifyFunc = new FName(asset, "None"),
        BlueprintReplicationCondition = ELifetimeCondition.COND_None,
        FieldSize = 1,
        ByteOffset = 0,
        ByteMask = 1,
        FieldMask = byte.MaxValue,
        NativeBool = true,
        Value = true
    };
}

static byte[] SerializeName(UAsset asset, string value)
{
    using var stream = new MemoryStream();
    using var writer = new AssetBinaryWriter(stream, asset);
    writer.Write(new FName(asset, value));
    return stream.ToArray();
}

sealed record LegacyExportPatch(
    string RelativeAssetPath,
    string ExportName,
    byte[] SourceData,
    byte[] PatchedData);

sealed class ScriptStatement
{
    public int? OriginalOffset { get; set; }
    public string Tag { get; set; }
    public KismetExpression Expression { get; set; }

    public ScriptStatement(int? originalOffset, string tag, KismetExpression expression)
    {
        OriginalOffset = originalOffset;
        Tag = tag;
        Expression = expression;
    }
}

sealed class FunctionScript
{
    private readonly UAsset _asset;
    private readonly RawExport _export;
    private readonly byte[] _propertyPrefix;
    private readonly byte[] _trailing;

    public List<ScriptStatement> Statements { get; }
    public List<FProperty> Properties { get; }
    public Dictionary<KismetExpression, string> PendingTargets { get; } = new();
    public FPackageIndex ExportIndex => FPackageIndex.FromExport(_asset.Exports.IndexOf(_export));

    private FunctionScript(
        UAsset asset,
        RawExport export,
        byte[] propertyPrefix,
        byte[] trailing,
        List<FProperty> properties,
        List<ScriptStatement> statements)
    {
        _asset = asset;
        _export = export;
        _propertyPrefix = propertyPrefix;
        _trailing = trailing;
        Properties = properties;
        Statements = statements;
    }

    public static FunctionScript Parse(UAsset asset, string functionName)
    {
        var export = asset.Exports.OfType<RawExport>()
            .Single(item => item.ObjectName.Value.Value == functionName &&
                item.ClassIndex.IsImport() &&
                item.ClassIndex.ToImport(asset).ObjectName.Value.Value == "Function");

        using var stream = new MemoryStream(export.Data, writable: false);
        using var reader = new AssetBinaryReader(stream, asset);
        var header = new FUnversionedHeader(reader);
        if (header.HasValues())
        {
            throw new InvalidDataException($"{functionName} has unsupported UObject properties.");
        }

        if (reader.ReadInt32() != 0)
        {
            throw new InvalidDataException($"{functionName} has an object GUID.");
        }

        if (asset.GetCustomVersion<FFrameworkObjectVersion>() < FFrameworkObjectVersion.RemoveUField_Next)
        {
            reader.ReadInt32();
        }

        reader.ReadInt32();
        var childCount = reader.ReadInt32();
        for (var index = 0; index < childCount; index++)
        {
            reader.ReadInt32();
        }

        var propertyPrefixLength = checked((int)reader.BaseStream.Position);
        var propertyCount = reader.ReadInt32();
        var properties = new List<FProperty>(propertyCount);
        for (var index = 0; index < propertyCount; index++)
        {
            properties.Add(MainSerializer.ReadFProperty(reader));
        }

        var bytecodeSize = reader.ReadInt32();
        var storageSize = reader.ReadInt32();
        var scriptStart = reader.BaseStream.Position;
        var expressions = new List<KismetExpression>();
        while (reader.BaseStream.Position - scriptStart < storageSize)
        {
            expressions.Add(ExpressionSerializer.ReadExpression(reader));
        }

        if (reader.BaseStream.Position - scriptStart != storageSize)
        {
            throw new InvalidDataException($"{functionName} bytecode storage size is inconsistent.");
        }

        var statements = new List<ScriptStatement>(expressions.Count);
        uint offset = 0;
        foreach (var expression in expressions)
        {
            var originalOffset = checked((int)offset);
            statements.Add(new ScriptStatement(originalOffset, $"old:{originalOffset}", expression));
            expression.Visit(asset, ref offset, (_, _) => { });
        }

        if (offset != bytecodeSize)
        {
            throw new InvalidDataException($"{functionName} bytecode size is inconsistent ({offset} != {bytecodeSize}).");
        }

        return new FunctionScript(
            asset,
            export,
            export.Data[..propertyPrefixLength],
            reader.ReadBytes(checked((int)(reader.BaseStream.Length - reader.BaseStream.Position))),
            properties,
            statements);
    }

    public ScriptStatement At(int originalOffset) =>
        Statements.Single(statement => statement.OriginalOffset == originalOffset);

    public uint OffsetOf(string tag) => CalculateOffsets()[tag];

    public void Rename(string name) => _export.ObjectName = new FName(_asset, name);

    public void InsertBefore(ScriptStatement anchor, ScriptStatement statement)
    {
        Statements.Insert(Statements.IndexOf(anchor), statement);
    }

    public void InsertAfter(ScriptStatement anchor, ScriptStatement statement)
    {
        Statements.Insert(Statements.IndexOf(anchor) + 1, statement);
    }

    public Dictionary<int, uint> FinalizeScript()
    {
        RefreshContextOffsets();
        var offsetsByTag = CalculateOffsets();
        var oldToNew = Statements
            .Where(statement => statement.OriginalOffset.HasValue)
            .ToDictionary(statement => statement.OriginalOffset!.Value, statement => offsetsByTag[statement.Tag]);

        foreach (var statement in Statements)
        {
            ExpressionWalker.Visit(statement.Expression, _asset, expression =>
            {
                if (PendingTargets.ContainsKey(expression))
                {
                    return;
                }

                switch (expression)
                {
                    case EX_Jump jump when oldToNew.TryGetValue(checked((int)jump.CodeOffset), out var jumpTarget):
                        jump.CodeOffset = jumpTarget;
                        break;
                    case EX_JumpIfNot jumpIfNot when oldToNew.TryGetValue(checked((int)jumpIfNot.CodeOffset), out var conditionalTarget):
                        jumpIfNot.CodeOffset = conditionalTarget;
                        break;
                    case EX_PushExecutionFlow push when oldToNew.TryGetValue(checked((int)push.PushingAddress), out var pushTarget):
                        push.PushingAddress = pushTarget;
                        break;
                    case EX_Skip skip when oldToNew.TryGetValue(checked((int)skip.CodeOffset), out var skipTarget):
                        skip.CodeOffset = skipTarget;
                        break;
                    case EX_SkipOffsetConst skipOffset when oldToNew.TryGetValue(checked((int)skipOffset.Value), out var latentTarget):
                        skipOffset.Value = latentTarget;
                        break;
                    case EX_SwitchValue switchValue:
                        if (oldToNew.TryGetValue(checked((int)switchValue.EndGotoOffset), out var switchTarget))
                        {
                            switchValue.EndGotoOffset = switchTarget;
                        }

                        for (var index = 0; index < switchValue.Cases.Length; index++)
                        {
                            var current = switchValue.Cases[index];
                            if (oldToNew.TryGetValue(checked((int)current.NextOffset), out var caseTarget))
                            {
                                current.NextOffset = caseTarget;
                                switchValue.Cases[index] = current;
                            }
                        }
                        break;
                }
            });
        }

        foreach (var (expression, targetTag) in PendingTargets)
        {
            if (!offsetsByTag.TryGetValue(targetTag, out var target))
            {
                throw new InvalidDataException($"Unknown bytecode target tag: {targetTag}");
            }

            switch (expression)
            {
                case EX_Jump jump:
                    jump.CodeOffset = target;
                    break;
                case EX_JumpIfNot jumpIfNot:
                    jumpIfNot.CodeOffset = target;
                    break;
                case EX_SkipOffsetConst skipOffset:
                    skipOffset.Value = target;
                    break;
                case EX_PushExecutionFlow push:
                    push.PushingAddress = target;
                    break;
                default:
                    throw new InvalidDataException($"Unsupported pending branch type: {expression.GetType().Name}");
            }
        }

        Rebuild();
        return oldToNew;
    }

    public void Rebuild()
    {
        using var scriptStream = new MemoryStream();
        using (var writer = new AssetBinaryWriter(scriptStream, _asset))
        {
            foreach (var statement in Statements)
            {
                ExpressionSerializer.WriteExpression(statement.Expression, writer);
            }
        }
        var scriptData = scriptStream.ToArray();

        var bytecodeSize = Statements.Aggregate(0u, (total, statement) => total + statement.Expression.GetSize(_asset));
        using var output = new MemoryStream();
        using var binary = new AssetBinaryWriter(output, _asset);
        binary.Write(_propertyPrefix);
        binary.Write(Properties.Count);
        foreach (var property in Properties)
        {
            MainSerializer.WriteFProperty(property, binary);
        }
        binary.Write(checked((int)bytecodeSize));
        binary.Write(scriptData.Length);
        binary.Write(scriptData);
        binary.Write(_trailing);
        _export.Data = output.ToArray();
    }

    private Dictionary<string, uint> CalculateOffsets()
    {
        var result = new Dictionary<string, uint>();
        uint offset = 0;
        foreach (var statement in Statements)
        {
            if (!result.TryAdd(statement.Tag, offset))
            {
                throw new InvalidDataException($"Duplicate bytecode tag: {statement.Tag}");
            }

            offset += statement.Expression.GetSize(_asset);
        }

        return result;
    }

    private void RefreshContextOffsets()
    {
        foreach (var statement in Statements)
        {
            ExpressionWalker.Visit(statement.Expression, _asset, expression =>
            {
                if (expression is EX_Context context)
                {
                    context.Offset = context.ContextExpression.GetSize(_asset);
                }
            });
        }
    }
}

static class ExpressionWalker
{
    public static void Visit(KismetExpression expression, UAsset asset, Action<KismetExpression> visitor)
    {
        uint offset = 0;
        expression.Visit(asset, ref offset, (node, _) => visitor(node));
    }
}

static class IoStoreToc
{
    private const int HeaderSize = 0x90;
    private const int ChunkIdSize = 12;
    private const int OffsetAndLengthSize = 10;
    private const int EntryMetaSize = 33;
    private const byte PerfectHashWithOverflowVersion = 5;
    private const byte EncryptedFlag = 0x02;
    private const byte SignedFlag = 0x04;
    private const byte IndexedFlag = 0x08;
    private static readonly byte[] Magic = "-==--==--==--==-"u8.ToArray();

    public static void RestoreReferenceIndex(string referencePath, string generatedPath, string outputPath)
    {
        var reference = ParsedToc.Read(referencePath);
        var generated = ParsedToc.Read(generatedPath);
        ValidateCompatible(reference, generated);

        var generatedByChunk = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < generated.EntryCount; i++)
        {
            var id = generated.ChunkId(i);
            if (!generatedByChunk.TryAdd(id, i))
            {
                throw new InvalidDataException($"Generated container has duplicate chunk ID {id}.");
            }
        }

        var header = generated.Header.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(48, 4), checked((uint)reference.DirectoryIndex.Length));
        reference.Header.AsSpan(56, 8).CopyTo(header.AsSpan(56, 8));
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(84, 4), checked((uint)reference.PerfectHashSeedCount));
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(96, 4), checked((uint)reference.OverflowCount));

        using var output = new MemoryStream();
        output.Write(header);
        output.Write(reference.ChunkIds);
        for (var i = 0; i < reference.EntryCount; i++)
        {
            var id = reference.ChunkId(i);
            if (!generatedByChunk.TryGetValue(id, out var generatedIndex))
            {
                throw new InvalidDataException($"Generated container is missing reference chunk ID {id}.");
            }

            output.Write(generated.OffsetAndLengths.AsSpan(generatedIndex * OffsetAndLengthSize, OffsetAndLengthSize));
        }

        output.Write(reference.PerfectHashSeeds);
        output.Write(reference.OverflowIndices);
        output.Write(generated.CompressionBlocks);
        output.Write(generated.CompressionMethods);
        output.Write(reference.DirectoryIndex);
        for (var i = 0; i < reference.EntryCount; i++)
        {
            var generatedIndex = generatedByChunk[reference.ChunkId(i)];
            output.Write(generated.EntryMetas.AsSpan(generatedIndex * EntryMetaSize, EntryMetaSize));
        }

        var fullOutputPath = Path.GetFullPath(outputPath);
        if (fullOutputPath.Equals(Path.GetFullPath(referencePath), StringComparison.OrdinalIgnoreCase) ||
            fullOutputPath.Equals(Path.GetFullPath(generatedPath), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Container index restoration requires a distinct output path.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath)!);
        File.WriteAllBytes(fullOutputPath, output.ToArray());
        VerifyReferenceIndex(referencePath, fullOutputPath);
    }

    public static void VerifyReferenceIndex(string referencePath, string candidatePath)
    {
        var reference = ParsedToc.Read(referencePath);
        var candidate = ParsedToc.Read(candidatePath);
        ValidateCompatible(reference, candidate);

        Require(candidate.PerfectHashSeedCount > 0, "Candidate container has no perfect-hash seeds.");
        Require(candidate.ChunkIds.AsSpan().SequenceEqual(reference.ChunkIds),
            "Candidate chunk order differs from the working reference.");
        Require(candidate.PerfectHashSeeds.AsSpan().SequenceEqual(reference.PerfectHashSeeds),
            "Candidate perfect-hash seeds differ from the working reference.");
        Require(candidate.OverflowIndices.AsSpan().SequenceEqual(reference.OverflowIndices),
            "Candidate perfect-hash overflow table differs from the working reference.");
        Require(candidate.DirectoryIndex.AsSpan().SequenceEqual(reference.DirectoryIndex),
            "Candidate directory index or mount point differs from the working reference.");
        Require((candidate.ContainerFlags & IndexedFlag) != 0, "Candidate container is not indexed.");
        Require(candidate.CompressionBlockCount > 0, "Candidate container has no compression blocks.");
        Require(candidate.CompressionBlockEntrySize == 12, "Candidate compression block entry size is not 12 bytes.");
    }

    private static void ValidateCompatible(ParsedToc reference, ParsedToc candidate)
    {
        Require(reference.Version == PerfectHashWithOverflowVersion,
            $"Reference TOC version {reference.Version} is not PerfectHashWithOverflow ({PerfectHashWithOverflowVersion}).");
        Require(candidate.Version == reference.Version, "Candidate TOC version differs from the working reference.");
        Require(reference.EntryCount == candidate.EntryCount, "Candidate chunk count differs from the working reference.");
        Require(reference.ContainerId == candidate.ContainerId, "Candidate container ID differs from the working reference.");
        Require(reference.PerfectHashSeedCount > 0, "Working reference has no perfect-hash seeds.");
        Require((reference.ContainerFlags & (EncryptedFlag | SignedFlag)) == 0,
            "Encrypted or signed reference containers are not supported.");
        Require((candidate.ContainerFlags & (EncryptedFlag | SignedFlag)) == 0,
            "Encrypted or signed candidate containers are not supported.");

        var referenceIds = new HashSet<string>(StringComparer.Ordinal);
        var candidateIds = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < reference.EntryCount; i++) referenceIds.Add(reference.ChunkId(i));
        for (var i = 0; i < candidate.EntryCount; i++) candidateIds.Add(candidate.ChunkId(i));
        Require(referenceIds.SetEquals(candidateIds), "Candidate chunk IDs differ from the working reference.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }

    private sealed class ParsedToc
    {
        public required byte[] Header { get; init; }
        public required byte Version { get; init; }
        public required int EntryCount { get; init; }
        public required int CompressionBlockCount { get; init; }
        public required int CompressionBlockEntrySize { get; init; }
        public required int CompressionBlockSize { get; init; }
        public required int PerfectHashSeedCount { get; init; }
        public required int OverflowCount { get; init; }
        public required ulong ContainerId { get; init; }
        public required byte ContainerFlags { get; init; }
        public required byte[] ChunkIds { get; init; }
        public required byte[] OffsetAndLengths { get; init; }
        public required byte[] PerfectHashSeeds { get; init; }
        public required byte[] OverflowIndices { get; init; }
        public required byte[] CompressionBlocks { get; init; }
        public required byte[] CompressionMethods { get; init; }
        public required byte[] DirectoryIndex { get; init; }
        public required byte[] EntryMetas { get; init; }

        public string ChunkId(int index)
        {
            if ((uint)index >= (uint)EntryCount) throw new ArgumentOutOfRangeException(nameof(index));
            return Convert.ToHexString(ChunkIds.AsSpan(index * ChunkIdSize, ChunkIdSize));
        }

        public static ParsedToc Read(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("IoStore TOC was not found.", path);
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length < HeaderSize) throw new InvalidDataException($"IoStore TOC is truncated: {path}");
            if (!bytes.AsSpan(0, Magic.Length).SequenceEqual(Magic))
            {
                throw new InvalidDataException($"IoStore TOC has an invalid magic value: {path}");
            }

            var headerSize = ReadInt32(bytes, 20, "header size");
            Require(headerSize == HeaderSize, $"Unsupported IoStore TOC header size {headerSize} in {path}.");

            var version = bytes[16];
            var entryCount = ReadInt32(bytes, 24, "entry count");
            var compressionBlockCount = ReadInt32(bytes, 28, "compression block count");
            var compressionBlockEntrySize = ReadInt32(bytes, 32, "compression block entry size");
            var compressionMethodCount = ReadInt32(bytes, 36, "compression method count");
            var compressionMethodNameLength = ReadInt32(bytes, 40, "compression method name length");
            var compressionBlockSize = ReadInt32(bytes, 44, "compression block size");
            var directoryIndexSize = ReadInt32(bytes, 48, "directory index size");
            var perfectHashSeedCount = version >= PerfectHashWithOverflowVersion
                ? ReadInt32(bytes, 84, "perfect-hash seed count")
                : 0;
            var overflowCount = version >= PerfectHashWithOverflowVersion
                ? ReadInt32(bytes, 96, "perfect-hash overflow count")
                : 0;
            var containerId = BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(56, 8));
            var containerFlags = bytes[80];

            var cursor = HeaderSize;
            byte[] Take(int length, string description)
            {
                if (length < 0 || cursor > bytes.Length - length)
                {
                    throw new InvalidDataException($"IoStore TOC {description} is truncated in {path}.");
                }

                var result = bytes.AsSpan(cursor, length).ToArray();
                cursor += length;
                return result;
            }

            var chunkIds = Take(CheckedLength(entryCount, ChunkIdSize, "chunk IDs"), "chunk IDs");
            var offsetAndLengths = Take(CheckedLength(entryCount, OffsetAndLengthSize, "offset table"), "offset table");
            var perfectHashSeeds = Take(CheckedLength(perfectHashSeedCount, sizeof(int), "perfect-hash seeds"), "perfect-hash seeds");
            var overflowIndices = Take(CheckedLength(overflowCount, sizeof(int), "perfect-hash overflow table"), "perfect-hash overflow table");
            var compressionBlocks = Take(CheckedLength(compressionBlockCount, compressionBlockEntrySize, "compression blocks"), "compression blocks");
            var compressionMethods = Take(CheckedLength(compressionMethodCount, compressionMethodNameLength, "compression methods"), "compression methods");
            if ((containerFlags & SignedFlag) != 0)
            {
                throw new InvalidDataException($"Signed IoStore TOCs are not supported: {path}");
            }

            var directoryIndex = Take(directoryIndexSize, "directory index");
            var entryMetas = Take(CheckedLength(entryCount, EntryMetaSize, "entry metadata"), "entry metadata");
            Require(cursor == bytes.Length, $"IoStore TOC contains unsupported trailing metadata in {path}.");

            return new ParsedToc
            {
                Header = bytes.AsSpan(0, HeaderSize).ToArray(),
                Version = version,
                EntryCount = entryCount,
                CompressionBlockCount = compressionBlockCount,
                CompressionBlockEntrySize = compressionBlockEntrySize,
                CompressionBlockSize = compressionBlockSize,
                PerfectHashSeedCount = perfectHashSeedCount,
                OverflowCount = overflowCount,
                ContainerId = containerId,
                ContainerFlags = containerFlags,
                ChunkIds = chunkIds,
                OffsetAndLengths = offsetAndLengths,
                PerfectHashSeeds = perfectHashSeeds,
                OverflowIndices = overflowIndices,
                CompressionBlocks = compressionBlocks,
                CompressionMethods = compressionMethods,
                DirectoryIndex = directoryIndex,
                EntryMetas = entryMetas
            };
        }

        private static int ReadInt32(byte[] bytes, int offset, string description)
        {
            var value = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, sizeof(uint)));
            if (value > int.MaxValue) throw new InvalidDataException($"IoStore TOC {description} is too large.");
            return (int)value;
        }

        private static int CheckedLength(int count, int itemSize, string description)
        {
            if (count < 0 || itemSize < 0) throw new InvalidDataException($"IoStore TOC {description} has a negative size.");
            try
            {
                return checked(count * itemSize);
            }
            catch (OverflowException exception)
            {
                throw new InvalidDataException($"IoStore TOC {description} is too large.", exception);
            }
        }
    }
}
