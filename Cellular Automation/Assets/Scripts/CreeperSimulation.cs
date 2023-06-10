using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CreeperSimulation : MonoBehaviour
{
    public const string COMPUTE_SHADER_NAME = "CreepCompute";
    private static ComputeShader compute;
    public const string COMPUTE_SHADER_FUNCTION_NAME = "CalculateFutureDensity";
    private static int kernel = 0;

    public bool update = true;
    public bool emission = true;

    public int width = 100;
    public int height = 100;

    public int simulationCount = 0;
    public float densitySum = 0;

    public int FlowMin = 1;
    public int FlowMax = 10;

    private ComputeBuffer Heights_Buffer;
    private ComputeBuffer Density_Buffer;
    private ComputeBuffer Future_Density_Buffer;

    [HideInInspector] public float[] denisty;

    private void Update()
    {
        if(update || Input.GetKeyDown(KeyCode.RightArrow))
        {
            Simulate();
        }        
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
            kernel = compute.FindKernel(COMPUTE_SHADER_FUNCTION_NAME);
        }
    }

    public void InitializeBuffers()
    {
        if (compute == null)
        {
            GetComputeShader();
        }

        if (denisty == null || denisty.Length != width * height)
        {
            denisty = new float[width * height];

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    denisty[x + y * width] = 0;
                }
            }

            for (int x = 0; x < 35; x++)
            {
                for (int y = height - 2; y < height - 1; y++)
                {
                    denisty[x + y * width] = FlowMax;
                }
            }
        }

        Heights_Buffer = new ComputeBuffer(width * height, sizeof(float));
        Heights_Buffer.SetData(new float[width * height]);

        Density_Buffer = new ComputeBuffer(width * height, sizeof(float));
        Density_Buffer.SetData(denisty);

        Future_Density_Buffer = new ComputeBuffer(width * height, sizeof(float));
        Future_Density_Buffer.SetData(denisty);

        compute.SetBuffer(kernel, "Heights_Buffer", Heights_Buffer);

        compute.SetBuffer(kernel, "Density_Buffer", Density_Buffer);
        compute.SetBuffer(kernel, "Future_Density_Buffer", Future_Density_Buffer);
    }

    private void ReleaseBuffers()
    {

        if (Heights_Buffer != null)
        {
            Heights_Buffer.Release();
        }

        if (Density_Buffer != null)
        {
            Density_Buffer.Release();
        }

        if (Future_Density_Buffer != null)
        {
            Future_Density_Buffer.Release();
        }
    }

    public void Simulate()
    {

        int numberOfThreads = 8;

        InitializeBuffers();

        compute.SetInt("XWidth", width);
        compute.SetInt("YWidth", height);

        compute.SetFloat("FlowMin", FlowMin);
        compute.SetFloat("FlowMax", FlowMax);

        compute.SetInt("Simulation_Count", simulationCount);

        Density_Buffer.SetData(denisty);

        //calls compute kenrel, each pixel will have it's own thread because of the size used
        compute.Dispatch(kernel, Mathf.CeilToInt(width / (float)numberOfThreads), Mathf.CeilToInt(height / (float)numberOfThreads), 1);

        if (denisty == null || denisty.Length != width * height)
        {
            denisty = new float[width * height];
        }

        Future_Density_Buffer.GetData(denisty);

        ReleaseBuffers();

        simulationCount++;
    }
}
