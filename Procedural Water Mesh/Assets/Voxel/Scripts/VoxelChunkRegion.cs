using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VoxelChunkRegion : MonoBehaviour
{
    [Header("Debug Info")]
    public int totalVertexCount;
    public float totalLoadTime = 0;
    public int totalChunks = 0;
    public float averageLoadTimePerChunk;

    [Header("Sizing")]
    public Vector2Int position;

    public int chunkSize = 16;
    public int regionSize = 16;
    public int pillarHeight = 4;    

    public VoxelChunk chunkPrefab;

    public VoxelChunkPillar[] pillars;

    public VoxelStateGenerator stateGenerator;

    public Transform setTrans;

    private void Start()
    {
        UpdatePosition();
        //CreatePillars();

        StartCoroutine(CreatePillarsCo());
    }

    IEnumerator CreatePillarsCo()
    {
        var timer = new System.Diagnostics.Stopwatch();
        timer.Start();

        pillars = new VoxelChunkPillar[regionSize * regionSize];

        for (int x = 0; x < regionSize; x++)
        {
            for (int y = 0; y < regionSize; y++)
            {
                CreatePillar(new(x, y));
                yield return null;
            }
        }

        print("Finished creating pillars in " + timer.ElapsedMilliseconds + " mms");

        StartCoroutine(UpdatePillarsCo());
    }

    IEnumerator UpdatePillarsCo()
    {
        var timer = new System.Diagnostics.Stopwatch();
        timer.Start();

        totalVertexCount = 0;
        totalLoadTime = 0;
        totalChunks = 0;

        for (int x = 0; x < regionSize; x++)
        {
            for (int y = 0; y < regionSize; y++)
            {
                pillars[x + y * regionSize].CreatePillar();

                totalVertexCount += pillars[x + y * regionSize].totalVertexCount;
                totalLoadTime += pillars[x + y * regionSize].totalLoadTime;

                if(pillars[x + y * regionSize].totalVertexCount > 0)
                {
                    totalChunks++;
                }

                yield return null;
            }
        }

        averageLoadTimePerChunk = totalLoadTime / totalChunks;

        print("Finished updating pillars in " + (timer.ElapsedMilliseconds / 1000f) + " secs");

        StartCoroutine(UpdateChunksCo());
    }

    IEnumerator UpdateChunksCo()
    {
        var timer = new System.Diagnostics.Stopwatch();
        timer.Start();

        totalVertexCount = 0;
        totalLoadTime = 0;
        totalChunks = 0;

        for (int x = 0; x < regionSize; x++)
        {
            for (int z = 0; z < regionSize; z++)
            {
                for (int y = pillarHeight - 1; y >= 0; y--)
                {
                    if (stateGenerator.loadingData)
                    {
                        yield return null;
                    }

                    RequestChunkData(new(x, y, z), pillars[x + z * regionSize]);

                    //var chunk = GetChunk(new(x, y, z));

                    //totalVertexCount += chunk.VertexCount;
                    //totalLoadTime += chunk.LastLoadTime;

                    //if (chunk.VertexCount > 0)
                    //{
                    //    totalChunks++;
                    //}

                    yield return null;
                }

                
            }
        }

        averageLoadTimePerChunk = totalLoadTime / totalChunks;

        print("Finished updating pillars in " + (timer.ElapsedMilliseconds / 1000f) + " secs");
    }

    private void CreatePillars()
    {
        pillars = new VoxelChunkPillar[regionSize * regionSize];

        for (int x = 0; x < regionSize; x++)
        {
            for (int y = 0; y < regionSize; y++)
            {
                CreatePillar(new(x, y));
            }
        }
    }

    private void UpdatePillars()
    {
        for (int x = 0; x < regionSize; x++)
        {
            for (int y = 0; y < regionSize; y++)
            {
                pillars[x + y * regionSize].CreatePillar();
            }
        }
    }

    private void CreatePillar(Vector2Int position)
    {
        var pillarObj = new GameObject("Pillar");
        var pillar = pillarObj.AddComponent<VoxelChunkPillar>();

        pillar.chunkSize = chunkSize;
        pillar.chunkPrefab = chunkPrefab;
        pillar.height = pillarHeight;
        
        pillar.position = position + this.position * regionSize;
        pillar.UpdatePosition();
        pillar.transform.parent = transform;

        pillars[position.x + position.y * regionSize] = pillar;
    }

    public void UpdatePosition()
    {
        transform.position = new Vector3(position.x, 0, position.y) * chunkSize * regionSize;
    }

    private void RequestChunkData(Vector3Int localChunkPos, VoxelChunkPillar pillar)
    {
        Vector3Int offset = new Vector3Int(position.x, 0, position.y) * regionSize;

        Vector3Int realWorldPos = localChunkPos + offset;                
        
        stateGenerator.RequestChunkStates(realWorldPos, pillar);
    }

    public enum ChunkRequestType
    {
        Empty, //An empty chunk
        Full, //A full chunk
        Standard //A chunk that's not full and not empty
    }

    private Queue<Vector3Int> requestedStandardChunks = new Queue<Vector3Int>();
    private Queue<Vector3Int> requestedFullChunks = new Queue<Vector3Int>();
    private Queue<Vector3Int> requestedEmptyChunks = new Queue<Vector3Int>();

    public void RequestChunk(Vector3Int localChunkPos, ChunkRequestType type)
    {
        var pillar = pillars[localChunkPos.x + localChunkPos.z * regionSize];

        if (!InBounds(localChunkPos))
        {
            return;
        }

        switch (type)
        {
            case ChunkRequestType.Empty:
                requestedEmptyChunks.Enqueue(localChunkPos);
                break;
            case ChunkRequestType.Full:
                requestedFullChunks.Enqueue(localChunkPos);
                break;
            case ChunkRequestType.Standard:
                requestedStandardChunks.Enqueue(localChunkPos);
                break;
            default:
                break;
        }

    }

    private IEnumerator BuildChunks()
    {
        while (true)
        {
            ProcessNextChunkRequest();

            yield return null;
        }
    }

    private void ProcessNextChunkRequest()
    {

    }

    public bool InBounds(Vector3Int localChunkPos)
    {
        if (localChunkPos.x < 0 || localChunkPos.x >= regionSize
            || localChunkPos.y < 0 || localChunkPos.y >= pillarHeight
            || localChunkPos.z < 0 || localChunkPos.z >= regionSize)
        {
            return false;
        }

        return true;
    }

    public bool HasChunkBeenMade(Vector3Int localChunkPos)
    {
        if (!InBounds(localChunkPos))
        {
            return false;
        }

        return pillars[localChunkPos.x + localChunkPos.z * regionSize].chunks[localChunkPos.y] != null;
    }

    public VoxelChunk GetChunk(Vector3Int localChunkPos)
    {
        if(!InBounds(localChunkPos))
        {
            return null;
        }

        return pillars[localChunkPos.x + localChunkPos.z * regionSize].chunks[localChunkPos.y];
    }

    public void SetVoxel(string id, Vector3Int position)
    {
        Vector3Int chunkPos = position / chunkSize;

        var chunk = GetChunk(chunkPos);

        bool inBounds = InBounds(chunkPos);

        if (!inBounds)
        {
            return;
        }

        bool madeNew = false;

        if(chunk == null)
        {
            pillars[chunkPos.x + chunkPos.z * regionSize].CreateChunk(chunkPos.y);
            chunk = GetChunk(chunkPos);
            print("Created new chunk");
            madeNew = true;
        }

        if(chunk == null)
        {
            return;
        }

        Vector3Int localPos = position - chunkPos * chunkSize;

        //chunk.mesh.states.palette.PrintVoxelIDs();

        bool changed = false;

        chunk.SetVoxel(id, localPos, out changed, true);

        //chunk.mesh.states.palette.PrintVoxelIDs();        

        if (!changed && !madeNew)
        {
            return;
        }

        //Debug.Log("set voxel at " + localPos + " chunk pos " + chunkPos);
        chunk.UpdateMesh();

        VoxelChunk neighbor;

        if(localPos.x == 0)
        {
            neighbor = GetChunk(chunkPos + new Vector3Int(-1, 0, 0));

            if(neighbor != null)
            {
                neighbor.SetVoxel(id, new(chunkSize, localPos.y, localPos.z), out changed, true);

                if (changed)
                {
                    neighbor.UpdateMesh();
                }
            }
        }
        else if (localPos.x == chunkSize - 1)
        {
            neighbor = GetChunk(chunkPos + new Vector3Int(1, 0, 0));

            if (neighbor != null)
            {
                neighbor.SetVoxel(id, new(-1, localPos.y, localPos.z), out changed, true);

                if (changed)
                {
                    neighbor.UpdateMesh();
                }
            }
        }

        if (localPos.y == 0)
        {
            neighbor = GetChunk(chunkPos + new Vector3Int(0, -1, 0));

            if (neighbor != null)
            {
                neighbor.SetVoxel(id, new(localPos.x, chunkSize, localPos.z), out changed, true);

                if (changed)
                {
                    neighbor.UpdateMesh();
                }
            }
        }
        else if (localPos.y == chunkSize - 1)
        {
            neighbor = GetChunk(chunkPos + new Vector3Int(0, 1, 0));

            if (neighbor != null)
            {
                neighbor.SetVoxel(id, new(localPos.x, -1, localPos.z), out changed, true);

                if (changed)
                {
                    neighbor.UpdateMesh();
                }
            }
        }

        if (localPos.z == 0)
        {
            neighbor = GetChunk(chunkPos + new Vector3Int(0, 0, -1));

            if (neighbor != null)
            {
                neighbor.SetVoxel(id, new(localPos.x, localPos.y, chunkSize), out changed, true);

                if (changed)
                {
                    neighbor.UpdateMesh();
                }
            }
        }
        else if (localPos.z == chunkSize - 1)
        {
            neighbor = GetChunk(chunkPos + new Vector3Int(0, 0, 1));

            if (neighbor != null)
            {
                neighbor.SetVoxel(id, new(localPos.x, localPos.y, -1), out changed, true);

                if (changed)
                {
                    neighbor.UpdateMesh();
                }
            }
        }

    }

    public void SetVoxel(string id, Vector3Int position, ref HashSet<Vector3Int> chunksToUpdate)
    {       

        Vector3Int chunkPos = position / chunkSize;

        var chunk = GetChunk(chunkPos);

        bool inBounds = InBounds(chunkPos);

        if (!inBounds)
        {
            return;
        }

        bool madeNew = false;

        if (chunk == null)
        {
            pillars[chunkPos.x + chunkPos.z * regionSize].CreateChunk(chunkPos.y);
            chunk = GetChunk(chunkPos);
            print("Created new chunk");
            madeNew = true;
        }

        if (chunk == null)
        {
            return;
        }

        Vector3Int localPos = position - chunkPos * chunkSize;

        //chunk.mesh.states.palette.PrintVoxelIDs();

        bool changed = false;

        chunk.SetVoxel(id, localPos, out changed, true);

        //chunk.mesh.states.palette.PrintVoxelIDs();        

        if (!changed && !madeNew)
        {
            return;
        }

        //Debug.Log("set voxel at " + localPos + " chunk pos " + chunkPos);
        //chunk.UpdateMesh();
        chunksToUpdate.Add(chunkPos);

        VoxelChunk neighbor;

        if (localPos.x == 0)
        {
            neighbor = GetChunk(chunkPos + new Vector3Int(-1, 0, 0));

            if (neighbor != null)
            {
                neighbor.SetVoxel(id, new(chunkSize, localPos.y, localPos.z), out changed, true);

                if (changed)
                {
                    chunksToUpdate.Add(chunkPos + new Vector3Int(-1, 0, 0));
                    //neighbor.UpdateMesh();
                }
            }
        }
        else if (localPos.x == chunkSize - 1)
        {
            neighbor = GetChunk(chunkPos + new Vector3Int(1, 0, 0));

            if (neighbor != null)
            {
                neighbor.SetVoxel(id, new(-1, localPos.y, localPos.z), out changed, true);

                if (changed)
                {
                    chunksToUpdate.Add(chunkPos + new Vector3Int(1, 0, 0));
                    //neighbor.UpdateMesh();
                }
            }
        }

        if (localPos.y == 0)
        {
            neighbor = GetChunk(chunkPos + new Vector3Int(0, -1, 0));

            if (neighbor != null)
            {
                neighbor.SetVoxel(id, new(localPos.x, chunkSize, localPos.z), out changed, true);

                if (changed)
                {
                    chunksToUpdate.Add(chunkPos + new Vector3Int(0, -1, 0));
                    //neighbor.UpdateMesh();
                }
            }
        }
        else if (localPos.y == chunkSize - 1)
        {
            neighbor = GetChunk(chunkPos + new Vector3Int(0, 1, 0));

            if (neighbor != null)
            {
                neighbor.SetVoxel(id, new(localPos.x, -1, localPos.z), out changed, true);

                if (changed)
                {
                    chunksToUpdate.Add(chunkPos + new Vector3Int(0, -1, 0));
                    //neighbor.UpdateMesh();
                }
            }
        }

        if (localPos.z == 0)
        {
            neighbor = GetChunk(chunkPos + new Vector3Int(0, 0, -1));

            if (neighbor != null)
            {
                neighbor.SetVoxel(id, new(localPos.x, localPos.y, chunkSize), out changed, true);

                if (changed)
                {
                    chunksToUpdate.Add(chunkPos + new Vector3Int(0, 0, -1));
                    //neighbor.UpdateMesh();
                }
            }
        }
        else if (localPos.z == chunkSize - 1)
        {
            neighbor = GetChunk(chunkPos + new Vector3Int(0, 0, 1));

            if (neighbor != null)
            {
                neighbor.SetVoxel(id, new(localPos.x, localPos.y, -1), out changed, true);

                if (changed)
                {
                    chunksToUpdate.Add(chunkPos + new Vector3Int(0, 0, 1));
                    //neighbor.UpdateMesh();
                }
            }
        }

    }

    public void SetVoxels(string id, List<Vector3Int> positions)
    {
        HashSet<Vector3Int> toUpdate = new HashSet<Vector3Int>();

        for (int i = 0; i < positions.Count; i++)
        {
            SetVoxel(id, positions[i], ref toUpdate);
        }

        List<Vector3Int> toUpdateList = new List<Vector3Int>(toUpdate);

        for (int i = 0; i < toUpdateList.Count; i++)
        {
            var chunk = GetChunk(toUpdateList[i]);


            if(chunk == null)
            {
                //pillars[toUpdateList[i].x + toUpdateList[i].z * regionSize].CreateChunk(toUpdateList[i].y);
                //chunk = GetChunk(toUpdateList[i]);
                //chunk.UpdateMesh();
            }

            if (chunk != null)
            {
                chunk.UpdateMesh();
            }
        }
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.C) && Time.time > 10)
        {
            //SetVoxel("minecraft:crafting_table", new((int)setTrans.position.x, (int)setTrans.position.y, (int)setTrans.position.z));
            Vector3Int pos = new((int)setTrans.position.x, (int)setTrans.position.y, (int)setTrans.position.z);
            //SetVoxel("base:air", pos);

            List<Vector3Int> positions = new List<Vector3Int>();

            for (int x = 0; x < 3; x++)
            {
                for (int y = 0; y < 3; y++)
                {
                    for (int z = 0; z < 3; z++)
                    {
                        positions.Add(pos + new Vector3Int(x, y, z));
                    }
                }
            }

            SetVoxels("base:air", positions);
        }
    }
}
