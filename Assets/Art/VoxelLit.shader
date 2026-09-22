Shader "Moycraft/Voxel Lit"
{
    Properties
    {
        [MainTexture] _BaseMap("Block atlas", 2D) = "white" {}
        [MainColor] _BaseColor("Tint", Color) = (1,1,1,1)
        [HideInInspector] _Cull("Cull", Float) = 2
        [HideInInspector] _Cutoff("Alpha cutoff", Range(0,1)) = 0.5
        [HideInInspector] _Surface("Surface", Float) = 0
        [HideInInspector] _Smoothness("Smoothness", Float) = 0
        [HideInInspector] _Metallic("Metallic", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "VoxelForward"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Back ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex VoxelVertex
            #pragma fragment VoxelFragment
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            // Defaults to daytime (0) in scene previews without a running cycle.
            float _MoycraftNightBlend;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half sky : TEXCOORD3;
                half fog : TEXCOORD4;
                half3 vertexLight : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings VoxelVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input,output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv,_BaseMap);
                output.sky = input.color.r;
                output.fog = ComputeFogFactor(position.positionCS.z);
                output.vertexLight = VertexLighting(position.positionWS,output.normalWS);
                return output;
            }
            half3 DiffuseLight(Light light, half3 normal)
            {
                return LightingLambert(light.color * light.distanceAttenuation * light.shadowAttenuation,
                    light.direction,normal);
            }
            half4 VoxelFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 normal = NormalizeNormalPerPixel(input.normalWS);
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).rgb * _BaseColor.rgb;
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                // Sun controls daylight color/intensity, not a moving projected shadow.
                // The voxel sky field keeps shade below canopies and overhangs.
                Light sun = GetMainLight();
                half3 ambient = lerp(SampleSH(normal),half3(0.003,0.005,0.012),_MoycraftNightBlend);
                // Keep exposed sides readable: top 1.0, sides 0.85, bottom 0.70.
                half faceShade = 0.85h + 0.15h * normal.y;
                half3 illumination = (ambient + sun.color * sun.distanceAttenuation * faceShade) * input.sky;
                #if defined(_ADDITIONAL_LIGHTS)
                    #if USE_CLUSTER_LIGHT_LOOP
                        UNITY_LOOP for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT,MAX_VISIBLE_LIGHTS); ++lightIndex)
                        {
                            CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                            illumination += DiffuseLight(GetAdditionalLight(lightIndex,input.positionWS,half4(1,1,1,1)),normal);
                        }
                    #endif
                    uint pixelLightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        illumination += DiffuseLight(GetAdditionalLight(lightIndex,input.positionWS,half4(1,1,1,1)),normal);
                    LIGHT_LOOP_END
                #endif
                #if defined(_ADDITIONAL_LIGHTS_VERTEX)
                    illumination += input.vertexLight;
                #endif
                half3 color = albedo * illumination;
                // Outdoor fog must not become an emissive fill inside a sealed room.
                color = MixFogColor(color,unity_FogColor.rgb * input.sky,input.fog);
                return half4(color,1);
            }
            ENDHLSL
        }
        // Shared LitInput layout keeps the material buffer compatible with these URP passes.
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
