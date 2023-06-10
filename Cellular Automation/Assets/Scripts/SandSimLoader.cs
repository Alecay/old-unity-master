using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SandSimLoader : MonoBehaviour
{

    public const string COMPUTE_SHADER_NAME = "SandSimCompute";
    private static ComputeShader compute;
    public const string SIMULATION_FUNCTION_NAME = "CalculateFutureCells";
    public const string COPY_FUNCTION_NAME = "CopyFutureCellsToCells";
    public const string TEXTURE_FUNCTION_NAME = "CreateTexture";
    private static int simKernel = 0;
    private static int copyKernel = 0;
    private static int textureKernel = 0;

    private ComputeBuffer Cells_Buffer;
    private ComputeBuffer Future_Cells_Buffer;

    public bool update = true;
    public bool emissionEnabled = true;
    public bool useCellAverage = true;

    public float cellMin = 1.0f;
    public float cellMax = 5.0f;

    public UpdateSpeed updateSpeed;
    public enum UpdateSpeed
    {
        OneX,
        TwoX,
        ThreeX,
        FourX
    }

    public int width = 100;
    public int height = 100;

    public Color color;

    public int simulationCount = 0;
    public float denistySum = 0;

    public RenderTexture texture;

    public Material mat;

    private void Start()
    {
        InitializeBuffers();
    }

    private void Update()
    {
        if (update || Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKey(KeyCode.UpArrow))
        {
            for (int i = 0; i < (int)updateSpeed + 1; i++)
            {
                Run(i == (int)updateSpeed);
            }
        }

        if (Input.GetKeyDown(KeyCode.S))
        {
            for (int x = 0; x < width; x++)
            {
                for (int y = height - 4; y < height; y++)
                {
                    //cells[x + y * width] = Mathf.Round(Random.value);
                }
            }
        }
        else if (Input.GetKey(KeyCode.W))
        {
            for (int x = width / 2; x < width / 2 + 5; x++)
            {
                for (int y = height - 2; y < height; y++)
                {
                    //cells[x + y * width] = 5.0f;
                }
            }
        }
        else if (Input.GetKeyDown(KeyCode.U))
        {
            update = !update;
        }
        else if (Input.GetKeyDown(KeyCode.E))
        {
            emissionEnabled = !emissionEnabled;
        }
        else if (Input.GetKeyDown(KeyCode.A))
        {
            useCellAverage = !useCellAverage;
        }
        else if (Input.GetKeyDown(KeyCode.C))
        {
            InitializeBuffers();
        }
        else if (Input.GetKeyDown(KeyCode.P))
        {            
            for (int x = 0; x < width; x++)
            {
                //Debug.Log("(" + x + ", 0):  " + cells[x]);
            }
        }        
    }

    private void OnDestroy()
    {
        ReleaseBuffers();
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
            simKernel = compute.FindKernel(SIMULATION_FUNCTION_NAME);
            copyKernel = compute.FindKernel(COPY_FUNCTION_NAME);
            textureKernel = compute.FindKernel(TEXTURE_FUNCTION_NAME);
        }
    }

    public void InitializeBuffers()
    {
        if (compute == null)
        {
            GetComputeShader();
        }

        Cells_Buffer = new ComputeBuffer(width * height, sizeof(float));
        Cells_Buffer.SetData(new float[width * height]);

        Future_Cells_Buffer = new ComputeBuffer(width * height, sizeof(float));
        Future_Cells_Buffer.SetData(new float[width * height]);
    }

    private void ReleaseBuffers()
    {

        if (Cells_Buffer != null)
        {
            Cells_Buffer.Release();
        }

        if (Future_Cells_Buffer != null)
        {
            Future_Cells_Buffer.Release();
        }
    }

    public void Run(bool updateTexture = false)
    {
        int numberOfThreads = 8;        

        compute.SetInt("Simulations", simulationCount);
        compute.SetInt("RandIndex", 0);

        compute.SetFloat("Emission_Enabled", emissionEnabled ? 1 : 0);

        compute.SetInt("XWidth", width);
        compute.SetInt("YWidth", height);

        compute.SetVector("Color", color);

        compute.SetFloat("Cell_Min", cellMin);
        compute.SetFloat("Cell_Max", cellMax);

        compute.SetBuffer(simKernel, "Cells_Buffer", Cells_Buffer);
        compute.SetBuffer(simKernel, "Future_Cells_Buffer", Future_Cells_Buffer);

        //SIMULATE
        //calls compute kenrel, each pixel will have it's own thread because of the size used
        compute.Dispatch(simKernel, Mathf.CeilToInt(width / (float)numberOfThreads), Mathf.CeilToInt(height / (float)numberOfThreads), 1);

        //COPY
        compute.SetBuffer(copyKernel, "Cells_Buffer", Cells_Buffer);
        compute.SetBuffer(copyKernel, "Future_Cells_Buffer", Future_Cells_Buffer);

        //calls compute kenrel, each pixel will have it's own thread because of the size used
        compute.Dispatch(copyKernel, Mathf.CeilToInt(width / (float)numberOfThreads), Mathf.CeilToInt(height / (float)numberOfThreads), 1);

        //Update Texture

        if (updateTexture)
        {
            if (texture == null || texture.width != width || texture.height != height)
            {
                texture = new RenderTexture(width, height, 24);
                texture.enableRandomWrite = true;
                texture.filterMode = FilterMode.Point;
                texture.Create();

                if (mat)
                {
                    mat.SetTexture("_MainTex", texture);
                }
            }

            compute.SetFloat("Use_Cell_Average", useCellAverage ? 1.0f : 0.0f);

            compute.SetVector("Color", color);
            compute.SetTexture(textureKernel, "Result", texture);

            compute.SetBuffer(textureKernel, "Cells_Buffer", Cells_Buffer);

            //calls compute kenrel, each pixel will have it's own thread because of the size used
            compute.Dispatch(textureKernel, Mathf.CeilToInt(width / (float)numberOfThreads), Mathf.CeilToInt(height / (float)numberOfThreads), 1);
        }


        simulationCount++;
    }
}
