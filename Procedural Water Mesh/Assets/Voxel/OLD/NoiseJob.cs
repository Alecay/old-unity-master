using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;

public class NoiseJob : MonoBehaviour
{
	public NativeArray<float> densityValues;

    private void Start()
    {
        requestTimeInfo = new RequestTimeInfo();

  //      int points = width * width * height;

		//densityValues = new NativeArray<float>(points, Allocator.Persistent);

		//var job = new DensityJob()
		//{
		//	width = width,
		//	height = height,
		//	noiseData = new DensityJob.NoiseData(noiseData),
		//	densityValues = densityValues
		//};

		//job.Schedule(points, width).Complete();

		//string values = "Values: ";

  //      for (int i = 0; i < 25; i++)
  //      {
		//	values += densityValues[i] + ", ";
  //      }

		//print(values);
    }

    private void OnDisable()
    {
		if(densityValues.IsCreated)
			densityValues.Dispose();
    }

    [System.Serializable]
	public class NoiseData
	{
		public int seed = 42;

		[Range(1, 8)]
		public int octaves = 5;

		public float scale = 100.0f;

		public float persistance = 0.5f;
		public float lacunarity = 2.0f;

		public Vector3 offset = new Vector3(0, 0, 0);

		public int width = 16;

		public int worldHeight = 256;

		public float heightMultiplier = 16.0f;

		public int heightOffset = 1;
	}

	[SerializeField]
	public NoiseData noiseData;

	[BurstCompile]
    private struct DensityJob : IJobParallelFor
    {
		public int width;
		public int height;

		public NoiseData noiseData;

		public NativeArray<float> densityValues;

		public struct NoiseData
		{
			public int seed;

			[Range(1, 8)]
			public int octaves;

			public float scale;

			public float persistance;
			public float lacunarity;

			public Vector3 offset;

			public int width;

			public int worldHeight;

			public float heightMultiplier;

			public int heightOffset;

            public NoiseData(int seed, int octaves, float scale, float persistance, float lacunarity, 
				Vector3 offset, int width, int worldHeight, float heightMultiplier, int heightOffset)
            {
                this.seed = seed;
                this.octaves = octaves;
                this.scale = scale;
                this.persistance = persistance;
                this.lacunarity = lacunarity;
                this.offset = offset;
                this.width = width;
                this.worldHeight = worldHeight;
                this.heightMultiplier = heightMultiplier;
                this.heightOffset = heightOffset;
            }

			public NoiseData(NoiseJob.NoiseData noiseData)
            {
				this.seed = noiseData.seed;
				this.octaves = noiseData.octaves;
				this.scale = noiseData.scale;
				this.persistance = noiseData.persistance;
				this.lacunarity = noiseData.lacunarity;
				this.offset = noiseData.offset;
				this.width = noiseData.width;
				this.worldHeight = noiseData.worldHeight;
				this.heightMultiplier = noiseData.heightMultiplier;
				this.heightOffset = noiseData.heightOffset;
			}
        }

        //Modulo 289 without a division(only multiplications)

  //      static float mod289(float x) { return x - math.floor(x * (1.0f / 289.0f)) * 289.0f; }
  //      static float2 mod289(float2 x) { return x - math.floor(x * (1.0f / 289.0f)) * 289.0f; }
  //      static float3 mod289(float3 x) { return x - math.floor(x * (1.0f / 289.0f)) * 289.0f; }
  //      static float4 mod289(float4 x) { return x - math.floor(x * (1.0f / 289.0f)) * 289.0f; }

  //      // Modulo 7 without a division
  //      static float3 mod7(float3 x) { return x - math.floor(x * (1.0f / 7.0f)) * 7.0f; }
  //      static float4 mod7(float4 x) { return x - math.floor(x * (1.0f / 7.0f)) * 7.0f; }

  //      // Permutation polynomial: (34x^2 + x) math.mod 289
  //      static float permute(float x) { return mod289((34.0f * x + 1.0f) * x); }
  //      static float3 permute(float3 x) { return mod289((34.0f * x + 1.0f) * x); }
  //      static float4 permute(float4 x) { return mod289((34.0f * x + 1.0f) * x); }

  //      static float taylorInvSqrt(float r) { return 1.79284291400159f - 0.85373472095314f * r; }
  //      static float4 taylorInvSqrt(float4 r) { return 1.79284291400159f - 0.85373472095314f * r; }

