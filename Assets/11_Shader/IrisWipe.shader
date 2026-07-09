Shader "Custom/IrisWipe"
{
    Properties
    {
        _Color ("Color", Color) = (0,0,0,1)
        _Radius ("Radius", Range(0, 2)) = 1.5
        _AspectRatio ("Aspect Ratio", Float) = 1.78
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            fixed4 _Color;
            float _Radius;
            float _AspectRatio;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 画面中心からの距離（アスペクト比補正あり）
                float2 diff = i.uv - float2(0.5, 0.5);
                diff.x *= _AspectRatio;
                float dist = length(diff);

                // Radiusより内側＝透明（ゲーム画面が見える）
                // Radiusより外側＝黒（不透明）
                float edge = 0.02;
                float alpha = smoothstep(_Radius - edge, _Radius + edge, dist);

                return fixed4(_Color.rgb, alpha);
            }
            ENDCG
        }
    }
}