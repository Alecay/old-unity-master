using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

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

    /// <summary>
    /// The name of the textures to use for each corresponding face of the voxel
    /// </summary>
    public string[] textureIDs = new string[] { "up", "down", "left", "right", "forward", "back" };

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
    /// Helps know how many textures are used in the block
    /// </summary>
    public enum TextureMode
    {
        Single,//All sides share the same texture
        Multiple,//All Sides have different textures
        TopDiffers,//Top side is different from all other sides
        Pillar//Top and bottom have different textures
    }

    public TextureMode textureMode;

    /// <summary>
    /// Get the textureID of the selected face
    /// </summary>
    /// <param name="faceIndex">int ranging from 0-5 -> Up, Down, Left, Right, Forward, Back</param>
    /// <returns>String of the textureID of the selected face</returns>
    public string GetTextureID(int faceIndex)
    {
        return textureIDs[faceIndex % 6];
    }

    public string GetTextureID(int faceIndex, int rotationIndex)
    {
        int[] downIndices = new int[]  { 1, 0, 2, 3, 4, 5 };
        int[] leftIndices = new int[]  { 3, 2, 0, 1, 4, 5 };
        int[] rightIndices = new int[] { 2, 3, 1, 0, 4, 5 };


        switch (rotationIndex)
        {
            default:
            case 0:
                return textureIDs[faceIndex % 6];
                break;
        }


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

    public VoxelData Copy()
    {
        string[] textureIDs = new string[6];
        this.textureIDs.CopyTo(textureIDs, 0);
        return new VoxelData(id, type, textureIDs);
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

[CreateAssetMenu(fileName = "Voxel Collection", menuName = "Voxel Collection")]

public class VoxelDataCollection : ScriptableObject
{
    [SerializeField]
    public string collectionID;

    [SerializeField]
    public Texture2D icon;

    [SerializeField]
    public List<VoxelData> voxels = new List<VoxelData>();

    public List<string> voxelIDs = new List<string>();

    private static Dictionary<string, VoxelData> allVoxels = new Dictionary<string, VoxelData>();

    public static Dictionary<string, VoxelDataCollection> collections = new Dictionary<string, VoxelDataCollection>();

    public static bool collectionDictsInitialized = false;

    public static VoxelDataCollection GetCollection(string collectionID)
    {
        return collections[collectionID];
    }

    public static List<string> GetAllVoxelIDsInCollection(string collectionID)
    {
        return collections[collectionID].voxelIDs;
    }

    public static List<string> GetCollectionIDs()
    {
        return new List<string>(collections.Keys);
    }

    public static List<string> GetTextureIDs()
    {
        return new List<string>(allVoxels.Keys);
    }

    public static bool ValidVoxelID(string fullID)
    {
        return allVoxels.ContainsKey(fullID);
    }

    public static VoxelData GetVoxelData(string fullID)
    {
        return allVoxels[fullID];
    }

    public static VoxelData GetVoxelData(string collectionID, string id)
    {
        return allVoxels[collectionID + ":" + id];
    }

    public static void UpdateCollectionsDict()
    {
        if (collectionDictsInitialized)
        {
            return;
        }

        collectionDictsInitialized = true;

        var allCollections = GetAllInstances<VoxelDataCollection>();

        collections.Clear();
        allVoxels.Clear();

        string textureName;

        for (int collectionIndex = 0; collectionIndex < allCollections.Length; collectionIndex++)
        {
            if (allCollections[collectionIndex] == null || allCollections[collectionIndex].collectionID == null)
            {
                continue;
            }

            collections.Add(allCollections[collectionIndex].collectionID, allCollections[collectionIndex]);

            for (int voxelIndex = 0; voxelIndex < allCollections[collectionIndex].voxels.Count; voxelIndex++)
            {
                textureName = allCollections[collectionIndex].collectionID + ":" + allCollections[collectionIndex].voxels[voxelIndex].id;
                if (allVoxels.ContainsKey(textureName))
                {
                    Debug.LogError("Error skipped adding " + textureName + " voxelData");
                    continue;
                }

                allVoxels.Add(textureName, allCollections[collectionIndex].voxels[voxelIndex]);

                //Debug.Log("Loaded " + allCollections[collectionIndex].voxels[voxelIndex].ToString());
            }
        }

        //Debug.Log("Loaded " + collections.Count + " voxel collections with a total of " + allVoxels.Count + " voxels");
    }

    public static T[] GetAllInstances<T>() where T : ScriptableObject
    {
        string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);  //FindAssets uses tags check documentation for more info
        T[] a = new T[guids.Length];
        for (int i = 0; i < guids.Length; i++)         //probably could get optimized 
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            a[i] = AssetDatabase.LoadAssetAtPath<T>(path);
        }

        return a;

    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(VoxelDataCollection))]
