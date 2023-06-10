using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Rendering;

public class MarchingCubesMeshGenerator 
{
    public int width;
    public int height;

    public int PointsCount
    {
        get
        {
            return (width + 1) * (width + 1) * (height + 1);
        }
    }

    [Range(-1,1)]
    public float isoLevel;
    public bool interpolate = true;
    [Range(0, 8)]
    public int meshSimplificationLevel = 0;

    public NativeArray<float> densityValues;

    private NativeList<TriangleInfo> triangles;

    public NativeArray<Vector3> vertices;
    public NativeArray<Vector3> normals;
    public NativeArray<Color> colors;
    public NativeArray<int> indices;

    public bool creatingMesh = false;

    private struct TriangleInfo
    {
        public Vector3 vertex0;
        public Vector3 vertex1;
        public Vector3 vertex2;

        public Color color;

        private Vector3 normal;

        public Vector3 Normal
        {
            get
            {
                return normal;
            }
        }

        public TriangleInfo(Vector3 vertex0, Vector3 vertex1, Vector3 vertex2, Color color)
        {
            this.vertex0 = vertex0;
            this.vertex1 = vertex1;
            this.vertex2 = vertex2;
            this.color = color;
            this.normal = Vector3.Cross((vertex1 - vertex0), (vertex2 - vertex0)).normalized;
        }
    }

    [BurstCompile]
    struct MarchJob : IJobParallelFor
    {
        public NativeList<TriangleInfo>.ParallelWriter triangles;

        [ReadOnly] [NativeDisableParallelForRestriction] public NativeArray<float> densityValues;

        //public int numPointsPerAxis;
        public int numPointsPerXAxis;
        public int numPointsPerYAxis;
        public float isoLevel;
        public bool interpolate;

        public int increment;

        Vector3 interpolateVerts(Vector4 v1, Vector4 v2)
        {
            Vector3 v3 = v1;
            Vector3 v4 = v2;

            float t = (isoLevel - v1.w) / (v2.w - v1.w);
            return v3 + t * (v4 - v3);
        }

        int3 IDFromIndex(int index)
        {
            return new int3(index % numPointsPerXAxis, index / (numPointsPerXAxis * numPointsPerXAxis), index / numPointsPerXAxis % numPointsPerXAxis);
        }

        int LinearIndex(int x, int y, int z)
        {
            return (y * numPointsPerXAxis * numPointsPerXAxis + z * numPointsPerXAxis + x);
        }

        private float GetDenisty(int x, int y, int z)
        {
            return densityValues[LinearIndex(x, y, z)];
        }

        private Vector4 GetCubeCorner(int3 id, int increment, int index)
        {
            index %= 8;

            int3 offset = CubeOffset(index) * increment;
            int3 pos = id + offset;

            return new Vector4(pos.x, pos.y, pos.z, GetDenisty(pos.x, pos.y, pos.z));

        }

        private int3 CubeOffset(int index)
        {
            index %= 8;

            switch (index)
            {
                default:
                case 0:
                    return new int3(0, 0, 0);
                case 1:
                    return new int3(1, 0, 0);
                case 2:
                    return new int3(1, 0, 1);
                case 3:
                    return new int3(0, 0, 1);
                case 4:
                    return new int3(0, 1, 0);
                case 5:
                    return new int3(1, 1, 0);
                case 6:
                    return new int3(1, 1, 1);
                case 7:
                    return new int3(0, 1, 1);
            }
        }

