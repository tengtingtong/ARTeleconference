Shader "Custom/LightEs_ChromaKey" {
	Properties{
		_MainTex("Base (RGB)", 2D) = "white" {}
		_MaskCol("Mask Color", Color) = (0, 0.6823, 0.2862, 1.0)
		_MainColor("Main Color", Color) = (0, 0, 0, 0)
		_Sensitivity("Threshold Sensitivity", Range(0,1)) = 0.23
		_Smooth("Smoothing", Range(0,1)) = 0.02
	}
		SubShader{
		Tags{ "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" }
		LOD 100
		ZTest Always Cull Back ZWrite On Lighting Off Fog{ Mode off }
		CGPROGRAM
#pragma surface surf Lambert alpha noforwardadd finalcolor:lightEstimation
//#pragma surface surf Lambert noforwardadd finalcolor:lightEstimation
		struct Input {
		float2 uv_MainTex;
	};

	sampler2D _MainTex;
	float4 _MaskCol;
	float4 _MainColor;
	float _Sensitivity;
	float _Smooth;
	fixed _GlobalLightEstimation;
	fixed _BrightnessThreshold;


	void lightEstimation(Input IN, SurfaceOutput o, inout fixed4 color)
	{
		color *= _GlobalLightEstimation;
	}

	void surf(Input IN, inout SurfaceOutput o) {
		half4 c = tex2D(_MainTex, IN.uv_MainTex);

		float maskY = 0.2989 * _MaskCol.r + 0.5866 * _MaskCol.g + 0.1145 * _MaskCol.b;
		float maskCr = 0.7132 * (_MaskCol.r - maskY);
		float maskCb = 0.5647 * (_MaskCol.b - maskY);

		float Y = 0.2989 * c.r + 0.5866 * c.g + 0.1145 * c.b;
		float Cr = 0.7132 * (c.r - Y);
		float Cb = 0.5647 * (c.b - Y);

		float blendValue = smoothstep(_Sensitivity, _Sensitivity + _Smooth, distance(float2(Cr, Cb), float2(maskCr, maskCb)));
		o.Albedo = _MainColor;
		o.Alpha = 1.0 * blendValue * _BrightnessThreshold;
		o.Emission = c.rgb * blendValue * 0.5f;
	}
	ENDCG
	}
		FallBack "Diffuse"
}