using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(HexGPUMesh))]
public class DigHexMesh : MonoBehaviour
{
    public Camera cam;
    private HexGPUMesh gpuMesh;
    public Transform cursor;

    [Header("Hex Variables")]
    public int numberOfLayers = 10;
    public int visibleLayer = 0;
    private int[] hexIndices = new int[1];
    private int[] visibleHexIndices = new int[1];
    private float[] visibleHexHeights = new float[1];

    private int Width
    {
        get
        {
            return gpuMesh.size.x;
        }
    }

    private int Height
    {
        get
        {
            return gpuMesh.size.y;
        }
    }

    [System.Serializable]
    public class HoverInfo
    {
        public Vector3 lastMousePosition;
        public Vector3 worldPosition;
        public Vector2Int lastPosition;
        public Vector2Int positon;
        public bool newPositionThisFrame;
        public bool inBounds;
        public int visibleIndex;
    }

    public HoverInfo hoverInfo = new HoverInfo();
    private Plane hitPlane = new Plane(Vector3.back, Vector3.zero);

    private void Start()
    {
        gpuMesh = GetComponent<HexGPUMesh>();
        Initialize();
        UpdateMesh();        
    }

    private void Update()
    {
        RecordMouseMovement();
        Dig();

        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            visibleLayer++;
            UpdateHoverInfo();
            UpdateMesh();
        }
        else if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            visibleLayer--;
            UpdateHoverInfo();
            UpdateMesh();
        }
    }

    private int LinearIndex(int x, int y, int z)
    {
        return x + y * Width + z * Width * Height;
    }

    private void Initialize()
    {
        hexIndices = new int[Width * Height * numberOfLayers];
        visibleLayer = numberOfLayers - 1;

        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                for (int z = 0; z < numberOfLayers; z++)
                {
                    if(true || Random.value > 0.8f || z == numberOfLayers - 1)
                        hexIndices[LinearIndex(x, y, z)] = z + 1;
                    else
                        hexIndices[LinearIndex(x, y, z)] = 0;
                }
            }
        }

        UpdateVisibleHexIndices();
    }

    private int GetVisibleHexIndex(int x, int y)
    {
        int index = 0;
        visibleLayer = Mathf.Clamp(visibleLayer, 0, numberOfLayers - 1);
        for (int z = visibleLayer; z >= 0; z--)
        {
            index = LinearIndex(x, y, z);
            if (hexIndices[index] > 0)
            {
                return hexIndices[index];
            }
        }

        return 0;
    }

    private void UpdateVisibleHexIndices()
    {
        if(visibleHexIndices == null || visibleHexIndices.Length != Width * Height)
        {
            visibleHexIndices = new int[Width * Height];
        }

        if (visibleHexHeights == null || visibleHexHeights.Length != Width * Height)
        {
            visibleHexHeights = new float[Width * Height];
        }

        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                visibleHexIndices[LinearIndex(x, y, 0)] = GetVisibleHexIndex(x, y);
                visibleHexHeights[LinearIndex(x, y, 0)] = 0;// visibleHexIndices[LinearIndex(x, y, 0)] * -0.1f;
            }
        }
    }

    private void UpdateMesh()
    {
        UpdateVisibleHexIndices();
        gpuMesh.SetHexIndices(visibleHexIndices);
        gpuMesh.SetHexHeights(visibleHexHeights);
        gpuMesh.UpdateMesh();
    }

    private void RecordMouseMovement()
    {
        if(Input.mousePosition != hoverInfo.lastMousePosition)
        {
            hoverInfo.lastMousePosition = Input.mousePosition;
            UpdateHoverInfo();
        }
    }

    private void UpdateHoverInfo()
    {        
        Ray inRay = cam.ScreenPointToRay(Input.mousePosition);


        if (hitPlane.Raycast(inRay, out float enter))
        {
            Vector3 hitPoint = inRay.GetPoint(enter);           
            Vector2Int position = gpuMesh.WorldToHexPosition(hitPoint);

            cursor.transform.position = gpuMesh.HexCenter(position) + new Vector3(0, 0, -0.1f);

            hoverInfo.worldPosition = hitPoint;
            hoverInfo.positon = position;
            hoverInfo.newPositionThisFrame = hoverInfo.lastPosition != hoverInfo.positon;
            hoverInfo.lastPosition = position;
            hoverInfo.inBounds = gpuMesh.PositionInBounds(position);
            hoverInfo.visibleIndex = hoverInfo.inBounds ? GetVisibleHexIndex(position.x, position.y) : -1;
        }
    }

    private void Dig()
    {
        if (!hoverInfo.inBounds)
        {
            return;
        }

        if (Input.GetMouseButtonDown(0) || (hoverInfo.newPositionThisFrame && Input.GetMouseButton(0)))
        {
            UpdateHoverInfo();

            if(hoverInfo.visibleIndex > 1)
            {
                hexIndices[LinearIndex(hoverInfo.positon.x, hoverInfo.positon.y, hoverInfo.visibleIndex - 1)] = 0;                
                UpdateMesh();
            }
        }
        else if (Input.GetMouseButtonDown(1) || (hoverInfo.newPositionThisFrame && Input.GetMouseButton(1)))
        {
            UpdateHoverInfo();

            if (hoverInfo.visibleIndex < numberOfLayers)
            {
                hexIndices[LinearIndex(hoverInfo.positon.x, hoverInfo.positon.y, hoverInfo.visibleIndex)] = hoverInfo.visibleIndex + 1;
                UpdateMesh();
            }
        }
    }
}