        public void Execute(int index)
        {
            int3 id = IDFromIndex(index);

            //print("Starting " + id.ToString());

            if (index >= numPointsPerXAxis * numPointsPerXAxis * numPointsPerYAxis)
            {
                return;
            }

            if (index != LinearIndex(id.x, id.y, id.z))
            {
                //print("Skip " + id.ToString());
            }

            // Stop one point before the end because voxel includes neighbouring points
            if (id.x >= numPointsPerXAxis - 1 || id.y >= numPointsPerYAxis - 1 || id.z >= numPointsPerXAxis - 1)
            {
                //print("Skip " + id.ToString());
                return;
            }

            if (increment < 0)
            {
                increment = 1;
            }

            if (id.x % increment > 0 || id.y % increment > 0 || id.z % increment > 0)
            {
                return;
            }

            NativeArray<Vector4> cubeCorners = new NativeArray<Vector4>(8, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            cubeCorners[0] = GetCubeCorner(id, increment, 0);
            cubeCorners[1] = GetCubeCorner(id, increment, 1);
            cubeCorners[2] = GetCubeCorner(id, increment, 2);
            cubeCorners[3] = GetCubeCorner(id, increment, 3);

            cubeCorners[4] = GetCubeCorner(id, increment, 4);
            cubeCorners[5] = GetCubeCorner(id, increment, 5);
            cubeCorners[6] = GetCubeCorner(id, increment, 6);
            cubeCorners[7] = GetCubeCorner(id, increment, 7);

            // Calculate unique index for each cube configuration.
            // There are 256 possible values
            // A value of 0 means cube is entirely inside surface; 255 entirely outside.
            // The value is used to look up the edge table, which indicates which edges of the cube are cut by the isosurface.
            int cubeIndex = 0;
            if (cubeCorners[0].w < isoLevel)
                cubeIndex |= 1;
            if (cubeCorners[1].w < isoLevel)
                cubeIndex |= 2;
            if (cubeCorners[2].w < isoLevel)
                cubeIndex |= 4;
            if (cubeCorners[3].w < isoLevel)
                cubeIndex |= 8;
            if (cubeCorners[4].w < isoLevel)
                cubeIndex |= 16;
            if (cubeCorners[5].w < isoLevel)
                cubeIndex |= 32;
            if (cubeCorners[6].w < isoLevel)
                cubeIndex |= 64;
            if (cubeCorners[7].w < isoLevel)
                cubeIndex |= 128;

            int tCount = 0;

            for (int i = 0; MarchTables.Triangulation[cubeIndex][i] != -1; i += 3)
            {
                tCount++;
            }

            // Create triangles for current cube configuration

            int a0, a1, a2, b0, b1, b2;

            Vector3 v0, v1, v2;
            Color color;

            for (int i = 0; MarchTables.Triangulation[cubeIndex][i] != -1; i += 3)
            {
                // Get indices of corner points A and B for each of the three edges
                // of the cube that need to be joined to form the triangle.
                a0 = MarchTables.CornerIndexAFromEdge[MarchTables.Triangulation[cubeIndex][i]];
                b0 = MarchTables.CornerIndexBFromEdge[MarchTables.Triangulation[cubeIndex][i]];

                a1 = MarchTables.CornerIndexAFromEdge[MarchTables.Triangulation[cubeIndex][i + 1]];
                b1 = MarchTables.CornerIndexBFromEdge[MarchTables.Triangulation[cubeIndex][i + 1]];

                a2 = MarchTables.CornerIndexAFromEdge[MarchTables.Triangulation[cubeIndex][i + 2]];
                b2 = MarchTables.CornerIndexBFromEdge[MarchTables.Triangulation[cubeIndex][i + 2]];

                //DrawTriangle dTri;

                if (interpolate)
                {
                    v0 = interpolateVerts(cubeCorners[a0], cubeCorners[b0]);
                    v1 = interpolateVerts(cubeCorners[a1], cubeCorners[b1]);
                    v2 = interpolateVerts(cubeCorners[a2], cubeCorners[b2]);
                }
                else
                {
                    v0 = cubeCorners[a0];
                    v1 = cubeCorners[a1];
                    v2 = cubeCorners[a2];
                }

                //triColors[i / 3] = new Color(1, 1, 1, 1);
                //color = new Color((Mathf.Sin(id.x) + 1) / 2, (Mathf.Sin(id.y) + 1) / 2, (Mathf.Sin(id.z) + 1) / 2, 1);
                color = Color.white;

                triangles.AddNoResize(new TriangleInfo(v0, v1, v2, color));
            }            

            cubeCorners.Dispose();
        }
    }

