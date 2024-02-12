using System;
using BepInEx;
using HarmonyLib;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Configgy;
using UnityEngine;

namespace BigCG;

[BepInPlugin(Guid, Name, Version)]
public class Plugin : BaseUnityPlugin
{
    public const string Guid = "waffle.ultrakill.bigcg";
    public const string Name = "Big Cybergrind";
    public const string Version = "1.0.0";

    [Configgable("", "Enable Big CG", 0, "Enable features of the mod.\nDisable this to reenable score submission.")]
    public static ConfigToggle ModEnabled = new(true);
    [Configgable("", "Grid Length", 0, "Length of the grid - default is 16.\nThis starts lagging really bad at high values.")]
    public static int GridSize = 32;
    [Configgable("", "Fast Animation", 1, "Animations between waves are worse, but lag less. Good for performance.")]
    public static bool FastAnim = false;

    private static Harmony _harmony = new(Guid);
    private static FieldInfo s_GridSize = AccessTools.Field(typeof(Plugin), nameof(GridSize));
    private static MethodInfo s_mainGridSize = AccessTools.Method(typeof(Plugin), "get_MainGridSize");
    private static MethodInfo s_fixPatterns = AccessTools.Method(typeof(PatternFixer), nameof(PatternFixer.FixPatterns));
    private static MethodInfo s_readAllLines = AccessTools.Method(typeof(File), nameof(File.ReadAllLines), [typeof(string)]);
    private static List<GameObject> s_mergedMeshes = new();

    public static int MainGridSize => GridSize >= 16 ? 16 : GridSize; //if the size > 16 the main baked mesh should only be 16, otherwise it should be less.

    private void Start()
    {
        ConfigBuilder config = new(Guid, Name);
        config.Build();

        // this mod is all patches :3
        ModEnabled.OnValueChanged = (value) =>
        {
            if (value)
            {
                _harmony.PatchAll(typeof(Plugin));
            }
            else
            {
                _harmony.UnpatchSelf();
            }
        };
    }

