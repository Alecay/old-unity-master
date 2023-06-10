using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TextureViewer3D : MonoBehaviour
{	
	[Range(0,1)]
	public float sliceDepth;
	Material material;

	public RenderTexture texture;
	public ComputeShader densityCompute;

	public int size;
	public float boundsSize;
	public float noiseHeightMultiplier;
	public float noiseScale;
	public float lacunarity = 2;
	[Range(0,1)]
	public float persistence = 0.5f;
	[Range(-1,1)]
	public float surfaceLevel;

	void Start()
	{		
		material = GetComponentInChildren<MeshRenderer>().material;

		Create3DTexture(ref texture, size, "Raw Density Texture");
		ComputeDensity();
	}

	void ComputeDensity()
	{
		// Get points (each point is a vector4: xyz = position, w = density)
		int textureSize = texture.width;

		densityCompute.SetTexture(0, "DensityTexture", texture);
		densityCompute.SetInt("textureSize", textureSize);

		densityCompute.SetFloat("planetSize", boundsSize);
		densityCompute.SetFloat("noiseHeightMultiplier", noiseHeightMultiplier);
		densityCompute.SetFloat("noiseScale", noiseScale);
		densityCompute.SetFloat("lacunarity", lacunarity);
		densityCompute.SetFloat("persistence", persistence);

		ComputeHelper.Dispatch(densityCompute, textureSize, textureSize, textureSize);

		//ProcessDensityMap();
	}

	public void Display() {

	}

	
	void Update()
	{
		ComputeDensity();

		material.SetFloat("sliceDepth", sliceDepth);
		material.SetFloat("surfaceLevel", surfaceLevel);
		material.SetTexture("DisplayTexture", texture);

	}

	void Create3DTexture(ref RenderTexture texture, int size, string name)
	{
		//
		var format = UnityEngine.Experimental.Rendering.GraphicsFormat.R32_SFloat;
		if (texture == null || !texture.IsCreated() || texture.width != size || texture.height != size || texture.volumeDepth != size || texture.graphicsFormat != format)
		{
			//Debug.Log ("Create tex: update noise: " + updateNoise);
			if (texture != null)
			{
				texture.Release();
			}
			const int numBitsInDepthBuffer = 0;
			texture = new RenderTexture(size, size, numBitsInDepthBuffer);
			texture.graphicsFormat = format;
			texture.volumeDepth = size;
			texture.enableRandomWrite = true;
			texture.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;


			texture.Create();
		}
		texture.wrapMode = TextureWrapMode.Repeat;
		texture.filterMode = FilterMode.Bilinear;
		texture.name = name;
	}
}
