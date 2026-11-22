struct VSIn { float2 position : POSITION; float coverage : COVERAGE; };
struct VSOut { float4 position : SV_Position; float coverage : COVERAGE; };
cbuffer DrawConstants : register(b0) {
    float4 startColor;
    float4 endColor;
    float gradientTop, gradientHeight;
    float shapeLeft, shapeTop, shapeWidth, shapeHeight;
    float cornerRadius, strokeWidth, strokeMode, cornerMask, horizontal, maskRadius;
    float4 maskBox;
    float4 arc;
    float renderScale, geometryKind;
    float2 lineStart;
    float2 lineEnd;
    float2 lineJoin;
};

// D3D12_GeometryKind in D3D12_RectanglePipeline.vb.
static const int RoundedRectangle = 1;
static const int Ellipse = 2;
static const int FlatLine = 3;
static const int RoundLine = 4;
static const int JoinedRoundLines = 5;

float IntervalCoverage(float position, float lower, float upper, float pixelWidth) {
    return saturate(0.5 + (position - lower) / pixelWidth) -
           saturate(0.5 + (position - upper) / pixelWidth);
}

float HalfPlaneCoverage(float distance, float2 normal, float pixelWidth) {
    // The projection of a square pixel onto an edge normal has a trapezoidal density.
    float2 span = abs(normal) * pixelWidth;
    float major = max(span.x, span.y);
    float minor = min(span.x, span.y);
    float offset = abs(distance);
    float tail = max(0.0, (major + minor) * 0.5 - offset);
    float area = offset <= (major - minor) * 0.5
        ? 0.5 - offset / max(major, 0.000001)
        : tail * tail / max(2.0 * major * minor, 0.000001);
    return distance < 0.0 ? 1.0 - area : area;
}

float ProjectedIntervalCoverage(float position, float lower, float upper, float2 normal, float pixelWidth) {
    return HalfPlaneCoverage(lower - position, normal, pixelWidth) -
           HalfPlaneCoverage(upper - position, normal, pixelWidth);
}

float BoxCoverage(float2 position, float2 halfSize, float pixelWidth) {
    return IntervalCoverage(position.x, -halfSize.x, halfSize.x, pixelWidth) *
           IntervalCoverage(position.y, -halfSize.y, halfSize.y, pixelWidth);
}

float RoundedRectangleCoverage(float2 position, float2 halfSize, float radius, float pixelWidth) {
    float box = BoxCoverage(position, halfSize, pixelWidth);
    if (radius <= 0.0) return box;
    float2 corner = max(abs(position) - (halfSize - radius), 0.0);
    float lengthToCorner = length(corner);
    if (lengthToCorner <= 0.0) return box;
    float coverage = HalfPlaneCoverage(lengthToCorner - radius, corner / lengthToCorner, pixelWidth);
    return min(box, coverage);
}

float EllipseCoverage(float2 position, float2 radii, float pixelWidth) {
    float2 unitPosition = position / max(radii, 0.0001);
    float norm = length(unitPosition);
    float2 gradient = unitPosition / max(radii, 0.0001);
    float gradientLength = length(gradient);
    float2 normal = gradientLength > 0.0 ? gradient / max(gradientLength, 0.0001) : float2(1.0, 0.0);
    // Convert the implicit ellipse to a signed distance around its edge.
    float distance = (norm - 1.0) * max(norm, 0.0001) / max(gradientLength, 0.0001);
    return min(BoxCoverage(position, radii, pixelWidth), HalfPlaneCoverage(distance, normal, pixelWidth));
}

float ContourCoverage(float2 position, int kind, float pixelWidth) {
    float2 halfSize = float2(shapeWidth, shapeHeight) * 0.5;
    float2 local = position - float2(shapeLeft, shapeTop) - halfSize;
    float halfStroke = strokeWidth * 0.5;
    float2 outer = halfSize + halfStroke;
    float2 inner = halfSize - halfStroke;
    bool hasInner = strokeMode > 0.5 && all(inner > 0);
    float outerCoverage = 0.0;
    float innerCoverage = 0.0;
    if (kind == Ellipse) {
        outerCoverage = EllipseCoverage(local, outer, pixelWidth);
        if (hasInner) innerCoverage = EllipseCoverage(local, inner, pixelWidth);
    } else {
        // A zero radius keeps square miter corners. Otherwise the stroke offsets the radius, too.
        float outerRadius = cornerRadius > 0 ? cornerRadius + halfStroke : 0;
        outerCoverage = RoundedRectangleCoverage(local, outer, outerRadius, pixelWidth);
        if (hasInner) innerCoverage = RoundedRectangleCoverage(local, inner, max(0, cornerRadius - halfStroke), pixelWidth);
    }
    return saturate(outerCoverage - innerCoverage);
}

float LineCoverage(float2 position, float2 start, float2 finish, bool roundCaps, float pixelWidth) {
    float2 segment = finish - start;
    float segmentLength = length(segment);
    float2 axis = segmentLength > 0 ? segment / max(segmentLength, 0.000001) : float2(1.0, 0.0);
    float2 normal = float2(-axis.y, axis.x);
    float2 relative = position - start;
    float along = dot(relative, axis);
    float across = dot(relative, normal);
    float halfStroke = strokeWidth * 0.5;
    float side = ProjectedIntervalCoverage(across, -halfStroke, halfStroke, normal, pixelWidth);
    if (!roundCaps) {
        return side * ProjectedIntervalCoverage(along, 0.0, segmentLength, axis, pixelWidth);
    }

    float2 radial = relative - axis * clamp(along, 0.0, segmentLength);
    float distance = length(radial);
    float2 capNormal = distance > 0.0 ? radial / max(distance, 0.000001) : normal;
    float cap = HalfPlaneCoverage(distance - halfStroke, capNormal, pixelWidth);
    float endCoverage = ProjectedIntervalCoverage(along, -halfStroke, segmentLength + halfStroke, axis, pixelWidth);
    return min(side, min(cap, endCoverage));
}
VSOut VSMain(VSIn input) { VSOut output; output.position = float4(input.position, 0.0, 1.0); output.coverage = input.coverage; return output; }
float4 PSMain(VSOut input) : SV_Target {
    input.position.xy /= renderScale;
    int kind = (int)geometryKind;
    if (kind != 0) {
        float pixelWidth = 1.0 / renderScale;
        float alpha = 0.0;
        if (kind == FlatLine || kind == RoundLine) {
            alpha = startColor.a * LineCoverage(input.position.xy, lineStart, lineEnd, kind == RoundLine, pixelWidth);
        } else if (kind == JoinedRoundLines) {
            // Match the two consecutive Direct2D strokes with a single source-over result.
            float first = startColor.a * LineCoverage(input.position.xy, lineStart, lineJoin, true, pixelWidth);
            float last = startColor.a * LineCoverage(input.position.xy, lineJoin, lineEnd, true, pixelWidth);
            alpha = last + first * (1.0 - last);
        } else {
            alpha = startColor.a * ContourCoverage(input.position.xy, kind, pixelWidth);
        }
        return float4(startColor.rgb * alpha, alpha);
    }
    if (arc.y > 0) {
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
