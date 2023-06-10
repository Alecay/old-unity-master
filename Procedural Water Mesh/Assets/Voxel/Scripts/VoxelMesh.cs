using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]
public class VoxelMesh : MonoBehaviour
{
    [Header("Debug Info")]
    public int vertexCount = 0;
    public float lastLoadTime = 0;

    [Header("Sizing")]

    [Range(16, 64)]
    public int width = 16;

    /// <summary>
    /// Total number of voxels in the mesh
    /// </summary>
    public int NumberOfVoxels
    {
        get
        {
            return width * width * width;
        }
    }

    /*
    public class IDData
    {
        public int width;
        public readonly int[] ids;

        public Palette palette;

        public IDData(int width, Palette palette)
        {
            this.width = width;
            this.ids = new int[width * width * width];
            this.palette = palette;
        }

        public IDData(int width, Palette palette, int[] ids)
        {
            this.width = width;
            this.ids = ids;
            this.palette = palette;
        }

        public int GetID(int x, int y, int z)
        {
            return ids[GreedyMeshing.XYZToLinearIndex(x, y, z, width)];
        }

        public int GetID(Vector3Int position)
        {
            return GetID(position.x, position.y, position.z);
        }

        public void SetIDs(int[] ids)
        {
            int length = width * width * width;
            for (int index = 0; index < length; index++)
            {
                ids[index] = ids[index];
            }
        }

        /// <summary>
        /// Takes a dictionary with the id int of the voxel and the array of the locations the voxel is in this mesh
        /// </summary>
        /// <param name="voxelLocations"></param>
        public void SetIDs(Dictionary<int, int[]> voxelLocations)
        {
            int length = width * width * width;

            for (int index = 0; index < length; index++)
            {
                ids[index] = 0;
            }

            foreach (var id in voxelLocations.Keys)
            {
                for (int locIndex = 0; locIndex < voxelLocations[id].Length; locIndex++)
                {
                    ids[voxelLocations[id][locIndex]] = id;
                }
            }
        }

        public void BoxFill(int startX, int startY, int startZ, int endX, int endY, int endZ, string idString)
        {
            int id = palette.GetVoxelIndex(idString);

            for (int x = startX; x <= endX; x++)
            {
                for (int y = startY; y <= endY; y++)
                {
                    for (int z = startZ; z <= endZ; z++)
                    {
                        ids[GreedyMeshing.XYZToLinearIndex(x, y, z, width)] = id;
                    }
                }
            }
        }

        public void SetSampleIDs()
        {          
            int currentIDInt;
            int currentIndex;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < width; y++)
                {
                    for (int z = 0; z < width; z++)
                    {
                        currentIndex = GreedyMeshing.XYZToLinearIndex(x, y, z, width);
                        currentIDInt = 0;

                        if (y < 10)
                        {
                            currentIDInt = palette.GetVoxelIndex("minecraft:stone");
                        }
                        else if (y < 12)
                        {
                            currentIDInt = palette.GetVoxelIndex("minecraft:dirt");
                        }
                        else if (y < 13)
                        {
                            currentIDInt = palette.GetVoxelIndex("minecraft:grass");
                        }

                        if(currentIDInt == -1)
                        {
                            currentIDInt = 0;
                        }

                        ids[currentIndex] = currentIDInt;
                    }
                }
            }

            //long Hole
            BoxFill(5, 5, 0, 8, 9, 15, "minecraft:air");

            //Cave
            BoxFill(0, 5, 4, 15, 11, 10, "minecraft:air");

            //Top part
            BoxFill(5, 12, 5, 12, 12, 8, "minecraft:air");

            //Top part
            BoxFill(2, 12, 9, 12, 12, 10, "minecraft:dirt");

            //Top part
            BoxFill(2, 12, 0, 12, 12, 3, "minecraft:purpur");

            //Top part
            BoxFill(5, 15, 5, 10, 15, 10, "minecraft:purpur");
        }

        public void SetRandomIDs()
        {
            float r = 0.7f;

            int currentIDInt;
            int currentIndex;

            float value;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < width; y++)
                {
                    for (int z = 0; z < width; z++)
                    {
                        currentIndex = GreedyMeshing.XYZToLinearIndex(x, y, z, width);                        
                        value = Random.Range(0f, 1f);

                        if(value > r)
                        {
                            currentIDInt = palette.GetVoxelIndex("minecraft:air");
                        }
                        else
                        {
                            currentIDInt = palette.GetVoxelIndex("minecraft:stone");


                            if (y < 3)
                            {
                                currentIDInt = palette.GetVoxelIndex("minecraft:purpur");
                            }
                            else if (y < 10)
                            {
                                currentIDInt = palette.GetVoxelIndex("minecraft:stone");
                            }
                            else if (y < 12)
                            {
                                currentIDInt = palette.GetVoxelIndex("minecraft:dirt");
                            }
                            else if (y < 13)
                            {
                                currentIDInt = palette.GetVoxelIndex("minecraft:grass");
                            }
                            else
                            {
                                currentIDInt = palette.GetVoxelIndex("minecraft:air");
                            }
                        }

                        ids[currentIndex] = currentIDInt;
                    }
                }
            }
        }
    }
    */