public class VDCEditor : Editor
{
    VoxelDataCollection collection;

    static float lastTime;

    private void OnEnable()
    {
        collection = target as VoxelDataCollection;

        TextureDataCollection.UpdateCollectionsDict();
        VoxelDataCollection.UpdateCollectionsDict();

        collection.voxelIDs.Clear();
        for (int i = 0; i < collection.voxels.Count; i++)
        {
            collection.voxelIDs.Add(collection.voxels[i].id);
        }

        lastTime = (float)EditorApplication.timeSinceStartup;
    }

    private void OnDisable()
    {
        VoxelDataCollection.collectionDictsInitialized = false;
    }

    public override void OnInspectorGUI()
    {
        GUILayout.BeginHorizontal();

        collection.collectionID = EditorGUILayout.TextField("Collection ID", collection.collectionID);

        if (GUILayout.Button("Sort", GUILayout.MaxWidth(50)))
        {
            collection.voxels.Sort((t1, t2) => t1.id.CompareTo(t2.id));
        }

        GUILayout.EndHorizontal();

        collection.icon = (Texture2D)EditorGUILayout.ObjectField("Icon", collection.icon, typeof(Texture2D), false, GUILayout.Height(16));

        GUILayout.Space(10);

        EditorGUILayout.LabelField("Voxels");

        EditorGUI.indentLevel++;

        GUILayout.Space(5);

        bool remove = false;
        for (int i = 0; i < collection.voxels.Count; i++)
        {
            DrawVoxelDataField(collection.voxels[i], out remove);

            if (remove)
            {
                collection.voxels.RemoveAt(i);
                break;
            }
        }

        GUILayout.Space(10);

        GUILayout.BeginHorizontal();

        GUILayout.Space(EditorGUIUtility.currentViewWidth - 45);

        bool addNew;

        addNew = GUILayout.Button("+", GUILayout.MaxWidth(20));

        if (addNew)
        {
            collection.voxels.Add(collection.voxels.Count > 0 ? collection.voxels[^1].Copy() : new("", VoxelData.Type.NonSolid, ""));            
        }

        GUILayout.EndHorizontal();

        EditorGUI.indentLevel--;

        if (GUI.changed)
        {
            EditorUtility.SetDirty(collection);
        }
    }

    private void DrawVoxelDataField(VoxelData data, out bool remove)
    {

        EditorGUILayout.LabelField(data.id);

        EditorGUI.indentLevel++;

        GUILayout.BeginHorizontal();

        data.id = EditorGUILayout.TextField("ID", data.id);
        GUILayout.Space(10);
        remove = GUILayout.Button("Remove", GUILayout.MaxWidth(70));

        GUILayout.EndHorizontal();

        data.type = (VoxelData.Type)EditorGUILayout.EnumPopup("Type", data.type);

        //data.texture = (Texture2D)EditorGUILayout.ObjectField("Texture", data.texture, typeof(Texture2D), false, GUILayout.Height(16));

        if(data.type == VoxelData.Type.Solid)
        {
            DrawVoxelDataPopup(data);
        }
        else
        {
            data.SetTextureIDs(new string[] { "base:none", "base:none", "base:none", "base:none", "base:none", "base:none" });
        }

        EditorGUI.indentLevel--;
    }

