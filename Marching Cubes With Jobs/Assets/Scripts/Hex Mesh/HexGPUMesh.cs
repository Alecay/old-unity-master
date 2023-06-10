using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class HexGPUMesh : MonoBehaviour
{
    [Header("Debug / Info")]
    public int vertexCount = 0;

    [Header("Hex Variables")]
    public Vector2Int size = new Vector2Int(10, 10);
    public const int MAX_LENGTH = 500;
    private int[] hexIndices = new int[1];
    private float[] hexHeights = new float[1];

    private ComputeBuffer hexIndicesBuffer;
    private ComputeBuffer heightsBuffer;

    public float radius = 1;
    public float spacing = 0f;

    public bool useCenteredUvs = false;
    
    public Hex.Orientation orientation;

    public Color color;
    
    GraphicsBuffer verticesBuffer;
    GraphicsBuffer normalsBuffer;
    GraphicsBuffer colorsBuffer;
    GraphicsBuffer uvsBuffer;

    [Header("Mesh Variables")]
    public Mesh mesh;
    public Mesh collisionMesh;
    public MeshFilter filter;
    public MeshRenderer mRenderer;   

    public ComputeShader computeShader;
    private Bounds meshBounds;

    [Header("Textures")]
    public List<Texture2D> textures = new List<Texture2D>();

    private void OnValidate()
    {
        radius = Mathf.Clamp(radius, 1 / 10000f, float.MaxValue);
        spacing = Mathf.Clamp(spacing, 0, float.MaxValue);
        size = new Vector2Int(Mathf.Clamp(size.x, 1, MAX_LENGTH), Mathf.Clamp(size.y, 1, MAX_LENGTH));
    }

    private void Start()
    {
        filter = GetComponent<MeshFilter>();
        mRenderer = GetComponent<MeshRenderer>();

        //UpdateMesh();
    }

    private void OnDisable()
    {
        Dispose();
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

        hexIndicesBuffer?.Dispose();
        hexIndicesBuffer = null;

        heightsBuffer?.Dispose();
        heightsBuffer = null;
    }

    private void Update()
    {
        //UpdateMesh();
    }

    private int GetEnabledHexesCount()
    {
        int count = 0;
        for (int i = 0; i < hexIndices.Length; i++)
        {
            if (hexIndices[i] > 0)
                count++;
        }

        return count;
    }

    private void InitializeHexData()
    {
        int numOfHexes = size.x * size.y;

        if (hexIndices.Length != numOfHexes)
        {
            hexIndices = new int[numOfHexes];
            for (int i = 0; i < numOfHexes; i++)
            {
                hexIndices[i] = Random.Range(0, textures.Count + 1);
            }
        }

        if (hexHeights.Length != numOfHexes)
        {
            hexHeights = new float[numOfHexes];
            for (int i = 0; i < numOfHexes; i++)
            {
                hexHeights[i] = 0;
            }
        }

    }

    public void SetHexIndices(int[] indices)
    {
        int numOfHexes = size.x * size.y;

        if (hexIndices.Length != numOfHexes)
        {
            hexIndices = new int[numOfHexes];
        }

        indices.CopyTo(hexIndices, 0);
    }

    public void SetHexHeights(float[] heights)
    {
        int numOfHexes = size.x * size.y;

        if (hexHeights.Length != numOfHexes)
        {
            hexHeights = new float[numOfHexes];
        }

        heights.CopyTo(hexHeights, 0);
    }

    private void UpdateBounds()
    {
        ComputeBuffer boundsBuffer = new ComputeBuffer(2, sizeof(float) * 2);
        boundsBuffer.SetData(new Vector2[2]);

        computeShader.SetInt("_XWidth", size.x);
        computeShader.SetInt("_YWidth", size.y);

        computeShader.SetInt("_Orientation", (int)orientation);

        computeShader.SetFloat("_Radius", radius);
        computeShader.SetFloat("_Spacing", spacing);

        computeShader.SetFloat("_UseCenteredUvs", useCenteredUvs ? 1 : 0);

        computeShader.SetVector("_Color", color);

        //Run Calculate bounds
        computeShader.SetBuffer(1, "Bounds_Buffer", boundsBuffer);

        computeShader.Dispatch(1, 1, 1, 1);

        Vector2[] data = new Vector2[2];

        boundsBuffer.GetData(data);
        boundsBuffer.Dispose();

        meshBounds = new Bounds(data[0], new Vector3(data[1].x, data[1].y, 0.1f));
    }

    private void UpdateBuffers()
    {
        int numOfHexes = size.x * size.y;

        if(hexIndicesBuffer == null || hexIndicesBuffer.count != numOfHexes)
        {
            hexIndicesBuffer?.Dispose();
            hexIndicesBuffer = null;

            heightsBuffer?.Dispose();
            heightsBuffer = null;

            hexIndicesBuffer = new ComputeBuffer(numOfHexes, sizeof(float));
            heightsBuffer = new ComputeBuffer(numOfHexes, sizeof(float));

            hexIndicesBuffer.SetData(hexIndices);
            heightsBuffer.SetData(hexHeights);
        }

    }

    private void RunCompute()
    {
        verticesBuffer ??= mesh.GetVertexBuffer(0);
        normalsBuffer ??= mesh.GetVertexBuffer(1);
        uvsBuffer ??= mesh.GetVertexBuffer(2);
        colorsBuffer ??= mesh.GetVertexBuffer(3);

        //UpdateBuffers();

        computeShader.SetInt("_XWidth", size.x);
        computeShader.SetInt("_YWidth", size.y);

        computeShader.SetInt("_Orientation", (int)orientation);

        computeShader.SetFloat("_Radius", radius);
        computeShader.SetFloat("_Spacing", spacing);

        computeShader.SetFloat("_UseCenteredUvs", useCenteredUvs ? 1 : 0);

        computeShader.SetVector("_Color", color);

        computeShader.SetBuffer(0, "Hex_Indices_Buffer", hexIndicesBuffer);
        computeShader.SetBuffer(0, "Heights_Buffer", heightsBuffer);        

        computeShader.SetBuffer(0, "VerticesBuffer", verticesBuffer);
        computeShader.SetBuffer(0, "NormalsBuffer", normalsBuffer);
        computeShader.SetBuffer(0, "UVsBuffer", uvsBuffer);
        computeShader.SetBuffer(0, "ColorsBuffer", colorsBuffer);

        hexIndicesBuffer.SetData(hexIndices);
        heightsBuffer.SetData(hexHeights);

        int kThreadCount = 8;
        int dispatchX = Mathf.CeilToInt(size.x / (float)kThreadCount);
        int dispatchY = Mathf.CeilToInt(size.y / (float)kThreadCount);
        computeShader.Dispatch(0, dispatchX, dispatchY, 1);
        return;
    }

    public void UpdateMesh()
    {
        if (mesh == null)
        {
            mesh = new Mesh();
            mesh.name = "Hex Mesh";
            mesh.vertexBufferTarget |= GraphicsBuffer.Target.Raw;
        }

        SetMaterialProperties(mRenderer, textures, new List<Vector4>(textures.Count));

        InitializeHexData();

        int numOfHexes = GetEnabledHexesCount();// size.x * size.y;
        int triangleCount = numOfHexes * 4;
        int indicesCount = triangleCount * 3;
        int vertexCount = numOfHexes * 6;

        this.vertexCount = vertexCount;

        if (verticesBuffer == null || verticesBuffer.count < vertexCount)
        {
            Dispose();

            UpdateBuffers();

            mesh.SetVertexBufferParams(vertexCount,
                new VertexAttributeDescriptor(VertexAttribute.Position, stream: 0),
                new VertexAttributeDescriptor(VertexAttribute.Normal, stream: 1),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, dimension: 3, stream: 2),
                new VertexAttributeDescriptor(VertexAttribute.Color, dimension : 4, stream: 3));

            //mesh.SetIndexBufferParams(vertexCount, IndexFormat.UInt32);
            //var ib = new NativeArray<int>(vertexCount, Allocator.Temp);

            //int[] indices = new int[vertexCount];

            //for (var i = 0; i < vertexCount; i++)
            //    indices[i] = i;

            mesh.SetIndexBufferParams(indicesCount, IndexFormat.UInt32);
            var ib = new NativeArray<int>(indicesCount, Allocator.Temp);

            int[] indices = new int[indicesCount];

            int start = 0;
            int vStart = 0;
            for (var i = 0; i < numOfHexes; i++)
            {
                start = i * 12;
                vStart = i * 6;

                //Triangle 1
                indices[start + 0] = vStart + 0;
                indices[start + 1] = vStart + 2;
                indices[start + 2] = vStart + 1;

                //Triangle 2
                indices[start + 3] = vStart + 0;
                indices[start + 4] = vStart + 3;
                indices[start + 5] = vStart + 2;

                //Triangle 3
                indices[start + 6] = vStart + 0;
                indices[start + 7] = vStart + 4;
                indices[start + 8] = vStart + 3;

                //Triangle 4
                indices[start + 9] = vStart + 0;
                indices[start + 10] = vStart + 5;
                indices[start + 11] = vStart + 4;
            }                

            ib.CopyFrom(indices);

            mesh.SetIndexBufferData(ib, 0, 0, ib.Length, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);

            ib.Dispose();

            var submesh = new SubMeshDescriptor(0, indicesCount, MeshTopology.Triangles);
            UpdateBounds();
            submesh.bounds = meshBounds;
            mesh.SetSubMesh(0, submesh);
            mesh.bounds = submesh.bounds;

            filter ??= GetComponent<MeshFilter>();
            filter.sharedMesh = mesh;
        }

        RunCompute();

        if (verticesBuffer != null && verticesBuffer.count != vertexCount)
        {
            var submesh = new SubMeshDescriptor(0, indicesCount, MeshTopology.Triangles);
            UpdateBounds();
            submesh.bounds = meshBounds;
            mesh.SetSubMesh(0, submesh);
            mesh.bounds = submesh.bounds;

            filter ??= GetComponent<MeshFilter>();
            filter.sharedMesh = mesh;
        }

        //print("Index count " + indicesCount);
    }

    public void SetMaterialProperties(MeshRenderer meshRenderer, List<Texture2D> textures, List<Vector4> animationInfo)
    {
        //Animation Info vector format:
        //the number of frames as x and the speed as y

        if (meshRenderer == null)
        {
            Debug.LogError("Mesh Renderer Is null");
            return;
        }

        Texture2DArray array = new Texture2DArray(textures[0].width, textures[0].height, textures.Count, textures[0].format, false);

        for (int i = 0; i < textures.Count; i++)
        {
            array.SetPixels(textures[i].GetPixels(), i);
        }

        array.filterMode = FilterMode.Point;
        array.wrapMode = TextureWrapMode.Repeat;        
        array.Apply();

        MaterialPropertyBlock props = new MaterialPropertyBlock();
        props.SetTexture("_MainTex", array);
        props.SetFloat("_ArrayLength", textures.Count);
        props.SetVectorArray("_AnimationInfo", animationInfo);
        props.SetVector("_MainTex_ST", useCenteredUvs ? new Vector4(1, 1, 0, 0) : new Vector4(size.x, size.y, 0, 0));        

        //Debug.Log("Created Texture array with " + array.depth + " textures");

        meshRenderer.SetPropertyBlock(props);

        //Debug.Log("Setting material props, animation data length: " + animationInfo.Count);

    }

    public Vector2Int WorldToHexPosition(Vector3 worldPoint)
    {
        return Hex.WorldToHexPosition(orientation, radius, spacing, 0, worldPoint);
    }

    public Vector3 HexCenter(Vector2Int position)
    {
        return Hex.Center(orientation, radius, spacing, 0, position);
    }

    public bool PositionInBounds(Vector2Int position)
    {
        return position.x >= 0 && position.x < size.x && position.y >= 0 && position.y < size.y;
    }
}