    [BurstCompile]
    struct MarchAndMeshJob : IJobParallelFor
    {       
        public NativeList<Vector3>.ParallelWriter vertices;
        public NativeList<Vector3>.ParallelWriter normals;
        public NativeList<Color>.ParallelWriter colors;
        public NativeList<int>.ParallelWriter indices;

        [ReadOnly] [NativeDisableParallelForRestriction] 
        public NativeArray<float> densityValues;

        //public int numPointsPerAxis;
        public int numPointsPerXAxis;
        public int numPointsPerYAxis;
        public float isoLevel;
        public bool interpolate;

        public int increment;

        Vector3 interpolateVerts(Vector4 v1, Vector4 v2)
        {
            Vector3 v3 = v1;
            Vector3 v4 = v2;

            float t = (isoLevel - v1.w) / (v2.w - v1.w);
            return v3 + t * (v4 - v3);
        }

        int3 IDFromIndex(int index)
        {
            return new int3(index % numPointsPerXAxis, index / (numPointsPerXAxis * numPointsPerXAxis), index / numPointsPerXAxis % numPointsPerXAxis);
        }

        int LinearIndex(int x, int y, int z)
        {
            return (y * numPointsPerXAxis * numPointsPerXAxis + z * numPointsPerXAxis + x);
        }

        private float GetDenisty(int x, int y, int z)
        {
            return densityValues[LinearIndex(x, y, z)];
        }

        private Vector4 GetCubeCorner(int3 id, int increment, int index)
        {
            index %= 8;

            int3 offset = CubeOffset(index) * increment;
            int3 pos = id + offset;

            return new Vector4(pos.x, pos.y, pos.z, GetDenisty(pos.x, pos.y, pos.z));

        }

        private int3 CubeOffset(int index)
        {
            index %= 8;

            switch (index)
            {
                default:
                case 0:
                    return new int3(0, 0, 0);
                case 1:
                    return new int3(1, 0, 0);
                case 2:
                    return new int3(1, 0, 1);
                case 3:
                    return new int3(0, 0, 1);
                case 4:
                    return new int3(0, 1, 0);
                case 5:
                    return new int3(1, 1, 0);
                case 6:
                    return new int3(1, 1, 1);
                case 7:
                    return new int3(0, 1, 1);
            }
        }

