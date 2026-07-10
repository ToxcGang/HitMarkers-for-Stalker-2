using UAssetAPI;
using UAssetAPI.CustomVersions;
using UAssetAPI.ExportTypes;
using UAssetAPI.FieldTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;

if (args.Length == 2 && args[0] == "--verify")
{
    var verificationRoot = Path.GetFullPath(args[1]);
    VerifyPatchedAssets(verificationRoot);
    Console.WriteLine($"Verified HitMarkers cooked assets in {verificationRoot}");
    return 0;
}

var diagnostics = args.Length == 3 && args[0] == "--diagnostics";
if (diagnostics) args = args[1..];

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: HitMarkersPatcher [--diagnostics] <legacy-source> <legacy-output> | --verify <legacy-root>");
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
PatchHolder(Path.Combine(outputRoot, "Stalker2", "Content", "Mods", "ShowDMG", "bp_dmgActorHolder.uasset"), diagnostics);
PatchSpawner(Path.Combine(outputRoot, "Stalker2", "Content", "Mods", "ShowDMG", "bpac_dmgWidgetSpawner.uasset"), diagnostics);
PatchRunner(Path.Combine(outputRoot, "Stalker2", "Content", "Mods", "ShowDMG", "BP_Run_ModActor.uasset"), diagnostics);
VerifyPatchedAssets(outputRoot);

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
            PrintLog(asset, "[HitMarkers][Diagnostic] widget created, inserted, visible, fade started")));
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

    var holderMap = graph.FinalizeScript();
    UpdateEntrypoint(asset, "ReceiveBeginPlay", 215, holderMap[215]);
    UpdateEntrypoint(asset, "ReceiveTick", 612, holderMap[732]);
    asset.Write(path);
}

static void PatchSpawner(string path, bool diagnostics)
{
    var asset = LoadAsset(path);
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
    UpdateEntrypoint(asset, "ReceiveTick", 454, spawnerMap[454]);

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

static void PatchRunner(string path, bool diagnostics)
{
    var asset = LoadAsset(path);
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

        var insertion = graph.Statements.IndexOf(anchor) + 1;
        graph.Statements.InsertRange(insertion, new[] { setRelation, setHealth, enableBroadcast, bind, addDelegate });
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
    UpdateEntrypoint(asset, "ReceiveBeginPlay", 1622, graph.OffsetOf("init_log"));
    UpdateEntrypoint(asset, "ReceiveTick", 1531, runnerMap[1623]);
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

static byte[] SerializeName(UAsset asset, string value)
{
    using var stream = new MemoryStream();
    using var writer = new AssetBinaryWriter(stream, asset);
    writer.Write(new FName(asset, value));
    return stream.ToArray();
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
