using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[System.Serializable]
public class ComputeHelper
{
    [Tooltip("The data creating compute shader")]
    [SerializeField] protected ComputeShader computeShader = default;

    [Tooltip("Should this loader deinitialize after updating")]
    public bool deinitializeAfterUpdate = false;

    [Space(10)]

    ///<summary>
    /// The buffer that holds the data created by the compute shader
    /// </summary>
    public ComputeBuffer dataBuffer;

    /// <summary>
    /// Length of the data buffer
    /// </summary>
    public int bufferLength;

    /// <summary>
    /// The side of the data stored in the buffer
    /// </summary>
    protected int DATA_STRIDE = sizeof(float);

    /// <summary>
    /// A state variable to help keep track of whether compute buffers have been set up
    /// </summary>
    protected bool initialized;

    /// <summary>
    /// The id of the kernel in the compute shader
    /// </summary>
    protected int kernelID;

    /// <summary>
    /// Vector that matches dispatch size of mesh generation compute function
    /// </summary>
    protected Vector3Int dispatchSizes;

    /// <summary>
    /// Vector that reprsents how many times the mesh gen function should dispatch
    /// </summary>
    protected Vector3Int dispatchTimes;

    /// <summary>
    /// Name of the function to call in the compute shader
    /// </summary>
    protected string FUNCTION_NAME = "Main";

    /// <summary>
    /// Name of the data buffer
    /// </summary>
    protected string BUFFER_NAME = "Buffer";

    protected bool waitingForData = false;

    /// <summary>
    /// Waiting for data from data request
    /// </summary>
    public bool WaitingForData
    {
        get
        {
            return waitingForData;
        }
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
        computeShader.GetKernelThreadGroupSizes(kernelID, out uint threadGroupSizeX, out uint threadGroupSizeY, out uint threadGroupSizeZ);
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
        kernelID = computeShader.FindKernel(FUNCTION_NAME);

        if (kernelID == -1)
        {
            Debug.LogError("Error failed to find a kernel with name " + FUNCTION_NAME + " in the computeShader");
        }

        dataBuffer = new ComputeBuffer(bufferLength, DATA_STRIDE, ComputeBufferType.Structured);        
    }

    /// <summary>
    /// Dispose of the buffers
    /// </summary>
    protected virtual void DisposeBuffers()
    {
        if(dataBuffer != null)
            dataBuffer.Dispose();
    }

    /// <summary>
    /// Set the compute shader's vaiables
    /// </summary>
    protected virtual void SetComputeVariables()
    {
        computeShader.SetBuffer(kernelID, BUFFER_NAME, dataBuffer);
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
        computeShader.Dispatch(kernelID,
            Mathf.CeilToInt(dispatchTimes.x / (float)dispatchSizes.x),
            Mathf.CeilToInt(dispatchTimes.y / (float)dispatchSizes.y),
            Mathf.CeilToInt(dispatchTimes.z / (float)dispatchSizes.z));

        if (deinitializeAfterUpdate)
        {
            Deinitialize();
        }
    }

    /// <summary>
    /// Request the data be copied to the cpu
    /// </summary>
    public virtual void RequestData()
    {
        if (waitingForData)
        {
            Debug.LogError("Requested data but another request is ongoing");
            return;
        }

        waitingForData = true;

        AsyncGPUReadback.Request(dataBuffer, r1 => OnDataAvalible(r1));
    }

    public delegate void AlertOnDataAvalible();
    public AlertOnDataAvalible onDataAvalible;

    /// <summary>
    /// Called when the requested data becomes avalible
    /// </summary>
    /// <param name="request"></param>
    protected virtual void OnDataAvalible(AsyncGPUReadbackRequest request)
    {
        if (request.hasError || !Application.isPlaying)
        {
            return;
        }

        onDataAvalible?.Invoke();

        waitingForData = false;
    }    
}
