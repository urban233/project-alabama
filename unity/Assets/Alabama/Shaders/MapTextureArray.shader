Shader "Alabama/Map Texture Array"
{
    Properties
    {
        _BaseArray("Source textures", 2DArray) = "" {}
        _BaseColor("Tint", Color) = (1,1,1,1)
        _Smoothness("Smoothness", Range(0,1)) = 0.06
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Cull Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D_ARRAY(_BaseArray);
        SAMPLER(sampler_BaseArray);
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _Smoothness;
        CBUFFER_END
        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            float2 layerCutoff : TEXCOORD2;
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            float2 uv : TEXCOORD2;
            nointerpolation float2 layerCutoff : TEXCOORD3;
            half fog : TEXCOORD4;
            half3 vertexLighting : TEXCOORD5;
        };
        Varyings Vert(Attributes input)
        {
            Varyings output;
            VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
            output.positionCS = position.positionCS;
            output.positionWS = position.positionWS;
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.uv = input.uv;
            output.layerCutoff = input.layerCutoff;
            output.fog = ComputeFogFactor(position.positionCS.z);
            output.vertexLighting = VertexLighting(position.positionWS, output.normalWS);
            return output;
        }
        half4 Albedo(Varyings input)
        {
            return SAMPLE_TEXTURE2D_ARRAY(_BaseArray, sampler_BaseArray, input.uv, input.layerCutoff.x) * _BaseColor;
        }
        void Cutout(Varyings input)
        {
            #if defined(_ALPHATEST_ON)
                clip(Albedo(input).a - input.layerCutoff.y);
            #endif
        }
        half4 Forward(Varyings input) : SV_Target
        {
            half4 colour = Albedo(input);
            #if defined(_ALPHATEST_ON)
                clip(colour.a - input.layerCutoff.y);
            #endif
            InputData lighting = (InputData)0;
            lighting.positionWS = input.positionWS;
            lighting.normalWS = NormalizeNormalPerPixel(input.normalWS);
            lighting.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
            lighting.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
            lighting.fogCoord = InitializeInputDataFog(float4(input.positionWS,1), input.fog);
            lighting.bakedGI = SampleSH(lighting.normalWS);
            lighting.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
            lighting.vertexLighting = input.vertexLighting;
            lighting.shadowMask = half4(1,1,1,1);
            SurfaceData surface = (SurfaceData)0;
            surface.albedo = colour.rgb;
            surface.alpha = 1;
            surface.smoothness = _Smoothness;
            surface.normalTS = half3(0,0,1);
            surface.occlusion = 1;
            half4 result = UniversalFragmentPBR(lighting, surface);
            result.rgb = MixFog(result.rgb, lighting.fogCoord);
            return result;
        }
        half4 Depth(Varyings input) : SV_Target { Cutout(input); return 0; }
        half4 Normals(Varyings input) : SV_Target
        {
            Cutout(input);
            float3 normal = NormalizeNormalPerPixel(input.normalWS);
            #if defined(_GBUFFER_NORMALS_OCT)
                return half4(PackFloat2To888(saturate(PackNormalOctQuadEncode(normal) * .5 + .5)),0);
            #else
                return half4(normal,0);
            #endif
        }
        float3 _LightDirection;
        float3 _LightPosition;
        Varyings ShadowVert(Attributes input)
        {
            Varyings output = Vert(input);
            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 direction = normalize(_LightPosition - output.positionWS);
            #else
                float3 direction = _LightDirection;
            #endif
            output.positionCS = ApplyShadowClamping(TransformWorldToHClip(
                ApplyShadowBias(output.positionWS, output.normalWS, direction)));
            return output;
        }
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Forward
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ColorMask 0
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ShadowVert
            #pragma fragment Depth
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask 0
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Depth
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Normals
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            ENDHLSL
        }
    }
}