  //      static float2 fade(float2 t) { return t * t * t * (t * (t * 6.0f - 15.0f) + 10.0f); }
  //      static float3 fade(float3 t) { return t * t * t * (t * (t * 6.0f - 15.0f) + 10.0f); }
  //      static float4 fade(float4 t) { return t * t * t * (t * (t * 6.0f - 15.0f) + 10.0f); }

  //      static float4 grad4(float j, float4 ip)
  //      {
  //          float4 ones = new float4(1.0f, 1.0f, 1.0f, -1.0f);
  //          float3 pxyz = math.floor(math.frac(new float3(j) * ip.xyz) * 7.0f) * ip.z - 1.0f;
  //          float pw = 1.5f - math.dot(math.abs(pxyz), ones.xyz);
  //          float4 p = new float4(pxyz, pw);
  //          float4 s = new float4(p < 0.0f);
  //          p.xyz = p.xyz + (s.xyz * 2.0f - 1.0f) * s.www;
  //          return p;
  //      }

  //      // Hashed 2-D gradients with an extra rotation.
  //      // (The constant 0.0243902439 is 1/41)
  //      static float2 rgrad2(float2 p, float rot)
  //      {
  //          // For more isotropic gradients, math.sin/math.cos can be used instead.
  //          float u = permute(permute(p.x) + p.y) * 0.0243902439f + rot; // Rotate by shift
  //          u = math.frac(u) * 6.28318530718f; // 2*pi
  //          return new float2(math.cos(u), math.sin(u));
  //      }

  //      public static float snoise(float3 v)
		//{
		//	float2 C = new float2(1.0f / 6.0f, 1.0f / 3.0f);
		//	float4 D = new float4(0.0f, 0.5f, 1.0f, 2.0f);

		//	// First corner
		//	float3 i = math.floor(v + math.dot(v, C.yyy));
		//	float3 x0 = v - i + math.dot(i, C.xxx);

		//	// Other corners
		//	float3 g = math.step(x0.yzx, x0.xyz);
		//	float3 l = 1.0f - g;
		//	float3 i1 = math.min(g.xyz, l.zxy);
		//	float3 i2 = math.max(g.xyz, l.zxy);

		//	//   x0 = x0 - 0.0 + 0.0 * C.xxx;
		//	//   x1 = x0 - i1  + 1.0 * C.xxx;
		//	//   x2 = x0 - i2  + 2.0 * C.xxx;
		//	//   x3 = x0 - 1.0 + 3.0 * C.xxx;
		//	float3 x1 = x0 - i1 + C.xxx;
		//	float3 x2 = x0 - i2 + C.yyy; // 2.0*C.x = 1/3 = C.y
		//	float3 x3 = x0 - D.yyy; // -1.0+3.0*C.x = -0.5 = -D.y

		//	// Permutations
		//	i = mod289(i);
		//	float4 p = permute(permute(permute(
		//								 i.z + new float4(0.0f, i1.z, i2.z, 1.0f))
		//							 + i.y + new float4(0.0f, i1.y, i2.y, 1.0f))
		//					 + i.x + new float4(0.0f, i1.x, i2.x, 1.0f));

		//	// Gradients: 7x7 points over a square, mapped onto an octahedron.
		//	// The ring size 17*17 = 289 is close to a multiple of 49 (49*6 = 294)
		//	float n_ = 0.142857142857f; // 1.0/7.0
		//	float3 ns = n_ * D.wyz - D.xzx;

		//	float4 j = p - 49.0f * math.floor(p * ns.z * ns.z); //  math.mod(p,7*7)

		//	float4 x_ = math.floor(j * ns.z);
		//	float4 y_ = math.floor(j - 7.0f * x_); // math.mod(j,N)

		//	float4 x = x_ * ns.x + ns.yyyy;
		//	float4 y = y_ * ns.x + ns.yyyy;
		//	float4 h = 1.0f - math.abs(x) - math.abs(y);

		//	float4 b0 = new float4(x.xy, y.xy);
		//	float4 b1 = new float4(x.zw, y.zw);