    private void DrawVoxelDataPopup(VoxelData data)
    {
        var collectionIDs = TextureDataCollection.GetCollectionIDs().ToArray();
        string[] textureIDs;
        int collectionIndex = 0;
        int textureIndex = 0;

        string[] sideNames = new string[] { "Up", "Down", "Left", "Right", "Forward", "Back" };

        data.textureMode = (VoxelData.TextureMode)EditorGUILayout.EnumPopup("Texture Mode", data.textureMode);


        if (data.textureMode == VoxelData.TextureMode.Single)
        {
            for (int i = 0; i < collectionIDs.Length; i++)
            {
                if (collectionIDs[i] == data.textureIDs[0].Split(':', 2)[0])
                {
                    collectionIndex = i;
                    break;
                }
            }

            textureIDs = TextureDataCollection.GetAllTextureIDsInCollection(collectionIDs[collectionIndex]).ToArray();

            for (int i = 0; i < textureIDs.Length; i++)
            {
                if (!data.textureIDs[0].Contains(":"))
                {
                    continue;
                }

                if (textureIDs[i] == data.textureIDs[0].Split(':', 2)[1])
                {
                    textureIndex = i;
                    break;
                }
            }

            //EditorGUILayout.LabelField(sideNames[0]);

            EditorGUI.indentLevel++;

            GUILayout.BeginHorizontal();

            GUILayout.Space(30);

            collectionIndex = EditorGUILayout.Popup(collectionIndex, collectionIDs);
            textureIndex = EditorGUILayout.Popup(textureIndex, textureIDs);

            GUILayout.EndHorizontal();

            for (int sideIndex = 0; sideIndex < 6; sideIndex++)
            {
                data.textureIDs[sideIndex] = collectionIDs[collectionIndex] + ":" + textureIDs[textureIndex];
            }

            var r = GUILayoutUtility.GetLastRect();
            Rect drawRect = new Rect(r.x + r.height * 1.75f, r.y, r.height, r.height);
            float width = EditorGUIUtility.currentViewWidth;

            var tData = TextureDataCollection.GetTextureData(data.textureIDs[0]);

            EditorGUI.DrawPreviewTexture(drawRect, tData.Texture);

            EditorGUI.indentLevel--;
        }
        else if (data.textureMode == VoxelData.TextureMode.Multiple)
        {
            for (int sideIndex = 0; sideIndex < 6; sideIndex++)
            {
                for (int i = 0; i < collectionIDs.Length; i++)
                {
                    if (collectionIDs[i] == data.textureIDs[sideIndex].Split(':', 2)[0])
                    {
                        collectionIndex = i;
                        break;
                    }
                }

                textureIDs = TextureDataCollection.GetAllTextureIDsInCollection(collectionIDs[collectionIndex]).ToArray();

                for (int i = 0; i < textureIDs.Length; i++)
                {
                    if (textureIDs[i] == data.textureIDs[sideIndex].Split(':', 2)[1])
                    {
                        textureIndex = i;
                        break;
                    }
                }

                EditorGUILayout.LabelField(sideNames[sideIndex]);

                EditorGUI.indentLevel++;

                GUILayout.BeginHorizontal();

                GUILayout.Space(30);

                collectionIndex = EditorGUILayout.Popup(collectionIndex, collectionIDs);
                textureIndex = EditorGUILayout.Popup(textureIndex, textureIDs);

                GUILayout.EndHorizontal();

                data.textureIDs[sideIndex] = collectionIDs[collectionIndex] + ":" + textureIDs[textureIndex];
                //EditorGUILayout.LabelField(data.textureIDs[sideIndex]);

                var r = GUILayoutUtility.GetLastRect();
                Rect drawRect = new Rect(r.x + r.height * 1.75f, r.y, r.height, r.height);
                float width = EditorGUIUtility.currentViewWidth;

                EditorGUI.DrawPreviewTexture(drawRect, TextureDataCollection.GetTextureData(data.textureIDs[sideIndex]).Texture);

                EditorGUI.indentLevel--;
            }

        }
        else if (data.textureMode == VoxelData.TextureMode.TopDiffers)
        {
            for (int sideIndex = 0; sideIndex < 2; sideIndex++)
            {
                for (int i = 0; i < collectionIDs.Length; i++)
                {
                    if (collectionIDs[i] == data.textureIDs[sideIndex].Split(':', 2)[0])
                    {
                        collectionIndex = i;
                        break;
                    }
                }

                textureIDs = TextureDataCollection.GetAllTextureIDsInCollection(collectionIDs[collectionIndex]).ToArray();

                for (int i = 0; i < textureIDs.Length; i++)
                {
                    if (textureIDs[i] == data.textureIDs[sideIndex].Split(':', 2)[1])
                    {
                        textureIndex = i;
                        break;
                    }
                }

                EditorGUILayout.LabelField(sideIndex == 0 ? "Up" : "Other");

                EditorGUI.indentLevel++;

                GUILayout.BeginHorizontal();

                GUILayout.Space(30);

                collectionIndex = EditorGUILayout.Popup(collectionIndex, collectionIDs);
                textureIndex = EditorGUILayout.Popup(textureIndex, textureIDs);

                GUILayout.EndHorizontal();

                data.textureIDs[sideIndex] = collectionIDs[collectionIndex] + ":" + textureIDs[textureIndex];
                //EditorGUILayout.LabelField(data.textureIDs[sideIndex]);

                var r = GUILayoutUtility.GetLastRect();
                Rect drawRect = new Rect(r.x + r.height * 1.75f, r.y, r.height, r.height);
                float width = EditorGUIUtility.currentViewWidth;

                EditorGUI.DrawPreviewTexture(drawRect, TextureDataCollection.GetTextureData(data.textureIDs[sideIndex]).Texture);

                EditorGUI.indentLevel--;
            }

            for (int sideIndex = 2; sideIndex < 6; sideIndex++)
            {
                data.textureIDs[sideIndex] = data.textureIDs[1];
            }
        }
        else if (data.textureMode == VoxelData.TextureMode.Pillar)
        {
            for (int sideIndex = 0; sideIndex < 3; sideIndex++)
            {
                for (int i = 0; i < collectionIDs.Length; i++)
                {
                    if (collectionIDs[i] == data.textureIDs[sideIndex].Split(':', 2)[0])
                    {
                        collectionIndex = i;
                        break;
                    }
                }

                textureIDs = TextureDataCollection.GetAllTextureIDsInCollection(collectionIDs[collectionIndex]).ToArray();

                for (int i = 0; i < textureIDs.Length; i++)
                {
                    if (textureIDs[i] == data.textureIDs[sideIndex].Split(':', 2)[1])
                    {
                        textureIndex = i;
                        break;
                    }
                }

                EditorGUILayout.LabelField(sideIndex == 0 ? "Up" : (sideIndex == 1 ? "Down" : "Other"));

                EditorGUI.indentLevel++;

                GUILayout.BeginHorizontal();

                GUILayout.Space(30);

                collectionIndex = EditorGUILayout.Popup(collectionIndex, collectionIDs);
                textureIndex = EditorGUILayout.Popup(textureIndex, textureIDs);

                GUILayout.EndHorizontal();

                data.textureIDs[sideIndex] = collectionIDs[collectionIndex] + ":" + textureIDs[textureIndex];
                //EditorGUILayout.LabelField(data.textureIDs[sideIndex]);

                var r = GUILayoutUtility.GetLastRect();
                Rect drawRect = new Rect(r.x + r.height * 1.75f, r.y, r.height, r.height);
                float width = EditorGUIUtility.currentViewWidth;

                EditorGUI.DrawPreviewTexture(drawRect, TextureDataCollection.GetTextureData(data.textureIDs[sideIndex]).Texture);

                EditorGUI.indentLevel--;
            }

            for (int sideIndex = 3; sideIndex < 6; sideIndex++)
            {
                data.textureIDs[sideIndex] = data.textureIDs[2];
            }
        }

        if (GUI.changed)
        {
            EditorUtility.SetDirty(collection);
        }

        GUILayout.Space(10);
    }

    public override Texture2D RenderStaticPreview(string assetPath, Object[] subAssets, int width, int height)
    {
        if (collection?.icon == null)
        {
            return null;
        }

        Texture2D texture = collection.icon;

        // example.PreviewIcon must be a supported format: ARGB32, RGBA32, RGB24,
        // Alpha8 or one of float formats
        Texture2D icon = new Texture2D(width, height);
        EditorUtility.CopySerialized(texture, icon);

        return icon;
    }
}

#endif
