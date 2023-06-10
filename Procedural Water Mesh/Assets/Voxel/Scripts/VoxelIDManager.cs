using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class VoxelIDManager
{
    /*
    [System.Serializable]
    /// <summary>
    /// The data of a voxel
    /// </summary>    
    public class VoxelData
    {
        /// <summary>
        /// ID of this voxel, used to lookup the voxel's data
        /// </summary>
        public string id = "voxel";

        [HideInInspector]
        public int index = -1;

        /// <summary>
        /// The name of the textures to use for each corresponding face of the voxel
        /// </summary>
        private string[] textureIDs = new string[] { "up", "down", "left", "right", "forward", "back" };

        /// <summary>
        /// The type of voxel solid or non-solid
        /// </summary>
        public enum Type
        {
            //Solid voxels are added to the mesh using greedy meshing algorithim
            Solid,
            //Non-Solid voxels are skipped when creating mesh using greedy meshing algorithim
            NonSolid
        }

        /// <summary>
        /// The type of voxel solid or non-solid
        /// </summary>
        public Type type = Type.Solid;     
        
        /// <summary>
        /// Get the textureID of the selected face
        /// </summary>
        /// <param name="faceIndex">int ranging from 0-5 -> Up, Down, Left, Right, Forward, Back</param>
        /// <returns>String of the textureID of the selected face</returns>
        public string GetTextureID(int faceIndex)
        {
            return textureIDs[faceIndex % 6];
        }

        public void SetTextureIDs(string[] textureIDs)
        {
            this.textureIDs = textureIDs;
        }

        public void CopyData(VoxelData data)
        {
            type = data.type;
            textureIDs = data.textureIDs;
        }

        public override string ToString()
        {
            return "(VoxelData) (ID: " + id + ") (TextureIDs: "
                + textureIDs[0] + ", " + textureIDs[1] + ", " + textureIDs[2]
                + ", " + textureIDs[3] + ", " + textureIDs[4] + ", " + textureIDs[5] + ")";
        }

        public VoxelData(string id, Type type, string[] textureIDs)
        {
            this.id = id;
            this.type = type;
            this.textureIDs = textureIDs;
        }

        public VoxelData(string id, Type type, string textureID)
        {
            this.id = id;
            this.type = type;
            this.textureIDs = new string[] { textureID, textureID, textureID, textureID, textureID, textureID };
        }

        public VoxelData()
        {

        }
    }

    [System.Serializable]
    public class TextureData
    {
        public string id = "texture";

        public int index = -1;

        public Texture2D texture = null;

        public void CopyData(TextureData data)
        {
            texture = data.texture;
        }

        public override string ToString()
        {
            return "(TextureData) (ID: " + id + ") (Texture Name: " + texture?.name + ")";
        }

        public TextureData()
        {
        }

        public TextureData(string id, Texture2D texture)
        {
            this.id = id;
            this.texture = texture;
        }
    }


    private static Dictionary<string, TextureData> textureDataIDLookup = new Dictionary<string, TextureData>();
    private static Dictionary<int, TextureData> textureDataIndexLookup = new Dictionary<int, TextureData>();

    private static Dictionary<string, VoxelData> voxelDataIDLookup = new Dictionary<string, VoxelData>();
    private static Dictionary<int, VoxelData> voxelDataIndexLookup = new Dictionary<int, VoxelData>();

    private static List<int> allNonSolidVoxelIndices = new List<int>();

    public static void AddNewTextureData(TextureData data)
    {
        if (textureDataIDLookup.ContainsKey(data.id))
        {
            return;
        }

        data.index = textureDataIDLookup.Count;

        textureDataIndexLookup.Add(textureDataIDLookup.Count, data);
        textureDataIDLookup.Add(data.id, data);
    }

    public static void AddNewVoxelData(VoxelData data)
    {
        if (voxelDataIDLookup.ContainsKey(data.id))
        {
            return;
        }

        data.index = voxelDataIDLookup.Count;

        voxelDataIndexLookup.Add(voxelDataIDLookup.Count, data);
        voxelDataIDLookup.Add(data.id, data);

        if(data.type == VoxelData.Type.NonSolid)
        {
            allNonSolidVoxelIndices.Add(data.index);
        }
    }

    public static VoxelData GetVoxelData(int index)
    {
        if(index >= voxelDataIDLookup.Count)
        {
            Debug.LogError("Error can't find voxelData with index " + index);
            return null;
        }

        return voxelDataIndexLookup[index];
    }

    public static VoxelData GetVoxelData(string id)
    {
        return voxelDataIDLookup[id];
    }

    public static int GetTextureIndex(int voxelIndex, int faceIndex)
    {
        var vData = GetVoxelData(voxelIndex);
        var tData = GetTextureData(vData.GetTextureID(faceIndex));

        return tData.index;
    }

    public static int GetTextureIndex(string voxelID, int faceIndex)
    {
        var vData = GetVoxelData(voxelID);
        var tData = GetTextureData(vData.GetTextureID(faceIndex));

        return tData.index;
    }

    public static TextureData GetTextureData(int index)
    {
        if (index >= textureDataIndexLookup.Count)
        {
            Debug.LogError("Error can't find textureData with index " + index);
            return null;
        }

        return textureDataIndexLookup[index];
    }

    public static TextureData GetTextureData(string id)
    {
        return textureDataIDLookup[id];
    }

    public static void LogData()
    {
        Debug.Log("====================================================================================");

        foreach (var index in textureDataIndexLookup.Keys)
        {
            Debug.Log("(Texture Index: " + index + ") " + textureDataIndexLookup[index].ToString());
        }

        Debug.Log("====================================================================================");

        foreach (var index in voxelDataIndexLookup.Keys)
        {
            Debug.Log("(Voxel Index: " + index + ") " + voxelDataIndexLookup[index].ToString());
        }
    }

    private static int numberOfIDs = 0;

    private static Dictionary<int, string> voxelIDs = new Dictionary<int, string>();
    private static Dictionary<string, int> indexLookup = new Dictionary<string, int>();

    //public static bool AddNewID(string id)
    //{
    //    int hash = id.GetHashCode();        

    //    if (!voxelIDs.ContainsKey(hash))
    //    {
    //        voxelIDs.Add(hash, id);
    //        return true;
    //    }
    //    else
    //    {
    //        Debug.LogWarning("Failed to add ID to manager, dictionary already contains an ID with the hash (" + hash + ") and ID (" + voxelIDs[hash] + ")");            
    //    }

    //    return false;
    //}

    public static void Initialize()
    {
        numberOfIDs = 0;

        AddNewTextureData(new("none", null));
        AddNewVoxelData(new("air", VoxelData.Type.NonSolid, "none"));        
    }

    public static void PrintIDs()
    {
        foreach (var hash in voxelIDs.Keys)
        {
            Debug.Log(voxelIDs[hash] + " (" + hash + ")");
        }
    }*/
}
