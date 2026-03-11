using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public static class PhysicsLayersDefine
{
    public readonly static uint Ground = 1 << 0;
    public readonly static uint Unit = 1 << 1;
    public readonly static uint Building = 1 << 2;
}