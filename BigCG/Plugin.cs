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
    [Configgable("", "Budget Multiplier", 1, "Increase the budget allocated to enemy spawns to offset the size of the map.")]
    public static int Multiplier = 2;
    [Configgable("Optimisations", "Optimised Cube Model", 2, "Replaces the grid cube model with one that has 1/5th of the tris.\nLooks worse when using texture or vertex warping.")]
    public static bool OptimizedModel = true;
    [Configgable("Optimisations", "Optimised Stair Model", 3, "Replaces the stairs with a ramp that has 1/10th of the tris.\nLooks worse generally and when using texture or vertex warping.")]
    public static bool OptimizedStairs = false;
    [Configgable("Optimisations", "Fast Animation", 4, "Animations between waves are buggy, but lag less.\nGood for performance, but not recommended.")]
    public static bool FastAnim = false;

    private static Harmony s_harmony = new(Guid);
    private static FieldInfo s_gridSize = AccessTools.Field(typeof(Plugin), nameof(GridSize));
    private static MethodInfo s_mainGridSize = AccessTools.Method(typeof(Plugin), "get_MainGridSize");
    private static MethodInfo s_fixPattern = AccessTools.Method(typeof(PatternFixer), nameof(PatternFixer.FixPattern));
    private static MethodInfo s_fixStringPattern = AccessTools.Method(typeof(PatternFixer), nameof(PatternFixer.FixStringPattern));
    private static MethodInfo s_readAllLines = AccessTools.Method(typeof(File), nameof(File.ReadAllLines), [typeof(string)]);

    private static List<GameObject> s_mergedMeshes = new();
    private static AssetBundle s_modBundle;
    private static Mesh s_optimizedCube;
    private static Mesh s_optimizedStairs;

    public static int MainGridSize => GridSize >= 16 ? 16 : GridSize; //if the size > 16 the main baked mesh should only be 16, otherwise it should be less.

    private void Start()
    {
        string modPath = Assembly.GetExecutingAssembly().Location.Substring(0, Assembly.GetExecutingAssembly().Location.LastIndexOf(Path.DirectorySeparatorChar));
        s_modBundle = AssetBundle.LoadFromFile(Path.Combine(modPath, "bigcgassets.bundle"));
        s_optimizedCube = s_modBundle.LoadAsset<Mesh>("optimizedgridcube.dae");
        s_optimizedStairs = s_modBundle.LoadAsset<Mesh>("optimizedstairs.dae");

        ConfigBuilder config = new(Guid, Name);
        config.Build();

        // this mod is all patches :3
        ModEnabled.OnValueChanged = value =>
        {
            if (value)
            {
                s_harmony.PatchAll(typeof(Plugin));
            }
            else
            {
                s_harmony.UnpatchSelf();
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
                instruction.operand = s_gridSize;
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
                yield return new CodeInstruction(OpCodes.Ldsfld, s_gridSize);
                yield return new CodeInstruction(OpCodes.Ldc_I4_3);
                yield return new CodeInstruction(OpCodes.Mul);
                yield return new CodeInstruction(OpCodes.Conv_R4);
                continue;
            }

            if (instruction.opcode == OpCodes.Ldc_I4_S && instruction.OperandIs(48))
            {
                yield return new CodeInstruction(OpCodes.Ldsfld, s_gridSize);
                yield return new CodeInstruction(OpCodes.Ldc_I4_3);
                yield return new CodeInstruction(OpCodes.Mul);
                continue;
            }

            yield return instruction;
        }
    }

    [HarmonyPatch(typeof(CustomPatterns), nameof(CustomPatterns.LoadPattern)), HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> CustomPatternsLoadPattern(IEnumerable<CodeInstruction> instructions)
    {
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.opcode == OpCodes.Ldc_I4_S)
            {
                switch ((sbyte)instruction.operand)
                {
                    case 15:
                        instruction.opcode = OpCodes.Ldsfld;
                        instruction.operand = s_gridSize;
                        yield return instruction;
                        yield return new CodeInstruction(OpCodes.Ldc_I4_1);
                        yield return new CodeInstruction(OpCodes.Sub);
                        continue;
                    case 16:
                        instruction.opcode = OpCodes.Ldsfld;
                        instruction.operand = s_gridSize;
                        yield return instruction;
                        continue;
                    case 17:
                        instruction.opcode = OpCodes.Ldsfld;
                        instruction.operand = s_gridSize;
                        yield return instruction;
                        yield return new CodeInstruction(OpCodes.Ldc_I4_1);
                        yield return new CodeInstruction(OpCodes.Add);
                        continue;
                    case 32:
                        instruction.opcode = OpCodes.Ldsfld;
                        instruction.operand = s_gridSize;
                        yield return instruction;
                        yield return new CodeInstruction(OpCodes.Ldc_I4_2);
                        yield return new CodeInstruction(OpCodes.Mul);
                        continue;
                    case 33:
                        instruction.opcode = OpCodes.Ldsfld;
                        instruction.operand = s_gridSize;
                        yield return instruction;
                        yield return new CodeInstruction(OpCodes.Ldc_I4_2);
                        yield return new CodeInstruction(OpCodes.Mul);
                        yield return new CodeInstruction(OpCodes.Ldc_I4_1);
                        yield return new CodeInstruction(OpCodes.Add);
                        continue;
                }
            }

            yield return instruction;

            //fixes custom patterns
            if (instruction.opcode == OpCodes.Call && instruction.OperandIs(s_readAllLines))
            {
                yield return new CodeInstruction(OpCodes.Call, s_fixStringPattern);
            }
        }
    }

    [HarmonyPatch(typeof(EndlessGrid), nameof(EndlessGrid.LoadPattern)), HarmonyPrefix]
    private static void FixPatterns(EndlessGrid __instance, ArenaPattern pattern)
    {
        PatternFixer.FixPattern(pattern);
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

        if (__instance.activateFirst)
        {
            __instance.primaryStairs.position = __instance.transform.position;
        }

        if (__instance.activateSecond)
        {
            __instance.secondaryStairs.position = __instance.transform.position;
        }
    }

    [HarmonyPatch(typeof(EndlessGrid), nameof(EndlessGrid.GetEnemies)), HarmonyPrefix]
    private static void MultiplyPoints(EndlessGrid __instance)
    {
        __instance.maxPoints *= Multiplier;
        Debug.Log("Multiplying points to " + __instance.maxPoints);
    }

    [HarmonyPatch(typeof(EndlessGrid), nameof(EndlessGrid.GetNextEnemy)), HarmonyPrefix]
    private static void CheckIfWasLastEnemy(EndlessGrid __instance, ref bool __state)
    {
        __state = __instance.enemyAmount == __instance.tempEnemyAmount;
    }

    [HarmonyPatch(typeof(EndlessGrid), nameof(EndlessGrid.GetNextEnemy)), HarmonyPostfix]
    private static void DividePoints(EndlessGrid __instance, ref bool __state)
    {
        if (__state || __instance.enemyAmount != __instance.tempEnemyAmount) //if it wasnt true but is now, continue. this is set at the end of getnextenemy
        {
            return;
        }

        __instance.maxPoints /= Multiplier;
        Debug.Log("Dividing points to " + __instance.maxPoints);
    }

    [HarmonyPatch(typeof(EndlessCube), nameof(EndlessCube.Awake)), HarmonyPostfix]
    private static void ReplaceCubeModel(EndlessCube __instance)
    {
        if (!OptimizedModel)
        {
            return;
        }

        __instance.MeshFilter.mesh = s_optimizedCube;
    }

    [HarmonyPatch(typeof(EndlessStairs), nameof(EndlessStairs.Start)), HarmonyPostfix]
    private static void ReplaceStairModel(EndlessStairs __instance)
    {
        if (!OptimizedStairs)
        {
            return;
        }

        __instance.PrimaryMeshFilter.mesh = s_optimizedStairs;
        __instance.SecondaryMeshFilter.mesh = s_optimizedStairs;
    }
}
