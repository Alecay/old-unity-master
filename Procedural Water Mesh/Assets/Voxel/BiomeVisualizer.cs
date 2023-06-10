using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class BiomeVisualizer : MonoBehaviour
{

    [System.Serializable]
    public class NoiseData
    {
        public int seed = 42;

        [Range(1, 8)]
        public int octaves = 5;

        public float scale = 100.0f;

        public float persistance = 0.5f;
        public float lacunarity = 2.0f;

        public Vector2 offset = new Vector2(0, 0);

        public float step = 0.1f;
    }

    [System.Serializable]
    public struct BiomeRequirements
    {
        public bool enabled;
        public string name;
        public Color color;

        [Range(-1, 1)]
        public float lowTempature;
        [Range(-1, 1)]
        public float highTempature;

        [Space(10)]
        [Range(-1, 1)]
        public float lowHumidity;
        [Range(-1, 1)]
        public float highHumidity;

        [Space(10)]
        [Range(-1, 1)]
        public float lowElevation;
        [Range(-1, 1)]
        public float highElevation;

        public float TempatureDistance(float tempature)
        {
            return Mathf.Min(Mathf.Abs(tempature - lowTempature), Mathf.Abs(tempature - highTempature));
        }

        public float HumidityDistance(float humidity)
        {
            return Mathf.Min(Mathf.Abs(humidity - lowHumidity), Mathf.Abs(humidity - highHumidity));
        }

        public float ElevationDistance(float elevation)
        {
            return Mathf.Min(Mathf.Abs(elevation - lowElevation), Mathf.Abs(elevation - highElevation));
        }

        public bool InTempRange(float tempature)
        {
            return lowTempature <= tempature && tempature <= highTempature;
        }

        public bool InHumidityRange(float humidity)
        {
            return lowHumidity <= humidity && humidity <= highHumidity;
        }

        public bool InElevationRange(float elevation)
        {
            return lowElevation <= elevation && elevation <= highElevation;
        }
    }

    [Header("Sizing")]
    [Range(50, 500)]
    public int width = 100;
    public int seed;
    [Range(3, 10)]
    public int steps = 5;
    public Vector2 offset;

    [Header("Tempature Variables")]    
    [SerializeField]
    public NoiseData tempNoiseData;
    public Color lowTempatureColor;
    public Color highTempatureColor;

    [Space(10)]
    [Header("Humidity Variables")]
    [SerializeField]
    public NoiseData humidityNoiseData;
    public Color lowHumidityColor;
    public Color highHumidityColor;

    [Space(10)]
    [Header("Elevation Variables")]
    [SerializeField]
    public NoiseData elevationNoiseData;
    public Color lowElevationColor;
    public Color highElevationColor;

    [Space(10)]
    [Header("Biome requirements")]
    public List<BiomeRequirements> biomeRequirements = new List<BiomeRequirements>();

    [HideInInspector]
    public Texture2D temperatureNoiseTexture;
    [HideInInspector]
    public Texture2D humiditiyNoiseTexture;
    [HideInInspector]
    public Texture2D elevationNoiseTexture;

    private float[] temperatureNoiseValues;
    private float[] humiditiyNoiseValues;
    private float[] elevationNoiseValues;

    public Texture2D biomeTexture;
    public BiomeNoiseGen noiseGen;

    private Texture2D CreateSampleBiomeTexture()
    {
        Texture2D texture = new Texture2D(width, width);
        texture.filterMode = FilterMode.Point;

        Color[] pixels = new Color[width * width];

        for (int y = 0; y < width; y++)
        {
            for (int x = 0; x < width; x++)
            {
                BiomeRequirements biome = GetSampleBiome(x, y);
                pixels[x + y * width] = biome.color;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        return texture;
    }

    private Texture2D CreateBiomeTexture()
    {
        Texture2D texture = new Texture2D(width, width, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;

        Color[] pixels = new Color[width * width];

        int index;

        for (int y = 0; y < width; y++)
        {
            for (int x = 0; x < width; x++)
            {
                index = x + y * width;
                BiomeRequirements biome = GetBiome(temperatureNoiseValues[index], humiditiyNoiseValues[index], elevationNoiseValues[index]);
                pixels[x + y * width] = biome.color;

                if(temperatureNoiseValues[index] <= 0f)
                {
                    //pixels[x + y * width] = Color.blue;
                }
                else
                {
                    //pixels[x + y * width] = Color.green;
                }
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        return texture;
    }

    private BiomeRequirements GetSampleBiome(int x, int y)
    {
        float xPercent = x / (float)width;
        float yPercent = y / (float)width;
        float humidity = Mathf.Lerp(-1, 1, xPercent);
        float temperature = Mathf.Lerp(-1, 1, yPercent);

        BiomeRequirements req;
        float dist;
        float minDist = 100;
        int index = -1;
        for (int i = 0; i < biomeRequirements.Count; i++)
        {
            req = biomeRequirements[i];

            if (!req.enabled)
            {
                continue;
            }

            dist = new Vector2(req.TempatureDistance(temperature), req.HumidityDistance(humidity)).magnitude;

            if(dist < minDist)
            {
                index = i;
                minDist = dist;
            }
        }

        return biomeRequirements[index];
    }

    private BiomeRequirements GetBiome(float temperature, float humidity, float elevation)
    {
        BiomeRequirements req;
        int index = -1;

        float minDist = 10000;
        float dist = 0;

        for (int i = 0; i < biomeRequirements.Count; i++)
        {
            req = biomeRequirements[i];

            if (!req.enabled)
            {
                continue;
            }

            //if (req.InElevationRange(elevation) && req.InTempRange(temperature) && req.InHumidityRange(humidity))
            //{
            //    index = i;
            //}

            if (req.InElevationRange(elevation))
            {
                dist = new Vector2(req.TempatureDistance(temperature), req.HumidityDistance(humidity)).magnitude;

                if (dist < minDist)
                {
                    index = i;
                    minDist = dist;
                }
            }
        }

        index = Mathf.Clamp(index, 0, biomeRequirements.Count - 1);

        return biomeRequirements[index];
    }

    public void UpdateTexture()
    {
        tempNoiseData.offset = offset;
        humidityNoiseData.offset = offset;
        elevationNoiseData.offset = offset;

        tempNoiseData.seed = seed;
        humidityNoiseData.seed = seed + 42;
        elevationNoiseData.seed = seed - 42;

        tempNoiseData.step = steps;
        humidityNoiseData.step = steps;
        elevationNoiseData.step = steps;

        temperatureNoiseTexture = noiseGen.GetTexture(width, tempNoiseData, lowTempatureColor, highTempatureColor);
        humiditiyNoiseTexture = noiseGen.GetTexture(width, humidityNoiseData, lowHumidityColor, highHumidityColor);
        elevationNoiseTexture = noiseGen.GetTexture(width, elevationNoiseData, lowElevationColor, highElevationColor);

        Color[] tempColors = temperatureNoiseTexture.GetPixels();
        Color[] humiditiyColors = humiditiyNoiseTexture.GetPixels();
        Color[] elevationColors = elevationNoiseTexture.GetPixels();

        temperatureNoiseValues = new float[tempColors.Length];
        humiditiyNoiseValues = new float[tempColors.Length];
        elevationNoiseValues = new float[tempColors.Length];

        for (int i = 0; i < tempColors.Length; i++)
        {
            temperatureNoiseValues[i] = (tempColors[i].a - 0.5f) * 2f;
            humiditiyNoiseValues[i] = (humiditiyColors[i].a - 0.5f) * 2f;
            elevationNoiseValues[i] = (elevationColors[i].a - 0.5f) * 2f;
        }

        biomeTexture = CreateBiomeTexture();
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(BiomeVisualizer))]
public class BVEditor : Editor
{
    BiomeVisualizer visualizer;

    private void OnEnable()
    {
        visualizer = target as BiomeVisualizer;
        visualizer.UpdateTexture();
    }

    public override void OnInspectorGUI()
    {

        float width = EditorGUIUtility.currentViewWidth * 0.8f;
        Rect drawRect1 = new Rect(width * 0.1f, width * 0.1f, width / 2f, width / 2f);
        Rect drawRect2 = new Rect(width * 0.65f, width * 0.1f, width / 2f, width / 2f);
        Rect drawRect3 = new Rect(width * 0.1f, width * 0.65f, width / 2f, width / 2f);
        Rect drawRect4 = new Rect(width * 0.65f, width * 0.65f, width / 2f, width / 2f);

        Rect drawRect5 = new Rect(width * 0.601f, width * 0.1f, width / 2f, width / 2f);
        Rect drawRect6 = new Rect(width * 0.1f, width * 0.601f, width / 2f, width / 2f);
        Rect drawRect7 = new Rect(width * 0.601f, width * 0.601f, width / 2f, width / 2f);

        GUILayout.Space(width * 1.2f);

        if(visualizer.temperatureNoiseTexture != null)
            EditorGUI.DrawPreviewTexture(drawRect1, visualizer.temperatureNoiseTexture);
        if (visualizer.humiditiyNoiseTexture != null)
            EditorGUI.DrawPreviewTexture(drawRect2, visualizer.humiditiyNoiseTexture);
        if (visualizer.elevationNoiseTexture != null)
            EditorGUI.DrawPreviewTexture(drawRect3, visualizer.elevationNoiseTexture);
        if (visualizer.biomeTexture != null)
            EditorGUI.DrawPreviewTexture(drawRect4, visualizer.biomeTexture);

        //if (visualizer.temperatureNoiseTexture != null)
        //{
        //    EditorGUI.DrawPreviewTexture(drawRect5, visualizer.temperatureNoiseTexture);
        //    EditorGUI.DrawPreviewTexture(drawRect6, visualizer.temperatureNoiseTexture);
        //    EditorGUI.DrawPreviewTexture(drawRect7, visualizer.temperatureNoiseTexture);
        //}
            

        base.OnInspectorGUI();

        if (GUI.changed)
            visualizer.UpdateTexture();
    }
}

#endif
