using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MarchingChunkManager : MonoBehaviour
{

    public MarchingCubesMeshGenerator chunkPrefab;

    public int chunkSize = 16;

    public Vector3Int size;

    public bool drawVisibleGizmos = true;
    public bool drawSurroundingGizmos = true;
    public HashSet<Vector3Int> visibleChunkPositions = new HashSet<Vector3Int>();
    public HashSet<Vector3Int> surroundingChunkPositions = new HashSet<Vector3Int>();

    private Dictionary<Vector3Int, MarchingCubesMeshGenerator> usedChunks = new Dictionary<Vector3Int, MarchingCubesMeshGenerator>();
    private Queue<MarchingCubesMeshGenerator> unusedChunks = new Queue<MarchingCubesMeshGenerator>();

    private Queue<Vector3Int> requestedChunks = new Queue<Vector3Int>();

    private Queue<Vector3Int> updateRequests = new Queue<Vector3Int>();

    public Transform playerPos;
    public Vector3Int playerChunkPos;

    public CameraDetector cameraDetector;

    private void Start()
    {
        visibleChunkPositions = new HashSet<Vector3Int>();
        surroundingChunkPositions = new HashSet<Vector3Int>();

        requestedChunks = new Queue<Vector3Int>(0);        

        playerPos.hasChanged = false;        

        playerChunkPos = new Vector3Int(-99, 42, 99);

        OnPlayerTrasformHasChanged();
    }

    private void Update()
    {
        CreateNextRequestedChunk();                      
    }

    private void LateUpdate()
    {
        if (playerPos.hasChanged)
        {
            OnPlayerTrasformHasChanged();
        }
    }

    private void OnPlayerTrasformHasChanged()
    {

        //Debug.Log("Player position has changed");

        var pos = GetPlayerChunkPosition();

        if (pos != playerChunkPos)
        {
            playerChunkPos = pos;
            OnPlayerChunkPositionChanged();
        }

        UpdateVisibleChunkPositions();


        GatherUnusedChunks();
        RequestNewlyVisibleChunks();

        playerPos.hasChanged = false;
    }

    private void OnPlayerChunkPositionChanged()
    {
        
    }      

    public void RequestChunk(int x, int y, int z)
    {
        requestedChunks.Enqueue(new Vector3Int(x, y, z));
    }

    public void CreateNextRequestedChunk()
    {
        if(requestedChunks.Count <= 0)
        {
            return;
        }

        Vector3Int chunkPos = requestedChunks.Dequeue();

        if (usedChunks.ContainsKey(chunkPos))
        {
            Debug.LogWarning("Warning requested a chunk that has already been made");
            return;
        }

        if(unusedChunks.Count <= 0)
        {
            CreateNewChunk();
        }
        else
        {
            //Debug.Log("Reusing chunk");
        }

        MarchingCubesMeshGenerator chunk = unusedChunks.Dequeue();

        chunk.transform.position = new Vector3(chunkPos.x, chunkPos.y, chunkPos.z) * chunkSize;// + Vector3.one * chunkSize * 0.5f;
        chunk.gameObject.SetActive(true);
        chunk.meshSimplificationLevel = 0;
        chunk.densityGenerator.noiseData.offset = new Vector3(chunkPos.x, chunkPos.y, chunkPos.z) * chunkSize;
        chunk.densityGenerator.UpdateData();
        chunk.meshSimplificationLevel = 0;

        float dist = Vector3.Distance(playerChunkPos, chunkPos);

        if(dist < 3)
        {
            //chunk.meshSimplificationLevel = 0;
        }
        else if (dist < 5)
        {
            //chunk.meshSimplificationLevel = 1;
        }
        else
        {
            //chunk.meshSimplificationLevel = 2;
        }

        chunk.UpdateMesh();

        usedChunks.Add(chunkPos, chunk);
        //CreateChunk(chunkPos.x, chunkPos.y, chunkPos.z);
    }

    private void GatherUnusedChunks()
    {
        List<Vector3Int> toRemove = new List<Vector3Int>();

        var keys = usedChunks.Keys;

        foreach (var key in keys)
        {
            if (!visibleChunkPositions.Contains(key))
            {
                toRemove.Add(key);
            }
        }

        foreach (var key in toRemove)
        {
            unusedChunks.Enqueue(usedChunks[key]);
            usedChunks[key].gameObject.SetActive(false);

            //Debug.Log("Removing " + usedChunks[key].gameObject.name);

            usedChunks.Remove(key);

        }
    }

    private void RequestNewlyVisibleChunks()
    {
        requestedChunks.Clear();
        foreach (var pos in visibleChunkPositions)
        {
            if (!usedChunks.ContainsKey(pos) && !requestedChunks.Contains(pos))
            {
                requestedChunks.Enqueue(pos);
                //Debug.Log("Requested chunk at " + pos);
            }
        }
    }


    private int chunkCount = 0;
    private MarchingCubesMeshGenerator CreateNewChunk()
    {
        var chunk = Instantiate(chunkPrefab);
        chunk.gameObject.SetActive(true);

        chunk.gameObject.transform.position = transform.position;
        chunk.voxelsPerAxis = chunkSize;

        chunk.gameObject.transform.parent = transform;

        chunk.gameObject.name = "Chunk " + chunkCount;
        chunk.densityGenerator.noiseData.chunkSize = chunkSize + 1;
        chunk.voxelsPerAxis = chunkSize;

        chunkCount++;

        unusedChunks.Enqueue(chunk);

        return chunk;
    }

    public Bounds GetChunkBounds(int x, int y, int z)
    {
        Vector3 offset = new Vector3(
            x * chunkSize,
            y * chunkSize,
            z * chunkSize);

        Vector3 center = offset + Vector3.one * chunkSize * 0.5f;
        Bounds b = new Bounds(center, Vector3.one * chunkSize);

        return b;
    }

    private void UpdateVisibleChunkPositions()
    {
        visibleChunkPositions.Clear();
        surroundingChunkPositions.Clear();

        List<Vector3Int> visiblePos = new List<Vector3Int>();
        List<Vector3Int> surroundingPos = new List<Vector3Int>();

        cameraDetector.UpdateFrustrum();
        bool visible = false;
        Bounds b;
        Vector3Int localChunkPos;
        Vector3Int globalChunkPos;
        Vector3Int offset = size / 2; new Vector3Int(Mathf.RoundToInt(size.x / 2f), Mathf.RoundToInt(size.y / 2f), Mathf.RoundToInt(size.z / 2f));

        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                for (int z = 0; z < size.z; z++)
                {
                    localChunkPos = new Vector3Int(x, y, z) - offset;
                    globalChunkPos = localChunkPos + playerChunkPos;
                    b = GetChunkBounds(globalChunkPos.x, globalChunkPos.y, globalChunkPos.z);
                    visible = cameraDetector.BoundsIsVisible(b);
                    float dist = Vector3.Distance(playerChunkPos, globalChunkPos);
                    if (visible || dist < 3)
                    {
                        visiblePos.Add(globalChunkPos);
                    }

                    surroundingPos.Add(globalChunkPos);
                }
            }
        }

        visibleChunkPositions = new HashSet<Vector3Int>(visiblePos);
        surroundingChunkPositions = new HashSet<Vector3Int>(surroundingPos);
    }

    private Vector3Int GetPlayerChunkPosition()
    {
        var pos = new Vector3Int(
            (int)(playerPos.position.x / chunkSize),
            (int)(playerPos.position.y / chunkSize),
            (int)(playerPos.position.z / chunkSize));

        return pos;
    }

    private void OnDrawGizmos()
    {
        if (!(drawVisibleGizmos || drawSurroundingGizmos))
        {
            return;
        }

        Bounds b;

        if (drawSurroundingGizmos)
        {
            Gizmos.color = Color.green;
            foreach (var pos in surroundingChunkPositions)
            {
                b = GetChunkBounds(pos.x, pos.y, pos.z);


                Gizmos.DrawWireCube(b.center, b.size);
            }
        }

        if (drawVisibleGizmos)
        {
            Gizmos.color = Color.blue;
            foreach (var pos in visibleChunkPositions)
            {
                b = GetChunkBounds(pos.x, pos.y, pos.z);


                Gizmos.DrawWireCube(b.center, b.size);
            }
        }
    }
}
