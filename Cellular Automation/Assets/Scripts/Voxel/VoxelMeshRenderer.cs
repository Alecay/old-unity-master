using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class VoxelMeshRenderer : MonoBehaviour
{
    [Tooltip("The size of the mesh in voxels")]
    [SerializeField] public Vector3Int size;
    [Tooltip("The size of each voxel")]
    [SerializeField] public float voxelSize;

    [Tooltip("The mesh creating compute shader")]
    [SerializeField] private ComputeShader meshGenComputeShader = default;
    [Tooltip("The triangle count adjustment compute shader")]
    [SerializeField] private ComputeShader triToVertComputeShader = default;
    [Tooltip("The material to render the mesh")]
    [SerializeField] private Material material = default;

    // The structure to send to the compute shader
    // This layout kind assures that the data is laid out sequentially
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct SourceVertex
    {
        public Vector3 position;
        public Vector2 uv;
    }

    // A state variable to help keep track of whether compute buffers have been set up
    private bool initialized;
    // A compute buffer to hold vertex data of the generated mesh
    private ComputeBuffer drawBuffer;
    // A compute buffer to hold indirect draw arguments
    private ComputeBuffer argsBuffer;
    // The id of the kernel in the pyramid compute shader
    private int idMeshGenKernel;
    // The id of the kernel in the tri to vert count compute shader
    private int idTriToVertKernel;
    // The x dispatch size for the meshGen compute shader
    private int dispatchSize;
    // The local bounds of the generated mesh
    private Bounds localBounds;

    // The size of one entry into the various compute buffers
    private const int SOURCE_VERT_STRIDE = sizeof(float) * (3 + 2);
    private const int SOURCE_TRI_STRIDE = sizeof(int);
    private const int DRAW_STRIDE = sizeof(float) * (3 + (3 + 2) * 3);
    private const int ARGS_STRIDE = sizeof(int) * 4;

    #region Buffer Variables

    private ComputeBuffer Enabled_Voxels_Buffer;

    private ComputeBuffer Outer_Enabled_Voxels_Buffer;

    private ComputeBuffer VoxelFace_Buffer;
    private ComputeBuffer Face_Size_Buffer;

    private ComputeBuffer Texture_Index_Buffer;
    private ComputeBuffer Animation_Info_Buffer;

    private ComputeBuffer Start_End_Buffer;

    private ComputeBuffer Visited_Buffer;

    #endregion

    private float[] GetRandomEnabled()
    {
        int width = size.x * size.y * size.z;
        float[] enabled = new float[width];

        for (int i = 0; i < width; i++)
        {
            enabled[i] = Random.value > 0.3f ? 1f : 0f;
        }

        return enabled;
    }

    private void OnEnable()
    {
        meshGenComputeShader = Instantiate(meshGenComputeShader);
        triToVertComputeShader = Instantiate(triToVertComputeShader);
        material = Instantiate(material);

        // If initialized, call on disable to clean things up
        if (initialized)
        {
            OnDisable();
        }
        initialized = true;

        // We split each triangle into three new ones
        //drawBuffer = new ComputeBuffer(numTriangles * 3, DRAW_STRIDE, ComputeBufferType.Append);
        drawBuffer = new ComputeBuffer(size.x * size.y * size.z * 36, DRAW_STRIDE, ComputeBufferType.Append);
        drawBuffer.SetCounterValue(0); // Set the count to zero
        argsBuffer = new ComputeBuffer(1, ARGS_STRIDE, ComputeBufferType.IndirectArguments);
        // The data in the args buffer corresponds to:
        // 0: vertex count per draw instance. We will only use one instance
        // 1: instance count. One
        // 2: start vertex location if using a Graphics Buffer
        // 3: and start instance location if using a Graphics Buffer
        argsBuffer.SetData(new int[] { 0, 1, 0, 0 });

        // Cache the kernel IDs we will be dispatching
        idMeshGenKernel = meshGenComputeShader.FindKernel("CreateDrawTriangles");
        idTriToVertKernel = triToVertComputeShader.FindKernel("Main");

        // Set data on the shaders
        meshGenComputeShader.SetBuffer(idMeshGenKernel, "_DrawTriangles", drawBuffer);

        triToVertComputeShader.SetBuffer(idTriToVertKernel, "_IndirectArgsBuffer", argsBuffer);

        material.SetBuffer("_DrawTriangles", drawBuffer);

        // Calculate the number of threads to use. Get the thread size from the kernel
        // Then, divide the number of triangles by that size

        //meshGenComputeShader.GetKernelThreadGroupSizes(idMeshGenKernel, out uint threadGroupSize, out _, out _);
        //dispatchSize = Mathf.CeilToInt((float)numTriangles / threadGroupSize);

        // Get the bounds of the source mesh and then expand by the pyramid height
        Vector3 scale = new Vector3(size.x * voxelSize, size.y * voxelSize, size.z * voxelSize);
        localBounds = new Bounds(transform.position + scale / 2f, scale);
        localBounds.Expand(voxelSize * 16);


        //Visited Voxels
        Visited_Buffer = new ComputeBuffer(size.x * size.y * size.z, sizeof(float));
        Visited_Buffer.SetData(new float[size.x * size.y * size.z]);

        meshGenComputeShader.SetBuffer(idMeshGenKernel, "Visited_Voxels_Buffer", Visited_Buffer);

        //Enabled Voxels
        Enabled_Voxels_Buffer = new ComputeBuffer(size.x * size.y * size.z, sizeof(float));
        Enabled_Voxels_Buffer.SetData(GetRandomEnabled());

        meshGenComputeShader.SetBuffer(idMeshGenKernel, "Enabled_Voxels_Buffer", Enabled_Voxels_Buffer);

        //Outer Enabled Voxels
        Outer_Enabled_Voxels_Buffer = new ComputeBuffer(
            size.x * size.z * 2 + //Top and bottom
            size.z * size.y * 2 + //Left and right
            size.x * size.y * 2,  //Forward and back
            sizeof(float));

        int correctLength =
            size.x * size.z * 2 + //Top and bottom
            size.z * size.y * 2 + //Left and right
            size.x * size.y * 2;  //Forward and back   

        Outer_Enabled_Voxels_Buffer.SetData(new float[correctLength]);

        meshGenComputeShader.SetBuffer(idMeshGenKernel, "Outer_Enabled_Voxels_Buffer", Outer_Enabled_Voxels_Buffer);

        VoxelFace_Buffer = new ComputeBuffer(size.x * size.y * size.z * 6, sizeof(float) * 3 * 4 + sizeof(float) * 4 * 4 + sizeof(float) * 3, ComputeBufferType.Append);

        meshGenComputeShader.SetBuffer(idMeshGenKernel, "VoxelFace_Buffer", VoxelFace_Buffer);

        //Mesh Face Size - Used to output the number of faces in the VoxelFace_Buffer
        Face_Size_Buffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.IndirectArguments);

        //Setup TextureIndexBuffer - Length = size.x * size.y * size.z * 6 - each value is a int
        Texture_Index_Buffer = new ComputeBuffer(size.x * size.y * size.z * 6, sizeof(int));
        Texture_Index_Buffer.SetData(new int[size.x * size.y * size.z * 6]);
        meshGenComputeShader.SetBuffer(idMeshGenKernel, "Texture_Index_Buffer", Texture_Index_Buffer);

        Animation_Info_Buffer = new ComputeBuffer(size.x * size.y * size.z * 6, sizeof(int));
        Animation_Info_Buffer.SetData(new int[size.x * size.y * size.z * 6]);
        meshGenComputeShader.SetBuffer(idMeshGenKernel, "Animation_Info_Buffer", Animation_Info_Buffer);


        Start_End_Buffer = new ComputeBuffer(size.x * size.y * size.z * 6, sizeof(int) * 2 + sizeof(int) * 2 * 2, ComputeBufferType.Append);
        meshGenComputeShader.SetBuffer(idMeshGenKernel, "Start_End_Buffer", Start_End_Buffer);
    }

    private void OnDisable()
    {
        // Dispose of buffers
        if (initialized)
        {
            drawBuffer.Release();
            argsBuffer.Release();

            if (Enabled_Voxels_Buffer != null)
            {
                Enabled_Voxels_Buffer.Release();
            }

            if (Outer_Enabled_Voxels_Buffer != null)
            {
                Outer_Enabled_Voxels_Buffer.Release();
            }

            if (VoxelFace_Buffer != null)
            {
                VoxelFace_Buffer.Release();
            }

            if (Face_Size_Buffer != null)
            {
                Face_Size_Buffer.Release();
            }

            if (Texture_Index_Buffer != null)
            {
                Texture_Index_Buffer.Release();
            }

            if (Animation_Info_Buffer != null)
            {
                Animation_Info_Buffer.Release();
            }

            if (Start_End_Buffer != null)
            {
                Start_End_Buffer.Release();
            }
        }
        initialized = false;
    }

    // This applies the game object's transform to the local bounds
    // Code by benblo from https://answers.unity.com/questions/361275/cant-convert-bounds-from-world-coordinates-to-loca.html
    public Bounds TransformBounds(Bounds boundsOS)
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

    // LateUpdate is called after all Update calls
    private void LateUpdate()
    {
        int numberOfThreads = 8;

        //meshGenComputeShader.SetBuffer(idMeshGenKernel, "_DrawTriangles", drawBuffer);        
        //triToVertComputeShader.SetBuffer(idTriToVertKernel, "_IndirectArgsBuffer", argsBuffer);

        //meshGenComputeShader.SetBuffer(idMeshGenKernel, "Enabled_Voxels_Buffer", Enabled_Voxels_Buffer);
        //meshGenComputeShader.SetBuffer(idMeshGenKernel, "Outer_Enabled_Voxels_Buffer", Outer_Enabled_Voxels_Buffer);
        //meshGenComputeShader.SetBuffer(idMeshGenKernel, "VoxelFace_Buffer", VoxelFace_Buffer);
        //meshGenComputeShader.SetBuffer(idMeshGenKernel, "Texture_Index_Buffer", Texture_Index_Buffer);
        //meshGenComputeShader.SetBuffer(idMeshGenKernel, "Animation_Info_Buffer", Animation_Info_Buffer);

        meshGenComputeShader.SetInt("XWidth", size.x);
        meshGenComputeShader.SetInt("YWidth", size.y);
        meshGenComputeShader.SetInt("ZWidth", size.z);

        meshGenComputeShader.SetFloat("Voxel_Size", voxelSize);

        meshGenComputeShader.SetFloat("UseTextureIndices", 0f);
        //meshGenComputeShader.SetFloat("UseTextureIndices", useTextureIndices ? 1f : 0f);

        Enabled_Voxels_Buffer.SetCounterValue(0);
        VoxelFace_Buffer.SetCounterValue(0);
        Face_Size_Buffer.SetCounterValue(0);

        // Clear the draw buffer of last frame's data
        drawBuffer.SetCounterValue(0);

        // Transform the bounds to world space
        Bounds bounds = TransformBounds(localBounds);

        // Update the shader with frame specific data
        meshGenComputeShader.SetMatrix("_LocalToWorld", transform.localToWorldMatrix);
        //meshGenComputeShader.SetFloat("_PyramidHeight", pyramidHeight * Mathf.Lerp(0.1f, 1f, (Mathf.Sin(animationFrequency * Time.timeSinceLevelLoad) + 1f) / 2f));

        // Dispatch the pyramid shader. It will run on the GPU
        meshGenComputeShader.Dispatch(idMeshGenKernel, Mathf.CeilToInt(size.x / (float)numberOfThreads), Mathf.CeilToInt(size.y / (float)numberOfThreads), size.z);

        // Copy the count (stack size) of the draw buffer to the args buffer, at byte position zero
        // This sets the vertex count for our draw procediral indirect call
        ComputeBuffer.CopyCount(drawBuffer, argsBuffer, 0);

        // This the compute shader outputs triangles, but the graphics shader needs the number of vertices,
        // we need to multiply the vertex count by three. We'll do this on the GPU with a compute shader 
        // so we don't have to transfer data back to the CPU
        triToVertComputeShader.Dispatch(idTriToVertKernel, 1, 1, 1);

        // DrawProceduralIndirect queues a draw call up for our generated mesh
        // It will receive a shadow casting pass, like normal
        Graphics.DrawProceduralIndirect(material, bounds, MeshTopology.Triangles, argsBuffer, 0,
            null, null, ShadowCastingMode.On, true, gameObject.layer);
    }


    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Vector3 scale = new Vector3(size.x * voxelSize, size.y * voxelSize, size.z * voxelSize);
        Gizmos.DrawWireCube(transform.position + scale / 2f, scale);
    }
}
