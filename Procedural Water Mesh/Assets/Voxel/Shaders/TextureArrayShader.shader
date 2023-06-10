Shader "Custom/TextureArrayShader"
{
    Properties
    {
        _Color("Color", Color) = (1,1,1,1)
        _MainTex("Albedo (RGB)", 2DArray) = "white" {}
        _ArrayLength("Number of Textures", Int) = 0
        _TextureIndexOffset("Texture Index Offset", Int) = 0
        _Speed("Speed", Float) = 1

        _Glossiness("Smoothness", Range(0,1)) = 0.5
        _Metallic("Metallic", Range(0,1)) = 0.0
        _ZOffset("Z Buffer Offset", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200
        Offset[_ZOffset],[_ZOffset]

        CGPROGRAM

        //#include "ArrayAnimation.cginc"

        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard fullforwardshadows vertex:vert

        // Use shader model 3.5 target, to get nicer looking lighting and texture array support
        #pragma target 3.5

        // texture arrays are not available everywhere,
        // only compile shader on platforms where they are
        #pragma require 2darray

        UNITY_DECLARE_TEX2DARRAY(_MainTex);

        struct Input
        {
            float2 uv_MainTex;
            float arrayIndex; // cannot start with “uv”
            float4 color: COLOR; // TODO could remove this if not using VertexColor and Texture2DArray together                
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;

        int _TextureIndexOffset;
        int _ArrayLength;
        float _Speed;

        //Create animation info array           
        float2 _AnimationInfo[128];

        // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
        // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
        // #pragma instancing_options assumeuniformscaling
        UNITY_INSTANCING_BUFFER_START(Props)
            // put more per-instance properties here
        UNITY_INSTANCING_BUFFER_END(Props)

        void vert(inout appdata_full v, out Input o)
        {            
            o.uv_MainTex = v.texcoord.xy;
            o.arrayIndex = v.texcoord.z;
            o.color = v.color;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {       
            int startingIndex = floor(IN.arrayIndex + 0.5f); //Round to neares int
            int frameIndex = startingIndex;
            int frames = _AnimationInfo[startingIndex].x; //Get number of frames from array

            if (frames < 1) 
            {
                frames = 1;
            }

            if (startingIndex < 0 || startingIndex >= _ArrayLength)
            {
                startingIndex = 0;
            }

            if (frames > 1) 
            {
                float framesPerSec = _AnimationInfo[startingIndex].y;

                //Get next index
                frameIndex = startingIndex + (int)floor((_Time.y * framesPerSec * _Speed) % frames);
                frameIndex %= _ArrayLength;
            }

            if (frameIndex < 0 || frameIndex >= _ArrayLength) 
            {
                frameIndex = 0;
            }

            // Albedo comes from a texture tinted by color

            float3 sampleUV = float3(IN.uv_MainTex.x, IN.uv_MainTex.y, frameIndex);

            fixed4 c = UNITY_SAMPLE_TEX2DARRAY(_MainTex, sampleUV) * _Color;
            o.Albedo = c.rgb * IN.color; // Combine normal color with the vertex color

            // Metallic and smoothness come from slider variables
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;
        }

        ENDCG
    }

    FallBack "Diffuse"
}