public static class Hex
{
    public const int MAX_CAL_RADIUS = 100;
    public enum Orientation { OddR, EvenR, OddQ, EvenQ };

    public static float Width(Orientation orientation, float radius)
    {
        if ((int)orientation < 2)
            return Mathf.Sqrt(3.0f) * radius;
        else
            return 2.0f * radius;
    }

    public static float Height(Orientation orientation, float radius)
    {
        if ((int)orientation < 2)
            return 2.0f * radius;
        else
            return Mathf.Sqrt(3.0f) * radius;
    }

    public static float WidthOffset(Orientation orientation, float radius)
    {
        if ((int)orientation < 2)
        {
            return Mathf.Sqrt(3.0f) * radius;
        }
        else
        {
            return 2.0f * radius * 3.0f / 4.0f;
        }

    }

    public static float HeightOffset(Orientation orientation, float radius)
    {
        if ((int)orientation < 2)
        {
            return 2.0f * radius * 3.0f / 4.0f;
        }
        else
        {
            return Mathf.Sqrt(3.0f) * radius;
        }
    }

    public static Vector3 Center(Orientation orientation, float radius, float spacing, float zHeight, Vector2Int coordinate)
    {
        int x = coordinate.x;
        int y = coordinate.y;

        Vector3 center = Vector3.zero;
        Vector3 offset = Vector3.zero;

        //OddR
        if ((int)orientation == 0)
        {
            if (y % 2 == 1 || y % 2 == -1)
            {
                offset = new Vector3((Width(orientation, radius) + spacing) / 2.0f, 0, 0);
            }
        }
        //EvenR
        else if ((int)orientation == 1)
        {
            if (y % 2 == 0)
            {
                offset = new Vector3((Width(orientation, radius) + spacing) / 2.0f, 0, 0);
            }
        }
        //OddQ
        else if ((int)orientation == 2)
        {
            if (x % 2 == 1 || x % 2 == -1)
            {
                offset = new Vector3(0, (Height(orientation, radius) + spacing) / 2.0f, 0);
            }
        }
        //EvenQ
        else if ((int)orientation == 3)
        {
            if (x % 2 == 0)
            {
                offset = new Vector3(0, (Height(orientation, radius) + spacing) / 2.0f, 0);
            }
        }

        offset = new Vector3(offset.x, offset.y, -zHeight);

        center = new Vector3(x * (WidthOffset(orientation, radius) + spacing), y * (HeightOffset(orientation,radius) + spacing), 0) 
            + offset + new Vector3(Width(orientation, radius), Height(orientation, radius), 0);

        return center;
    }

