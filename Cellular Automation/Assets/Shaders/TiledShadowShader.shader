Shader "Unlit/TiledShadowShader"
{
    Properties {
		_Color ("Color", Color) = (1,1,1,1)
		_MainTex ("Albedo (RGB)", 2D) = "white" {}
		_Map_Width ("Map Width",  Range(1, 32)) = 32		
		_Pixels_Per_Unit_Square ("Pixels Per Unit Square", Range(8, 256)) = 32
		_Max_Alpha ("Max Aplha", Range(0, 0.999)) = 0.5
		_Fade_Amount ("Fade Amount", Range(0, 0.999)) = 0.7
		_Fade_Falloff ("Fade Falloff", Range(0, 0.999)) = 0.7
		_Global_Illumination ("Global Illumination", Range(0, 1)) = 1
		_Min_Tile_Shadow_Alpha ("Min Tile Shadow Alpha", Range(0,1)) = 0
		_Tile_Shadow_Step ("Tile Shadow Step", Int) = 50
	}
	SubShader {
		Tags { "Queue"="Transparent" "RenderType"="Transparent" }
		LOD 200

		ZTest Off		
		
		CGPROGRAM		
		// Upgrade NOTE: excluded shader from DX11, OpenGL ES 2.0 because it uses unsized arrays
		//#pragma exclude_renderers d3d11 gles

		#pragma surface surf Lambert alpha
		#pragma target 4.0		

		sampler2D _MainTex;

		struct Input {
			float2 uv_MainTex;
		};
		
		fixed4 _Color;		

		int _Map_Width;				
		int _Pixels_Per_Unit_Square;
		
		float _Max_Alpha;
		float _Fade_Amount;
		float _Fade_Falloff;

		float _Global_Illumination;		
		float _Min_Tile_Shadow_Alpha;

		int _Tile_Shadow_Step;
		
		//Max Array size is 32x32
		uniform float occupiedTiles[256];	
		uniform float lastTileOccupied = 0;

		uniform float northOccupied[34];
		uniform float eastOccupied[32];		
		uniform float southOccupied[34];
		uniform float westOccupied[32];


		uniform float2 lightSourcePositions[100]; //(PixelX, PixelY)
		uniform float3 lightSourceSettings[100]; //(Strength, Intensity, Range)
		uniform float4 lightSourceColors[100]; //Color

		uniform float tileShadows[1023];
		uniform float lastTileShadow = 0;

		int ToLinearIndex(int x, int y)
		{
			int index = (y * _Map_Width + x);

			if(index < 0)
			{
				index = 0;
			}
			else if(index >= 1024)
			{
				index = 1023;
			}

			return index;
		}

		bool TileIsOccupied(int x, int y)
		{
			if(x == y && _Map_Width == 32 && x == _Map_Width - 1)
			{
				return lastTileOccupied == 1;
			}

			if(y == -1)
			{
				return southOccupied[x + 1] == 1;
			}
			else if(y == _Map_Width)
			{
				return northOccupied[x + 1] == 1;
			}

			if(x == -1)
			{
				return westOccupied[y] == 1;
			}
			else if(x == _Map_Width)
			{
				return eastOccupied[y] == 1;
			}


			int index = ToLinearIndex(x,y);

			return occupiedTiles[index] == 1;
		}

		int GetTileX(float2 uv)
		{
			int x = uv[0] * _Map_Width;
			return x;
		}

		int GetTileY(float2 uv)
		{
			int y = uv[1] * _Map_Width;
			return y;
		}

		int GetPixelX(float2 uv)
		{			
			int x = (uv[0] * _Map_Width * _Pixels_Per_Unit_Square) % _Pixels_Per_Unit_Square;
			return x;
		}

		int GetPixelY(float2 uv)
		{			
			int y = (uv[1] * _Map_Width * _Pixels_Per_Unit_Square) % _Pixels_Per_Unit_Square;
			return y;
		}

		float inverse_lerp(float a, float b, float l)
		{
			return  clamp((l - a) / (b - a), 0, 1);
		}

		float GetShadowPixel(int index, int x, int y, float fadeOffsetX, float fadeOffsetY)
		{

			index = clamp(index, 0, 7);
			x = clamp(x, 0, _Pixels_Per_Unit_Square - 1);
			y = clamp(y, 0, _Pixels_Per_Unit_Square - 1);

			fadeOffsetX = clamp(fadeOffsetX, 0, 0.999);
			fadeOffsetY = clamp(fadeOffsetY, 0, 0.999);

			_Pixels_Per_Unit_Square = clamp(_Pixels_Per_Unit_Square, 8, 256);

			float xPercent = 0;
			float yPercent = 0;
			float percent = 0;
			float alpha = 0; 

			if(index == 0) //North
			{
				yPercent = inverse_lerp(0, _Pixels_Per_Unit_Square - 1, y);
                percent = inverse_lerp(0, 1 - fadeOffsetY, yPercent);                
			}
			else if(index == 1) //NorthEast
			{
				yPercent = inverse_lerp(0, _Pixels_Per_Unit_Square - 1, y);
				xPercent = inverse_lerp(0, _Pixels_Per_Unit_Square - 1, x);

				float xOff = inverse_lerp(0, 1 - fadeOffsetX, xPercent);
                float yOff = inverse_lerp(0, 1 - fadeOffsetY, yPercent);

                percent = max(xOff, yOff);
			}
			else if(index == 2) //East
			{
				xPercent = inverse_lerp(0, _Pixels_Per_Unit_Square - 1, x);
                percent = inverse_lerp(0, 1 - fadeOffsetX, xPercent);                
			}
			else if(index == 3) //SouthEast
			{
				yPercent = inverse_lerp(_Pixels_Per_Unit_Square - 1, 0, y);
				xPercent = inverse_lerp(0, _Pixels_Per_Unit_Square - 1, x);
				float xOff = inverse_lerp(0, 1 - fadeOffsetX, xPercent);
				float yOff = inverse_lerp(0, 1 - fadeOffsetY, yPercent);
				percent = max(xOff, yOff);
			}
			else if(index == 4) //South
			{
				yPercent = inverse_lerp(_Pixels_Per_Unit_Square - 1, 0, y);
                percent = inverse_lerp(0, 1 - fadeOffsetY, yPercent);                
			}
			else if(index == 5) //SouthWest
			{
				yPercent = inverse_lerp(_Pixels_Per_Unit_Square - 1, 0, y);
				xPercent = inverse_lerp(_Pixels_Per_Unit_Square - 1, 0, x);
				float xOff = inverse_lerp(0, 1 - fadeOffsetX, xPercent);
				float yOff = inverse_lerp(0, 1 - fadeOffsetY, yPercent);
				percent = max(xOff, yOff);
			}
			else if(index == 6) //West
			{
				xPercent = inverse_lerp(_Pixels_Per_Unit_Square - 1, 0, x);
                percent = inverse_lerp(0, 1 - fadeOffsetX, xPercent);                
			}
			else if(index == 7) //NorthWest
			{
				yPercent = inverse_lerp(0, _Pixels_Per_Unit_Square - 1, y);
				xPercent = inverse_lerp(_Pixels_Per_Unit_Square - 1, 0, x);
				float xOff = inverse_lerp(0, 1 - fadeOffsetX, xPercent);
				float yOff = inverse_lerp(0, 1 - fadeOffsetY, yPercent);
				percent = max(xOff, yOff);
			}	
			
			alpha = lerp(_Max_Alpha, 0, percent);

			return alpha;
		}

		float GetAngledShadowPixel(int index, int x, int y)
		{
			index = clamp(index, 0, 7);

			float fallOff = (1 - _Fade_Amount) * _Fade_Falloff;

			if(index <= 2)
			{
				return GetShadowPixel(index, x, y, _Fade_Amount + fallOff, _Fade_Amount + fallOff);
			}
			else if(index == 3)
			{
				return GetShadowPixel(index, x, y, _Fade_Amount + fallOff, _Fade_Amount);
			}
			else if(index <= 6)
			{
				return GetShadowPixel(index, x, y, _Fade_Amount, _Fade_Amount);
			}
			else
			{
				return GetShadowPixel(index, x, y, _Fade_Amount, _Fade_Amount + fallOff);
			}


			return 0;
		}

		float GetShadowAlphaFromUV(float2 uv)
		{
			int x = GetTileX(uv);
			int y = GetTileY(uv);

			int pixelX = GetPixelX(uv);
			int pixelY = GetPixelY(uv);

			float maxAlpha = 0;
			float alpha = 0;

			if(TileIsOccupied(x,y))
			{
				return 0;
			}

			if(TileIsOccupied(x, y + 1))
			{
				alpha = GetAngledShadowPixel(4, pixelX, pixelY);
				if(alpha > maxAlpha)
				{
					maxAlpha = alpha;
				}
			}

			if(TileIsOccupied(x + 1, y + 1))
			{
				alpha = GetAngledShadowPixel(5, pixelX, pixelY);
				if(alpha > maxAlpha)
				{
					maxAlpha = alpha;
				}
			}

			if(TileIsOccupied(x + 1, y))
			{
				alpha = GetAngledShadowPixel(6, pixelX, pixelY);
				if(alpha > maxAlpha)
				{
					maxAlpha = alpha;
				}
			}

			if(TileIsOccupied(x + 1, y - 1))
			{
				alpha = GetAngledShadowPixel(7, pixelX, pixelY);
				if(alpha > maxAlpha)
				{
					maxAlpha = alpha;
				}
			}

			if(TileIsOccupied(x, y - 1))
			{
				alpha = GetAngledShadowPixel(0, pixelX, pixelY);
				if(alpha > maxAlpha)
				{
					maxAlpha = alpha;
				}
			}

			if(TileIsOccupied(x - 1, y - 1))
			{
				alpha = GetAngledShadowPixel(1, pixelX, pixelY);
				if(alpha > maxAlpha)
				{
					maxAlpha = alpha;
				}
			}

			if(TileIsOccupied(x - 1, y))
			{
				alpha = GetAngledShadowPixel(2, pixelX, pixelY);
				if(alpha > maxAlpha)
				{
					maxAlpha = alpha;
				}
			}

			if(TileIsOccupied(x - 1, y + 1))
			{
				alpha = GetAngledShadowPixel(3, pixelX, pixelY);
				if(alpha > maxAlpha)
				{
					maxAlpha = alpha;
				}
			}

			return maxAlpha;
		}

		float GetLightMagnitude(float2 uv)
		{

			int x = GetTileX(uv);
			int y = GetTileY(uv);

			int pixelX = GetPixelX(uv);
			int pixelY = GetPixelY(uv);				

			//float xDist = 0, yDist = 0, dist = 0, lightRange = 0;

			float maxLight = 1;

			for(int i = 0; i < 1024; i++)
			{
				if(lightSourceSettings[i][2] > 0)
				{
					int lightX = lightSourcePositions[i][0];
					int lightY = lightSourcePositions[i][1];

					float strength = lightSourceSettings[i][0];
					float lIntensity = lightSourceSettings[i][1];
					float range = lightSourceSettings[i][2];

					float xDist = abs(x * _Pixels_Per_Unit_Square + pixelX - lightX);
					float yDist = abs(y * _Pixels_Per_Unit_Square + pixelY - lightY);

					float dist = sqrt(pow(xDist, 2) + pow(yDist, 2));

					float lightRange = _Pixels_Per_Unit_Square * range / 10.0;
					float distRatio = (dist / lightRange);
					float iOffset = lIntensity * (1 - dist / lightRange);
					float sOffset = (1 - strength) * (1 - dist / lightRange);

					float light = clamp(distRatio - iOffset + sOffset, 0.1, 1.0);

					maxLight *= light;
				}
				else
				{
					break;
				}				
			}		
			
			maxLight = clamp(maxLight, 0.0, 1.0);
			
			return maxLight;
		}

		float4 GetLightTint(float2 uv)
		{
			int x = GetTileX(uv);
			int y = GetTileY(uv);

			int pixelX = GetPixelX(uv);
			int pixelY = GetPixelY(uv);				

			//float xDist = 0, yDist = 0, dist = 0, lightRange = 0;

			float4 maxColor = float4(1, 1, 1, 1);

			for(int i = 0; i < 100; i++)
			{
				if(lightSourceSettings[i][2] > 0)
				{
					int lightX = lightSourcePositions[i][0];
					int lightY = lightSourcePositions[i][1];

					float strength = lightSourceSettings[i][0];
					float lIntensity = lightSourceSettings[i][1];
					float range = lightSourceSettings[i][2];

					float xDist = abs(x * _Pixels_Per_Unit_Square + pixelX - lightX);
					float yDist = abs(y * _Pixels_Per_Unit_Square + pixelY - lightY);

					float dist = sqrt(pow(xDist, 2) + pow(yDist, 2));

					float lightRange = _Pixels_Per_Unit_Square * range / 10.0;
					float distRatio = (dist / lightRange);
					float iOffset = lIntensity * (1 - dist / lightRange);
					float sOffset = (1 - strength) * (1 - dist / lightRange);

					float light = clamp(distRatio - iOffset + sOffset, 0.1, 1.0);

					float4 lightColor = lightSourceColors[i];

					maxColor *= (lightColor);
				}
				else
				{
					break;
				}				
			}							
			
			return maxColor;
		}

		float GetTileShadow(int x, int y)
		{
			float value = 0;
			if(x == y && _Map_Width == 32 && x == _Map_Width - 1)
			{
				value = lastTileShadow;
			}
			else
			{
				value = tileShadows[x + y * _Map_Width];
			}

			value = clamp(value, 0, 1);

			if(value > 0)
			{
				value = lerp(value, 1, _Min_Tile_Shadow_Alpha);
			}			

			value = clamp(value + (1 - _Global_Illumination), 0, 1);

			float step = clamp(_Tile_Shadow_Step, 1, 100);

			value = round(value * step) / step;

			return value;
		}

		float GetTileShadowFromUV(float2 uv)
		{
			int x = GetTileX(uv);
			int y = GetTileY(uv);

			int pixelX = GetPixelX(uv);
			int pixelY = GetPixelY(uv);

			float maxAlpha = 0;
			float alpha = 0;

			float tileShadow = GetTileShadow(x,y);
			float otherShadow = 0;

			if(y < _Map_Width - 1)
			{
				otherShadow = GetTileShadow(x,y + 1);
				if(otherShadow != tileShadow)
				{
					alpha = GetAngledShadowPixel(4, pixelX, pixelY);
					if(alpha > maxAlpha)
					{
						maxAlpha = alpha;
					}
				}
			}

			if(x < _Map_Width - 1 && y < _Map_Width - 1)
			{
				otherShadow = GetTileShadow(x + 1, y + 1);
				if(otherShadow != tileShadow)
				{
					alpha = GetAngledShadowPixel(4, pixelX, pixelY);
					if(alpha > maxAlpha)
					{
						maxAlpha = alpha;
					}
				}
			}

			if(TileIsOccupied(x + 1, y))
			{
				alpha = GetAngledShadowPixel(6, pixelX, pixelY);
				if(alpha > maxAlpha)
				{
					maxAlpha = alpha;
				}
			}

			if(TileIsOccupied(x + 1, y - 1))
			{
				alpha = GetAngledShadowPixel(7, pixelX, pixelY);
				if(alpha > maxAlpha)
				{
					maxAlpha = alpha;
				}
			}

			if(TileIsOccupied(x, y - 1))
			{
				alpha = GetAngledShadowPixel(0, pixelX, pixelY);
				if(alpha > maxAlpha)
				{
					maxAlpha = alpha;
				}
			}

			if(TileIsOccupied(x - 1, y - 1))
			{
				alpha = GetAngledShadowPixel(1, pixelX, pixelY);
				if(alpha > maxAlpha)
				{
					maxAlpha = alpha;
				}
			}
		}

		float GetAlphaFromUV(float2 uv)
		{
			int x = GetTileX(uv);
			int y = GetTileY(uv);

			int pixelX = GetPixelX(uv);
			int pixelY = GetPixelY(uv);


			float tileShadow = GetTileShadow(x, y);
			float maxAlpha = 1 - _Global_Illumination;
			float shadowAlpha = GetShadowAlphaFromUV(uv);

			bool usedTileShadow = false;

			if(maxAlpha < tileShadow)
			{
				maxAlpha = tileShadow;
				usedTileShadow = true;
			}
			
			if(maxAlpha < shadowAlpha)
			{
				maxAlpha = shadowAlpha;
			}

			float lightMagnitude = GetLightMagnitude(uv);

			if(usedTileShadow == false)
			{				
				maxAlpha -= (1 - lightMagnitude);
			}
			else
			{
				maxAlpha -= (1 - lightMagnitude) * 0.5f;
			}

			maxAlpha = clamp(maxAlpha, 0, 1);

			return maxAlpha;
		}

		void surf (Input IN, inout SurfaceOutput o) 
		{
			fixed4 c = _Color;

			float4 lightTint = GetLightTint(IN.uv_MainTex);

			o.Albedo = c.rgb * lightTint.rgb;
			o.Alpha = GetAlphaFromUV(IN.uv_MainTex);
		}
		ENDCG
	}
	FallBack "Diffuse"
}
