using UAssetAPI;
using UAssetAPI.CustomVersions;
using UAssetAPI.ExportTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: HitMarkersPatcher <legacy-source> <legacy-output>");
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
PatchHolder(Path.Combine(outputRoot, "Stalker2", "Content", "Mods", "ShowDMG", "bp_dmgActorHolder.uasset"));
PatchSpawner(Path.Combine(outputRoot, "Stalker2", "Content", "Mods", "ShowDMG", "bpac_dmgWidgetSpawner.uasset"));
PatchRunner(Path.Combine(outputRoot, "Stalker2", "Content", "Mods", "ShowDMG", "BP_Run_ModActor.uasset"));

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

static void PatchHolder(string path)
{
    var asset = LoadAsset(path);
    var graph = FunctionScript.Parse(asset, "ExecuteUbergraph_bp_dmgActorHolder");

    var showContext = (EX_Context)graph.At(81).Expression;
    var drawContext = (EX_Context)graph.At(502).Expression;
    drawContext.ObjectExpression = showContext.ObjectExpression;
    drawContext.RValuePointer = showContext.RValuePointer;
    drawContext.ContextExpression = new EX_VirtualFunction
    {
        VirtualFunctionName = new FName(asset, "AddToViewport"),
        Parameters = new KismetExpression[] { new EX_IntConst { Value = 0 } }
    };

    graph.At(566).Expression = new EX_Nothing();

    Visit(graph.At(135).Expression, asset, expression =>
    {
        if (expression is EX_FloatConst delay && Math.Abs(delay.Value - 5f) < 0.001f)
        {
            delay.Value = 0.35f;
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

    var holderMap = graph.FinalizeScript();
    UpdateEntrypoint(asset, "ReceiveBeginPlay", 215, holderMap[215]);
    UpdateEntrypoint(asset, "ReceiveTick", 612, holderMap[612]);
    asset.Write(path);
}

static void PatchSpawner(string path)
{
    var asset = LoadAsset(path);
    var graph = FunctionScript.Parse(asset, "ExecuteUbergraph_bpac_dmgWidgetSpawner");

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

    var spawnerMap = graph.FinalizeScript();
    UpdateEntrypoint(asset, "ReceiveTick", 454, spawnerMap[454]);
    asset.Write(path);
}

static void PatchRunner(string path)
{
    var asset = LoadAsset(path);
    var graph = FunctionScript.Parse(asset, "ExecuteUbergraph_BP_Run_ModActor");

    Visit(graph.At(1479).Expression, asset, expression =>
    {
        if (expression is EX_StringConst text && text.Value == "Ally")
        {
            text.Value = "NA";
        }
    });

    var runnerMap = graph.FinalizeScript();
    UpdateEntrypoint(asset, "ReceiveBeginPlay", 1622, runnerMap[1622]);
    UpdateEntrypoint(asset, "ReceiveTick", 1531, runnerMap[1531]);
    asset.Write(path);
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
    private readonly byte[] _prefix;
    private readonly byte[] _trailing;

    public List<ScriptStatement> Statements { get; }
    public Dictionary<KismetExpression, string> PendingTargets { get; } = new();

    private FunctionScript(
        UAsset asset,
        RawExport export,
        byte[] prefix,
        byte[] trailing,
        List<ScriptStatement> statements)
    {
        _asset = asset;
        _export = export;
        _prefix = prefix;
        _trailing = trailing;
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

        var propertyCount = reader.ReadInt32();
        for (var index = 0; index < propertyCount; index++)
        {
            MainSerializer.ReadFProperty(reader);
        }

        var prefixLength = checked((int)reader.BaseStream.Position);
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
            export.Data[..prefixLength],
            reader.ReadBytes(checked((int)(reader.BaseStream.Length - reader.BaseStream.Position))),
            statements);
    }

    public ScriptStatement At(int originalOffset) =>
        Statements.Single(statement => statement.OriginalOffset == originalOffset);

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
        using var binary = new BinaryWriter(output);
        binary.Write(_prefix);
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