    public static Vector3 Corner(Orientation orientation, float radius, float spacing, float zHeight, Vector2Int coordinate, uint index)
    {
        Vector3 center = Center(orientation, radius, spacing, zHeight, coordinate);
        index %= 6;

        float startingDegree = ((int)orientation >= 2) ? -0.0f : 30.0f;

        float degree = index * 60.0f + startingDegree;
        float rad = Mathf.Deg2Rad * degree;

        Vector3 corner = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * radius + center;

        //if (moveTowardsCenter > 0)
        //{
        //    moveTowardsCenter = Mathf.Clamp(moveTowardsCenter, 0, Vector3.Distance(point, center));
        //    point = Vector3.MoveTowards(point, center, moveTowardsCenter);
        //}

        return corner;
    }

    public static Vector2Int WorldToHexPosition(Orientation orientation, float radius, float spacing, float zHeight, Vector3 worldPoint)
    {
        float xOffset = WidthOffset(orientation, radius) + spacing;
        float yOffset = HeightOffset(orientation, radius) + spacing;

        int x = Mathf.RoundToInt(worldPoint.x / xOffset);
        int y = Mathf.RoundToInt(worldPoint.y / yOffset);

        float minDist = float.MaxValue;
        float dist;
        Vector3 center;
        Vector2Int closePos = new Vector2Int(0, 0);
        int r = 5;

        for (int nx = x - r; nx <= x + r; nx++)
        {
            for (int ny = y - r; ny <= y + r; ny++)
            {
                center = Center(orientation, radius, spacing, zHeight, new Vector2Int(nx, ny));
                dist = Vector3.Distance(center, worldPoint);
                if(dist < minDist)
                {
                    minDist = dist;
                    closePos = new Vector2Int(nx, ny);
                }
            }
        }

        return closePos;
    }

