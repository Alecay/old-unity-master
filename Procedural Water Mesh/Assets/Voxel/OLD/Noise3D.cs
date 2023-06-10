//
// Description : Array and textureless GLSL 2D/3D/4D simplex 
//               noise functions.
//      Author : Ian McEwan, Ashima Arts.
//  Maintainer : stegu
//     Lastmath.mod : 20110822 (ijm)
//     License : Copyright (C) 2011 Ashima Arts. All rights reserved.
//               Distributed under the MIT License. See LICENSE file.
//               https://github.com/ashima/webgl-noise
//               https://github.com/stegu/webgl-noise
// 

using static Unity.Mathematics.math;

namespace Unity.Mathematics
{
    //public static partial class noise
    //{
    //    // Modulo 289 without a division (only multiplications)
    //    static float mod289(float x) { return x - floor(x * (1.0f / 289.0f)) * 289.0f; }
    //    static float2 mod289(float2 x) { return x - floor(x * (1.0f / 289.0f)) * 289.0f; }
    //    static float3 mod289(float3 x) { return x - floor(x * (1.0f / 289.0f)) * 289.0f; }
    //    static float4 mod289(float4 x) { return x - floor(x * (1.0f / 289.0f)) * 289.0f; }

    //    // Modulo 7 without a division
    //    static float3 mod7(float3 x) { return x - floor(x * (1.0f / 7.0f)) * 7.0f; }
    //    static float4 mod7(float4 x) { return x - floor(x * (1.0f / 7.0f)) * 7.0f; }

    //    // Permutation polynomial: (34x^2 + x) math.mod 289
    //    static float permute(float x) { return mod289((34.0f * x + 1.0f) * x); }
    //    static float3 permute(float3 x) { return mod289((34.0f * x + 1.0f) * x); }
    //    static float4 permute(float4 x) { return mod289((34.0f * x + 1.0f) * x); }

    //    static float taylorInvSqrt(float r) { return 1.79284291400159f - 0.85373472095314f * r; }
    //    static float4 taylorInvSqrt(float4 r) { return 1.79284291400159f - 0.85373472095314f * r; }

    //    static float2 fade(float2 t) { return t * t * t * (t * (t * 6.0f - 15.0f) + 10.0f); }
    //    static float3 fade(float3 t) { return t * t * t * (t * (t * 6.0f - 15.0f) + 10.0f); }
    //    static float4 fade(float4 t) { return t * t * t * (t * (t * 6.0f - 15.0f) + 10.0f); }

    //    static float4 grad4(float j, float4 ip)
    //    {
    //        float4 ones = float4(1.0f, 1.0f, 1.0f, -1.0f);
    //        float3 pxyz = floor(frac(float3(j) * ip.xyz) * 7.0f) * ip.z - 1.0f;
    //        float pw = 1.5f - dot(abs(pxyz), ones.xyz);
    //        float4 p = float4(pxyz, pw);
    //        float4 s = float4(p < 0.0f);
    //        p.xyz = p.xyz + (s.xyz * 2.0f - 1.0f) * s.www;
    //        return p;
    //    }

    //    // Hashed 2-D gradients with an extra rotation.
    //    // (The constant 0.0243902439 is 1/41)
    //    static float2 rgrad2(float2 p, float rot)
    //    {
    //        // For more isotropic gradients, math.sin/math.cos can be used instead.
    //        float u = permute(permute(p.x) + p.y) * 0.0243902439f + rot; // Rotate by shift
    //        u = frac(u) * 6.28318530718f; // 2*pi
    //        return float2(cos(u), sin(u));
    //    }

    //    public static float snoise(float3 v)
    //    {
    //        float2 C = float2(1.0f / 6.0f, 1.0f / 3.0f);
    //        float4 D = float4(0.0f, 0.5f, 1.0f, 2.0f);

    //        // First corner
    //        float3 i = floor(v + dot(v, C.yyy));
    //        float3 x0 = v - i + dot(i, C.xxx);

    //        // Other corners
    //        float3 g = step(x0.yzx, x0.xyz);
    //        float3 l = 1.0f - g;
    //        float3 i1 = min(g.xyz, l.zxy);
    //        float3 i2 = max(g.xyz, l.zxy);

    //        //   x0 = x0 - 0.0 + 0.0 * C.xxx;
    //        //   x1 = x0 - i1  + 1.0 * C.xxx;
    //        //   x2 = x0 - i2  + 2.0 * C.xxx;
    //        //   x3 = x0 - 1.0 + 3.0 * C.xxx;
    //        float3 x1 = x0 - i1 + C.xxx;
    //        float3 x2 = x0 - i2 + C.yyy; // 2.0*C.x = 1/3 = C.y
    //        float3 x3 = x0 - D.yyy; // -1.0+3.0*C.x = -0.5 = -D.y

    //        // Permutations
    //        i = mod289(i);
    //        float4 p = permute(permute(permute(
    //                                     i.z + float4(0.0f, i1.z, i2.z, 1.0f))
    //                                 + i.y + float4(0.0f, i1.y, i2.y, 1.0f))
    //                         + i.x + float4(0.0f, i1.x, i2.x, 1.0f));

    //        // Gradients: 7x7 points over a square, mapped onto an octahedron.
    //        // The ring size 17*17 = 289 is close to a multiple of 49 (49*6 = 294)
    //        float n_ = 0.142857142857f; // 1.0/7.0
    //        float3 ns = n_ * D.wyz - D.xzx;

    //        float4 j = p - 49.0f * floor(p * ns.z * ns.z); //  math.mod(p,7*7)

    //        float4 x_ = floor(j * ns.z);
    //        float4 y_ = floor(j - 7.0f * x_); // math.mod(j,N)

    //        float4 x = x_ * ns.x + ns.yyyy;
    //        float4 y = y_ * ns.x + ns.yyyy;
    //        float4 h = 1.0f - abs(x) - abs(y);

    //        float4 b0 = float4(x.xy, y.xy);
    //        float4 b1 = float4(x.zw, y.zw);

    //        //float4 s0 = float4(math.lessThan(b0,0.0))*2.0 - 1.0;
    //        //float4 s1 = float4(math.lessThan(b1,0.0))*2.0 - 1.0;
    //        float4 s0 = floor(b0) * 2.0f + 1.0f;
    //        float4 s1 = floor(b1) * 2.0f + 1.0f;
    //        float4 sh = -step(h, float4(0.0f));

    //        float4 a0 = b0.xzyw + s0.xzyw * sh.xxyy;
    //        float4 a1 = b1.xzyw + s1.xzyw * sh.zzww;

    //        float3 p0 = float3(a0.xy, h.x);
    //        float3 p1 = float3(a0.zw, h.y);
    //        float3 p2 = float3(a1.xy, h.z);
    //        float3 p3 = float3(a1.zw, h.w);

    //        //Normalise gradients
    //        float4 norm = taylorInvSqrt(float4(dot(p0, p0), dot(p1, p1), dot(p2, p2), dot(p3, p3)));
    //        p0 *= norm.x;
    //        p1 *= norm.y;
    //        p2 *= norm.z;
    //        p3 *= norm.w;

    //        // Mix final noise value
    //        float4 m = max(0.6f - float4(dot(x0, x0), dot(x1, x1), dot(x2, x2), dot(x3, x3)), 0.0f);
    //        m = m * m;
    //        return 42.0f * dot(m * m, float4(dot(p0, x0), dot(p1, x1), dot(p2, x2), dot(p3, x3)));
    //    }
    //}
}
