using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class ProceduralMeshGenerator : MonoBehaviour
{
    [Header("Mesh Components")]
    [Tooltip("The mesh creating compute shader")]
    [SerializeField] protected ComputeShader meshGenComputeShader = default;

    [Tooltip("The scale to render this mesh")]
    [SerializeField] protected Vector3 scale;

    [Tooltip("The maximum possible triangles this mesh could have")]
    [SerializeField] public uint maxTriangles;
    [Tooltip("The number of triangles in this mesh")]
    [SerializeField] public int triangleCount;
    [Tooltip("The number of vertices in this mesh")]
    [SerializeField] public int vertexCount;

    [HideInInspector] public bool shouldUpdateMeshThisFrame;

    /// <summary>
    /// A state variable to help keep track of whether compute buffers have been set up
    /// </summary>
    protected bool initialized;

    /// <summary>
    /// A compute buffer to hold vertex data of the generated mesh
    /// </summary>
    protected ComputeBuffer drawBuffer;
    /// <summary>
    /// A compute buffer to hold indirect draw arguments
    /// </summary>
    protected ComputeBuffer argsBuffer;
    /// <summary>
    /// A compute buffer to hold min and max bounds points
    /// </summary>
    protected ComputeBuffer boundsBuffer;

    /// <summary>
    /// The id of the kernel in the mesh gen compute shader
    /// </summary>
    protected int idMeshGenKernel;
    /// <summary>
    /// The id of the kernel in the tri to vert count compute shader
    /// </summary>
    protected int idTriToVertKernel;
    /// <summary>
    /// The id of the kernel in the bounds function in the compute shader
    /// </summary>
    protected int idBoundsKernel;

    /// <summary>
    /// The global bounds of the generated mesh
    /// </summary>    
    public Bounds globalBounds = new Bounds();

    [Tooltip("Vector that matches dispatch size of mesh generation compute function")]
    protected Vector3Int dispatchSizes;

    [Tooltip("Vector that reprsents how many times the mesh gen function should dispatch")]
    protected Vector3Int dispatchTimes;

    // The size of one entry into the various compute buffers
    protected const int DRAW_STRIDE = DrawTriangle.Stride;//Normal + 3 * (V3 positions + V2 UV + V4 Color)
    protected const int ARGS_STRIDE = sizeof(int) * 4;
    protected const int BOUNDS_STRIDE = sizeof(float) * 3;

    [Tooltip("The maxiumim amount of vertices that can be in a Unity Mesh object")]
    public const int MAX_VERTICES_IN_UNITY_MESH = 65535;

    [Space(10)]
    [Header("Unity Mesh Creation and Collider Info")]

    [Tooltip("Generated mesh created when createUnityMesh is set to true and mesh is updated")]
    public Mesh generatedMesh;

    [Tooltip("Generated mesh will be assigned to this renderer if it exisits")]
    public MeshFilter attachedFilter;
    public MeshRenderer attachedRenderer;
    [Tooltip("Generated mesh will be assigned to this collider if it exisits")]
    public MeshCollider attachedCollider;

    public bool simplify = false;

    [System.Serializable]
    // This describes a vertex on the generated mesh
    struct GlobalVertex
    {
        public Vector3 positionWS; // position in world space
        public Vector2 uv; // UV
        public Color color; //Vertex Color

        public const int Stride = sizeof(float) * (3 + 2 + 4);
    };

    [System.Serializable]
    // We have to insert three draw vertices at once so the triangle stays connected
    // in the graphics shader. This structure does that
    struct DrawTriangle
    {
        public Vector3 normalWS; // normal in world space. All points share this normal         
        public GlobalVertex vertex0;
        public GlobalVertex vertex1;
        public GlobalVertex vertex2;

        public const int Stride = sizeof(float) * 3 + GlobalVertex.Stride * 3;
    };

    protected void Awake()
    {
        //meshGenComputeShader = Instantiate(meshGenComputeShader);        
    }

    public virtual void OnEnable()
    {
        if (attachedCollider)
        {
            attachedCollider.enabled = true;
        }

        if (attachedRenderer)
        {
            attachedRenderer.enabled = true;
        }
    }

    public virtual void OnDisable()
    {
        // Dispose of buffers
        if (initialized)
        {
            DisposeBuffers();
        }
        initialized = false;
    }

    protected virtual void InitializeBuffers()
    {
        // If initialized, call on disable to clean things up
        if (initialized)
        {
            OnDisable();
        }
        initialized = true;

        UpdateMaxTriangles();

        drawBuffer = new ComputeBuffer((int)maxTriangles, DRAW_STRIDE, ComputeBufferType.Append);
        drawBuffer.SetCounterValue(0); // Set the count to zero

        argsBuffer = new ComputeBuffer(1, ARGS_STRIDE, ComputeBufferType.IndirectArguments);
        // The data in the args buffer corresponds to:
        // 0: vertex count per draw instance. We will only use one instance
        // 1: instance count. One
        // 2: start vertex location if using a Graphics Buffer
        // 3: and start instance location if using a Graphics Buffer
        argsBuffer.SetData(new int[] { 0, 1, 0, 0 });

        boundsBuffer = new ComputeBuffer(2, BOUNDS_STRIDE, ComputeBufferType.Structured, ComputeBufferMode.Immutable);
        boundsBuffer.SetData(new Vector3[2]);

        // Cache the kernel IDs we will be dispatching
        idMeshGenKernel = meshGenComputeShader.FindKernel("Main");
        idTriToVertKernel = meshGenComputeShader.FindKernel("TriToVertCount");
        idBoundsKernel = meshGenComputeShader.FindKernel("CalculateBounds");

        // Set data on the shaders
        meshGenComputeShader.SetBuffer(idMeshGenKernel, "_DrawTriangles", drawBuffer);
        meshGenComputeShader.SetBuffer(idTriToVertKernel, "_IndirectArgsBuffer", argsBuffer);
        meshGenComputeShader.SetBuffer(idBoundsKernel, "_BoundsBuffer", boundsBuffer);        

        // Calculate the number of threads to use. Get the thread size from the kernel
        // Then, divide the number of triangles by that size
        meshGenComputeShader.GetKernelThreadGroupSizes(idMeshGenKernel, out uint threadGroupSizeX, out uint threadGroupSizeY, out uint threadGroupSizeZ);
        dispatchSizes = new Vector3Int((int)threadGroupSizeX, (int)threadGroupSizeY, (int)threadGroupSizeZ);
    }

    protected virtual void DisposeBuffers()
    {
        drawBuffer.Release();
        argsBuffer.Release();
        boundsBuffer.Release();
    }

    // This applies the game object's transform to the local bounds
    // Code by benblo from https://answers.unity.com/questions/361275/cant-convert-bounds-from-world-coordinates-to-loca.html
    protected Bounds TransformBounds(Bounds boundsOS)
    {
        var center = transform.TransformPoint(boundsOS.center);

        // transform the local extents' axes
        var extents = boundsOS.extents;
        var axisX = transform.TransformVector(extents.x, 0, 0);
        var axisY = transform.TransformVector(0, extents.y, 0);
        var axisZ = transform.TransformVector(0, 0, extents.z);

        // sum their absolute value to get the world extents
        extents.x = Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x);
        extents.y = Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y);
        extents.z = Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z);

        return new Bounds { center = center, extents = extents };
    }

    protected virtual void UpdateBounds()
    {

        meshGenComputeShader.Dispatch(idBoundsKernel, 1, 1, 1);
        Vector3[] minMax = new Vector3[2];
        boundsBuffer.GetData(minMax);

        Vector3 center = (minMax[0] + minMax[1]) / 2f;
        Vector3 size = minMax[1] - minMax[0];

        globalBounds = new Bounds(center, size);
    }

    protected virtual void SetComputeVariables()
    {
        // Clear the draw buffer of last frame's data
        drawBuffer.SetCounterValue(0);

        // Update the shader with frame specific data
        meshGenComputeShader.SetMatrix("_LocalToWorld", transform.localToWorldMatrix);
        //meshGenComputeShader.SetMatrix("_LocalToWorld", Matrix4x4.identity);
        meshGenComputeShader.SetVector("_Scale", scale);

        meshGenComputeShader.SetVector("_GlobalBoundsMin", new Vector3());
        meshGenComputeShader.SetVector("_GlobalBoundsMax", new Vector3());
    }

    protected virtual void UpdateMaxTriangles()
    {
        //maxTriangles = calc amount
    }

    protected virtual void UpdateDispatchTimes()
    {
        dispatchTimes = new Vector3Int(1, 1, 1);
    }

    protected virtual void UpdateVertexAndTriangleCount()
    {
        argsBuffer.SetCounterValue(0);
        int[] args = new int[4];
        argsBuffer.GetData(args);
        int numberOfVerts = args[0];

        triangleCount = numberOfVerts / 3;
        vertexCount = numberOfVerts;
    }

    public virtual void UpdateMesh()
    {
        if (!initialized)
        {
            InitializeBuffers();
        }

        SetComputeVariables();

        UpdateDispatchTimes();        

        // Dispatch the pyramid shader. It will run on the GPU
        meshGenComputeShader.Dispatch(idMeshGenKernel,
            Mathf.CeilToInt(dispatchTimes.x / (float)dispatchSizes.x),
            Mathf.CeilToInt(dispatchTimes.y / (float)dispatchSizes.y),
            Mathf.CeilToInt(dispatchTimes.z / (float)dispatchSizes.z));

        // Copy the count (stack size) of the draw buffer to the args buffer, at byte position zero
        // This sets the vertex count for our draw procediral indirect call
        ComputeBuffer.CopyCount(drawBuffer, argsBuffer, 0);

        // This the compute shader outputs triangles, but the graphics shader needs the number of vertices,
        // we need to multiply the vertex count by three. We'll do this on the GPU with a compute shader 
        // so we don't have to transfer data back to the CPU
        meshGenComputeShader.Dispatch(idTriToVertKernel, 1, 1, 1);

        UpdateBounds();

        UpdateVertexAndTriangleCount();


        RequestTriangleData();
        //CreateUnityMesh();

        //OnDisable();

        if (attachedCollider)
        {
            attachedCollider.enabled = true;
        }

        if (attachedRenderer)
        {
            attachedRenderer.enabled = true;
        }
    }

    protected void CreateUnityMesh(bool recalBounds = true, bool recalNormals = false)
    {
        if (generatedMesh == null)
        {
            generatedMesh = new Mesh();
            generatedMesh.MarkDynamic();
        }

        generatedMesh.Clear();

        if (triangleCount <= 0 || triangleCount * 3 > MAX_VERTICES_IN_UNITY_MESH)
        {
            //Debug.LogWarning("Warning requested mesh was not created because there were no triangles in draw buffer or the mesh would have to many triangles");
            return;
        }     

        Vector3[] vertices = new Vector3[vertexCount];
        Vector3[] normals = new Vector3[vertexCount];
        Color[] colors = new Color[vertexCount];
        int[] triangles = new int[vertexCount];

        for (int i = 0; i < triangleCount; i++)
        {
            vertices[i * 3] = drawTriangles[i].vertex0.positionWS;
            vertices[i * 3 + 1] = drawTriangles[i].vertex1.positionWS;
            vertices[i * 3 + 2] = drawTriangles[i].vertex2.positionWS;

            normals[i * 3] = drawTriangles[i].normalWS;
            normals[i * 3 + 1] = drawTriangles[i].normalWS;
            normals[i * 3 + 2] = drawTriangles[i].normalWS;

            colors[i * 3] = drawTriangles[i].vertex0.color;
            colors[i * 3 + 1] = drawTriangles[i].vertex1.color;
            colors[i * 3 + 2] = drawTriangles[i].vertex2.color;

            triangles[i * 3] = i * 3;
            triangles[i * 3 + 1] = i * 3 + 1;
            triangles[i * 3 + 2] = i * 3 + 2;
        }

        //for (int i = 0; i < vertexCount; i++)
        //{
        //    vertices[i] = transform.InverseTransformPoint(vertices[i]);
        //    normals[i] = transform.InverseTransformVector(normals[i]);
        //}

        generatedMesh.vertices = vertices;
        generatedMesh.colors = colors;
        generatedMesh.triangles = triangles;


        if (recalBounds)
        {
            generatedMesh.RecalculateBounds();
        }

        if (recalNormals)
        {
            generatedMesh.RecalculateNormals();
        }
        else
        {
            generatedMesh.normals = normals;
        }

        generatedMesh.name = "GenMesh " + vertexCount + "(" + triangleCount + ")";

        if (attachedFilter)
        {
            attachedFilter.sharedMesh = generatedMesh;
        }

        if (attachedCollider)
        {
            attachedCollider.sharedMesh = generatedMesh;
        }

        if (simplify)
        {
            SimplifyMesh();
        }
    }

    private DrawTriangle[] drawTriangles;
    bool trianglesAvailable = false;

    protected void RequestTriangleData()
    {
        AsyncGPUReadback.Request(drawBuffer, r1 => OnTrianglesAvalible(r1, Time.frameCount));        
    }

    protected void OnTrianglesAvalible(AsyncGPUReadbackRequest request, int requestFrame)
    {
        if(request.hasError || !Application.isPlaying)
        {
            trianglesAvailable = false;
            return;
        }

        var data = request.GetData<DrawTriangle>();

        if(drawTriangles == null || data.Length != drawTriangles.Length)
        {
            drawTriangles = new DrawTriangle[data.Length];
        }        

        data.CopyTo(drawTriangles);

        CreateUnityMesh(false, false);

        if(triangleCount == 0)
        {
            enabled = false;            
        }

        OnDisable();
    }

    private void SimplifyMesh()
    {
        var vertices = generatedMesh.vertices;
        var triangles = generatedMesh.triangles;
        var colors = generatedMesh.colors;

        int vertexCount = vertices.Length;
        Vector3 thisVertex, otherVertex;

        float minDist = 0.01f;
        float dist;

        int[] firstArr = new int[vertexCount];
        int unqiueCount = 0;

        List<Vector3> newVerts = new List<Vector3>();
        List<Color> newColors = new List<Color>();
        Dictionary<int, int> oldAndNewIndices = new Dictionary<int, int>();

        //Go through each vertex and find the first other vertex that it is close too and save that index in this list
        for (int i = 0; i < vertexCount; i++)
        {
            thisVertex = vertices[i];
            for (int j = 0; j < vertexCount; j++)
            {
                otherVertex = vertices[j];

                dist = Vector3.Distance(thisVertex, otherVertex);

                if (dist <= minDist || i == j)
                {
                    firstArr[i] = j;                  
                    break;
                }
            }
        }

        //Go through and find the unqiue vertices and add them to the new verts and save a reference to the old index
        unqiueCount = 0;
        for (int i = 0; i < firstArr.Length; i++)
        {
            if (!oldAndNewIndices.ContainsKey(firstArr[i]))
            {
                newVerts.Add(vertices[firstArr[i]]);
                newColors.Add(colors[firstArr[i]]);

                oldAndNewIndices.Add(firstArr[i], unqiueCount);
                unqiueCount++;
            }
        }

        int triCount = triangles.Length;
        int[] newTriangles = new int[triCount];
        int first;
        //Go through each index of the triangles and change the index to match the new one
        for (int i = 0; i < triCount; i++)
        {
            first = firstArr[triangles[i]];

            //if (!oldAndNewIndices.ContainsKey(first))
            //{
            //    Debug.Log("This key was not present: " + first + " old index: " + triangles[i]);                
            //    continue;
            //}

            newTriangles[i] = oldAndNewIndices[first];
        }

        generatedMesh.Clear();
        generatedMesh.vertices = newVerts.ToArray();
        generatedMesh.triangles = newTriangles;
        generatedMesh.colors = newColors.ToArray();
        generatedMesh.RecalculateNormals();

        generatedMesh.name = "Simplified Gen Mesh " + newVerts.Count + " (" + triangles.Length + ")";

        if (attachedFilter)
        {
            attachedFilter.sharedMesh = generatedMesh;
        }

        if (attachedCollider)
        {
            attachedCollider.sharedMesh = generatedMesh;
        }

        float reducedAmount = (vertexCount - unqiueCount) / (float)vertexCount;

        Debug.Log("Unique verts: " + unqiueCount + " vs original: " + vertexCount + " reduced by " + reducedAmount.ToString("0%"));

        //string prnt = "First 20: ";

        //for (int i = 0; i < 20; i++)
        //{
        //    prnt += firstArr[i] + ", ";
        //}
        //Debug.Log(prnt);

    }


    // LateUpdate is called after all Update calls
    protected virtual void LateUpdate()
    {
        if (shouldUpdateMeshThisFrame)
        {
            shouldUpdateMeshThisFrame = false;
            UpdateMesh();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!enabled)
        {
            return;
        }

        Gizmos.color = Color.red;

        Gizmos.DrawWireCube(globalBounds.center, globalBounds.size);
    }
}
