sampler2D _ShadowDepthTex;
matrix _LightViewClipMatrix;

float _PositiveExponent;
float _NegativeExponent;

float _EVSMBias = 0.02;
float _EVSMLightBleedingReduction = 0.01;


// Common
float4 ComputeScreenPosInLight(float4 pos)
{
    float4 screenPos = float4(pos.xy * 0.5 + 0.5, pos.z, pos.w);
    return screenPos; 
}

// EVSM
float2 EVSM_GetExponents(float positiveExponent, float negativeExponent)
{
    const float maxExponent = 5.54f; // 16 Bit
    float2 exponents = float2(positiveExponent, negativeExponent);
    return min(exponents, maxExponent);
}

float2 EVSM_ApplyDepthWarp(float depth, float2 exponents)
{
    depth = 2.0f * depth - 1.0f;
    float pos =  exp( exponents.x * depth);
    float neg = -exp(-exponents.y * depth);
    return float2(pos, neg);
}

float EVSM_LinearStep(float a, float b, float v)
{
    return saturate((v - a) / (b - a));
}

float EVSM_ReduceLightBleeding(float pMax, float amount)
{
    return EVSM_LinearStep(amount, 1.0f, pMax);
}

float2 EVSM_ChebyshevUpperBound(float2 moments, float mean, float minVariance, float lightBleedingReduction)
{
    float variance = moments.y - (moments.x * moments.x);
    variance = max(variance, minVariance);

    float d = mean - moments.x;
    float pMax = variance / (variance + (d * d));

    pMax = EVSM_ReduceLightBleeding(pMax, lightBleedingReduction);

    return (mean <= moments.x ? 1.0f : pMax);
}

float EVSM_FLITER(float3 worldPos)
{
    float4 posLS = mul(_LightViewClipMatrix, float4(worldPos, 1));
    float4 ndcPosLS = posLS / posLS.w;
    float4 screenPosLS = ComputeScreenPosInLight(ndcPosLS);
    float depth = posLS.z;
    
    float2 exponents = EVSM_GetExponents(_PositiveExponent, _NegativeExponent);
    float2 warpedDepth = EVSM_ApplyDepthWarp(depth, exponents);

    float4 occluders = tex2D(_ShadowDepthTex, screenPosLS.xy);

    float2 depthScale = _EVSMBias * 0.01f * exponents * warpedDepth;
    float2 minVariance = depthScale * depthScale;

    float posContrib = EVSM_ChebyshevUpperBound(occluders.xz, warpedDepth.x, minVariance.x, _EVSMLightBleedingReduction);
    float negContrib = EVSM_ChebyshevUpperBound(occluders.yw, warpedDepth.y, minVariance.y, _EVSMLightBleedingReduction);
	
    return min(posContrib, negContrib);
}