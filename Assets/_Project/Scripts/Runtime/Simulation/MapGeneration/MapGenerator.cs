using Unity.Mathematics;


public static class MapGenerator
{

    public static float fade(float t)
    {
        return t * t * t * (t * (t * 6 - 15) + 10);
    }

    public static float2[] vectorMap=
    {
        new float2(1, 0),
        new float2(0, 1),
        new float2(-1, 0),
        new float2(0, -1),
        new float2(1,1),
        new float2(-1,1),
        new float2(-1,-1),
        new float2(1,-1)
    };

    public static float2 getGrad(uint[] permaturelist, uint x, uint y)
    {
        return vectorMap[permaturelist[(x & 255) + permaturelist[y & 255]] & 7];
    }

    public static float Perlin2D(float2 pos, uint[] permaturelist)
    {
        uint x0 = (uint)math.floor(pos.x) & 255;
        uint x1 = (x0 + 1) & 255;
        uint y0 = (uint)math.floor(pos.y) & 255;
        uint y1 = (y0 + 1) & 255;
        float xf = pos.x - math.floor(pos.x);
        float yf = pos.y - math.floor(pos.y);
        float sx = fade(xf);
        float sy = fade(yf);
        float2 g00 = getGrad(permaturelist, (uint)x0, (uint)y0);
        float2 g10 = getGrad(permaturelist, (uint)x1, (uint)y0);
        float2 g01 = getGrad(permaturelist, (uint)x0, (uint)y1);
        float2 g11 = getGrad(permaturelist, (uint)x1, (uint)y1);
        float n00 = math.dot(g00, new float2(xf, yf));
        float n10 = math.dot(g10, new float2(xf - 1.0f, yf));
        float n01 = math.dot(g01, new float2(xf, yf - 1.0f));
        float n11 = math.dot(g11, new float2(xf - 1.0f, yf - 1.0f));
        float nx0 = math.lerp(n00, n10, sx);
        float nx1 = math.lerp(n01, n11, sx);
        return math.lerp(nx0, nx1, sy);
    }

    public static float fbm2D(float2 pos, int octaves, float lacunarity, float scale, float persistence, uint[] permaturelist)
    {
        float total = 0.0f;
        float frequency = scale;
        float amplitude = 1.0f;
        float maxValue = 0.0f;

        for (int i = 0; i < octaves; i++)
        {
            total += Perlin2D(pos * frequency, permaturelist) * amplitude;
            maxValue += amplitude;
            amplitude *= persistence;
            frequency *= lacunarity;
        }

        float normalizedValue = total / maxValue;

        return math.clamp((normalizedValue+1)*0.5f, 0.0f, 1.0f);
    }
}
