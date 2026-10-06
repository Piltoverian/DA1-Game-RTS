using UnityEngine;
using System.Collections.Generic;
public static class MapGenRNG
{
    static uint SplitMix32(uint seed)
    {
        uint z = (seed += 0x9e3779b9);
        z= (z ^ (z >> 16)) * 0x85ebca6b;
        z= (z ^ (z >> 13)) * 0xC2B2AE35;
        uint result = z ^ (z >> 16);
        return result==0?0x6D2B79F5:result;
    }

    static uint XORShift32(uint currentstate)
    {
        uint x = currentstate;
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        return x;
    }

    public static float TurnToFloat(uint x)
    {
        return (x>> 8) * (1.0f / 16777216.0f);
    }

    public static uint[] GetPermatureList(uint seed)
    {
        uint[] result = new uint[512];
        for (uint i = 0; i < 256; i++)
        {
            result[i] = i;
        }
        uint currentstate = SplitMix32(seed);

        for (int i = 255; i >0 ; i--)
        {
            uint j = currentstate % (uint)(i+1);
            uint temp = result[i];
            result[i] = result[(int)j];
            result[(int)j] = temp;
            currentstate = XORShift32(currentstate);
        }

        for (int i=256; i < 512; i++)
        {
            result[i] = result[i - 256];
        }
        return result;
    }

}
