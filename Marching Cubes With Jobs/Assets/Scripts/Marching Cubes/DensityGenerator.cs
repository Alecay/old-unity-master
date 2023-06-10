using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;

public class DensityGenerator : ComputeHelper
{
    public int width;
    public int height;

    public NativeArray<float> values;

    [System.Serializable]
    public class NoiseData
    {
        public int seed = 42;

        [Range(1, 8)]
        public int octaves = 5;

        public float scale = 100.0f;

        public float persistance = 0.5f;
        public float lacunarity = 2.0f;

        public Vector3 offset = new Vector3(0, 0);

        public int step = 0;

        public NoiseData Copy()
        {
            return new NoiseData(seed, octaves, scale, persistance, lacunarity, offset, step);
        }

        public void SetValues(NoiseData data)
        {
            this.seed = data.seed;
            this.octaves = data.octaves;
            this.scale = data.scale;
            this.persistance = data.persistance;
            this.lacunarity = data.lacunarity;
            this.offset = data.offset;
            this.step = data.step;
        }

        public NoiseData(int seed, int octaves, float scale, float persistance, float lacunarity, Vector3 offset, int step)
        {
            this.seed = seed;
            this.octaves = octaves;
            this.scale = scale;
            this.persistance = persistance;
            this.lacunarity = lacunarity;
            this.offset = offset;
            this.step = step;
        }

        public static bool operator ==(NoiseData a, NoiseData b)
        {
            return a.seed == b.seed &&
                a.octaves == b.octaves &&
                a.scale == b.scale &&
                a.persistance == b.persistance &&
                a.lacunarity == b.lacunarity &&
                a.offset == b.offset &&
                a.step == b.step;
        }

        public static bool operator !=(NoiseData a, NoiseData b)
        {
            return !(a == b);
        }
    }

    public NoiseData noiseData;

    protected override void UpdateDispatchTimes()
    {
        dispatchTimes = new Vector3Int(width, height, width);
    }

    protected override void CreateBuffers()
    {
        FUNCTION_NAME = "Main";
        BUFFER_NAME = "Density_Values_Buffer";
        DATA_STRIDE = sizeof(float);
        bufferLength = width * width * height;
        base.CreateBuffers();
    }


    protected override void SetComputeVariables()
    {
        base.SetComputeVariables();

        computeShader.SetInt("Seed", noiseData.seed);
        computeShader.SetInt("Octaves", noiseData.octaves);

        computeShader.SetFloat("Scale", noiseData.scale);
        computeShader.SetFloat("Persistance", noiseData.persistance);
        computeShader.SetFloat("Lacunarity", noiseData.lacunarity);

        computeShader.SetVector("Offset", noiseData.offset);

        computeShader.SetInt("Width", width);
        computeShader.SetInt("Height", height);

        computeShader.SetFloat("Step", noiseData.step);
    }

    protected override void OnDataAvalible(AsyncGPUReadbackRequest request)
    {
        if (request.hasError || !Application.isPlaying)
        {
            return;
        }

        if(values == null || values.Length != width * width * height)
        {
            values = new NativeArray<float>(width * width * height, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        }

        var data = request.GetData<float>();

        data.CopyTo(values);

        data.Dispose();

        onDataAvalible?.Invoke();

        waitingForData = false;
    }

    public override void Deinitialize()
    {
        base.Deinitialize();

        if(values != null && values.IsCreated)
            values.Dispose();
    }

    public DensityGenerator(ComputeShader computeShader, int width, int height, NoiseData noiseData)
    {
        this.computeShader = computeShader;
        this.width = width;
        this.height = height;
        this.noiseData = noiseData.Copy();
    }
}
