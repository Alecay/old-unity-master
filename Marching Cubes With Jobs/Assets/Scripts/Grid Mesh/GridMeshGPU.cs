using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using UnityEngine.Rendering;

public class GridMeshGPU : MonoBehaviour
{
    GraphicsBuffer verticesBuffer;
    GraphicsBuffer normalsBuffer;
    GraphicsBuffer colorsBuffer;
    GraphicsBuffer uvsBuffer;

    public Mesh mesh;
    public MeshFilter filter;

    public ComputeShader computeShader;

    [Range(1, 100)]
    public int gridSize = 100;

    public Color color;

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
    }

    private void Update()
    {
        UpdateMesh();
    }

    private void RunCompute()
    {
        verticesBuffer ??= mesh.GetVertexBuffer(0);
        normalsBuffer ??= mesh.GetVertexBuffer(1);
        uvsBuffer ??= mesh.GetVertexBuffer(2);
        colorsBuffer ??= mesh.GetVertexBuffer(3);

        computeShader.SetInt("GridSize", gridSize);

        computeShader.SetBuffer(0, "VerticesBuffer", verticesBuffer);
        computeShader.SetBuffer(0, "NormalsBuffer", normalsBuffer);
        computeShader.SetBuffer(0, "UVsBuffer", uvsBuffer);
        computeShader.SetBuffer(0, "ColorsBuffer", colorsBuffer);

        int kThreadCount = 8;
        int dispatch = Mathf.CeilToInt(gridSize / (float)kThreadCount);
        computeShader.Dispatch(0, dispatch, dispatch, 1);
        return;
    }

    private void UpdateMesh()
    {
        if(mesh == null)
        {
            mesh = new Mesh();
            mesh.name = "Mesh";
            mesh.vertexBufferTarget |= GraphicsBuffer.Target.Raw;
        }

        int triangleCount = (gridSize * gridSize * 2);

        if (verticesBuffer == null || verticesBuffer.count != triangleCount)
        {            
            Dispose();            

            mesh.SetVertexBufferParams(triangleCount * 3, 
                new VertexAttributeDescriptor(VertexAttribute.Position, stream: 0),
                new VertexAttributeDescriptor(VertexAttribute.Normal, stream: 1),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, stream: 2),
                new VertexAttributeDescriptor(VertexAttribute.Color, stream: 3));

            mesh.SetIndexBufferParams(triangleCount * 3, IndexFormat.UInt32);
            var ib = new NativeArray<int>(triangleCount * 3, Allocator.Temp);

            int[] indices = new int[triangleCount * 3];

            for (var i = 0; i < triangleCount * 3; i++)
                indices[i] = i;

            ib.CopyFrom(indices);

            mesh.SetIndexBufferData(ib, 0, 0, ib.Length, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);

            ib.Dispose();

            var submesh = new SubMeshDescriptor(0, triangleCount * 3, MeshTopology.Triangles);
            submesh.bounds = new Bounds(Vector3.zero, new Vector3(gridSize + 1, gridSize + 1, 1));
            mesh.SetSubMesh(0, submesh);
            mesh.bounds = submesh.bounds;

            filter ??= GetComponent<MeshFilter>();
            filter.sharedMesh = mesh;
        }

        //if(verticesBuffer != null && verticesBuffer.count != targetCount)
        //{
        //    verticesBuffer.SetData(new Vector3[targetCount]);
        //    normalsBuffer.SetData(new Vector3[targetCount]);
        //    colorsBuffer.SetData(new Color[targetCount]);

        //    var submesh = new SubMeshDescriptor(0, targetCount * 3, MeshTopology.Triangles);
        //    submesh.bounds = new Bounds(Vector3.zero, new Vector3(gridSize + 1, gridSize + 1, 1));
        //    mesh.SetSubMesh(0, submesh);
        //    mesh.bounds = submesh.bounds;
        //}

        RunCompute();
    }
}
