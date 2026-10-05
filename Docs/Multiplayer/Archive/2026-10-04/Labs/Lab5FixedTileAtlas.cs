using System;
using System.Drawing;

// Fixed-size prototype atlas construction: native pixel samples, no resampling.
public static class Lab5FixedTileAtlas
{
    public static Bitmap Ramp(Bitmap soil, string shape, int direction, int sides, int ports)
    {
        if(soil.Width!=100||soil.Height!=100)throw new ArgumentException("Tile source must be 100x100");
        Bitmap tile=new Bitmap(100,100);
        int[] dx={0,1,0,-1},dy={-1,0,1,0};
        for(int y=0;y<100;y++)for(int x=0;x<100;x++){
            double px=(x+.5)/100,py=(y+.5)/100,a=1;
            if(shape=="turn"||shape=="tee"||shape=="cross"){
                double nearest=double.PositiveInfinity;
                for(int d=0;d<4;d++)if((ports&(1<<d))!=0){
                    double t=Math.Max(0,Math.Min(.5,(px-.5)*dx[d]+(py-.5)*dy[d]));
                    double ex=px-.5-t*dx[d],ey=py-.5-t*dy[d];
                    nearest=Math.Min(nearest,Math.Sqrt(ex*ex+ey*ey));
                }
                a=Smooth((.49-nearest)/.15);
            }else{
                double along=.5+(px-.5)*dx[direction]+(py-.5)*dy[direction];
                if(shape=="start"||shape=="single")a*=Smooth(along/.28);
                if(shape=="end"||shape=="single")a*=Smooth((1-along)/.28);
                if((sides&1)!=0)a*=Smooth(py/.2);
                if((sides&2)!=0)a*=Smooth((1-px)/.2);
                if((sides&4)!=0)a*=Smooth((1-py)/.2);
                if((sides&8)!=0)a*=Smooth(px/.2);
            }
            Color c=soil.GetPixel(x,y);tile.SetPixel(x,y,Color.FromArgb((int)Math.Round(a*255),c.R,c.G,c.B));
        }
        return tile;
    }
    private static double Smooth(double t){t=Math.Max(0,Math.Min(1,t));return t*t*(3-2*t);}
}