		//	//float4 s0 = float4(math.lessThan(b0,0.0))*2.0 - 1.0;
		//	//float4 s1 = float4(math.lessThan(b1,0.0))*2.0 - 1.0;
		//	float4 s0 = math.floor(b0) * 2.0f + 1.0f;
		//	float4 s1 = math.floor(b1) * 2.0f + 1.0f;
		//	float4 sh = -math.step(h, new float4(0.0f));

		//	float4 a0 = b0.xzyw + s0.xzyw * sh.xxyy;
		//	float4 a1 = b1.xzyw + s1.xzyw * sh.zzww;

		//	float3 p0 = new float3(a0.xy, h.x);
		//	float3 p1 = new float3(a0.zw, h.y);
		//	float3 p2 = new float3(a1.xy, h.z);
		//	float3 p3 = new float3(a1.zw, h.w);

		//	//Normalise gradients
		//	float4 norm = taylorInvSqrt(new float4(math.dot(p0, p0), math.dot(p1, p1), math.dot(p2, p2), math.dot(p3, p3)));
		//	p0 *= norm.x;
		//	p1 *= norm.y;
		//	p2 *= norm.z;
		//	p3 *= norm.w;

		//	// Mix final noise value
		//	float4 m = math.max(0.6f - new float4(math.dot(x0, x0), math.dot(x1, x1), math.dot(x2, x2), math.dot(x3, x3)), 0.0f);
		//	m = m * m;
		//	return 42.0f * math.dot(m * m, new float4(math.dot(p0, x0), math.dot(p1, x1), math.dot(p2, x2), math.dot(p3, x3)));
		//}

		float Noise3D(float x, float y, float z)
		{
			float minScale = 1.0f / 10000.0f;
			float Scale = noiseData.scale;

			if (Scale < minScale)
			{
				Scale = minScale;
			}

			float amplitude = 1;
			float frequency = 1;
			float noiseHeight = 0;

			float sampleX = 0;
			float sampleY = 0;
			float sampleZ = 0;
			float perlinValue = 0;

			float halfXWidth = noiseData.width / 2.0f;
			float halfYWidth = noiseData.worldHeight / 2.0f;
			float halfZWidth = noiseData.width / 2.0f;

			bool centered = false;

			if (!centered)
			{
				halfXWidth = 0;
				halfYWidth = 0;
				halfZWidth = 0;
			}

			float maxPerlinValue = 0;

			for (uint i = 0; i < noiseData.octaves; i++)
			{
				maxPerlinValue += amplitude;

				amplitude *= noiseData.persistance;
			}

			amplitude = 1;

			float seedX = math.lerp(-1000, 1000, (noise.snoise(new float2(noiseData.seed, noiseData.seed + 1)) + 1) / 2.0f);
			float seedY = math.lerp(-1000, 1000, (noise.snoise(new float2(noiseData.seed + 1, noiseData.seed)) + 1) / 2.0f);
			float seedZ = math.lerp(-1000, 1000, (noise.snoise(new float2(noiseData.seed + 1, noiseData.seed + 1)) + 1) / 2.0f);

			for (uint j = 0; j < noiseData.octaves; j++)
			{
				sampleX = ((x - halfXWidth + noiseData.offset.x) / Scale * frequency) + seedX + (j + 1);
				sampleY = ((y - halfYWidth + noiseData.offset.y) / Scale * frequency) + seedY + (j + 1);
				sampleZ = ((z - halfZWidth + noiseData.offset.z) / Scale * frequency) + seedZ + (j + 1);

				perlinValue = noise.snoise(new float3(sampleX, sampleY, sampleZ));
				//perlinValue = (perlinValue + 1) / 2.0f;

				noiseHeight += perlinValue * amplitude;

				amplitude *= noiseData.persistance;
				frequency *= noiseData.lacunarity;

			}

			return noiseHeight;
		}

