// The burning-page transition (Docs/mockups/burn.html is the reference: frame(p)). It draws the snapshot of the OLD screen with a fire front
// that moves from one corner to the opposite one: paper -> white-hot -> orange -> dark red -> char -> ash that falls away, and the live new screen
// shows through underneath. Every pixel has a "time of burning" t = 0.8 * distance from the corner + 0.2 * noise, the front is at _P,
// and s = (_P - t) / BAND says where the pixel is inside the burning band.
Shader "Mafia/UIBurn"
{
    Properties
    {
        _MainTex ("Snapshot of the old screen", 2D) = "white" {}
        _Noise ("Noise (R)", 2D) = "gray" {}
        _P ("Front position", Float) = 0
        _Corner ("Start corner (x, y: 0 or 1, y 1 = bottom)", Vector) = (1, 1, 0, 0)
        _Size ("Screen size in pixels", Vector) = (1080, 2400, 0, 0)
        _Tick ("Ember flicker step", Float) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex, _Noise;
            float _P, _Tick;
            float4 _Corner, _Size, _MainTex_ST;
            fixed4 _Color;       // RawImage tint (alpha is used for the "simple" fade)

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };

            v2f vert(appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv; o.color = v.color; return o; }

            // the colours are written as sRGB bytes like in the mockup; the project is in Linear, so they are converted here
            float3 C(float r, float g, float b)
            {
                float3 c = float3(r, g, b) / 255.0;
            #ifdef UNITY_COLORSPACE_GAMMA
                return c;
            #else
                return GammaToLinearSpace(c);
            #endif
            }

            float hash(float n) { return frac(sin(n) * 43758.5453); }

            fixed4 frag(v2f i) : SV_Target
            {
                const float BAND = 0.17;
                float2 uvj = float2(i.uv.x, 1.0 - i.uv.y);              // y down, like the mockup
                float d = length((uvj - _Corner.xy) * _Size.xy) / length(_Size.xy);
                float n = tex2D(_Noise, i.uv).r;
                float t = d * 0.8 + n * 0.2;
                float s = (_P - t) / BAND;

                float ma = 1.0;                                          // how much of the old page is left here
                float3 fc = float3(0, 0, 0); float fa = 0;               // the fire / char layer
                if (s <= 0.0)
                {
                    if (s > -0.14) { fa = 110.0 / 255.0 * (1.0 + s / 0.14); fc = C(255, 110.0 + 40.0 * n, 20); }       // a faint glow ahead of the front
                }
                else if (s < 1.0)
                {
                    if (s < 0.06) { fc = C(255, 246, 200); fa = 1; }
                    else if (s < 0.2) { float k = (s - 0.06) / 0.14; fc = C(255, 246 - 106 * k, 200 - 170 * k); fa = 1; }
                    else if (s < 0.38) { float k = (s - 0.2) / 0.18; fc = C(255 - 90 * k, 140 - 108 * k, 30 - 20 * k); fa = 1; }
                    else if (s < 0.62) { float k = (s - 0.38) / 0.24; fc = C(165 - 135 * k, 32 - 16 * k, 10 + 2 * k); fa = 1; }
                    else { float k = (s - 0.62) / 0.38; fc = C(30, 17, 13); fa = 1.0 - k; }
                    // an ember: a texel of the 195 x 395 grid that glows for a moment
                    float2 g = floor(uvj * float2(195.0, 395.0));
                    if (s > 0.28 && s < 0.9 && hash(g.y * 195.0 + g.x + _Tick * 91.7) > 0.972) { fc = C(255, 175, 45); fa = 1; }
                    ma = s < 0.78 ? 1.0 : 1.0 - (s - 0.78) / 0.22;
                }
                else ma = 0.0;

                fixed4 paper = tex2D(_MainTex, i.uv);
                float pa = paper.a * ma * i.color.a;
                fa *= ma * i.color.a;                                    // the char only sits where the old page still is
                float oa = fa + pa * (1.0 - fa);
                float3 rgb = oa > 0.0001 ? (fc * fa + paper.rgb * pa * (1.0 - fa)) / oa : float3(0, 0, 0);
                return fixed4(rgb, oa);
            }
            ENDCG
        }
    }
}
