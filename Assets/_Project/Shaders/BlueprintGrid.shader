// Plano del modo Construcción (BlueprintOverlay). Un quad del tamaño del mapa con dos modos:
//  _Mode 0: rejilla por niveles (9×9, 3×3 y casillas) con líneas de grosor constante en píxeles (fwidth); más tenue
//           donde no se puede pintar (canal R de _Mask).
//  _Mode 1: casillas en plano (canal G) como cuadrados rellenos con borde fino, y el contorno de la huella de las
//           salas en plano (canal B).
// _Mask es una textura de una casilla por píxel (filtro Point). WebGL 2 tiene derivadas (fwidth) de serie.
Shader "Myrmutation/BlueprintGrid"
{
    Properties
    {
        [HideInInspector] _MainTex ("Sprite", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_Mask);
            SAMPLER(sampler_Mask);
            float4 _Origin;       // xy: esquina inferior izquierda de la casilla (0, 0); zw: ancho y alto en casillas
            float _CellSize;
            float _Mode;
            float4 _LevelAlpha;   // x: 9×9, y: 3×3, z: casillas
            float4 _GridColor;
            float4 _DimColor;
            float4 _PlanColor;
            float _FillAlpha;
            float _LineAlpha;

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 cell : TEXCOORD0; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                float3 world = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(world);
                o.cell = (world.xy - _Origin.xy) / _CellSize;
                return o;
            }

            float4 Mask(float2 cell) { return SAMPLE_TEXTURE2D_LOD(_Mask, sampler_Mask, (floor(cell) + 0.5) / _Origin.zw, 0); }

            // Cobertura (0–1) de las líneas de 'width' píxeles en los múltiplos de 'period' casillas.
            float Lines(float2 cell, float2 fw, float period, float width)
            {
                float2 d = abs(frac(cell / period + 0.5) - 0.5) * period / fw;
                return saturate(width * 0.5 + 0.5 - min(d.x, d.y));
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 fw = max(fwidth(i.cell), 1e-5);   // casillas por píxel
                float4 m = Mask(i.cell);

                if (_Mode < 0.5)
                {
                    float a = max(max(Lines(i.cell, fw, 9.0, 2.0) * _LevelAlpha.x,
                                      Lines(i.cell, fw, 3.0, 1.5) * _LevelAlpha.y),
                                  Lines(i.cell, fw, 1.0, 1.0) * _LevelAlpha.z);
                    float4 col = m.r > 0.5 ? _DimColor : _GridColor;
                    return half4(col.rgb, col.a * a);
                }

                // Píxeles hasta los bordes de la casilla: izquierdo / abajo (lo) y derecho / arriba (hi).
                float2 f = frac(i.cell);
                float2 lo = f / fw, hi = (1.0 - f) / fw;
                float edge = saturate(1.5 - min(min(lo.x, lo.y), min(hi.x, hi.y)));
                float a = lerp(_FillAlpha, _LineAlpha, edge);
                if (m.b > 0.5)
                {
                    float2 c = floor(i.cell) + 0.5;
                    float outline = 0.0;
                    if (Mask(c + float2(-1, 0)).b < 0.5) outline = max(outline, saturate(2.5 - lo.x));
                    if (Mask(c + float2(1, 0)).b < 0.5) outline = max(outline, saturate(2.5 - hi.x));
                    if (Mask(c + float2(0, -1)).b < 0.5) outline = max(outline, saturate(2.5 - lo.y));
                    if (Mask(c + float2(0, 1)).b < 0.5) outline = max(outline, saturate(2.5 - hi.y));
                    a = lerp(a, _LineAlpha, outline);
                }
                return half4(_PlanColor.rgb, a * step(0.5, m.g));
            }
            ENDHLSL
        }
    }
}
