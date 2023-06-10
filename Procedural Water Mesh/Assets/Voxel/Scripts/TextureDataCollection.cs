using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[System.Serializable]
public class TextureData
{
    public string id = "texture";

    public Texture2D Texture
    {
        get
        {
            return textures?.Count > 0 ? textures[0] : null;
        }
    }

    public List<Texture2D> textures = new List<Texture2D>();

    public float animationSpeed = 0;

    public enum Type
    {
        Single,
        Animation
    }

    public Type type;

    public int GetAnimationDataInt()
    {
        int frames = textures == null ? 0 : textures.Count;
        int speed = (int)(animationSpeed * 10f);

        int info = speed * 1000 + frames;

        return info;
    }

    public Vector2 GetAnimationData()
    {
        //Returns the frames as x and the speed as y
        return new Vector2(textures == null ? 1 : textures.Count, animationSpeed);
    }

    public override string ToString()
    {
        return "(TextureData) (ID: " + id + ") (Texture Name: " + Texture?.name + ")";
    }

    public TextureData Copy()
    {
        return new(id, type, animationSpeed, textures);
    }

    public TextureData()
    {
    }

    public TextureData(string id, Type type, float animationSpeed, List<Texture2D> textures)
    {
        this.id = id;
        this.type = type;
        this.animationSpeed = animationSpeed;
        this.textures = new List<Texture2D>(textures);
    }
}

[System.Serializable]
[CreateAssetMenu(fileName = "Texture Collection", menuName = "Texture Collection")]
public class TextureDataCollection : ScriptableObject
{
    [SerializeField]
    public string collectionID;

    [SerializeField]
    public Texture2D icon;

    [SerializeField]
    public List<TextureData> textures = new List<TextureData>();

    public List<string> textureIDs = new List<string>();

    private static Dictionary<string, TextureData> allTextures = new Dictionary<string, TextureData>();    

    public static Dictionary<string, TextureDataCollection> collections = new Dictionary<string, TextureDataCollection>();

    public static bool collectionDictsInitialized = false;

    public static int NumberOfTextures
    {
        get
        {
            return allTextures.Count;
        }
    }

    public static TextureDataCollection GetCollection(string collectionID)
    {
        return collections[collectionID];
    }

    public static List<string> GetAllTextureIDsInCollection(string collectionID)
    {
        return collections[collectionID].textureIDs;
    }

    public static List<string> GetCollectionIDs()
    {
        return new List<string>(collections.Keys);
    }

    public static List<string> GetTextureIDs()
    {
        return new List<string>(allTextures.Keys);
    }

    public static TextureData GetTextureData(string fullID)
    {
        if(fullID == "base:none" || fullID == "none")
        {
            return new TextureData("base:none", TextureData.Type.Single, 0, new List<Texture2D>());
        }

        return allTextures[fullID];
    }

    public static TextureData GetTextureData(string collectionID, string id)
    {
        return allTextures[collectionID + ":" + id];
    }

