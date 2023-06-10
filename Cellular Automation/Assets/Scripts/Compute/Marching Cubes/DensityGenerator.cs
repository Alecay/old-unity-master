using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class DensityGenerator : ComputeLoader
{
    [System.Serializable]
    public class NoiseData
    {
        public int seed = 42;

        [Range(1, 8)]
        public int octaves = 5;

        public float scale = 100.0f;

        public float persistance = 0.5f;
        public float lacunarity = 2.0f;

        public Vector3 offset = new Vector3(0, 0, 0);

        public int chunkSize = 16;

        public Vector3 chunkOffset = new Vector3(0, 0, 0);

        public float heightMultiplier = 16.0f;

        public int heightOffset = 1;

        public uint worldHeight = 256;
    }

    [SerializeField]
    public NoiseData noiseData;
    public float radius = 100f;

    [Tooltip("Set the offset of the noise using the render's position")]
    public bool usePositionAsOffset = false;

    public ComputeBuffer densityValuesBuffer;
    public const int DENSITY_STRIDE = sizeof(float);

    [HideInInspector]
    public float[] densityValues;

    protected override void CreateBuffers()
    {
        base.CreateBuffers();

        densityValuesBuffer = new ComputeBuffer(noiseData.chunkSize * noiseData.chunkSize * noiseData.chunkSize, DENSITY_STRIDE);
        computeShader.SetBuffer(idKernel, "Density_Values_Buffer", densityValuesBuffer);
    }

    protected override void DisposeBuffers()
    {
        base.DisposeBuffers();

        densityValuesBuffer.Release();
    }

    protected override void SetComputeVariables()
    {
        computeShader.SetInt("Seed", noiseData.seed);
        computeShader.SetInt("Octaves", noiseData.octaves);

        computeShader.SetFloat("Scale", noiseData.scale);
        computeShader.SetFloat("Persistance", noiseData.persistance);
        computeShader.SetFloat("Lacunarity", noiseData.lacunarity);

        computeShader.SetVector("Offset", usePositionAsOffset ? transform.position : noiseData.offset);

        computeShader.SetInt("ChunkSize", noiseData.chunkSize);
        computeShader.SetVector("ChunkOffset", noiseData.chunkOffset);

        computeShader.SetFloat("HeightMultiplier", noiseData.heightMultiplier);

        computeShader.SetFloat("Radius", radius);
    }

    protected override void UpdateDispatchTimes()
    {
        dispatchTimes = new Vector3Int(noiseData.chunkSize, noiseData.chunkSize, noiseData.chunkSize);
    }

    public override void RequestData()
    {
        //UpdateData();

        AsyncGPUReadback.Request(densityValuesBuffer, r1 => OnDataAvalible(r1));
    }

    protected override void OnDataAvalible(AsyncGPUReadbackRequest request)
    {
        if (request.hasError || !Application.isPlaying)
        {            
            return;
        }

        var data = request.GetData<float>();
        densityValues = new float[data.Length];
        data.CopyTo(densityValues);

        onDataAvalible?.Invoke();
    }
}