		public void Execute(int index)
        {
			if(index >= width * width * height)
            {
				return;
            }

			int3 pos = LinearIndexToXYZInt3(index, width);

			densityValues[index] = Noise3D(pos.x, pos.y, pos.z);

			float nValue = Noise3D(pos.x, pos.y, pos.z);
			float density;

			float3 center = new float3(0, 0, 0);
			float3 point3D = new float3(pos.x, pos.y, pos.z) + (float3)noiseData.offset;
			float dist = math.distance(center, point3D);

			float nHeight = math.clamp(nValue, -1, 1) * noiseData.heightMultiplier;			

			float actualY = pos.y + noiseData.offset.y;

			dist = actualY - noiseData.heightOffset;

			density = math.clamp(nValue - (dist / noiseData.heightMultiplier), -1, 1);

			if (actualY < noiseData.heightOffset)
			{
				density = math.clamp(nValue - (dist / (noiseData.heightMultiplier * 0.5f)), -1, 1);
				//density = clamp(nValue - (dist / (HeightMultiplier * 1.0f)), -1, 1);
			}
			else
			{
				density = math.clamp(nValue - (dist / (noiseData.heightMultiplier * 2.0f)), -1, 1);
				//density = clamp(nValue - (dist / (HeightMultiplier * 0.5f)), -1, 1);
			}

			densityValues[index] = density;
		}
    }

    public struct DensityRequest
    {
        public VoxelP pillar;

        public DensityRequest(VoxelP pillar)
        {
            this.pillar = pillar;
        }
    }

    //Mesh request information
    private bool allowProcessingDensityRequests = true;
    private bool processingDensityRequest = false;//is a request currently being processed?    
    private Queue<DensityRequest> densityRequests = new Queue<DensityRequest>();
    public int TotalRequests
    {
        get
        {
            return densityRequests.Count;
        }
    }

    //Debug Info
    public RequestTimeInfo requestTimeInfo;

    [System.Serializable]
    public class RequestTimeInfo
    {
        public float requestTime = 0;

        private int processedRequests = 0;

        public float averageTimePerRequest;

        public void AddTime(float mms)
        {
            requestTime += mms;
            processedRequests++;

            averageTimePerRequest = (requestTime / processedRequests);
        }
    }

    public void StartProcessingDensityRequests()
    {
        allowProcessingDensityRequests = true;
        StartCoroutine(ProcessDensityRequestsCo());
    }

    private IEnumerator ProcessDensityRequestsCo()
    {
        processingDensityRequest = false;

        while (allowProcessingDensityRequests)
        {
            if (!processingDensityRequest && densityRequests.Count > 0)
            {
				ProcessNextDensityRequest();

			}

            yield return null;
        }

        yield return null;
    }

    private void ProcessNextDensityRequest()
    {
        float startTime = Time.realtimeSinceStartup;
        float mms;		

        //print("Started next render request");

        processingDensityRequest = true;
        var request = densityRequests.Dequeue();

        noiseData.width = request.pillar.width + 2;
        noiseData.worldHeight = request.pillar.height;
        noiseData.offset = new Vector3(request.pillar.position.x, 0, request.pillar.position.y) * request.pillar.width;

		int numPoints = noiseData.width * noiseData.width * noiseData.worldHeight;

		if (densityValues.IsCreated && densityValues.Length != numPoints)
        {
			densityValues.Dispose();			
		}

        if (!densityValues.IsCreated)
        {
			densityValues = new NativeArray<float>(numPoints, Allocator.Persistent);
			print("Created new array");
		}


		var job = new DensityJob()
		{
			width = noiseData.width,
			height = noiseData.worldHeight,
			noiseData = new DensityJob.NoiseData(noiseData),
			densityValues = densityValues
		};

		job.Schedule(numPoints, noiseData.width * noiseData.width).Complete();

        request.pillar.OnDensityReady();

        mms = (Time.realtimeSinceStartup - startTime) * 1000;

        requestTimeInfo.AddTime(mms);

        bool printMessages = false;

        if (printMessages)
        {
            print("Processed density request in " + mms.ToString("0.00") + " mms");
        }

        processingDensityRequest = false;        
    }

    public void RequestDensityValues(DensityRequest request)
    {
        densityRequests.Enqueue(request);
    }

    private static int LinearIndex(int x, int y, int z, int width)
	{
		return x + z * width + y * width * width;
	}

	private static Vector3Int LinearIndexToXYZ(int index, int width)
	{
		int sizeSqd = width * width;

		return new Vector3Int(index % sizeSqd % width, index / sizeSqd, index % sizeSqd / width);
	}

	private static int3 LinearIndexToXYZInt3(int index, int width)
	{
		int sizeSqd = width * width;

		return new int3(index % sizeSqd % width, index / sizeSqd, index % sizeSqd / width);
	}

}