    public VoxelStateCollection states = null;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MeshCollider meshCollider;

    //public Palette palette = new Palette();

    //Voxel Data Struct
    /*
        string id; -> the id of this voxel
        enum facing rotation; ->
            0 - Up
            1 - down
            .
            5 - Back
         
     */

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        meshCollider = GetComponent<MeshCollider>();
    }

    public void InitializeStatesArray()
    {
        states = new VoxelStateCollection(width, new Palette());       
    }

    public void UpdateMesh()
    {
        var timer = new System.Diagnostics.Stopwatch();
        timer.Start();

        GreedyMeshing.Slice3D slice3D = 
            new GreedyMeshing.Slice3D(width, states.palette, states.GetLocalIDArray(), states.GetBoundaryLocalIDArray(), states.palette.GetNonSolidIndices());
        
        meshFilter.sharedMesh = slice3D.GetMesh();
        meshCollider.sharedMesh = meshFilter.sharedMesh;

        vertexCount = meshFilter.sharedMesh.vertexCount;
        
        slice3D.SetMaterialProperties(meshRenderer, states.palette);

        //print("(" + name + ") Building mesh all sides took: " + timer.ElapsedMilliseconds + "mms");
        lastLoadTime = timer.ElapsedMilliseconds;
    }

    private void PreformSampleSlice()
    {
        if (name == "Voxel Mesh")
        {
            states.BoxFill(0, 0, 0, 15, 15, 15, "minecraft:acacia_planks");

            states.BoxFill(0, 14, 0, 15, 14, 15, "minecraft:air");

            states.BoxFill(0, 15, 0, 15, 15, 15, "minecraft:animated");
        }
        else
        {
            states.SetRandomIDs();
            states.BoxFill(5, 13, 5, 5, 13, 5, "minecraft:crafting_table");
        }

        var timer = new System.Diagnostics.Stopwatch();
        timer.Start();

        GreedyMeshing.Slice3D slice3D = 
            new GreedyMeshing.Slice3D(width, states.palette, states.GetLocalIDArray(), states.GetBoundaryLocalIDArray(), states.palette.GetNonSolidIndices());

        meshFilter.sharedMesh = slice3D.GetMesh();

        vertexCount = meshFilter.sharedMesh.vertexCount;

        slice3D.SetMaterialProperties(meshRenderer, states.palette);

        //print("(" + name + ") Building mesh all sides took: " + timer.ElapsedMilliseconds + "mms");
        lastLoadTime = timer.ElapsedMilliseconds;
    }

    private void Start()
    {
        TextureDataCollection.UpdateCollectionsDict();
        VoxelDataCollection.UpdateCollectionsDict();

        //InitializeStatesArray();
        
        //PreformSampleSlice();        
    }

    private void OnDisable()
    {

    }

    #region Voxel Data

    public class Palette
    {
        private List<string> textureIDs = new List<string>();
        private Dictionary<string, int> textureIndexLookup = new Dictionary<string, int>();
        private List<string> voxelIDs = new List<string>();
        private List<string> arrayIDs = new List<string>();

        public int TextureIDCount
        {
            get
            {
                return textureIDs.Count;
            }
        }

        public int VoxelIDCount
        {
            get
            {
                return voxelIDs.Count;
            }
        }

        public int ArrayIDCount
        {
            get
            {
                return arrayIDs.Count;
            }
        }

        public bool ContainsTexture(string textureID)
        {
            return textureIDs.Contains(textureID);
        }

        public bool ContainsVoxel(string voxelID)
        {
            return voxelIDs.Contains(voxelID);
        }

        public void Clear()
        {
            textureIDs.Clear();
            textureIndexLookup.Clear();
            voxelIDs.Clear();
            arrayIDs.Clear();
        }

        public void AddVoxel(string voxelID)
        {
            if (!voxelIDs.Contains(voxelID))
            {
                voxelIDs.Add(voxelID);

                var voxelData = VoxelDataCollection.GetVoxelData(voxelID);

                string textureID;

                for (int i = 0; i < 6; i++)
                {
                    textureID = voxelData.GetTextureID(i);

                    if (!textureIDs.Contains(textureID))
                    {
                        textureIndexLookup.Add(textureID, textureIDs.Count);
                        textureIDs.Add(textureID);                        

                        //Skip adding non-soild textures to array list
                        if (voxelData.type == VoxelData.Type.NonSolid)
                        {
                            continue;
                        }

                        var tData = TextureDataCollection.GetTextureData(textureID);

                        for (int j = 0; j < tData.textures.Count; j++)
                        {
                            if (j > 0)
                            {
                                arrayIDs.Add(textureID + "_frame_" + j);
                            }
                            else
                            {
                                arrayIDs.Add(textureID);
                            }
                        }
                    }
                }
            }
        }

        public int GetVoxelIndex(string voxelID)
        {
            for (int i = 0; i < voxelIDs.Count; i++)
            {
                if (voxelIDs[i] == voxelID)
                {
                    return i;
                }
            }

            return -1;
        }

        public string GetVoxelID(int index)
        {
            return voxelIDs[index];
        }

        public int GetTextureIndex(string textureID)
        {
            return textureIndexLookup[textureID];

            //for (int i = 0; i < textureIDs.Count; i++)
            //{
            //    if (textureIDs[i] == textureID)
            //    {
            //        return i;
            //    }
            //}

            //return -1;
        }

        public int GetArrayIndex(string textureID)
        {
            for (int i = 0; i < arrayIDs.Count; i++)
            {
                if (arrayIDs[i] == textureID)
                {
                    return i;
                }
            }

            return -1;
        }

        public string GetTextureID(int index)
        {
            return textureIDs[index];
        }

        public int GetTextureIndex(int voxelIndex, int faceIndex)
        {
            return GetTextureIndex(VoxelDataCollection.GetVoxelData(voxelIDs[voxelIndex]).GetTextureID(faceIndex));
        }

        public int[] GetVoxelTextureIndices()
        {
            int[] indices = new int[voxelIDs.Count * 6];
            VoxelData vData;
            for (int voxelIndex = 0; voxelIndex < voxelIDs.Count; voxelIndex++)
            {
                vData = VoxelDataCollection.GetVoxelData(voxelIDs[voxelIndex]);

                for (int faceIndex = 0; faceIndex < 6; faceIndex++)
                {
                    indices[voxelIndex * 6 + faceIndex] = GetTextureIndex(vData.GetTextureID(faceIndex));
                }
                
            }

            return indices;
        }

        public int[] GetArrayTextureIndices()
        {
            int[] indices = new int[textureIDs.Count];

            for (int i = 0; i < textureIDs.Count; i++)
            {
                indices[i] = GetArrayIndex(textureIDs[i]);
            }            

            return indices;
        }

        /// <summary>
        /// Gets the index of the voxel's face in the texture array
        /// </summary>
        /// <param name="voxelIndex"></param>
        /// <returns></returns>
        public int GetTextureArrayStartIndex(int voxelIndex, int faceIndex)
        {
            VoxelData vData = VoxelDataCollection.GetVoxelData(voxelIDs[voxelIndex]);

            return GetArrayIndex(vData.GetTextureID(faceIndex));

            //int tIndex = 0;

            //for (int i = 0; i < voxelIDs.Count; i++)
            //{
            //    var voxelData = VoxelDataCollection.GetVoxelData(voxelIDs[i]);

            //    if(voxelData.type == VoxelData.Type.NonSolid)
            //    {
            //        continue;
            //    }

            //    var tData = TextureDataCollection.GetTextureData(voxelData.GetTextureID(faceIndex));

            //    if(voxelIndex == i)
            //    {
            //        break;
            //    }

            //    tIndex += tData.textures.Count;
            //}

            //return tIndex;
        }

        public List<int> GetNonSolidIndices()
        {
            List<int> indices = new List<int>();

            VoxelData data;

            for (int i = 0; i < voxelIDs.Count; i++)
            {
                data = VoxelDataCollection.GetVoxelData(voxelIDs[i]);

                if (data.type == VoxelData.Type.NonSolid)
                {
                    indices.Add(i);
                }
            }

            return indices;
        }

        public void PrintInfo()
        {
            Debug.Log("(Palette) (VoxelIDs: " + voxelIDs.Count + ") (TextureIDs: " + textureIDs.Count + ") (ArrayIDs: " + arrayIDs.Count + ")");
        }

        public void PrintVoxelIDs()
        {
            string list = "Voxel IDs: ";

            for (int i = 0; i < voxelIDs.Count; i++)
            {
                list += "(" + i + ") " + voxelIDs[i] + ", ";


            }

            Debug.Log(list);
        }

        public void PrintTextureIDs()
        {
            string list = "Texture IDs: ";

            for (int i = 0; i < textureIDs.Count; i++)
            {
                list += "(" + i + ") " + textureIDs[i] + ", ";


            }

            Debug.Log(list);
        }

        public void PrintArrayIDs()
        {
            string list = "Array IDs: ";

            for (int i = 0; i < arrayIDs.Count; i++)
            {
                list += "(" + i + ") " + arrayIDs[i] + ", ";


            }

            Debug.Log(list);
        }
    }

    public class VoxelStateCollection
    {
        /// <summary>
        /// The Width of each size of the cuboid mesh
        /// </summary>
        public int width;

        public VoxelState[] states;
        public VoxelState[] boundaryStates; //Length = width * width * 6
        //Up xz, down xz, left yz, right yz, forward xy, back xy
        public Palette palette;

        public VoxelStateCollection(int width, Palette palette)
        {
            this.width = width;
            this.states = new VoxelState[width * width * width];
            this.boundaryStates = new VoxelState[width * width * 6];
            this.palette = palette;
        }

        public VoxelStateCollection(int width, Palette palette, VoxelState[] states)
        {
            this.width = width;
            this.states = states;
            this.palette = palette;
        }

        private bool InBounds(int x, int y, int z)
        {
            return x >= 0 && x < width && y >= 0 && y < width && z >= 0 && z < width;
        }

        private bool InBoundaryBounds(int x, int y, int z)
        {
            bool inRange = x >= -1 && x <= width && y >= -1 && y <= width && z >= -1 && z <= width;

            int outerCount = 0;

            if(x == -1 || x == width)
            {
                outerCount++;
            }

            if (y == -1 || y == width)
            {
                outerCount++;
            }

            if (z == -1 || z == width)
            {
                outerCount++;
            }

            return inRange && outerCount == 1;
        }

        private int GetBoundaryIndex(int x, int y, int z)
        {
            //Up
            if(y == width)
            {
                return (width * width) * 0 + x + z * width;
            }
            //Down
            else if(y == -1)
            {
                return (width * width) * 1 + x + z * width;
            }
            //Left
            else if (x == -1)
            {
                return (width * width) * 2 + z + y * width;
            }
            //Right
            else if (x == width)
            {
                return (width * width) * 3 + z + y * width;
            }
            //Forward
            else if (z == width)
            {
                return (width * width) * 4 + x + y * width;
            }
            //Back
            else if (z == -1)
            {
                return (width * width) * 5 + x + y * width;
            }

            return 0;
        }

        public int GetLocalID(int x, int y, int z)
        {
            if (InBounds(x, y, z))
            {
                return states[GreedyMeshing.XYZToLinearIndex(x, y, z, width)].localID;
            }
            else if (InBoundaryBounds(x, y, z))
            {
                return boundaryStates[GetBoundaryIndex(x, y, z)].localID;
            }

            return -1;
        }

        public string GetID(Vector3Int position)
        {
            return GetID(position.x, position.y, position.z);
        }

        public string GetID(int x, int y, int z)
        {
            if (InBounds(x, y, z))
            {
                return states[GreedyMeshing.XYZToLinearIndex(x, y, z, width)].id;
            }
            else if (InBoundaryBounds(x, y, z))
            {
                return boundaryStates[GetBoundaryIndex(x, y, z)].id;
            }

            return "none";
        }

        public int GetLocalID(Vector3Int position)
        {
            return GetLocalID(position.x, position.y, position.z);
        }

        public int[] GetLocalIDArray()
        {
            int[] ids = new int[width * width * width];

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < width; y++)
                {
                    for (int z = 0; z < width; z++)
                    {
                        ids[GreedyMeshing.XYZToLinearIndex(x, y, z, width)] = GetLocalID(x, y, z);
                    }
                }
            }

            return ids;
        }

        public int[] GetBoundaryLocalIDArray()
        {
            int[] ids = new int[width * width * 6];

            for (int x = -1; x <= width; x++)
            {
                for (int y = -1; y <= width; y++)
                {
                    for (int z = -1; z <= width; z++)
                    {
                        if (InBoundaryBounds(x, y, z))
                        {
                            ids[GetBoundaryIndex(x, y, z)] = GetLocalID(x, y, z);
                        }
                        

                        //ids[GreedyMeshing.XYZToLinearIndex(x, y, z, width)] = GetLocalID(x, y, z);
                    }
                }
            }

            return ids;
        }

        public void BoxFill(int startX, int startY, int startZ, int endX, int endY, int endZ, string idString)
        {
            palette.AddVoxel(idString);

            int id = palette.GetVoxelIndex(idString);

            for (int x = startX; x <= endX; x++)
            {
                for (int y = startY; y <= endY; y++)
                {
                    for (int z = startZ; z <= endZ; z++)
                    {
                        states[GreedyMeshing.XYZToLinearIndex(x, y, z, width)] = new VoxelState(idString, id);
                    }
                }
            }
        }

        public void SetVoxel(string idString, Vector3Int position, bool addNew = false)
        {
            if (addNew)
            {
                palette.AddVoxel(idString);
            }

            int id = palette.GetVoxelIndex(idString);

            if(InBounds(position.x, position.y, position.z))
            {
                states[GreedyMeshing.XYZToLinearIndex(position.x, position.y, position.z, width)] = new VoxelState(idString, id);
            }
            else if(InBoundaryBounds(position.x, position.y, position.z))
            {
                boundaryStates[GetBoundaryIndex(position.x, position.y, position.z)] = new VoxelState(idString, id);
            }

        }

        public void SetVoxel(string idString, Vector3Int position, out bool changed, bool addNew = false)
        {
            changed = false;

            if (addNew)
            {
                palette.AddVoxel(idString);
            }

            int id = palette.GetVoxelIndex(idString);

            if (InBounds(position.x, position.y, position.z))
            {
                changed = states[GreedyMeshing.XYZToLinearIndex(position.x, position.y, position.z, width)].id != idString;
                states[GreedyMeshing.XYZToLinearIndex(position.x, position.y, position.z, width)] = new VoxelState(idString, id);
            }
            else if (InBoundaryBounds(position.x, position.y, position.z))
            {
                changed = boundaryStates[GetBoundaryIndex(position.x, position.y, position.z)].id != idString;
                boundaryStates[GetBoundaryIndex(position.x, position.y, position.z)] = new VoxelState(idString, id);
            }

        }

        public void SetVoxels(string idString, List<Vector3Int> positions)
        {
            palette.AddVoxel(idString);

            //int id = palette.GetVoxelIndex(idString);

            for (int i = 0; i < positions.Count; i++)
            {
                SetVoxel(idString, positions[i], false);
                //states[GreedyMeshing.XYZToLinearIndex(positions[i].x, positions[i].y, positions[i].z, width)] = new VoxelState(idString, id);
            }
        }

        public void SetSampleIDs()
        {            
            string currentID;
            int currentIndex;

            palette.AddVoxel("minecraft:air");
            palette.AddVoxel("minecraft:stone");
            palette.AddVoxel("minecraft:dirt");
            palette.AddVoxel("minecraft:grass");
            palette.AddVoxel("minecraft:purpur");

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < width; y++)
                {
                    for (int z = 0; z < width; z++)
                    {
                        currentIndex = GreedyMeshing.XYZToLinearIndex(x, y, z, width);
                        currentID = "minecraft:air";

                        if (y < 10)
                        {
                            currentID = "minecraft:stone";
                        }
                        else if (y < 12)
                        {
                            currentID = "minecraft:dirt";
                        }
                        else if (y < 13)
                        {
                            currentID = "minecraft:grass";
                        }

                        states[currentIndex] = new VoxelState(currentID, palette.GetVoxelIndex(currentID));
                    }
                }
            }

            //long Hole
            BoxFill(5, 5, 0, 8, 9, 15, "minecraft:air");

            //Cave
            BoxFill(0, 5, 4, 15, 11, 10, "minecraft:air");

            //Top part
            BoxFill(5, 12, 5, 12, 12, 8, "minecraft:air");

            //Top part
            BoxFill(2, 12, 9, 12, 12, 10, "minecraft:dirt");

            //Top part
            BoxFill(2, 12, 0, 12, 12, 3, "minecraft:purpur");

            //Top part
            BoxFill(5, 15, 5, 10, 15, 10, "minecraft:purpur");
        }

        public void SetRandomIDs()
        {
            float r = 0.7f;
            float value;

            string currentID;
            int currentIndex;

            palette.AddVoxel("minecraft:air");
            palette.AddVoxel("minecraft:stone");
            palette.AddVoxel("minecraft:dirt");
            palette.AddVoxel("minecraft:grass");

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < width; y++)
                {
                    for (int z = 0; z < width; z++)
                    {
                        currentIndex = GreedyMeshing.XYZToLinearIndex(x, y, z, width);
                        currentID = "minecraft:air";
                        value = Random.Range(0f, 1f);

                        if (value < r)
                        {
                            if (y < 10)
                            {
                                currentID = "minecraft:stone";
                            }
                            else if (y < 12)
                            {
                                currentID = "minecraft:dirt";
                            }
                            else if (y < 13)
                            {
                                currentID = "minecraft:grass";
                            }
                        }

                        

                        states[currentIndex] = new VoxelState(currentID, palette.GetVoxelIndex(currentID));
                    }
                }
            }            
        }
    }

    [System.Serializable]
    /// <summary>
    /// The state of the current voxel
    /// </summary>
    public struct VoxelState
    {
        public string id;
        public int localID; //the numerical id

        public VoxelState(string id, int localID)
        {
            this.id = id;
            this.localID = localID;
        }
    }

    #endregion

    #region Voxel Editing



    #endregion
}
