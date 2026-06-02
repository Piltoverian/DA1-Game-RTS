using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public static class PhysicsLayersDefine
{
    public readonly static uint Everything = uint.MaxValue;
    public readonly static uint Ground = 1 << 3;
    public readonly static uint Units = 1 << 6;
    public readonly static uint Building = 1 << 8;
    public readonly static uint Nothing = 0;
}