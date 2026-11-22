cbuffer Quad : register(b0) { float4 bounds; float2 surfaceSize; float opacity; float radius; float4 maskBox; float cornerBits; float sigma; float2 sampleStep; float4 uvRect; float renderScale; float resolveScale; float encodeSdr; float pointSample; float decodeSrgb; float glyphMask; float cubicSampling; float padding; float4 glyphInk; }
Texture2D<float4> sourceImage : register(t0);
SamplerState imageSampler : register(s0);
SamplerState repeatSampler : register(s1);
struct Vertex { float4 position : SV_Position; float2 uv : TEXCOORD0; };
Vertex VS(uint i : SV_VertexID) {
    Vertex v;
    v.uv = float2(i & 1, i >> 1);
    float2 p = lerp(bounds.xy, bounds.zw, v.uv) / surfaceSize;
    v.position = float4(p * float2(2, -2) + float2(-1, 1), 0, 1);
    v.uv = uvRect.xy + v.uv * uvRect.zw;
    return v;
}
float4 TransferColor(float4 sampleColor) {
    if (sampleColor.a <= 0) return sampleColor;
    if (decodeSrgb > 0) {
        float3 rgb = saturate(sampleColor.rgb / sampleColor.a);
        sampleColor.rgb = lerp(rgb / 12.92, pow((rgb + 0.055) / 1.055, 2.4), step(0.04045, rgb)) * sampleColor.a;
    } else if (encodeSdr > 0) {
        float3 rgb = saturate(sampleColor.rgb / sampleColor.a);
        sampleColor.rgb = lerp(12.92 * rgb, 1.055 * pow(rgb, 1/2.4) - 0.055, step(0.0031308, rgb)) * sampleColor.a;
    }
    return sampleColor;
}
float CubicWeight(float distance) {
    float x = abs(distance);
    // Catmull-Rom with support over two texels on either side.
    if (x < 1.0) return (1.5 * x - 2.5) * x * x + 1.0;
    if (x < 2.0) return ((-0.5 * x + 2.5) * x - 4.0) * x + 2.0;
    return 0.0;
}

float4 SampleCubic(float2 uv) {
    uint width, height;
    sourceImage.GetDimensions(width, height);
    float2 dimensions = float2(width, height);
    float2 texelsPerPixel = dimensions * abs(uvRect.zw) / ((bounds.zw - bounds.xy) * renderScale);

    // Widen the kernel when shrinking. For large reductions, space linear samples out
    // over the full footprint to bound the number of taps per axis.
    float2 filterScale = max(1.0, texelsPerPixel);
    float2 support = 2.0 * filterScale;
    float2 spacing = max(1.0, filterScale * 0.5);
    float2 texel = uv * dimensions - 0.5;
    float2 first = ceil((texel - support) / spacing) * spacing;
    int2 count = clamp((int2)ceil((texel + support - first) / spacing), 1, 8);
    float4 sum = 0;
    float accumulatedWeight = 0;
    [loop]
    for (int y = 0; y < count.y; ++y) {
        float row = first.y + y * spacing.y;
        float weightY = CubicWeight((texel.y - row) / filterScale.y);
        [loop]
        for (int x = 0; x < count.x; ++x) {
            float column = first.x + x * spacing.x;
            float weight = weightY * CubicWeight((texel.x - column) / filterScale.x);
            sum += weight * sourceImage.SampleLevel(imageSampler, (float2(column, row) + 0.5) / dimensions, 0);
            accumulatedWeight += weight;
        }
    }
    float4 result = sum / max(accumulatedWeight, 0.0001);
    result.a = saturate(result.a);
    // Cubic lobes can overshoot. Keep the filtered SDR texel valid premultiplied color.
    result.rgb = clamp(result.rgb, 0, result.a);
    return result;
}

float4 PS(Vertex v) : SV_Target {
    v.position.xy /= renderScale;
    if (resolveScale > 0) {
        int n = (int)resolveScale;
        int2 p = int2(v.position.xy) * n;
        float4 sum = 0;
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) sum += sourceImage.Load(int3(p + int2(x,y), 0));
        return TransferColor(sum / (n*n));
    }
    if (glyphMask > 0) {
        float a = sourceImage.Sample(imageSampler, v.uv).r * glyphInk.a;
        return float4(glyphInk.rgb * a, a);
    }
    if (pointSample > 0) {
        uint w, h;
        sourceImage.GetDimensions(w, h);
        return TransferColor(sourceImage.Load(int3(clamp(int2(v.uv * float2(w, h)), 0, int2(w-1,h-1)), 0))) * opacity;
    }
    if (sigma < 0) return TransferColor(sourceImage.Sample(repeatSampler, (v.position.xy - bounds.xy) * sampleStep)) * opacity;
    float coverage = 1;
    if (radius > 0) {
        float2 halfSize = (maskBox.zw - maskBox.xy) * 0.5;
        float2 p = v.position.xy - (maskBox.xy + halfSize);
        int quadrant = (p.x >= 0 ? 1 : 0) + (p.y >= 0 ? 2 : 0);
        if (((int)cornerBits & (1 << quadrant)) != 0) {
            float2 q = abs(p) - (halfSize - radius);
            float d = length(max(q, 0)) + min(max(q.x, q.y), 0) - radius;
            float aa = max(fwidth(d), 0.5);
            coverage = 1 - smoothstep(-aa, aa, d);
        }
    }
    if (sigma > 0) {
        // Sample symmetrical pairs on a Gaussian kernel using clamped edge texels.
        float4 sum = sourceImage.SampleLevel(imageSampler, v.uv, 0);
        float total = 1;
        int count = (int)min(ceil(3 * sigma), 96);
        float spacing = max(1, ceil(3 * sigma) / count);
        for (int j = 1; j <= count; ++j) {
            float d = j * spacing;
            float weight = exp(-0.5 * d * d / (sigma * sigma));
            sum += weight * (sourceImage.SampleLevel(imageSampler, v.uv + sampleStep * d, 0) + sourceImage.SampleLevel(imageSampler, v.uv - sampleStep * d, 0));
            total += weight * 2;
        }
        return sum / total;
    }
    [branch]
    if (cubicSampling > 0) return TransferColor(SampleCubic(v.uv)) * (opacity * coverage);
    return TransferColor(sourceImage.Sample(imageSampler, v.uv)) * (opacity * coverage);
}

struct SubpixelOutput {
    float4 color : SV_Target0;
    float4 coverage : SV_Target1;
};
SubpixelOutput PSSubpixel(Vertex v) {
    float3 coverage = sourceImage.Sample(imageSampler, v.uv).rgb * glyphInk.a;
    SubpixelOutput output;
    output.color = float4(glyphInk.rgb * coverage, 0);
    output.coverage = float4(coverage, 0);
    return output;
}