        public void Execute(int index)
        {
            int3 id = IDFromIndex(index);

            //print("Starting " + id.ToString());

            if (index >= numPointsPerXAxis * numPointsPerXAxis * numPointsPerYAxis)
            {
                return;
            }

            if (index != LinearIndex(id.x, id.y, id.z))
            {
                //print("Skip " + id.ToString());
            }

            // Stop one point before the end because voxel includes neighbouring points
            if (id.x >= numPointsPerXAxis - 1 || id.y >= numPointsPerYAxis - 1 || id.z >= numPointsPerXAxis - 1)
            {
                //print("Skip " + id.ToString());
                return;
            }

            if (increment < 0)
            {
                increment = 1;
            }

            if (id.x % increment > 0 || id.y % increment > 0 || id.z % increment > 0)
            {
                return;
            }

            NativeArray<Vector4> cubeCorners = new NativeArray<Vector4>(8, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            cubeCorners[0] = GetCubeCorner(id, increment, 0);
            cubeCorners[1] = GetCubeCorner(id, increment, 1);
            cubeCorners[2] = GetCubeCorner(id, increment, 2);
            cubeCorners[3] = GetCubeCorner(id, increment, 3);

            cubeCorners[4] = GetCubeCorner(id, increment, 4);
            cubeCorners[5] = GetCubeCorner(id, increment, 5);
            cubeCorners[6] = GetCubeCorner(id, increment, 6);
            cubeCorners[7] = GetCubeCorner(id, increment, 7);

            // Calculate unique index for each cube configuration.
            // There are 256 possible values
            // A value of 0 means cube is entirely inside surface; 255 entirely outside.
            // The value is used to look up the edge table, which indicates which edges of the cube are cut by the isosurface.
            int cubeIndex = 0;
            if (cubeCorners[0].w < isoLevel)
                cubeIndex |= 1;
            if (cubeCorners[1].w < isoLevel)
                cubeIndex |= 2;
            if (cubeCorners[2].w < isoLevel)
                cubeIndex |= 4;
            if (cubeCorners[3].w < isoLevel)
                cubeIndex |= 8;
            if (cubeCorners[4].w < isoLevel)
                cubeIndex |= 16;
            if (cubeCorners[5].w < isoLevel)
                cubeIndex |= 32;
            if (cubeCorners[6].w < isoLevel)
                cubeIndex |= 64;
            if (cubeCorners[7].w < isoLevel)
                cubeIndex |= 128;

            int tCount = 0;

            for (int i = 0; MarchTables.Triangulation[cubeIndex][i] != -1; i += 3)
            {
                tCount++;
            }

            // Create triangles for current cube configuration

            int a0, a1, a2, b0, b1, b2;

            Vector3 v0, v1, v2;
            Color color;

            for (int i = 0; MarchTables.Triangulation[cubeIndex][i] != -1; i += 3)
            {
                // Get indices of corner points A and B for each of the three edges
                // of the cube that need to be joined to form the triangle.
                a0 = MarchTables.CornerIndexAFromEdge[MarchTables.Triangulation[cubeIndex][i]];
                b0 = MarchTables.CornerIndexBFromEdge[MarchTables.Triangulation[cubeIndex][i]];

                a1 = MarchTables.CornerIndexAFromEdge[MarchTables.Triangulation[cubeIndex][i + 1]];
                b1 = MarchTables.CornerIndexBFromEdge[MarchTables.Triangulation[cubeIndex][i + 1]];

                a2 = MarchTables.CornerIndexAFromEdge[MarchTables.Triangulation[cubeIndex][i + 2]];
                b2 = MarchTables.CornerIndexBFromEdge[MarchTables.Triangulation[cubeIndex][i + 2]];

                //DrawTriangle dTri;

                if (interpolate)
                {
                    v0 = interpolateVerts(cubeCorners[a0], cubeCorners[b0]);
                    v1 = interpolateVerts(cubeCorners[a1], cubeCorners[b1]);
                    v2 = interpolateVerts(cubeCorners[a2], cubeCorners[b2]);
                }
                else
                {
                    v0 = cubeCorners[a0];
                    v1 = cubeCorners[a1];
                    v2 = cubeCorners[a2];
                }

                //triColors[i / 3] = new Color(1, 1, 1, 1);
                color = new Color((Mathf.Sin(id.x) + 1) / 2, (Mathf.Sin(id.y) + 1) / 2, (Mathf.Sin(id.z) + 1) / 2, 1);

                var tInfo = new TriangleInfo(v0, v1, v2, color);
                //triangles.AddNoResize(new TriangleInfo(v0, v1, v2, color));

                {
                    int startIndex = 0;

                    vertices.AddNoResize(v0);
                    vertices.AddNoResize(v1);
                    vertices.AddNoResize(v2);

                    normals.AddNoResize(tInfo.Normal);
                    normals.AddNoResize(tInfo.Normal);
                    normals.AddNoResize(tInfo.Normal);

                    colors.AddNoResize(color);
                    colors.AddNoResize(color);
                    colors.AddNoResize(color);

                    indices.AddNoResize(startIndex);
                    indices.AddNoResize(startIndex + 1);
                    indices.AddNoResize(startIndex + 2);
                }

            }

            cubeCorners.Dispose();
        }
    }

