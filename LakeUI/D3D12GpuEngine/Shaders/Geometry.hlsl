struct VSIn { float2 position : POSITION; float coverage : COVERAGE; };
struct VSOut { float4 position : SV_Position; float coverage : COVERAGE; };
cbuffer DrawConstants : register(b0) { float4 startColor; float4 endColor; float gradientTop; float gradientHeight; float shapeLeft; float shapeTop; float shapeWidth; float shapeHeight; float cornerRadius; float strokeWidth; float strokeMode; float cornerMask; float horizontal; float maskRadius; float4 maskBox; float4 arc; float renderScale; };
VSOut VSMain(VSIn input) { VSOut output; output.position = float4(input.position, 0.0, 1.0); output.coverage = input.coverage; return output; }
float4 PSMain(VSOut input) : SV_Target { input.position.xy /= renderScale; if (arc.y > 0) {
        float2 p = input.position.xy - float2(shapeLeft + shapeWidth * 0.5, shapeTop + shapeHeight * 0.5);
        float phase = frac((atan2(p.y, p.x) - arc.x) / 6.283185307) * 6.283185307;
        float t = saturate(phase / arc.y);
        float distanceToLine = abs(length(p) - arc.z);
        if (phase > arc.y && arc.y < 6.28318) {
            float2 start = arc.z * float2(cos(arc.x), sin(arc.x));
            float2 end = arc.z * float2(cos(arc.x + arc.y), sin(arc.x + arc.y));
            float d0 = length(p - start), d1 = length(p - end);
            distanceToLine = min(d0, d1);
            t = d0 <= d1 ? 0 : 1;
        }
        float4 c = lerp(startColor, endColor, t * arc.w);
        float a = saturate(0.5 + strokeWidth * 0.5 - distanceToLine) * c.a;
        return float4(c.rgb * a, a);
    }
    float t = saturate(((horizontal > 0 ? input.position.x : input.position.y) - gradientTop) / max(gradientHeight, 1.0)); float4 c = lerp(startColor, endColor, t); float2 p = input.position.xy; float coverage = input.coverage; float insideDistance = min(min(p.x - shapeLeft, shapeLeft + shapeWidth - p.x), min(p.y - shapeTop, shapeTop + shapeHeight - p.y)); if (cornerRadius < 0.0) { float2 r = max(float2(shapeWidth, shapeHeight)*0.5, 0.001); float2 n = (p - float2(shapeLeft, shapeTop) - r)/r; float k = length(n); float d = (k-1.0) / max(length(n/r)/max(k, 0.0001), 0.0001); coverage *= saturate(0.5-d); insideDistance = -d; } else if (cornerRadius > 0.0 && ((int)cornerMask & (1 << ((p.x >= shapeLeft + shapeWidth * 0.5 ? 1 : 0) + (p.y >= shapeTop + shapeHeight * 0.5 ? 2 : 0)))) != 0) { float2 halfSize = float2(shapeWidth, shapeHeight) * 0.5; float2 center = float2(shapeLeft, shapeTop) + halfSize; float2 q = abs(p - center) - (halfSize - cornerRadius); float distanceToEdge = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - cornerRadius; float antialiasWidth = max(fwidth(distanceToEdge), 0.5); coverage *= 1.0 - smoothstep(-antialiasWidth, antialiasWidth, distanceToEdge); insideDistance = max(-distanceToEdge, 0.0); } if (strokeMode > 0.5) { float edgeWidth = max(fwidth(insideDistance), 0.5); coverage *= 1.0 - smoothstep(strokeWidth - edgeWidth, strokeWidth + edgeWidth, insideDistance); } if (maskRadius > 0) {
        float2 halfSize = (maskBox.zw - maskBox.xy) * 0.5;
        float2 q = abs(p - maskBox.xy - halfSize) - (halfSize - maskRadius);
        float d = length(max(q, 0)) + min(max(q.x, q.y), 0) - maskRadius;
        float aa = max(fwidth(d), 0.5);
        coverage *= 1 - smoothstep(-aa, aa, d);
    }
    return float4(c.rgb * c.a * coverage, c.a * coverage); }
