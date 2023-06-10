using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class HexProceduralMeshRenderer : ProceduralMeshRenderer
{
    [Space(10)]
    [Header("Hex Variables")]
    public Vector2Int size = new Vector2Int(10, 10);
    private Vector2Int sizeOnLastGen = new Vector2Int(0, 0);
    public const int MAX_LENGTH = 500;
    private bool[] enabledHexes = new bool[1];
    private float[] hexHeights = new float[1];

    private ComputeBuffer enabledBuffer;
    private ComputeBuffer heightsBuffer;

    public float radius = 1;
    public float spacing = 0f;

    public bool useCenteredUvs = false;

    public enum Orientation { OddR, EvenR, OddQ, EvenQ };
    public Orientation orientation;

    private void ValidateSize()
    {
        size = new Vector2Int(Mathf.Clamp(size.x, 1, MAX_LENGTH), Mathf.Clamp(size.y, 1, MAX_LENGTH));
    }

    private void ValidateEnabledHexes()
    {
        if (enabledHexes == null || enabledHexes.Length != size.x * size.y)
        {
            enabledHexes = new bool[size.x * size.y];

            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    int index = x + y * size.x;

                    enabledHexes[index] = true;
                }
            }
        }
    }

    private void ValidateHexHeights()
    {
        if (hexHeights == null || hexHeights.Length != size.x * size.y)
        {
            hexHeights = new float[size.x * size.y];

            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    int index = x + y * size.x;

                    hexHeights[index] = Mathf.Max(x, y);
                }
            }
        }
    }

    private float[] GetEnabledHexesFloats()
    {
        ValidateSize();
        ValidateEnabledHexes();

        float[] values = new float[size.x * size.y];

        for (int y = 0; y < size.y; y++)
        {
            for (int x = 0; x < size.x; x++)
            {
                int index = x + y * size.x;
                values[index] = enabledHexes[index] ? 1f : 0f;
            }
        }

        return values;
    }

    protected void Start()
    {
        UpdateMesh();
    }

    public override void OnDisable()
    {        
        if (initialized)
        {
            enabledBuffer.Release();
        }

        base.OnDisable();
    }

    public override void OnEnable()
    {
        base.OnEnable();

        //Enabled Hexes
        enabledBuffer = new ComputeBuffer(size.x * size.y, sizeof(float));
        enabledBuffer.SetData(GetEnabledHexesFloats());

        meshGenComputeShader.SetBuffer(idMeshGenKernel, "Enabled_Hexes_Buffer", enabledBuffer);

        ValidateHexHeights();

        //Hex Heights
        heightsBuffer = new ComputeBuffer(size.x * size.y, sizeof(float));
        heightsBuffer.SetData(hexHeights);

        meshGenComputeShader.SetBuffer(idMeshGenKernel, "Heights_Buffer", heightsBuffer);

        continuousMeshUpdates = false;
    }

    protected override void UpdateDispatchTimes()
    {
        dispatchTimes = new Vector3Int(size.x, size.y, 1);
    }

    protected override void UpdateMaxTriangles()
    {
        maxTriangles = (uint)(size.x * size.y * 4);
    }

    //2 100 85 6
    protected override void SetComputeVariables()
    {
        base.SetComputeVariables();

        meshGenComputeShader.SetInt("_XWidth", size.x);
        meshGenComputeShader.SetInt("_YWidth", size.y);

        meshGenComputeShader.SetFloat("_Radius", radius);
        meshGenComputeShader.SetFloat("_Spacing", spacing);

        meshGenComputeShader.SetInt("_Orientation", (int)orientation);

        meshGenComputeShader.SetFloat("_UseCenteredUvs", useCenteredUvs ? 1f : 0f);

        enabledBuffer.SetCounterValue(0);

        if(sizeOnLastGen != size)
        {
            sizeOnLastGen = size;
            shouldUpdateMeshThisFrame = true;
            OnEnable();
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(HexProceduralMeshRenderer))]
public class HPMREditor : Editor
{
    HexProceduralMeshRenderer meshRenderer;

    private void OnEnable()
    {
        meshRenderer = target as HexProceduralMeshRenderer;
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (GUI.changed && Application.isPlaying && Application.isEditor)
        {
            meshRenderer.shouldUpdateMeshThisFrame = true;
        }
    }
}
#endif
