using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class ComputeLoader : MonoBehaviour
{
    [Header("Compute Components")]
    [Tooltip("The data creating compute shader")]
    [SerializeField] protected ComputeShader computeShader = default;
    [Tooltip("Should the loader create a new instanced copy of the compute shader")]
    public bool createCopyOfComputeShader = true;

    [Tooltip("Should the renderer continuously update the data each frame")]
    [SerializeField] public bool continuousDataUpdates;
    [Tooltip("Should the renderer update the data this frame")]
    [HideInInspector] public bool shouldUpdateDataThisFrame;

    [Tooltip("Should this loader initialize when the start method is called")]
    public bool initializeOnStart = false;

    [Tooltip("Should this loader deinitialize after updating. Note if continuousDataUpdates is set to true then the loader will ignore this setting.")]
    public bool deinitializeAfterUpdate = false;

    /// <summary>
    /// A state variable to help keep track of whether compute buffers have been set up
    /// </summary>
    protected bool initialized;

    /// <summary>
    /// The id of the kernel in the compute shader
    /// </summary>
    protected int idKernel;

    /// <summary>
    /// Vector that matches dispatch size of mesh generation compute function
    /// </summary>
    protected Vector3Int dispatchSizes;

    /// <summary>
    /// Vector that reprsents how many times the mesh gen function should dispatch
    /// </summary>
    protected Vector3Int dispatchTimes;

    protected const string FUNCTION_NAME = "Main";

    protected void Awake()
    {
        if (createCopyOfComputeShader)
        {
            computeShader = Instantiate(computeShader);
        }
    }

    private void Start()
    {
        if (initializeOnStart)
        {
            Initialize();
        }
    }

    public virtual void OnDisable()
    {
        Deinitialize();
    }

    /// <summary>
    /// Create the buffers used in this compute shader and update thread group sizes
    /// </summary>
    public virtual void Initialize()
    {
        // If initialized, call on disable to clean things up
        if (initialized)
        {
            Deinitialize();
        }
        initialized = true;

        CreateBuffers();

        // Calculate the number of threads to use. Get the thread size from the kernel
        // Then, divide the number of triangles by that size
        computeShader.GetKernelThreadGroupSizes(idKernel, out uint threadGroupSizeX, out uint threadGroupSizeY, out uint threadGroupSizeZ);
        dispatchSizes = new Vector3Int((int)threadGroupSizeX, (int)threadGroupSizeY, (int)threadGroupSizeZ);
    }

    /// <summary>
    /// If this lodaer has been initialized the dispose of the buffers
    /// </summary>
    public virtual void Deinitialize()
    {
        // Dispose of buffers
        if (initialized)
        {
            DisposeBuffers();
        }
        initialized = false;
    }

    /// <summary>
    /// Create buffers
    /// </summary>
    protected virtual void CreateBuffers()
    {
        // Cache the kernel IDs we will be dispatching
        idKernel = computeShader.FindKernel(FUNCTION_NAME);

        if(idKernel == -1)
        {
            Debug.LogError("Error failed to find a kernel with name " + FUNCTION_NAME + " in the computeShader");
        }
    }

    /// <summary>
    /// Dispose of the buffers
    /// </summary>
    protected virtual void DisposeBuffers()
    {

    }

    /// <summary>
    /// Set the compute shader's vaiables
    /// </summary>
    protected virtual void SetComputeVariables()
    {

    }

    /// <summary>
    /// Update the size of the dispatch call (x, y, z)
    /// </summary>
    protected virtual void UpdateDispatchTimes()
    {
        dispatchTimes = new Vector3Int(1, 1, 1);
    }

    /// <summary>
    /// Initialize the loader if if hasn't already been initialized.
    /// Set the compute shader's vairables and
    /// dispatch the compute shader to generate the data
    /// </summary>
    public virtual void UpdateData()
    {
        if (!initialized)
        {
            Initialize();
        }

        SetComputeVariables();

        UpdateDispatchTimes();

        // Dispatch the shader. It will run on the GPU
        computeShader.Dispatch(idKernel,
            Mathf.CeilToInt(dispatchTimes.x / (float)dispatchSizes.x),
            Mathf.CeilToInt(dispatchTimes.y / (float)dispatchSizes.y),
            Mathf.CeilToInt(dispatchTimes.z / (float)dispatchSizes.z));

        if (deinitializeAfterUpdate && !continuousDataUpdates)
        {
            Deinitialize();
        }
    }

    /// <summary>
    /// Request the data be copied to the cpu
    /// </summary>
    public virtual void RequestData()
    {
        //AsyncGPUReadback.Request(bufferName, r1 => OnDataAvalible(r1));
    }

    public delegate void AlertOnDataAvalible();
    public AlertOnDataAvalible onDataAvalible;

    protected virtual void OnDataAvalible(AsyncGPUReadbackRequest request)
    {
        if (request.hasError || !Application.isPlaying)
        {
            //trianglesAvailable = false;
            return;
        }

        onDataAvalible?.Invoke();

        //var data = request.GetData<DrawTriangle>();

        //if (drawTriangles == null || data.Length != drawTriangles.Length)
        //{
        //    drawTriangles = new DrawTriangle[data.Length];
        //}

        //data.CopyTo(drawTriangles);

        //CreateUnityMesh(false, false);

        //if (triangleCount == 0)
        //{
        //    enabled = false;
        //}

        //OnDisable();
    }
    
    protected virtual void Update()
    {
        if (continuousDataUpdates || shouldUpdateDataThisFrame)
        {
            shouldUpdateDataThisFrame = false;
            UpdateData();
        }
    }
}
