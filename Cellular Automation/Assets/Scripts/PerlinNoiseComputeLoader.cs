using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class PerlinNoiseComputeLoader : MonoBehaviour
{
    public const string COMPUTE_SHADER_NAME = "PerlinNoiseCompute";
    private static ComputeShader compute;
    public const string NOISE_FUNCTION_NAME = "GenerateNoise";    
    public const string TEXTURE_FUNCTION_NAME = "CreateTexture";
    private static int noiseKernel = 0;
    private static int textureKernel = 0;

    private ComputeBuffer Noise_Values_Buffer;

    public int width = 100;
    public int height = 100;

    public int seed = 42;
    public int octaves = 5;

    public float scale = 100.0f;

    public float persistance = 0.5f;
    public float lacunarity = 2.0f;

    public Vector2 offset = new Vector2(0, 0);

    public Color color;

    public RenderTexture texture;

    public Material mat;

    private void OnEnable()
    {
        InitializeBuffers();
    }

    private void OnDisable()
    {
        ReleaseBuffers();
        texture = null;
    }

    private void GetComputeShader()
    {
        //Resources.LoadAll("Scripts");
        compute = Resources.Load<ComputeShader>("Scripts/Compute/" + COMPUTE_SHADER_NAME);

        ComputeShader[] compShaders = (ComputeShader[])Resources.FindObjectsOfTypeAll(typeof(ComputeShader));
        for (int i = 0; i < compShaders.Length; i++)
        {
            if (compShaders[i].name == COMPUTE_SHADER_NAME)
            {
                compute = compShaders[i];
                break;
            }
        }

        if (compute == null)
        {
            Debug.LogError("Failed to find compute shader by the name of " + COMPUTE_SHADER_NAME);
        }
        else
        {
            noiseKernel = compute.FindKernel(NOISE_FUNCTION_NAME);            
            textureKernel = compute.FindKernel(TEXTURE_FUNCTION_NAME);
        }
    }

    public void InitializeBuffers()
    {
        if (compute == null)
        {
            GetComputeShader();
        }

        Noise_Values_Buffer = new ComputeBuffer(width * height, sizeof(float));
        Noise_Values_Buffer.SetData(new float[width * height]);
    }

    private void ReleaseBuffers()
    {
        if (Noise_Values_Buffer != null)
        {
            Noise_Values_Buffer.Release();
        }
    }

    public void GenerateNoise(bool updateTexture = false)
    {
        if(compute == null || texture == null || texture.width != width || texture.height != height)
        {
            InitializeBuffers();
        }

        int numberOfThreads = 8;
        compute.SetInt("XWidth", width);
        compute.SetInt("YWidth", height);

        compute.SetInt("Seed", seed);
        compute.SetInt("Octaves", octaves);
        compute.SetFloat("Scale", scale);
        compute.SetFloat("Persistance", persistance);
        compute.SetFloat("Lacunarity", lacunarity);
        compute.SetVector("Offset", offset);

        compute.SetVector("Color", color);

        compute.SetBuffer(noiseKernel, "Noise_Values_Buffer", Noise_Values_Buffer);        

        //SIMULATE
        //calls compute kenrel, each pixel will have it's own thread because of the size used
        compute.Dispatch(noiseKernel, Mathf.CeilToInt(width / (float)numberOfThreads), Mathf.CeilToInt(height / (float)numberOfThreads), 1);

        //Update Texture

        if (updateTexture)
        {
            if(texture == null || texture.width != width || texture.height != height)
            {
                texture = new RenderTexture(width, height, 24);
                texture.enableRandomWrite = true;
                texture.filterMode = FilterMode.Point;                
                texture.Create();
            }

            if (mat)
            {
                mat.SetTexture("_MainTex", texture);
            }

            compute.SetVector("Color", color);
            compute.SetTexture(textureKernel, "Result", texture);

            compute.SetBuffer(textureKernel, "Noise_Values_Buffer", Noise_Values_Buffer);

            //calls compute kenrel, each pixel will have it's own thread because of the size used
            compute.Dispatch(textureKernel, Mathf.CeilToInt(width / (float)numberOfThreads), Mathf.CeilToInt(height / (float)numberOfThreads), 1);
        }        
    }

    public float[] GetNoiseValues()
    {
        float[] values = new float[width * height];

        Noise_Values_Buffer.GetData(values);

        return values;
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(PerlinNoiseComputeLoader))]
public class PNCLEditor : Editor
{
    PerlinNoiseComputeLoader pncl;

    private void OnEnable()
    {
        pncl = target as PerlinNoiseComputeLoader;

        if(pncl != null)
        {
            pncl.InitializeBuffers();
        }
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (GUI.changed)
        {
            pncl.GenerateNoise(true);
        }
    }
}
#endif
