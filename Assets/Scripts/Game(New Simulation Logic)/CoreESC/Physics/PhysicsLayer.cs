using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public static class PhysicsLayersDefine
{
    public readonly static uint Ground = 1 << 6;
    public readonly static uint Unit = 1 << 7;
    public readonly static uint Building = 1 << 8;
}