    [HarmonyPatch(typeof(LeaderboardController), nameof(LeaderboardController.SubmitCyberGrindScore))]
    [HarmonyPatch(typeof(LeaderboardController), nameof(LeaderboardController.SubmitLevelScore))]
    [HarmonyPrefix]
    private static bool DisableCg()
    {
        Debug.Log("BCG patches enabled, disable CG ‼️");
        return false;
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(CustomPatterns), nameof(CustomPatterns.GeneratePatternPreview))]
    [HarmonyPatch(typeof(EndlessGrid), nameof(EndlessGrid.LoadPattern))]
    [HarmonyPatch(typeof(EndlessGrid), nameof(EndlessGrid.MakeGridDynamic))]
    [HarmonyPatch(typeof(EndlessGrid), nameof(EndlessGrid.OneDone))]
    [HarmonyPatch(typeof(EndlessGrid), nameof(EndlessGrid.Start))]
    private static IEnumerable<CodeInstruction> ReplaceGridSize(IEnumerable<CodeInstruction> instructions)
    {
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.opcode == OpCodes.Ldc_I4_S && (sbyte)instruction.operand == 16)
            {
                instruction.opcode = OpCodes.Ldsfld;
                instruction.operand = s_GridSize;
            }

            yield return instruction;
        }
    }

    [HarmonyPatch(typeof(EndlessGrid), nameof(EndlessGrid.SetupStaticGridMesh))]
    private static IEnumerable<CodeInstruction> ReplaceMainGridSize(IEnumerable<CodeInstruction> instructions)
    {
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.opcode == OpCodes.Ldc_I4_S && (sbyte)instruction.operand == 16)
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = s_mainGridSize;
            }

            yield return instruction;
        }
    }

    [HarmonyPatch(typeof(CustomPatterns), nameof(CustomPatterns.BuildButtons)), HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> FixButtonSize(IEnumerable<CodeInstruction> instructions)
    {
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.opcode == OpCodes.Ldc_R4 && instruction.OperandIs(48f))
            {
                yield return new CodeInstruction(OpCodes.Ldsfld, s_GridSize);
                yield return new CodeInstruction(OpCodes.Ldc_I4_3);
                yield return new CodeInstruction(OpCodes.Mul);
                yield return new CodeInstruction(OpCodes.Conv_R4);
                continue;
            }

            if (instruction.opcode == OpCodes.Ldc_I4_S && instruction.OperandIs(48))
            {
                yield return new CodeInstruction(OpCodes.Ldsfld, s_GridSize);
                yield return new CodeInstruction(OpCodes.Ldc_I4_3);
                yield return new CodeInstruction(OpCodes.Mul);
                continue;
            }

            yield return instruction;
        }
    }

    [HarmonyPatch(typeof(CustomPatterns), nameof(CustomPatterns.LoadPattern)), HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> CustomPatterns_LoadPattern(IEnumerable<CodeInstruction> instructions)
    {
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.opcode == OpCodes.Ldc_I4_S)
            {
                switch ((sbyte)instruction.operand)
                {
                    case 15:
                        instruction.opcode = OpCodes.Ldsfld;
                        instruction.operand = s_GridSize;
                        yield return instruction;
                        yield return new CodeInstruction(OpCodes.Ldc_I4_1);
                        yield return new CodeInstruction(OpCodes.Sub);
                        continue;
                    case 16:
                        instruction.opcode = OpCodes.Ldsfld;
                        instruction.operand = s_GridSize;
                        yield return instruction;
                        continue;
                    case 17:
                        instruction.opcode = OpCodes.Ldsfld;
                        instruction.operand = s_GridSize;
                        yield return instruction;
                        yield return new CodeInstruction(OpCodes.Ldc_I4_1);
                        yield return new CodeInstruction(OpCodes.Add);
                        continue;
                    case 32:
                        instruction.opcode = OpCodes.Ldsfld;
                        instruction.operand = s_GridSize;
                        yield return instruction;
                        yield return new CodeInstruction(OpCodes.Ldc_I4_2);
                        yield return new CodeInstruction(OpCodes.Mul);
                        continue;
                    case 33:
                        instruction.opcode = OpCodes.Ldsfld;
                        instruction.operand = s_GridSize;
                        yield return instruction;
                        yield return new CodeInstruction(OpCodes.Ldc_I4_2);
                        yield return new CodeInstruction(OpCodes.Mul);
                        yield return new CodeInstruction(OpCodes.Ldc_I4_1);
                        yield return new CodeInstruction(OpCodes.Add);
                        continue;
                }
            }

            yield return instruction;

            if (instruction.opcode == OpCodes.Call && instruction.OperandIs(s_readAllLines))
            {
                yield return new CodeInstruction(OpCodes.Call, s_fixPatterns);
            }
        }
    }

    [HarmonyPatch(typeof(EndlessGrid), nameof(EndlessGrid.NextWave)), HarmonyPostfix]
    private static void DestroyBakedMeshes()
    {
        s_mergedMeshes.RemoveAll(x => x == null);
        foreach (GameObject mesh in s_mergedMeshes)
        {
            Destroy(mesh);
        }
    }

    [HarmonyPatch(typeof(EndlessGrid), nameof(EndlessGrid.SetupStaticGridMesh)), HarmonyPostfix]
    private static void DoOtherBakes()
    {
        int cubeCount = GridSize * GridSize;
        int cubesDone = MainGridSize * MainGridSize;

        while (cubesDone != cubeCount)
        {
            int amountLeft = cubeCount - cubesDone;
            int amountToDo = MeshBaking.MaxCubesPerMesh > amountLeft ? amountLeft : MeshBaking.MaxCubesPerMesh;
            s_mergedMeshes.Add(MeshBaking.CreateCubeMesh(cubesDone - 1, amountToDo));
            cubesDone += amountToDo;
        }

        int stairCount = EndlessGrid.Instance.spawnedPrefabs.Count(x => x.GetComponent<EndlessStairs>() != null);
        int stairsDone = 0;

        while (stairsDone != stairCount)
        {
            int amountLeft = stairCount - stairsDone;
            int amountToDo = MeshBaking.MaxStairsPerMesh > amountLeft ? amountLeft : MeshBaking.MaxStairsPerMesh;
            s_mergedMeshes.Add(MeshBaking.CreateStairsMesh(stairsDone - 1, amountToDo));
            stairsDone += amountToDo;
        }
    }

    [HarmonyPatch(typeof(EndlessGrid), nameof(EndlessGrid.SetupStaticGridMesh)), HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> PreventStairCreation(IEnumerable<CodeInstruction> instructions)
    {
        CodeInstruction[] ciArray = instructions.ToArray();
        for (int i = 0; i < ciArray.Length; i++)
        {
            if (i < 2)
            {
                yield return ciArray[i];
                continue;
            }

            if (ciArray[i - 2].opcode == OpCodes.Ldloc_S && ciArray[i - 1].opcode == OpCodes.Ldc_I4_1 && ciArray[i].opcode == OpCodes.Bne_Un)
            {
                yield return new CodeInstruction(OpCodes.Pop);
                yield return new CodeInstruction(OpCodes.Ldc_I4, 100); //idfk i cba, normally compares loc7 to 1, now it compares to 100, should never be true
            }

            yield return ciArray[i];
        }
    }

    [HarmonyPatch(typeof(EndlessCube), nameof(EndlessCube.Update)), HarmonyPostfix]
    private static void FixSlowMoveCube(EndlessCube __instance)
    {
        if (!FastAnim || !__instance.active)
        {
            return;
        }

        __instance.tf.position = __instance.targetPos;
    }

    [HarmonyPatch(typeof(EndlessStairs), nameof(EndlessStairs.Update)), HarmonyPostfix]
    private static void FixSlowMoveStairs(EndlessStairs __instance)
    {
        if (!FastAnim || !__instance.moving)
        {
            return;
        }

        __instance.primaryStairs.position = __instance.transform.position;
        __instance.secondaryStairs.position = __instance.transform.position;
    }
}

