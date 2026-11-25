Shader "NatureCalls/Map2/Painterly Surface"
{
    Properties
    {
        _BaseColor ("Paint Color", Color) = (0.82, 0.76, 0.67, 1)
        _BrushMap ("Packed Brushwork", 2D) = "gray" {}
        _BrushScale ("Brush Density", Float) = 1.45
        _BrushStrength ("Brush Contrast", Range(0,1)) = 0.6
        _Gloss ("Glaze", Range(0,1)) = 0.08
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float _BrushScale;
            half _BrushStrength;
            half _Gloss;
        CBUFFER_END
        ENDHLSL
        Pass
        {
            Name "PainterlyForward"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BrushMap);
            SAMPLER(sampler_BrushMap);
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half fog : TEXCOORD2;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fog = ComputeFogFactor(pos.positionCS.z);
                return output;
            }
            half3 LitDirection(Light light, half3 normal, half3 view)
            {
                half diffuse = saturate(dot(normal, light.direction));
                half sheen = pow(saturate(dot(normal, normalize(light.direction + view))), 19) * _Gloss;
                return light.color * (diffuse * 0.87h + sheen) * light.distanceAttenuation * light.shadowAttenuation;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half3 normal = normalize(input.normalWS);
                half3 weight = pow(abs(normal), 4);
                weight /= max(dot(weight, half3(1,1,1)), 0.001h);
                float3 p = input.positionWS * _BrushScale;
                half brush = dot(weight, half3(
                    SAMPLE_TEXTURE2D(_BrushMap, sampler_BrushMap, p.zy).r,
                    SAMPLE_TEXTURE2D(_BrushMap, sampler_BrushMap, p.xz).r,
                    SAMPLE_TEXTURE2D(_BrushMap, sampler_BrushMap, p.xy).r));
                half stroke = (brush - 0.48h) * _BrushStrength;
                half3 albedo = _BaseColor.rgb * (1 + stroke * 1.65h);
                albedo += half3(0.22h, 0.105h, 0.025h) * max(stroke, 0);
                half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 light = max(SampleSH(normal), half3(.27h,.25h,.29h));
                light += LitDirection(mainLight, normal, view);
                #if defined(_ADDITIONAL_LIGHTS)
                uint lightCount = GetAdditionalLightsCount();
                for (uint index = 0; index < lightCount; index++)
                    light += LitDirection(GetAdditionalLight(index, input.positionWS), normal, view);
                #endif
                return half4(MixFog(albedo * light, input.fog), 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment DepthFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            float3 _LightPosition;
            struct ShadowAttributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            float4 ShadowVert(ShadowAttributes input) : SV_POSITION
            {
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                float3 normal = TransformObjectToWorldNormal(input.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 direction = normalize(_LightPosition - world);
                #else
                    float3 direction = _LightDirection;
                #endif
                return ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(world, normal, direction)));
            }
            half4 DepthFrag(float4 position : SV_POSITION) : SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            float4 DepthVert(float4 position : POSITION) : SV_POSITION { return TransformObjectToHClip(position.xyz); }
            half4 DepthFrag(float4 position : SV_POSITION) : SV_Target { return position.z; }
            ENDHLSL
        }
    }
}
