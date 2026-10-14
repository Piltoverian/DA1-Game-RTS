using System;
using System.Collections.Generic;
using System.Diagnostics;

// Opt-in diagnostic only. Enabled by the isolated Play-mode benchmark, not gameplay.
public static class RuntimePerformanceProbe
{
    public static bool Enabled;
    public static readonly Dictionary<string,double> Milliseconds=new Dictionary<string,double>();
    public readonly struct Scope : IDisposable
    {
        readonly string name;readonly long start;
        public Scope(string label){name=Enabled?label:null;start=name==null?0:Stopwatch.GetTimestamp();}
        public void Dispose(){if(name==null)return;Milliseconds.TryGetValue(name,out double value);Milliseconds[name]=value+(Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency;}
    }
    public static Scope Measure(string name)=>new Scope(name);
}