    [BurstCompile]
    struct MeshGenJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction] [ReadOnly]
        public NativeList<TriangleInfo> triangles;

        public NativeArray<Vector3> vertices;
        public NativeArray<Vector3> normals;
        public NativeArray<Color> colors;
        public NativeArray<int> indices;

        public void Execute(int index)
        {
            int triIndex = index / 3;
            int localIndex = index % 3;

            vertices[index] = localIndex == 0 ? triangles[triIndex].vertex0 : localIndex == 1 ? triangles[triIndex].vertex1 : triangles[triIndex].vertex2;
            normals[index] = triangles[triIndex].Normal;
            colors[index] = triangles[triIndex].color;
            indices[index] = index;
        }
    }

    int GetFactor(int num, int n)
    {
        int testNum = 1;
        int currentFactorIndex = 0;
        int lastFactor = 1;

        if (n <= 0 || num <= 1)
        {
            return 1;
        }

        //increase testNum while it's less than or equal to num
        while (testNum < num)
        {
            //If testNum is a factor of num
            if (num % testNum == 0)
            {
                //Save this testNum in lastFactor
                lastFactor = testNum;

                //If the currentIndex == the desired index N then return testNum
                if (currentFactorIndex == n)
                {
                    return testNum;
                }

                //Every found factor increment the index
                currentFactorIndex++;
            }

            testNum++;
        }

        return lastFactor;
    }

