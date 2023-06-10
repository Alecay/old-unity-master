using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
public class MarchingCubesGPUMesh : MonoBehaviour
{
    [Header("Sizing & Positions")]
    public float voxelScale = 1f;
    public int width;
    public int height;
    public Vector2Int position;

    [Header("Meshing")]
    public bool updateMeshOnStart = false;
    public int triangleCount = 0;
    [Range(-1, 1)]
    public float isoLevel = 0f;
    [Range(0, 8)]
    public int meshSimplificationLevel = 0;

    [Header("Noise Information")]
    public DensityGenerator.NoiseData noiseData;

    //Mesh data    
    [HideInInspector] public Mesh renderMesh;
    [HideInInspector] public MeshFilter filter;

    [HideInInspector] public Mesh collisionMesh;
    [HideInInspector] public MeshCollider mCollider;

    //Mesh Buffers
    GraphicsBuffer verticesBuffer;
    GraphicsBuffer normalsBuffer;
    GraphicsBuffer colorsBuffer;
    GraphicsBuffer uvsBuffer;

    //Compute Shaders
    [Header("Compute Shaders")]
    public ComputeShader marchComputeShader;
    public ComputeShader densityComputeShader;

    //Density Generator to create density values
    public DensityGenerator densityGenerator;

    //Marching Cubes Mesh Buffers
    public ComputeBuffer trisPerVoxelBuffer;
    public ComputeBuffer overallTrisBuffer;

    //Voxel ID info & buffers
    public ComputeBuffer voxelIDsBuffer;
    public ComputeBuffer voxelColorsBuffer;
    private int[] voxelIDs;
    private Color[] voxelColors;

    //Modified density values
    public ComputeBuffer modifiedBuffer;
    private Dictionary<Vector3Int, float> modifiedDensityValues = new Dictionary<Vector3Int, float>();

    [Header("Digging")]
    public Transform digPoint;
    private Vector3Int lastDigPosition;

    private bool waitingForMeshUpdate = false;
    private bool waitingForTriangleCount = false;

    public bool allowDig = false;

    private Vector3Int[] digOffsets;
    private float[] digValues;

