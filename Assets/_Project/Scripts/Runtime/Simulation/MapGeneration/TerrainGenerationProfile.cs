using System.Collections.Generic;
using System.Diagnostics;

// Optional wall-clock telemetry; accumulates all candidates, including rejected ones.
public sealed class TerrainGenerationProfile
{
    public readonly Dictionary<string,double> milliseconds=new Dictionary<string,double>();
    public static long Start(GeneratedTerrain m)=>m.profile==null?0:Stopwatch.GetTimestamp();
    public static void End(GeneratedTerrain m,string phase,ref long stamp)
    {
        if(m.profile==null)return;long now=Stopwatch.GetTimestamp();
        double elapsed=(now-stamp)*1000.0/Stopwatch.Frequency;
        m.profile.milliseconds.TryGetValue(phase,out double previous);m.profile.milliseconds[phase]=previous+elapsed;
        stamp=Stopwatch.GetTimestamp();
    }
}
