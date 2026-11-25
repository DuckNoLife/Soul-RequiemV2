// Soul Requiem — 09/11 (§202): sprite lit 2D (URP) của Ren với KATANA + BAO KIẾM ĐỔI THIẾT KẾ (UV-map / lookup).
// Mỗi khung thân Ren có ảnh phụ _KatanaUV (Sprite Secondary Texture, bake bằng Catalog/v2/tools/ren2/katana_uv.py):
//   A = có kiếm, B = phần*50 (1 lưỡi, 2 tsuba, 3 chuôi, 4 bao), R = vị trí dọc phần, G = (hàng ngang << 5) | (bóng << 3).
// Shader tra _KatanaDesign (bảng 64x32 của thanh katana đang cầm, katana_design.py): hàng = phần*8 + hàng ngang, cột = R.
// Màu thiết kế nhân bóng gốc (0.78 / 1 / 1.2) rồi tính đèn 2D; alpha thiết kế < 1 = phát sáng (cộng sau đèn, nhân _GlowBoost).
Shader "SoulRequiem/Sprite-Lit-Katana"
{
    Properties
    {
        _MainTex("Diffuse", 2D) = "white" {}
        _MaskTex("Mask", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}
        [MaterialToggle] _ZWrite("ZWrite", Float) = 0

        _KatanaUV("Katana UV (secondary texture)", 2D) = "black" {}
        _KatanaDesign("Katana Design 64x32", 2D) = "white" {}
        _GlowBoost("Glow Boost", Float) = 1
        [HDR] _GlowTint("Glow Tint", Color) = (1, 1, 1, 1)

        [HideInInspector] _Color("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha("Enable External Alpha", Float) = 0
    }

    HLSLINCLUDE
        #define KATANA_PROPS \
            half4 _Color; \
            half _GlowBoost; \
            half4 _GlowTint;

        // khai báo SAU các include của URP (TEXTURE2D cần Core.hlsl)
        #define KATANA_TEXTURES \
            TEXTURE2D(_KatanaUV); \
            TEXTURE2D(_KatanaDesign); \
            SAMPLER(sampler_point_clamp);

        // Thay màu điểm ảnh katana theo bảng thiết kế. rgb vào/ra (đã nhân bóng), glow ra = độ phát sáng 0..1.
        #define KATANA_LOOKUP(KUV_, KRGB_, KGLOW_) \
        { \
            KGLOW_ = 0; \
            half4 k_ = SAMPLE_TEXTURE2D(_KatanaUV, sampler_point_clamp, KUV_); \
            if (k_.a > 0.5) \
            { \
                float r8 = round(k_.r * 255.0), g8 = round(k_.g * 255.0), b8 = round(k_.b * 255.0); \
                float part = round(b8 / 50.0); \
                float vlev = floor(g8 / 32.0); \
                float shade = fmod(floor(g8 / 8.0), 4.0); \
                float row = (part - 1.0) * 8.0 + vlev; \
                float2 duv = float2((floor(r8 * 64.0 / 256.0) + 0.5) / 64.0, 1.0 - (row + 0.5) / 32.0); \
                half4 d_ = SAMPLE_TEXTURE2D(_KatanaDesign, sampler_point_clamp, duv); \
                half mul = shade < 0.5 ? 0.78 : (shade < 1.5 ? 1.0 : 1.2); \
                KRGB_ = saturate(d_.rgb * mul); \
                KGLOW_ = 1.0 - d_.a; \
            } \
        }
    ENDHLSL

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite [_ZWrite]

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex LitVertex
            #pragma fragment LitFragment

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"

            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY
            #pragma multi_compile _ SKINNED_SPRITE

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color        : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_LIT_OUTPUTS
                half4 color        : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Lit2DCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                KATANA_PROPS
            CBUFFER_END
            KATANA_TEXTURES

            Varyings LitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonLitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 LitFragment(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half glow;
                KATANA_LOOKUP(input.uv, tex.rgb, glow)
                const half4 main = input.color * tex;
                const half4 mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, input.uv);
                const half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv));

                SurfaceData2D surfaceData;
                InputData2D inputData;
                InitializeSurfaceData(main.rgb, main.a, mask, normalTS, surfaceData);
                InitializeInputData(input.uv, input.lightingUV, inputData);
            #if defined(DEBUG_DISPLAY)
                SETUP_DEBUG_TEXTURE_DATA_2D_NO_TS(inputData, input.positionWS, input.positionCS, _MainTex);
                surfaceData.normalWS = input.normalWS;
            #endif
                half4 lit = CombinedShapeLightShared(surfaceData, inputData);
                lit.rgb += tex.rgb * _GlowTint.rgb * glow * _GlowBoost * main.a;
                return lit;
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "NormalsRendering"}

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex NormalsRenderingVertex
            #pragma fragment NormalsRenderingFragment

            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE

            struct Attributes
            {
                COMMON_2D_NORMALS_INPUTS
                float4 color        : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_NORMALS_OUTPUTS
                half4   color           : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Normals2DCommon.hlsl"

            CBUFFER_START( UnityPerMaterial )
                KATANA_PROPS
            CBUFFER_END

            Varyings NormalsRenderingVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonNormalsVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 NormalsRenderingFragment(Varyings input) : SV_Target
            {
                return CommonNormalsFragment(input, input.color);
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" "Queue"="Transparent" "RenderType"="Transparent"}

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            CBUFFER_START(UnityPerMaterial)
                KATANA_PROPS
            CBUFFER_END
            KATANA_TEXTURES

            Varyings UnlitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonUnlitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 UnlitFragment(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half glow;
                KATANA_LOOKUP(input.uv, tex.rgb, glow)
                half4 c = input.color * tex;
                c.rgb += tex.rgb * _GlowTint.rgb * glow * _GlowBoost * c.a;
                return c;
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/2D/Sprite-Lit-Default"
}