    public static Vector2Int HexNeighbor(Orientation orientation, Vector2Int position, int index)
    {
        index = Mathf.Clamp(index, 0, 5);

        int x = position.x;
        int y = position.y;
        int calX = x;
        int calY = y;

        switch (orientation)
        {
            case Orientation.OddR:
                if (y % 2 == 0)
                {
                    switch (index)
                    {
                        case 0:
                            calY++;
                            break;
                        case 1:
                            calX--;
                            calY++;
                            break;
                        case 2:
                            calX--;
                            break;
                        case 3:
                            calX--;
                            calY--;
                            break;
                        case 4:
                            calY--;
                            break;
                        case 5:
                            calX++;
                            break;
                        default:
                            break;
                    }

                }
                else
                {
                    switch (index)
                    {
                        case 0:
                            calX++;
                            calY++;
                            break;
                        case 1:
                            calY++;
                            break;
                        case 2:
                            calX--;
                            break;
                        case 3:
                            calY--;
                            break;
                        case 4:
                            calX++;
                            calY--;
                            break;
                        case 5:
                            calX++;
                            break;
                        default:
                            break;
                    }
                }
                break;
            case Orientation.EvenR:
                if (y % 2 == 1)
                {
                    switch (index)
                    {
                        case 0:
                            calY++;
                            break;
                        case 1:
                            calX--;
                            calY++;
                            break;
                        case 2:
                            calX--;
                            break;
                        case 3:
                            calX--;
                            calY--;
                            break;
                        case 4:
                            calY--;
                            break;
                        case 5:
                            calX++;
                            break;
                        default:
                            break;
                    }

                }
                else
                {
                    switch (index)
                    {
                        case 0:
                            calX++;
                            calY++;
                            break;
                        case 1:
                            calY++;
                            break;
                        case 2:
                            calX--;
                            break;
                        case 3:
                            calY--;
                            break;
                        case 4:
                            calX++;
                            calY--;
                            break;
                        case 5:
                            calX++;
                            break;
                        default:
                            break;
                    }
                }
                break;
            case Orientation.OddQ:
                if (x % 2 == 0)
                {
                    switch (index)
                    {
                        case 0:
                            calX++;
                            break;
                        case 1:
                            calY++;
                            break;
                        case 2:
                            calX--;
                            break;
                        case 3:
                            calX--;
                            calY--;
                            break;
                        case 4:
                            calY--;
                            break;
                        case 5:
                            calX++;
                            calY--;
                            break;
                        default:
                            break;
                    }
                }
                else
                {
                    switch (index)
                    {
                        case 0:
                            calX++;
                            calY++;
                            break;
                        case 1:
                            calY++;
                            break;
                        case 2:
                            calX--;
                            calY++;
                            break;
                        case 3:
                            calX--;
                            break;
                        case 4:
                            calY--;
                            break;
                        case 5:
                            calX++;
                            break;
                        default:
                            break;
                    }
                }
                break;
            case Orientation.EvenQ:
                if (x % 2 == 1)
                {
                    switch (index)
                    {
                        case 0:
                            calX++;
                            break;
                        case 1:
                            calY++;
                            break;
                        case 2:
                            calX--;
                            break;
                        case 3:
                            calX--;
                            calY--;
                            break;
                        case 4:
                            calY--;
                            break;
                        case 5:
                            calX++;
                            calY--;
                            break;
                        default:
                            break;
                    }
                }
                else
                {
                    switch (index)
                    {
                        case 0:
                            calX++;
                            calY++;
                            break;
                        case 1:
                            calY++;
                            break;
                        case 2:
                            calX--;
                            calY++;
                            break;
                        case 3:
                            calX--;
                            break;
                        case 4:
                            calY--;
                            break;
                        case 5:
                            calX++;
                            break;
                        default:
                            break;
                    }
                }
                break;
            default:
                break;
        }

        return new Vector2Int(calX, calY);
    }