    private void Start()
    {
        mCollider = GetComponent<MeshCollider>();

        List<Vector3Int> offsets = new List<Vector3Int>();
        List<float> values = new List<float>();
        Vector3Int point;
        int size = 5;

        for (int x = -size; x <= size; x++)
        {
            for (int y = -size; y <= size; y++)
            {
                for (int z = -size; z <= size; z++)
                {
                    point = new Vector3Int(x, y, z);

                    if(point.magnitude < size)
                    {
                        offsets.Add(point);
                        values.Add(1 - point.magnitude / (float)size);
                    }
                }
            }
        }

        digOffsets = offsets.ToArray();
        digValues = values.ToArray();

        voxelIDs = new int[width * height * width];
        voxelColors = new Color[] {Color.blue, Color.black, Color.green, Color.white };

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                for (int z = 0; z < width; z++)
                {
                    voxelIDs[x + y * width * width + z * width] = 0;


                    if (y > 25)
                    {
                        voxelIDs[x + y * width * width + z * width] = 2;
                    }

                    if (y > 50)
                    {
                        voxelIDs[x + y * width * width + z * width] = 3;
                    }
                }
            }
        }

        modifiedDensityValues = new Dictionary<Vector3Int, float>();
        if(updateMeshOnStart)
            UpdateMesh();
    }

    private void OnDisable()
    {
        Dispose();

        densityGenerator?.Deinitialize();

        trisPerVoxelBuffer?.Dispose();

        overallTrisBuffer?.Dispose();
    }

    private void Dispose()
    {
        verticesBuffer?.Dispose();
        verticesBuffer = null;

        normalsBuffer?.Dispose();
        normalsBuffer = null;

        colorsBuffer?.Dispose();
        colorsBuffer = null;

        uvsBuffer?.Dispose();
        uvsBuffer = null;

        modifiedBuffer?.Dispose();
        modifiedBuffer = null;

        voxelIDsBuffer?.Dispose();
        voxelIDsBuffer = null;

        voxelColorsBuffer?.Dispose();
        voxelColorsBuffer = null;
    }

    private void Update()
    {
        Vector3Int point = new Vector3Int(Mathf.FloorToInt(digPoint.position.x), Mathf.FloorToInt(digPoint.position.y), Mathf.FloorToInt(digPoint.position.z));
        point -= new Vector3Int((int)noiseData.offset.x, (int)noiseData.offset.y, (int)noiseData.offset.z);
        Vector3Int chunkPos = new Vector3Int(Mathf.FloorToInt(digPoint.position.x) / width, 0, Mathf.FloorToInt(digPoint.position.z) / width);
        Vector3Int meshChunkPos = new Vector3Int((int)noiseData.offset.x, 0, (int)noiseData.offset.z) / width;
        bool changedPos = false;
        bool changed = false;
        bool inChunk = (chunkPos - meshChunkPos).magnitude <= 1;
        if (point != lastDigPosition)
        {
            lastDigPosition = point;
            changedPos = true;
        }

        if (changedPos && inChunk && allowDig)
        {
            for (int i = 0; i < digOffsets.Length; i++)
            {
                if (AddModifiedDensityValue(digOffsets[i] + point, digValues[i]) && !changed)
                {
                    changed = true;
                }
            }

            if (changed)
                UpdateMesh();
        }            
    }

    public bool AddModifiedDensityValue(Vector3Int position, float value)
    {
        bool changed = false;
        bool inBounds = (position.x >= 0 && position.x <= width && position.y > 0 && position.y <= height && position.z >= 0 && position.z <= width);

        if (!inBounds)
        {
            return false;
        }

        int voxelIndex = voxelIDs[position.x + position.y * width * width + position.z * width];

        if(voxelIndex == 0)
        {
            value /= 4f;
        }

        if (modifiedDensityValues.TryGetValue(position, out float density))
        {            
            float clamped = Mathf.Clamp(modifiedDensityValues[position] + value, -1, 1);
            changed = density != clamped;
            modifiedDensityValues[position] = clamped;
        }
        else
        {
            changed = true;
            modifiedDensityValues.Add(position, value);            
        }

        return changed;
    }

    private Vector4[] GetModifiedArray()
    {
        Vector3Int[] keys = new Vector3Int[modifiedDensityValues.Count];
        modifiedDensityValues.Keys.CopyTo(keys, 0);
        List<Vector4> modified = new List<Vector4>();
        Vector3Int key;

        for (int i = 0; i < keys.Length; i++)
        {
            key = keys[i];
            modified.Add(new Vector4(key.x, key.y, key.z, modifiedDensityValues[key]));
        }

        return modified.ToArray();
    }

    private void UpdateTriangleCount()
    {
        if(densityGenerator == null)
        {
            densityGenerator = new DensityGenerator(densityComputeShader, width + 1, height + 1, noiseData);
            densityGenerator.deinitializeAfterUpdate = false;
            densityGenerator.UpdateData();
        }

        if(densityGenerator.width != width + 1 || densityGenerator.height != height + 1 || densityGenerator.noiseData != noiseData)
        {
            densityGenerator.Deinitialize();
            densityGenerator.width = width + 1;
            densityGenerator.height = height + 1;
            densityGenerator.noiseData.SetValues(noiseData);
            densityGenerator.UpdateData();
        }

        trisPerVoxelBuffer = new ComputeBuffer(width * width * height, sizeof(int), ComputeBufferType.Structured);
        overallTrisBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Structured);

        marchComputeShader.SetBuffer(0, "_Density_Values_Buffer", densityGenerator.dataBuffer);
        marchComputeShader.SetBuffer(0, "_Triangles_Per_Voxel_Buffer", trisPerVoxelBuffer);

        if (modifiedBuffer == null || modifiedBuffer.count != modifiedDensityValues.Count)
        {
            modifiedBuffer?.Dispose();
            modifiedBuffer = new ComputeBuffer(modifiedDensityValues.Count == 0 ? 1 : modifiedDensityValues.Count, sizeof(float) * 4);
        }

        modifiedBuffer.SetData(GetModifiedArray());
        marchComputeShader.SetBuffer(0, "_Modified_Density_Values_Buffer", modifiedBuffer);
        marchComputeShader.SetInt("ModifiedLength", modifiedDensityValues.Count);

        marchComputeShader.SetInt("Width", width);
        marchComputeShader.SetInt("Height", height);
        marchComputeShader.SetFloat("isoLevel", isoLevel);
        marchComputeShader.SetBool("interpolate", true);
        marchComputeShader.SetInt("meshSimplificationLevel", meshSimplificationLevel);

        int kThreadCount = 8;
        int dispatch = Mathf.CeilToInt(width / (float)kThreadCount);
        int dispatchY = Mathf.CeilToInt(height / (float)kThreadCount);
        marchComputeShader.Dispatch(0, dispatch, dispatchY, dispatch);

        marchComputeShader.SetBuffer(1, "_Triangles_Per_Voxel_Buffer", trisPerVoxelBuffer);
        marchComputeShader.SetBuffer(1, "_Overall_Triangle_Count_Buffer", overallTrisBuffer);

        marchComputeShader.Dispatch(1, 1, 1, 1);

        //RequestTriangleCount();

        int[] tris = new int[1];
        overallTrisBuffer.GetData(tris);
        triangleCount = tris[0];
    }

    private void RunCompute()
    {
        //Mesh creation
        marchComputeShader.SetBuffer(2, "_Density_Values_Buffer", densityGenerator.dataBuffer);
        marchComputeShader.SetBuffer(2, "_Triangles_Per_Voxel_Buffer", trisPerVoxelBuffer);


        if(modifiedBuffer == null || modifiedBuffer.count != modifiedDensityValues.Count)
        {
            modifiedBuffer?.Dispose();
            modifiedBuffer = new ComputeBuffer(modifiedDensityValues.Count == 0 ? 1 : modifiedDensityValues.Count, sizeof(float) * 4);
        }

        modifiedBuffer.SetData(GetModifiedArray());
        marchComputeShader.SetBuffer(2, "_Modified_Density_Values_Buffer", modifiedBuffer);
        marchComputeShader.SetInt("ModifiedLength", modifiedDensityValues.Count);

        if(voxelIDs == null || voxelIDs.Length != width * width * height)
        {
            voxelIDs = new int[width * height * width];            

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int z = 0; z < width; z++)
                    {
                        voxelIDs[x + y * width * width + z * width] = 0;


                        if (y > 25)
                        {
                            voxelIDs[x + y * width * width + z * width] = 2;
                        }

                        if (y > 50)
                        {
                            voxelIDs[x + y * width * width + z * width] = 3;
                        }
                    }
                }
            }
        }

        if(voxelColors == null || voxelColors.Length == 0)
        {
            voxelColors = new Color[] { Color.blue, Color.black, Color.green, Color.white };
        }

        if (voxelIDsBuffer == null || voxelIDsBuffer.count != voxelIDs.Length)
        {
            voxelIDsBuffer?.Dispose();
            voxelIDsBuffer = new ComputeBuffer(voxelIDs.Length, sizeof(int));
            voxelIDsBuffer.SetData(voxelIDs);
        }

        marchComputeShader.SetBuffer(2, "_Voxel_IDs_Buffer", voxelIDsBuffer);

        if (voxelColorsBuffer == null || voxelColorsBuffer.count != voxelColors.Length)
        {
            voxelColorsBuffer?.Dispose();
            voxelColorsBuffer = new ComputeBuffer(voxelColors.Length, sizeof(float) * 4);
            voxelColorsBuffer.SetData(voxelColors);
        }

        marchComputeShader.SetBuffer(2, "_Voxel_Colors_Buffer", voxelColorsBuffer);

        verticesBuffer ??= renderMesh.GetVertexBuffer(0);
        normalsBuffer ??= renderMesh.GetVertexBuffer(1);
        uvsBuffer ??= renderMesh.GetVertexBuffer(2);
        colorsBuffer ??= renderMesh.GetVertexBuffer(3);

        marchComputeShader.SetBuffer(2, "VerticesBuffer", verticesBuffer);
        marchComputeShader.SetBuffer(2, "NormalsBuffer", normalsBuffer);
        marchComputeShader.SetBuffer(2, "UVsBuffer", uvsBuffer);
        marchComputeShader.SetBuffer(2, "ColorsBuffer", colorsBuffer);

        int kThreadCount = 8;
        int dispatch = Mathf.CeilToInt(width / (float)kThreadCount);
        int dispatchY = Mathf.CeilToInt(height / (float)kThreadCount);

        marchComputeShader.Dispatch(2, dispatch, dispatchY, dispatch);

        return;
    }

    public void UpdateMesh()
    {
        if (waitingForTriangleCount)
        {
            return;
        }

        UpdatePosition();

        if (renderMesh == null)
        {
            renderMesh = new Mesh();
            renderMesh.name = "Mesh";
            renderMesh.vertexBufferTarget |= GraphicsBuffer.Target.Raw;
        }

        UpdateTriangleCount();

        if(triangleCount <= 0 ||  triangleCount > 50000)
        {
            return;
        }

        if (verticesBuffer == null || verticesBuffer.count < triangleCount * 3)
        {
            Dispose();

            renderMesh.SetVertexBufferParams(triangleCount * 3,
                new VertexAttributeDescriptor(VertexAttribute.Position, stream: 0),
                new VertexAttributeDescriptor(VertexAttribute.Normal, stream: 1),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, stream: 2),
                new VertexAttributeDescriptor(VertexAttribute.Color, dimension: 4, stream: 3));

            renderMesh.SetIndexBufferParams(triangleCount * 3, IndexFormat.UInt32);
            var ib = new NativeArray<int>(triangleCount * 3, Allocator.Temp);

            int[] indices = new int[triangleCount * 3];

            for (var i = 0; i < triangleCount * 3; i++)
                indices[i] = i;

            ib.CopyFrom(indices);

            renderMesh.SetIndexBufferData(ib, 0, 0, ib.Length, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);

            ib.Dispose();

            var submesh = new SubMeshDescriptor(0, triangleCount * 3, MeshTopology.Triangles);
            Vector3 size = new Vector3(width + 1, height + 1, width + 1);
            submesh.bounds = new Bounds(size / 2f, size);
            renderMesh.SetSubMesh(0, submesh);
            renderMesh.bounds = submesh.bounds;

            filter ??= GetComponent<MeshFilter>();
            filter.sharedMesh = renderMesh;
        }

        if(verticesBuffer != null && verticesBuffer.count != triangleCount * 3)
        {
            var submesh = new SubMeshDescriptor(0, triangleCount * 3, MeshTopology.Triangles);
            Vector3 size = new Vector3(width + 1, height + 1, width + 1);
            submesh.bounds = new Bounds(size / 2f, size);
            renderMesh.SetSubMesh(0, submesh, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
            renderMesh.bounds = submesh.bounds;

            filter ??= GetComponent<MeshFilter>();
            filter.sharedMesh = renderMesh;
        }

        RunCompute();
    }

    public void StartUpdateMeshCo()
    {
        if(!waitingForMeshUpdate)
            StartCoroutine(UpdateMeshCo());
    }

    private IEnumerator UpdateMeshCo()
    {
        waitingForMeshUpdate = true;
        if (waitingForTriangleCount)
        {
            waitingForMeshUpdate = false;
            yield break;
        }

        if (renderMesh == null)
        {
            renderMesh = new Mesh();
            renderMesh.name = "Mesh";
            renderMesh.vertexBufferTarget |= GraphicsBuffer.Target.Raw;
        }

        RequestTriangleCount();

        while (waitingForTriangleCount)
        {
            yield return null;
        }

        if (triangleCount <= 0 || triangleCount > 50000)
        {
            waitingForMeshUpdate = false;
            yield break;
        }

        if (verticesBuffer == null || verticesBuffer.count < triangleCount * 3)
        {
            Dispose();

            renderMesh.SetVertexBufferParams(triangleCount * 3,
                new VertexAttributeDescriptor(VertexAttribute.Position, stream: 0),
                new VertexAttributeDescriptor(VertexAttribute.Normal, stream: 1),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, stream: 2),
                new VertexAttributeDescriptor(VertexAttribute.Color, stream: 3));

            renderMesh.SetIndexBufferParams(triangleCount * 3, IndexFormat.UInt32);
            var ib = new NativeArray<int>(triangleCount * 3, Allocator.Temp);

            int[] indices = new int[triangleCount * 3];

            for (var i = 0; i < triangleCount * 3; i++)
                indices[i] = i;

            ib.CopyFrom(indices);

            renderMesh.SetIndexBufferData(ib, 0, 0, ib.Length, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);

            ib.Dispose();

            var submesh = new SubMeshDescriptor(0, triangleCount * 3, MeshTopology.Triangles);
            Vector3 size = new Vector3(width + 1, height + 1, width + 1);
            submesh.bounds = new Bounds(size / 2f, size);
            renderMesh.SetSubMesh(0, submesh);
            renderMesh.bounds = submesh.bounds;

            filter ??= GetComponent<MeshFilter>();
            filter.sharedMesh = renderMesh;
        }

        if (verticesBuffer != null && verticesBuffer.count != triangleCount * 3)
        {
            var submesh = new SubMeshDescriptor(0, triangleCount * 3, MeshTopology.Triangles);
            Vector3 size = new Vector3(width + 1, height + 1, width + 1);
            submesh.bounds = new Bounds(size / 2f, size);
            renderMesh.SetSubMesh(0, submesh, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
            renderMesh.bounds = submesh.bounds;
        }

        RunCompute();

        waitingForMeshUpdate = false;
    }

    public void RequestTriangleCount()
    {
        if (waitingForTriangleCount)
        {
            Debug.LogError("Requested data but another request is ongoing");
            return;
        }

        waitingForTriangleCount = true;

        if (densityGenerator == null)
        {
            densityGenerator = new DensityGenerator(densityComputeShader, width + 1, height + 1, noiseData);
            densityGenerator.deinitializeAfterUpdate = false;
            densityGenerator.UpdateData();
        }

        if (densityGenerator.width != width + 1 || densityGenerator.height != height + 1 || densityGenerator.noiseData != noiseData)
        {
            densityGenerator.Deinitialize();
            densityGenerator.width = width + 1;
            densityGenerator.height = height + 1;
            densityGenerator.noiseData.SetValues(noiseData);
            densityGenerator.UpdateData();
        }

        trisPerVoxelBuffer = new ComputeBuffer(width * width * height, sizeof(int), ComputeBufferType.Structured);
        overallTrisBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Structured);

        marchComputeShader.SetBuffer(0, "_Density_Values_Buffer", densityGenerator.dataBuffer);
        marchComputeShader.SetBuffer(0, "_Triangles_Per_Voxel_Buffer", trisPerVoxelBuffer);

        if (modifiedBuffer == null || modifiedBuffer.count != modifiedDensityValues.Count)
        {
            modifiedBuffer?.Dispose();
            modifiedBuffer = new ComputeBuffer(modifiedDensityValues.Count == 0 ? 1 : modifiedDensityValues.Count, sizeof(float) * 4);
        }

        modifiedBuffer.SetData(GetModifiedArray());
        marchComputeShader.SetBuffer(0, "_Modified_Density_Values_Buffer", modifiedBuffer);
        marchComputeShader.SetInt("ModifiedLength", modifiedDensityValues.Count);

        marchComputeShader.SetInt("Width", width);
        marchComputeShader.SetInt("Height", height);
        marchComputeShader.SetFloat("isoLevel", isoLevel);
        marchComputeShader.SetBool("interpolate", true);
        marchComputeShader.SetInt("meshSimplificationLevel", meshSimplificationLevel);

        int kThreadCount = 8;
        int dispatch = Mathf.CeilToInt(width / (float)kThreadCount);
        int dispatchY = Mathf.CeilToInt(height / (float)kThreadCount);
        marchComputeShader.Dispatch(0, dispatch, dispatchY, dispatch);

        marchComputeShader.SetBuffer(1, "_Triangles_Per_Voxel_Buffer", trisPerVoxelBuffer);
        marchComputeShader.SetBuffer(1, "_Overall_Triangle_Count_Buffer", overallTrisBuffer);

        marchComputeShader.Dispatch(1, 1, 1, 1);

        AsyncGPUReadback.Request(overallTrisBuffer, r1 => OnTriangleCountAvalible(r1));
    }

    protected void OnTriangleCountAvalible(AsyncGPUReadbackRequest request)
    {
        if (request.hasError || !Application.isPlaying)
        {
            waitingForTriangleCount = false;
            return;
        }
        
        var data = request.GetData<int>();
        triangleCount = data[0];

        data.Dispose();

        waitingForTriangleCount = false;
    }

    public void UpdatePosition()
    {
        transform.position = new Vector3(position.x, 0, position.y) * width * voxelScale;
        noiseData.offset = new Vector3(position.x, 0, position.y) * width;
    }

    public void SetCollisonMesh(Mesh collisionMesh)
    {
        this.collisionMesh = collisionMesh;
        mCollider ??= GetComponent<MeshCollider>();
        mCollider.sharedMesh = collisionMesh;
    }

    private void OnDrawGizmosSelected()
    {
        if (MCCEditor.showGizmos)
        {
            if (meshSimplificationLevel == 0)
            {
                Gizmos.color = Color.blue;
            }
            else if (meshSimplificationLevel == 1)
            {
                Gizmos.color = Color.yellow;
            }
            else
            {
                Gizmos.color = Color.red;
            }


            Vector3 size = new Vector3(width, height, width);
            Vector3 center = size * 0.5f + new Vector3(position.x, 0, position.y) * width * voxelScale;

            Gizmos.DrawWireCube(center, size);
        }

    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(MarchingCubesGPUMesh))]
[CanEditMultipleObjects]
public class MCCEditor : Editor
{
    public static bool showGizmos = false;
    static MarchingCubesGPUMesh marchingCubes;

    private void OnEnable()
    {
        marchingCubes = target as MarchingCubesGPUMesh;
    }

    public override void OnInspectorGUI()
    {
        showGizmos = EditorGUILayout.Toggle("Show Gizmos", showGizmos);

        base.OnInspectorGUI();

        if (GUI.changed && Application.isPlaying)
        {
            marchingCubes.UpdateMesh();
        }
    }
}

#endif