    public static void UpdateCollectionsDict()
    {
        if (collectionDictsInitialized)
        {
            return;
        }

        collectionDictsInitialized = true;

        var allCollections = GetAllInstances<TextureDataCollection>();

        collections.Clear();
        allTextures.Clear();

        string textureName;

        for (int collectionIndex = 0; collectionIndex < allCollections.Length; collectionIndex++)
        {
            if(allCollections[collectionIndex] == null || allCollections[collectionIndex].collectionID == null)
            {
                continue;
            }

            collections.Add(allCollections[collectionIndex].collectionID, allCollections[collectionIndex]);

            for (int textureIndex = 0; textureIndex < allCollections[collectionIndex].textures.Count; textureIndex++)
            {
                textureName = allCollections[collectionIndex].collectionID + ":" + allCollections[collectionIndex].textures[textureIndex].id;
                if (allTextures.ContainsKey(textureName))
                {
                    Debug.LogError("Error skipped adding " + textureName + " textureData");
                    continue;
                }                

                allTextures.Add(textureName, allCollections[collectionIndex].textures[textureIndex]);

                //Debug.Log("Loaded " + textureName);
            }
        }

        //Debug.Log("Loaded " + collections.Count + " texture collections with a total of " + allTextures.Count + " textures");        
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
[CustomEditor(typeof(TextureDataCollection))]
public class TDCEditor : Editor
{
    TextureDataCollection collection;

    private void OnEnable()
    {
        collection = target as TextureDataCollection;

        TextureDataCollection.UpdateCollectionsDict();

        collection.textureIDs.Clear();
        for (int i = 0; i < collection.textures.Count; i++)
        {
            collection.textureIDs.Add(collection.textures[i].id);
        }
    }

    private void OnDisable()
    {
        TextureDataCollection.collectionDictsInitialized = false;
    }

    public override void OnInspectorGUI()
    {
        GUILayout.BeginHorizontal();

        collection.collectionID = EditorGUILayout.TextField("Collection ID", collection.collectionID);

        if (GUILayout.Button("Sort", GUILayout.MaxWidth(50)))
        {
            collection.textures.Sort((t1, t2) => t1.id.CompareTo(t2.id));
        }

        GUILayout.EndHorizontal();

        collection.icon = (Texture2D)EditorGUILayout.ObjectField("Icon", collection.icon, typeof(Texture2D), false, GUILayout.Height(16));

        GUILayout.Space(10);

        EditorGUILayout.LabelField("Textures");

        EditorGUI.indentLevel++;

        GUILayout.Space(5);

        bool remove = false;
        for (int i = 0; i < collection.textures.Count; i++)
        {
            DrawTextureDataField(collection.textures[i], out remove);

            if (remove)
            {
                collection.textures.RemoveAt(i);
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
            collection.textures.Add(collection.textures.Count > 0 ? collection.textures[^1].Copy() : new("", TextureData.Type.Single, 0, new List<Texture2D> { null }));
        }

        GUILayout.EndHorizontal();

        EditorGUI.indentLevel--;

        if (GUI.changed)
        {
            EditorUtility.SetDirty(collection);
        }
    }

    private void DrawTextureDataField(TextureData data, out bool remove)
    {

        EditorGUILayout.LabelField(data.id);

        EditorGUI.indentLevel++;

        GUILayout.BeginHorizontal();

        data.id = EditorGUILayout.TextField("ID", data.id);
        GUILayout.Space(10);
        remove = GUILayout.Button("Remove", GUILayout.MaxWidth(70));

        GUILayout.EndHorizontal();

        data.type = (TextureData.Type)EditorGUILayout.EnumPopup("Type", data.type);

        if(data.type == TextureData.Type.Single)
        {
            if (data.textures.Count > 1)
            {
                data.textures = new List<Texture2D> { data.textures[0] };
            }
            else if (data.textures.Count == 0)
            {
                data.textures.Add(null);
            }

            data.animationSpeed = 0;
            data.textures[0] = (Texture2D)EditorGUILayout.ObjectField("Texture", data.textures[0], typeof(Texture2D), false, GUILayout.Height(16));
        }
        else
        {
            data.animationSpeed = EditorGUILayout.FloatField("Animation Speed", data.animationSpeed);

            GUILayout.Space(5);
            GUILayout.BeginHorizontal();

            EditorGUILayout.LabelField("Frames");
            EditorGUI.indentLevel++;

            bool addFrame = GUILayout.Button("Add", GUILayout.MaxWidth(70));

            GUILayout.EndHorizontal();

            if (addFrame)
            {
                data.textures.Add(data.textures[^1]);
            }

            GUI.enabled = true;
            for (int i = 0; i < data.textures.Count; i++)
            {
                GUILayout.BeginHorizontal();

                GUI.enabled = true;
                data.textures[i] = (Texture2D)EditorGUILayout.ObjectField(i.ToString(), data.textures[i], typeof(Texture2D), false, GUILayout.Height(16));

                GUI.enabled = i > 0;
                bool removeFrame = GUILayout.Button("Remove", GUILayout.MaxWidth(70));

                if (removeFrame)
                {
                    data.textures.RemoveAt(i);
                    break;
                }

                GUILayout.EndHorizontal();
            }
            GUI.enabled = true;

            EditorGUI.indentLevel--;
        }

        

        EditorGUI.indentLevel--;
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
