Shader "RTS/Terrain Material Atlas"
{
    Properties
    {
        _BaseMap("Canonical colors",2D)="white"{}
        _GeometryMap("Canonical tier height",2D)="black"{}
        _SourceAtlas("Six materials",2D)="white"{}
        _FadeOpacity("Mountain opacity",Range(0,1))=.25
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Pass
        {
            Name "TerrainSurface"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_GeometryMap); SAMPLER(sampler_GeometryMap);
            TEXTURE2D(_SourceAtlas); SAMPLER(sampler_SourceAtlas);
            CBUFFER_START(UnityPerMaterial)
            float4 _SourceAtlas_TexelSize;
            float4 _Grid; // origin X,Z,cell size,ground region cells
            float4 _ViewForward;
            float _VisualSeed, _FadeOpacity;
            float4 _FadeAnchors[16]; // world XYZ, radius world; positive radius enabled
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION;float2 uv:TEXCOORD0;float4 face:TEXCOORD1; };
            struct Varyings { float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float3 plane:TEXCOORD1;nointerpolation float4 face:TEXCOORD2; };
            Varyings Vert(Attributes input)
            {
                Varyings o;o.plane=TransformObjectToWorld(input.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.plane);
                o.uv=input.uv;o.face=input.face;return o;
            }
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7))+_VisualSeed*.013)*43758.5453); }
            float Noise(float2 p)
            {
                float2 a=floor(p),f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(Hash(a),Hash(a+float2(1,0)),f.x),lerp(Hash(a+float2(0,1)),Hash(a+1),f.x),f.y);
            }
            float3 Panel(int index,float2 uv)
            {
                float2 size=_SourceAtlas_TexelSize.zw/float2(3,2);
                float2 p=frac(uv)*size-.5,base=floor(p),f=frac(p);
                float2 slot=float2(index%3,1-index/3)*size;
                float2 a=base-floor(base/size)*size,b=(base+1)-floor((base+1)/size)*size;
                float3 c00=SAMPLE_TEXTURE2D(_SourceAtlas,sampler_SourceAtlas,(slot+a+.5)*_SourceAtlas_TexelSize.xy).rgb;
                float3 c10=SAMPLE_TEXTURE2D(_SourceAtlas,sampler_SourceAtlas,(slot+float2(b.x,a.y)+.5)*_SourceAtlas_TexelSize.xy).rgb;
                float3 c01=SAMPLE_TEXTURE2D(_SourceAtlas,sampler_SourceAtlas,(slot+float2(a.x,b.y)+.5)*_SourceAtlas_TexelSize.xy).rgb;
                float3 c11=SAMPLE_TEXTURE2D(_SourceAtlas,sampler_SourceAtlas,(slot+b+.5)*_SourceAtlas_TexelSize.xy).rgb;
                return lerp(lerp(c00,c10,f.x),lerp(c01,c11,f.x),f.y);
            }
            struct Output { float4 color:SV_Target;float depth:SV_Depth; };
            Output Frag(Varyings input)
            {
                float4 color=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv);clip(color.a-.1);
                float tier=SAMPLE_TEXTURE2D(_GeometryMap,sampler_GeometryMap,input.uv).r;
                float altitude=input.face.x+tier*input.face.y;
                // Inverse projection: planar sprite pixel -> virtual world surface -> real camera depth.
                float3 world=input.plane+_ViewForward.xyz*(altitude/_ViewForward.y);
                float2 grid=(world.xz-_Grid.xy)/_Grid.z;
                if(input.face.w<.5&&((input.face.z>.5&&input.face.z<1.5)||(input.face.z>4.5&&input.face.z<5.5)))
                {
                    float blend=smoothstep(.15,.85,Noise(grid/max(2,_Grid.w)));
                    color.rgb=lerp(Panel(0,grid/4),Panel(1,grid/4),blend);
                    color.rgb=lerp(color.rgb,Panel(5,grid/7.37),.06);
                }
                if(input.face.w<.5&&input.face.z>2.5&&input.face.z<3.5)
                {
                    float3 normal=normalize(cross(ddy(world),ddx(world)));normal*=normal.y<0?-1:1;
                    color.rgb=Panel(3,grid/3)*(.72+.28*saturate(dot(normal,normalize(float3(-.35,1,-.45)))));
                }
                if(input.face.z>2.5)
                {
                    float fade=0;
                    [unroll]for(int a=0;a<16;a++)
                    {
                        float4 focus=_FadeAnchors[a];if(focus.w<=0)continue;
                        float3 delta=world-focus.xyz;float along=dot(delta,_ViewForward.xyz);
                        float perpendicular=length(delta-_ViewForward.xyz*along);
                        // Only the mountain in front of the focus, with a soft screen-space corridor.
                        fade=max(fade,(along<-.03?1:0)*(1-smoothstep(focus.w*.65,focus.w,perpendicular)));
                    }
                    float opacity=lerp(1,_FadeOpacity,fade);
                    // Stable ordered dithering releases depth where mountain pixels disappear.
                    float2 pixel=floor(input.positionCS.xy);
                    float threshold=frac(dot(pixel,float2(.754877666,.569840296)));
                    clip(opacity-threshold);
                }
                float4 actual=TransformWorldToHClip(world);float depth=actual.z/actual.w;
                #if defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES) || defined(SHADER_API_GLES3)
                depth=depth*.5+.5;
                #endif
                Output result;result.color=color;result.depth=depth;return result;
            }
            ENDHLSL
        }
    }
}