    //Gets a ring of hexes
    public static Vector2Int[] HexRing(Orientation orientation, Vector2Int center, int radius)
    {
        List<Vector2Int> list = new List<Vector2Int>();

        radius = Mathf.Clamp(radius, 1, MAX_CAL_RADIUS);
        if (radius > 1)
        {
            Vector2Int ringStart = center;
            for (int i = 0; i < radius - 1; i++)
            {
                ringStart = HexNeighbor(orientation, ringStart, 4);
            }

            Vector2Int current = ringStart;
            for (int i = 0; i < 6; i++)
            {
                for (int j = 0; j < radius - 1; j++)
                {
                    Vector2Int before = current;
                    current = HexNeighbor(orientation, current, i);
                    if (current != before)
                    {
                        list.Add(current);
                    }
                }
            }
        }
        else
        {
            return list.ToArray();
        }

        return list.ToArray();
    }

    //Gets the number of hexes in a sipral of input size
    public static int NumberOfHexesInSpiral(int size)
    {
        int count = 0;

        if (size == 1)
        {
            count = 1;
        }
        else if (size > 1)
        {
            count = 1;
            for (int i = size; i > 0; i--)
            {
                count += 6 * (i - 1);
            }
        }

        return count;
    }

    //Gets a spiral, basically a filled ring, in a spiraling shape
    public static Vector2Int[] HexSpiral(Orientation orientation, Vector2Int center, int radius, bool inculdeCenter = true)
    {
        radius = Mathf.Clamp(radius, 1, MAX_CAL_RADIUS);
        List<Vector2Int> list = new List<Vector2Int>();
        int startingRadius = (inculdeCenter) ? 1 : 2;
        for (int i = startingRadius; i <= radius; i++)
        {
            if (HexRing(orientation, center, i) != null)
            {
                list.AddRange(HexRing(orientation, center, i));
            }
        }

        return list.ToArray();
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(HexGPUMesh))]
public class HexGPUMeshEditor : Editor
{
    HexGPUMesh mesh;

    private void OnEnable()
    {
        mesh = target as HexGPUMesh;
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if(GUI.changed && Application.isPlaying)
        {
            mesh.UpdateMesh();
        }
    }
}
#endif
