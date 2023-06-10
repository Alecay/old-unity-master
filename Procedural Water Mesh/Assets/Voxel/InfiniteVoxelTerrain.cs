using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InfiniteVoxelTerrain : MonoBehaviour
{
    public float voxelScale = 0.25f;

    public int chunkSize = 32;
    public int chunkHeight = 384;    

    public VoxelP chunkPrefab;

    public VoxelMeshGenerator meshGenerator;
    public VoxelDensityGenerator densityGenerator;

    public Vector2Int playerChunkPos;
    private Vector2Int lastPlayerChunkPos;

    [Range(5, 35)]
    public int viewDistance = 5;

    public struct ChunkRequest
    {
        public Vector2Int position;
        public int levelOfDetail;

        public ChunkRequest(Vector2Int position, int levelOfDetail)
        {
            this.position = position;
            this.levelOfDetail = levelOfDetail;
        }
    }

    public struct ChunkData
    {
        public VoxelP mesh;
        public Vector2Int Position
        {
            get
            {
                return mesh.position;
            }
        }

        public int LevelOfDetail
        {
            get
            {
                return mesh.levelOfDetail;
            }

            set
            {
                mesh.levelOfDetail = value;
            }
        }

        private Dictionary<int, Mesh> lodMeshes;
        public Mesh collisionMesh;

        public ChunkData(VoxelP mesh) : this()
        {
            this.mesh = mesh;
            lodMeshes = new Dictionary<int, Mesh>();
        }

        public void AddLODMesh(int levelOfDetail, Mesh mesh)
        {
            if (lodMeshes.ContainsKey(levelOfDetail))
            {
                lodMeshes[levelOfDetail] = mesh;
            }
            else
            {
                lodMeshes.Add(levelOfDetail, mesh);
            }
        }

        public Mesh GetLODMesh(int levelOfDetail)
        {
            if (lodMeshes.ContainsKey(levelOfDetail))
            {
                return lodMeshes[levelOfDetail];
            }
            else
            {
                return null;
            }
        }
    }

    private Queue<ChunkRequest> requestedChunks = new Queue<ChunkRequest>();
    private Queue<VoxelP> unusedChunks = new Queue<VoxelP>();
    private Dictionary<Vector2Int, ChunkData> usedChunks = new Dictionary<Vector2Int, ChunkData>();

    private Vector2Int[] offsets;

    public int totalChunksCreated = 0;

    public Transform playerTrans;

    public float percentLoaded;

    private void Start()
    {
        densityGenerator.noiseData.seed = Random.Range(-1000, 1000);

        requestedChunks = new Queue<ChunkRequest>();
        unusedChunks = new Queue<VoxelP>();
        usedChunks = new Dictionary<Vector2Int, ChunkData>();

        CreateOffsets();

        RequestChunksInViewDistance();

        densityGenerator.StartProcessingDensityRequests();
        meshGenerator.StartProcessingMeshRequests();
    }

    private void Update()
    {
        for (int i = 0; i < 5; i++)
        {
            if (requestedChunks.Count > 0 && !densityGenerator.ProcessingDensityRequest && !meshGenerator.ProcessingMeshRequest)
            {
                ProcessNextRequest();
            }
        }



        playerChunkPos = new Vector2Int(Mathf.FloorToInt(playerTrans.position.x / (chunkSize * voxelScale)), 
            Mathf.FloorToInt(playerTrans.position.z / (chunkSize * voxelScale)));

        if(playerChunkPos != lastPlayerChunkPos)
        {
            if(usedChunks.TryGetValue(playerChunkPos, out ChunkData chunk) && chunk.LevelOfDetail != 3)
            {
                Debug.LogWarning("Warning player has entered a chunk with low LOD");
            }

            GatherUnusedChunks();
            RequestChunksInViewDistance();
            lastPlayerChunkPos = playerChunkPos;
        }
    }

    private void GatherUnusedChunks()
    {
        var keys = usedChunks.Keys;
        ChunkData chunk;

        List<Vector2Int> toRemove = new List<Vector2Int>();

        foreach (var pos in keys)
        {
            chunk = usedChunks[pos];

            if((pos - playerChunkPos).magnitude > viewDistance)
            {
                chunk.mesh.ClearMesh();                
                toRemove.Add(pos);
                unusedChunks.Enqueue(chunk.mesh);
            }
        }

        for (int i = 0; i < toRemove.Count; i++)
        {
            usedChunks.Remove(toRemove[i]);
        }
    }

    private void RequestChunksInViewDistance()
    {
        requestedChunks.Clear();

        Vector2Int pos;
        ChunkData chunk;
        int levelOfDetail = 3;

        int minLODRange = viewDistance - viewDistance / 4;
        int midLODRange = viewDistance - viewDistance / 2;
        for (int i = 0; i < offsets.Length; i++)
        {
            pos = offsets[i] + playerChunkPos;

            levelOfDetail = 3;
            if (offsets[i].magnitude > minLODRange)
            {
                levelOfDetail = 1;
            }
            else if (offsets[i].magnitude > midLODRange)
            {
                levelOfDetail = 2;
            }

            if (usedChunks.TryGetValue(pos, out chunk))
            {
                if(chunk.LevelOfDetail != levelOfDetail)
                {
                    requestedChunks.Enqueue(new ChunkRequest(offsets[i] + playerChunkPos, levelOfDetail));
                }
            }
            else
            {
                requestedChunks.Enqueue(new ChunkRequest(offsets[i] + playerChunkPos, levelOfDetail));
            }
            
        }
    }

    private void ProcessNextRequest()
    {
        var request = requestedChunks.Dequeue();
        ChunkData chunk;

        if (usedChunks.TryGetValue(request.position, out chunk))
        {
            if(chunk.LevelOfDetail != request.levelOfDetail)
            {
                //Store current mesh with LOD
                //chunk.AddLODMesh(chunk.LevelOfDetail, chunk.mesh.RenderMesh);
                //if (chunk.LevelOfDetail == 3)
                //{
                //    if(chunk.mesh.CollisionMesh == null)
                //    {
                //        Debug.LogError("Error storing null mesh");
                //    }
                //    chunk.collisionMesh = chunk.mesh.CollisionMesh;
                //    print("Storing Collision mesh");
                //}

                ////Lookup mesh with LOD
                //Mesh m = chunk.GetLODMesh(request.levelOfDetail);
                //if(m != null)
                //{
                //    print("Reusing LOD Mesh");
                //    chunk.mesh.SetMesh(m, true);

                //    if(request.levelOfDetail == 3)
                //    {
                //        chunk.mesh.SetMesh(chunk.collisionMesh, false, true);
                //        print("Reusing Collision mesh");
                //    }
                //}
                //else //Create new mesh if not availible
                //{                    
                //    chunk.mesh.UpdateMesh();
                //}

                //Set new LOD
                chunk.LevelOfDetail = request.levelOfDetail;
                chunk.mesh.UpdateMesh();
            }            
            return;
        }


        if(unusedChunks.Count > 0)
        {
            chunk = new ChunkData(unusedChunks.Dequeue());
        }
        else
        {
            chunk = new ChunkData(CreateNewChunk(request.position, request.levelOfDetail));
        }

        chunk.mesh.position = request.position;
        chunk.mesh.UpdatePosition();
        chunk.LevelOfDetail = request.levelOfDetail;

        densityGenerator.RequestDensityValues(new VoxelDensityGenerator.DensityRequest(chunk.mesh));

        usedChunks.Add(request.position, chunk);

        totalChunksCreated = usedChunks.Count + unusedChunks.Count;
        percentLoaded = usedChunks.Count / (float)offsets.Length * 100;
    }

    private VoxelP CreateNewChunk(Vector2Int position, int levelOfDetail)
    {
        var chunk = Instantiate(chunkPrefab);
        chunk.ClearMesh();

        chunk.gameObject.name = "Chunk";        

        chunk.width = chunkSize;
        chunk.height = chunkHeight;

        chunk.voxelScale = voxelScale;

        chunk.levelOfDetail = levelOfDetail;        

        chunk.position = position;
        chunk.UpdatePosition();
        chunk.transform.parent = transform;

        chunk.meshGenerator = meshGenerator;
        chunk.densityGenerator = densityGenerator;        
        
        return chunk;
    }

    private void CreateOffsets()
    {
        int width = viewDistance * 2 + 1;
        offsets = new Vector2Int[width * width];

        for (int z = 0; z < width; z++)
        {
            for (int x = 0; x < width; x++)
            {
                offsets[x + z * width] = new Vector2Int(x - viewDistance, z - viewDistance);
                //offsets.Add(new Vector2Int(x - viewDistance, z - viewDistance));
            }
        }

        List<Vector2Int> sorted = new List<Vector2Int>(offsets);

        //sorted.Sort((pos1, pos2) => Mathf.Max(Mathf.Abs(pos1.x), Mathf.Abs(pos1.y)).CompareTo(Mathf.Max(Mathf.Abs(pos2.x), Mathf.Abs(pos2.y))));
        sorted.Sort((pos1, pos2) => pos1.magnitude.CompareTo(pos2.magnitude));

        List<Vector2Int> circle = new List<Vector2Int>();
        for (int i = 0; i < sorted.Count; i++)
        {
            if(sorted[i].magnitude <= viewDistance + 1)
            {
                circle.Add(sorted[i]);
            }
        }

        offsets = circle.ToArray();
    }

    private void OnDrawGizmos()
    {
        int viewDistance = this.viewDistance * 2;

        Gizmos.color = Color.blue;
        Vector3 p1, p2;
        Vector3 anchor = new Vector3(playerChunkPos.x - viewDistance / 2, 0, playerChunkPos.y - viewDistance / 2) * chunkSize * voxelScale;


        for (int x = 0; x <= viewDistance + 1; x++)
        {
            if(x == viewDistance / 2 || x == viewDistance / 2 + 1)
            {
                Gizmos.color = Color.red;
            }
            else
            {
                Gizmos.color = Color.blue;
            }            

            p1 = new Vector3(x, 0, 0) * chunkSize * voxelScale + anchor;
            p2 = new Vector3(x, 0, viewDistance + 1) * chunkSize * voxelScale + anchor;

            Gizmos.DrawLine(p1, p2);
        }

        for (int z = 0; z <= viewDistance + 1; z++)
        {
            if (z == viewDistance / 2 || z == viewDistance / 2 + 1)
            {
                Gizmos.color = Color.red;
            }
            else
            {
                Gizmos.color = Color.blue;
            }

            p1 = new Vector3(0, 0, z) * chunkSize * voxelScale + anchor;
            p2 = new Vector3(viewDistance + 1, 0, z) * chunkSize * voxelScale + anchor;

            Gizmos.DrawLine(p1, p2);
        }        
    }

    public Vector3Int ToWorldPosition(Vector3 point)
    {
        Vector3Int worldPos = new Vector3Int(
            Mathf.FloorToInt(point.x / voxelScale),
            Mathf.FloorToInt(point.y / voxelScale),
            Mathf.FloorToInt(point.z / voxelScale));

        return worldPos;
    }

    public void SetVoxel(Vector3 hitPoint, string voxelID)
    {
        Vector2Int chunkPos = new Vector2Int(Mathf.FloorToInt(hitPoint.x / (chunkSize * voxelScale)), Mathf.FloorToInt(hitPoint.z / (chunkSize * voxelScale)));
        Vector3Int worldPos = new Vector3Int(Mathf.FloorToInt(hitPoint.x / voxelScale), Mathf.FloorToInt(hitPoint.y / voxelScale), Mathf.FloorToInt(hitPoint.z / voxelScale));
        Vector3Int localPos = worldPos - new Vector3Int(chunkPos.x, 0, chunkPos.y) * chunkSize;
        //Debug.Log("Chunk pos: " + chunkPos.ToString());

        if (usedChunks.TryGetValue(chunkPos, out ChunkData chunk))
        {            
            if (!chunk.mesh.waitingForMeshUpdate)
            {                
                chunk.mesh.SetVoxel(localPos, voxelID);

                chunk.mesh.SetVoxel(localPos - new Vector3Int(1, 0, 0), voxelID);
                chunk.mesh.SetVoxel(localPos + new Vector3Int(1, 0, 0), voxelID);
                chunk.mesh.SetVoxel(localPos - new Vector3Int(0, 0, 1), voxelID);
                chunk.mesh.SetVoxel(localPos + new Vector3Int(0, 0, 1), voxelID);


                chunk.mesh.UpdateMeshWithPirority();
            }
            else
            {
                //Debug.Log("skipped waiting for mesh update");
            }
                
        }

        Vector2Int nChunkPos;

        if(localPos.x == 0)
        {
            nChunkPos = chunkPos - new Vector2Int(1, 0);
            if (usedChunks.TryGetValue(nChunkPos, out ChunkData nChunk))
            {
                if (!nChunk.mesh.waitingForMeshUpdate)
                {                    
                    nChunk.mesh.SetVoxel(new Vector3Int(chunkSize, localPos.y, localPos.z), voxelID);
                    nChunk.mesh.UpdateMeshWithPirority();
                }
            }
        }
        else if (localPos.x == chunkSize - 1)
        {
            nChunkPos = chunkPos + new Vector2Int(1, 0);
            if (usedChunks.TryGetValue(nChunkPos, out ChunkData nChunk))
            {
                if (!nChunk.mesh.waitingForMeshUpdate)
                {
                    nChunk.mesh.SetVoxel(new Vector3Int(-1, localPos.y, localPos.z), voxelID);
                    nChunk.mesh.UpdateMeshWithPirority();
                }
            }
        }

        if (localPos.z == 0)
        {
            nChunkPos = chunkPos - new Vector2Int(0, 1);
            if (usedChunks.TryGetValue(nChunkPos, out ChunkData nChunk))
            {
                if (!nChunk.mesh.waitingForMeshUpdate)
                {
                    nChunk.mesh.SetVoxel(new Vector3Int(localPos.x, localPos.y, chunkSize), voxelID);
                    nChunk.mesh.UpdateMeshWithPirority();
                }
            }
        }
        else if (localPos.z == chunkSize - 1)
        {
            nChunkPos = chunkPos + new Vector2Int(0, 1);
            if (usedChunks.TryGetValue(nChunkPos, out ChunkData nChunk))
            {
                if (!nChunk.mesh.waitingForMeshUpdate)
                {
                    nChunk.mesh.SetVoxel(new Vector3Int(localPos.x, localPos.y, -1), voxelID);
                    nChunk.mesh.UpdateMeshWithPirority();
                }
            }
        }

    }
}