    public Mesh GetMesh()
    {
        int increment = 1;// GetFactor(NumPointsPerAxis - 1, meshSimplificationLevel);
        if (increment < 1)
        {
            increment = 1;
        }

        int numPoints = PointsCount;

        Dispose();

        triangles = new NativeList<TriangleInfo>(numPoints * 6, Allocator.Persistent);

        var marchJob = new MarchJob
        {
            triangles = triangles.AsParallelWriter(),
            densityValues = densityValues,
            numPointsPerXAxis = width + 1,
            numPointsPerYAxis = height + 1,
            isoLevel = isoLevel,
            interpolate = interpolate,
            increment = increment,
        };

        marchJob.Schedule(numPoints, width + 1).Complete();        

        int numVerts = triangles.Length * 3;

        vertices = new NativeArray<Vector3>(numVerts, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        normals = new NativeArray<Vector3>(numVerts, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        colors = new NativeArray<Color>(numVerts, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        indices = new NativeArray<int>(numVerts, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

        var meshJob = new MeshGenJob
        {
            triangles = triangles,
            vertices = vertices,
            normals = normals,
            colors = colors,
            indices = indices
        };

        meshJob.Schedule(numVerts, 16).Complete();

        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetColors(colors);
        mesh.SetIndices(indices, MeshTopology.Triangles, 0);
        mesh.SetSubMesh(0, new UnityEngine.Rendering.SubMeshDescriptor(0, numVerts));

        Debug.Log("Triangles created: " + triangles.Length);
        Debug.Log("Vertices in mesh: " + triangles.Length * 3);


        return mesh;

            
    }

    public IEnumerator CreateMesh(MeshFilter filter)
    {
        creatingMesh = true;
        int increment = 1;// GetFactor(NumPointsPerAxis - 1, meshSimplificationLevel);
        if (increment < 1)
        {
            increment = 1;
        }

        int numPoints = PointsCount;

        if (!triangles.IsCreated)
        {
            triangles = new NativeList<TriangleInfo>(numPoints * 6, Allocator.Persistent);
        }
        else
        {
            triangles.Clear();
        }        

        float marchStartTime = Time.realtimeSinceStartup;
        float meshStartTime = Time.realtimeSinceStartup;
        float maxTime = 3 / 1000f;

        var marchJob = new MarchJob
        {
            triangles = triangles.AsParallelWriter(),
            densityValues = densityValues,
            numPointsPerXAxis = width + 1,
            numPointsPerYAxis = height + 1,
            isoLevel = isoLevel,
            interpolate = interpolate,
            increment = increment,
        };

        var marchHandle = marchJob.Schedule(numPoints, width + 1);

        while (Time.realtimeSinceStartup < marchStartTime + maxTime)
        {
            yield return null;
        }

        marchHandle.Complete();

        int numVerts = triangles.Length * 3;

        ReSizeMeshArrays(numVerts);

        var meshJob = new MeshGenJob
        {
            triangles = triangles,
            vertices = vertices,
            normals = normals,
            colors = colors,
            indices = indices
        };

        var meshHandle = meshJob.Schedule(numVerts, numVerts / 9 + (numVerts / 9 > 0 ? 1 : 0));

        meshStartTime = Time.realtimeSinceStartup;

        while (Time.realtimeSinceStartup < meshStartTime + maxTime)
        {
            yield return null;
        }

        meshHandle.Complete();

        if(triangles.Length == 0)
        {
            creatingMesh = false;
            filter.sharedMesh = null;
            yield break;
        }

        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetColors(colors);
        mesh.SetIndices(indices, MeshTopology.Triangles, 0);
        mesh.SetSubMesh(0, new UnityEngine.Rendering.SubMeshDescriptor(0, numVerts));        

        Debug.Log("Triangles created: " + triangles.Length);
        Debug.Log("Vertices in mesh: " + triangles.Length * 3);

        filter.sharedMesh = mesh;        

        Debug.Log("Created mesh in " + ((Time.realtimeSinceStartup - meshStartTime) * 1000f) + " mms");

        creatingMesh = false;

        yield return null;
    }

    public IEnumerator UpdateMeshAndAssign(Mesh mesh, MeshFilter filter)
    {
        creatingMesh = true;
        int increment = GetFactor(width, meshSimplificationLevel);
        if (increment < 1)
        {
            increment = 1;
        }

        int numPoints = PointsCount;

        if (!triangles.IsCreated)
        {
            triangles = new NativeList<TriangleInfo>(numPoints * 6, Allocator.Persistent);
        }
        else
        {
            triangles.Clear();
        }

        float marchStartTime = Time.realtimeSinceStartup;
        float meshStartTime = Time.realtimeSinceStartup;
        float maxTime = 3 / 1000f;

        var marchJob = new MarchJob
        {
            triangles = triangles.AsParallelWriter(),
            densityValues = densityValues,
            numPointsPerXAxis = width + 1,
            numPointsPerYAxis = height + 1,
            isoLevel = isoLevel,
            interpolate = interpolate,
            increment = increment,
        };

        var marchHandle = marchJob.Schedule(numPoints, width + 1);

        while (Time.realtimeSinceStartup < marchStartTime + maxTime)
        {
            yield return null;
        }

        marchHandle.Complete();

        int numVerts = triangles.Length * 3;

        ReSizeMeshArrays(numVerts);

        var meshJob = new MeshGenJob
        {
            triangles = triangles,
            vertices = vertices,
            normals = normals,
            colors = colors,
            indices = indices
        };

        var meshHandle = meshJob.Schedule(numVerts, numVerts / 9 + (numVerts / 9 > 0 ? 1 : 0));

        meshStartTime = Time.realtimeSinceStartup;

        while (Time.realtimeSinceStartup < meshStartTime + maxTime)
        {
            yield return null;
        }

        meshHandle.Complete();

        if (triangles.Length == 0)
        {
            creatingMesh = false;
            filter.sharedMesh = null;
            yield break;
        }

        if(mesh == null)
        {
            mesh = new Mesh();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.MarkDynamic();
        }
        else
        {
            mesh.Clear();
        }        

        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetColors(colors);
        mesh.SetIndices(indices, MeshTopology.Triangles, 0);
        mesh.SetSubMesh(0, new UnityEngine.Rendering.SubMeshDescriptor(0, numVerts));

        //Debug.Log("Triangles created: " + triangles.Length);
        //Debug.Log("Vertices in mesh: " + triangles.Length * 3);

        mesh.name = "Voxel Mesh " + triangles.Length + " (" + (triangles.Length * 3) + ")";

        filter.sharedMesh = mesh;

        Debug.Log("Created mesh in " + ((Time.realtimeSinceStartup - meshStartTime) * 1000f) + " mms");

        creatingMesh = false;

        yield return null;
    }

    public IEnumerator UpdateMeshAndAssign(Mesh mesh, MeshCollider collider)
    {
        creatingMesh = true;
        int increment = GetFactor(width, meshSimplificationLevel);
        if (increment < 1)
        {
            increment = 1;
        }

        int numPoints = PointsCount;

        if (!triangles.IsCreated)
        {
            triangles = new NativeList<TriangleInfo>(numPoints * 6, Allocator.Persistent);
        }
        else
        {
            triangles.Clear();
        }

        float marchStartTime = Time.realtimeSinceStartup;
        float meshStartTime = Time.realtimeSinceStartup;
        float maxTime = 3 / 1000f;

        var marchJob = new MarchJob
        {
            triangles = triangles.AsParallelWriter(),
            densityValues = densityValues,
            numPointsPerXAxis = width + 1,
            numPointsPerYAxis = height + 1,
            isoLevel = isoLevel,
            interpolate = interpolate,
            increment = increment,
        };

        var marchHandle = marchJob.Schedule(numPoints, width + 1);

        while (Time.realtimeSinceStartup < marchStartTime + maxTime)
        {
            yield return null;
        }

        marchHandle.Complete();

        int numVerts = triangles.Length * 3;

        ReSizeMeshArrays(numVerts);

        var meshJob = new MeshGenJob
        {
            triangles = triangles,
            vertices = vertices,
            normals = normals,
            colors = colors,
            indices = indices
        };

        var meshHandle = meshJob.Schedule(numVerts, numVerts / 9 + (numVerts / 9 > 0 ? 1 : 0));

        meshStartTime = Time.realtimeSinceStartup;

        while (Time.realtimeSinceStartup < meshStartTime + maxTime)
        {
            yield return null;
        }

        meshHandle.Complete();

        if (triangles.Length == 0)
        {
            creatingMesh = false;
            collider.sharedMesh = null;
            yield break;
        }

        if (mesh == null)
        {
            mesh = new Mesh();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.MarkDynamic();
        }
        else
        {
            mesh.Clear();
        }

        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetColors(colors);
        mesh.SetIndices(indices, MeshTopology.Triangles, 0);
        mesh.SetSubMesh(0, new UnityEngine.Rendering.SubMeshDescriptor(0, numVerts));

        //Debug.Log("Triangles created: " + triangles.Length);
        //Debug.Log("Vertices in mesh: " + triangles.Length * 3);

        mesh.name = "Voxel Mesh " + triangles.Length + " (" + (triangles.Length * 3) + ")";

        collider.sharedMesh = mesh;

        Debug.Log("Created mesh in " + ((Time.realtimeSinceStartup - meshStartTime) * 1000f) + " mms");

        creatingMesh = false;

        yield return null;
    }

    public IEnumerator CreateMeshALL(MeshFilter filter)
    {
        creatingMesh = true;
        int increment = 1;// GetFactor(NumPointsPerAxis - 1, meshSimplificationLevel);
        if (increment < 1)
        {
            increment = 1;
        }

        int numPoints = PointsCount;

        if (!triangles.IsCreated)
        {
            triangles = new NativeList<TriangleInfo>(numPoints * 6, Allocator.Persistent);
        }
        else
        {
            triangles.Clear();
        }

        if (!densityValues.IsCreated)
        {
            creatingMesh = false;            
            yield break;
        }

        float marchStartTime = Time.realtimeSinceStartup;
        float meshStartTime = Time.realtimeSinceStartup;
        float maxTime = 3 / 1000f;

        NativeList<Vector3> verts = new NativeList<Vector3>(numPoints * 6, Allocator.TempJob);
        NativeList<Vector3> normals = new NativeList<Vector3>(numPoints * 6, Allocator.TempJob);
        NativeList<Color> colors = new NativeList<Color>(numPoints * 6, Allocator.TempJob);
        NativeList<int> indices = new NativeList<int>(numPoints * 6, Allocator.TempJob);

        var marchJob = new MarchAndMeshJob
        {
            vertices = verts.AsParallelWriter(),
            normals = normals.AsParallelWriter(),
            colors = colors.AsParallelWriter(),
            indices = indices.AsParallelWriter(),

            densityValues = densityValues,
            numPointsPerXAxis = width + 1,
            numPointsPerYAxis = height + 1,
            isoLevel = isoLevel,
            interpolate = interpolate,
            increment = increment,
        };

        marchJob.Schedule(numPoints, width + 1).Complete();


        if (verts.Length == 0)
        {
            creatingMesh = false;
            filter.sharedMesh = null;
            yield break;
        }

        int[] ind = new int[verts.Length];

        for (int i = 0; i < ind.Length; i++)
        {
            ind[i] = i;
        }

        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        mesh.SetVertices((NativeArray<Vector3>)verts);
        mesh.SetNormals((NativeArray<Vector3>)normals);
        mesh.SetColors((NativeArray<Color>)colors);
        mesh.SetIndices(ind, MeshTopology.Triangles, 0);
        mesh.SetSubMesh(0, new UnityEngine.Rendering.SubMeshDescriptor(0, verts.Length));

        Debug.Log("Triangles created: " + verts.Length / 3);
        Debug.Log("Vertices in mesh: " + verts.Length);

        verts.Dispose();
        normals.Dispose();
        colors.Dispose();
        indices.Dispose();

        filter.sharedMesh = mesh;

        Debug.Log("Created mesh in " + ((Time.realtimeSinceStartup - meshStartTime) * 1000f) + " mms");

        creatingMesh = false;

        yield return null;
    }

    public void Dispose()
    {
        if (triangles.IsCreated)
            triangles.Dispose();

        if (vertices.IsCreated)
            vertices.Dispose();

        if (normals.IsCreated)
            normals.Dispose();

        if (colors.IsCreated)
            colors.Dispose();

        if (indices.IsCreated)
            indices.Dispose();
    }

    private void ReSizeMeshArrays(int length)
    {

        if (!vertices.IsCreated || vertices.Length < length)
        {
            if(vertices.IsCreated)
                vertices.Dispose();

            vertices = new NativeArray<Vector3>(length, Allocator.Persistent, NativeArrayOptions.ClearMemory);

            if (normals.IsCreated)
                normals.Dispose();

            normals = new NativeArray<Vector3>(length, Allocator.Persistent, NativeArrayOptions.ClearMemory);

            if (colors.IsCreated)
                colors.Dispose();

            colors = new NativeArray<Color>(length, Allocator.Persistent, NativeArrayOptions.ClearMemory);

            if (indices.IsCreated)
                indices.Dispose();

            indices = new NativeArray<int>(length, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        }

        //if (!normals.IsCreated || normals.Length < length)
        //{
        //    if (normals.IsCreated)
        //        normals.Dispose();

        //    normals = new NativeArray<Vector3>(length, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        //}

        //if (!colors.IsCreated || colors.Length < length)
        //{
        //    if (colors.IsCreated)
        //        colors.Dispose();

        //    colors = new NativeArray<Color>(length, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        //}

        //if (!indices.IsCreated || indices.Length < length)
        //{
        //    if (indices.IsCreated)
        //        indices.Dispose();

        //    indices = new NativeArray<int>(length, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        //}
    }

    public MarchingCubesMeshGenerator(int width, int height, float isoLevel, bool interpolate)
    {
        this.width = width;
        this.height = height;
        this.isoLevel = isoLevel;
        this.interpolate = interpolate;
    }
}
