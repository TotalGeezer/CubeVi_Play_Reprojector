Shader "Hidden/SplitTexture"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Half ("Half (0 for Left, 1 for Right)", Float) = 0
        _FlipY ("Flip Y", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Half;
            float _FlipY;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                
                // Adjust UV to get left or right half
                float2 uv = v.uv;
                if (_Half == 0) // Left half
                    uv.x = uv.x * 0.5;
                else // Right half
                    uv.x = 0.5 + uv.x * 0.5;
                
                // Flip Y if needed (WGC captures upside down)
                if (_FlipY > 0.5)
                    uv.y = 1.0 - uv.y;
                    
                o.uv = uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return tex2D(_MainTex, i.uv);
            }
            ENDCG
        }
    }
}