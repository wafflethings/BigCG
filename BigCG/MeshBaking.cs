using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BigCG;

public static class MeshBaking
{
    public const int MaxCubesPerMesh = 150; //you need a good amount to minimize mesh count, but not go over 2^16 verts
    public const int MaxStairsPerMesh = 150; //same thing

    private static void CreateCombinedObject(out GameObject gameObject, out MeshRenderer renderer, out MeshFilter filter)
    {
        gameObject = new("Combined Static Mesh (BigCG)")
        {
            transform = { parent = EndlessGrid.Instance.transform },
            layer = LayerMask.NameToLayer("Outdoors")
        };

        renderer = gameObject.AddComponent<MeshRenderer>();
        filter = gameObject.AddComponent<MeshFilter>();
    }

    private static void CompleteMesh(Mesh mesh, CombineInstance[] combineInstances)
    {
        mesh.CombineMeshes(combineInstances, true, true);
        mesh.Optimize();
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        mesh.UploadMeshData(false);
    }

    public static GameObject CreateCubeMesh(int start, int amount)
    {
        CreateCombinedObject(out GameObject meshObject, out MeshRenderer meshRenderer, out MeshFilter meshFilter);
        List<CombineInstance> combineInstances = new();
        Material[] cubeMaterials = EndlessGrid.Instance.cubes[0][0].MeshRenderer.sharedMaterials;

        for (int j = 0; j < cubeMaterials.Length; j++)
        {
            for (int i = 0; i < amount; i++)
            {
                int row = (i + start) / Plugin.GridSize;
                EndlessCube thisCube = EndlessGrid.Instance.cubes[row][(i + start) - (row * Plugin.GridSize)];

                if ((bool)thisCube)
                {
                    combineInstances.Add(new CombineInstance
                    {
                        transform = thisCube.MeshRenderer.localToWorldMatrix,
                        mesh = thisCube.MeshFilter.sharedMesh,
                        subMeshIndex = j
                    });

                    thisCube.MeshRenderer.enabled = false;
                }
            }
        }

        Mesh resultMesh = new();
        CompleteMesh(resultMesh, combineInstances.ToArray());
        meshFilter.sharedMesh = resultMesh;
        meshRenderer.sharedMaterials = cubeMaterials;
        meshRenderer.enabled = true;

        return meshObject;
    }

    public static GameObject CreateStairsMesh(int start, int amount)
    {
        CreateCombinedObject(out GameObject meshObject, out MeshRenderer meshRenderer, out MeshFilter meshFilter);
        List<CombineInstance> combineInstances = new();
        Material[] cubeMaterials = EndlessGrid.Instance.cubes[0][0].MeshRenderer.sharedMaterials;

        int index = 0;
        foreach (GameObject spawnedPrefab in EndlessGrid.Instance.spawnedPrefabs.Where(x => x.GetComponent<EndlessStairs>() != null))
        {
            if (index > start + amount || index < start)
            {
                continue;
            }

            index++;
            EndlessStairs stairs = spawnedPrefab.GetComponent<EndlessStairs>();

            if (stairs.ActivateFirst)
            {
                combineInstances.Add(new CombineInstance
                {
                    transform = stairs.PrimaryMeshRenderer.localToWorldMatrix,
                    mesh = stairs.PrimaryMeshFilter.sharedMesh
                });

                stairs.PrimaryMeshRenderer.enabled = false;
            }

            if (stairs.ActivateSecond)
            {
                combineInstances.Add(new CombineInstance
                {
                    transform = stairs.SecondaryMeshRenderer.localToWorldMatrix,
                    mesh = stairs.SecondaryMeshFilter.sharedMesh
                });

                stairs.SecondaryMeshRenderer.enabled = false;
            }
        }

        Mesh resultMesh = new();
        CompleteMesh(resultMesh, combineInstances.ToArray());
        meshFilter.sharedMesh = resultMesh;
        meshRenderer.sharedMaterials = cubeMaterials;
        meshRenderer.enabled = true;

        return meshObject;
    }
}
