using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;


#if UNITY_EDITOR
using UnityEditor;
#endif

public class BiomeNoiseGen : ComputeLoader
{
    [Space(10)]
    [Header("Noise Gen")]
    public RenderTexture noiseTexture;

    public int width = 100;

    public BiomeVisualizer.NoiseData noiseData;

    public Color minColor;
    public Color maxColor;

    protected override void CreateBuffers()
    {
        base.CreateBuffers();

        noiseTexture = new RenderTexture(width, width, 24);
        noiseTexture.enableRandomWrite = true;
        noiseTexture.filterMode = FilterMode.Point;
        noiseTexture.Create();
    }

    protected override void SetComputeVariables()
    {
        base.SetComputeVariables();

        if(noiseTexture == null)
        {
            Deinitialize();
            Initialize();
        }

        computeShader.SetTexture(idKernel, "Result", noiseTexture);
        computeShader.SetFloat("ColorStep", noiseData.step);

        computeShader.SetInt("Seed", noiseData.seed);
        computeShader.SetInt("Octaves", noiseData.octaves);

        computeShader.SetFloat("Scale", noiseData.scale);
        computeShader.SetFloat("Persistance", noiseData.persistance);
        computeShader.SetFloat("Lacunarity", noiseData.lacunarity);

        computeShader.SetVector("Offset", noiseData.offset);

        computeShader.SetInt("TextureSize", width);

        computeShader.SetVector("MinColor", minColor);
        computeShader.SetVector("MaxColor", maxColor);        
    }

    protected override void DisposeBuffers()
    {
        base.DisposeBuffers();      
    }

    protected override void UpdateDispatchTimes()
    {
        dispatchTimes = new Vector3Int(width, width, 1);
    }

    public Texture2D GetTexture(int width, BiomeVisualizer.NoiseData noiseData, Color minColor, Color maxColor)
    {
        this.width = width;
        this.noiseData = noiseData;
        this.minColor = minColor;
        this.maxColor = maxColor;

        Deinitialize();
        UpdateData();

        Texture2D tex = new Texture2D(width, width, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        RenderTexture.active = noiseTexture;
        tex.ReadPixels(new Rect(0, 0, noiseTexture.width, noiseTexture.height), 0, 0);
        tex.Apply();
        return tex;
    }

}

#if UNITY_EDITOR
[CustomEditor(typeof(BiomeNoiseGen))]
public class BNGEditor : Editor
{
    BiomeNoiseGen noiseGen;

    private void OnEnable()
    {
        noiseGen = target as BiomeNoiseGen;
        noiseGen.UpdateData();
    }

    public override void OnInspectorGUI()
    {

        float width = EditorGUIUtility.currentViewWidth * 0.8f;
        Rect drawRect1 = new Rect(width * 0.4f, width * 0.1f, width / 2f, width / 2f);
        Rect drawRect2 = new Rect(width * 0.65f, width * 0.1f, width / 2f, width / 2f);

        GUILayout.Space(width * 1.4f / 2f);

        if(noiseGen.noiseTexture != null)
            EditorGUI.DrawPreviewTexture(drawRect1, noiseGen.noiseTexture);
        //EditorGUI.DrawPreviewTexture(drawRect2, noiseGen.texture);

        base.OnInspectorGUI();

        if (GUI.changed)
            noiseGen.UpdateData();

        //var r = GUILayoutUtility.GetLastRect();
        //float width = EditorGUIUtility.currentViewWidth * 0.8f;
        //Rect drawRect = new Rect(r.x + r.height * 2f, r.y + r.height * 2f, width, width);

        //GUILayout.Space(width * 1.1f);

        //EditorGUI.DrawPreviewTexture(drawRect, visualizer.texture);

    }
}

#endif
