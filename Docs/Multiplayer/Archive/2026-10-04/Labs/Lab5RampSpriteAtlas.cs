using System;
using System.Drawing;
using System.Drawing.Drawing2D;
public static class Lab5RampSpriteAtlas {
 public static Bitmap Straight(Bitmap source,int direction,string widthRole){
  int n=source.Width;var tile=new Bitmap(n,n);
  // Repeat only native pixels from the road core. Baked rock shoulders never enter wide ramp cores.
  for(int y=0;y<n;y++)for(int x=0;x<n;x++){
   int sx=100+(x%218);Color c=source.GetPixel(sx,y);
   double alpha=Math.Min(1,Math.Min((y+1)/20.0,(n-y)/20.0));
   if(widthRole=="single"||widthRole=="left")alpha*=Math.Min(1,(x+1)/32.0);
   if(widthRole=="single"||widthRole=="right")alpha*=Math.Min(1,(n-x)/32.0);
   tile.SetPixel(x,y,Color.FromArgb((int)(c.A*alpha),c.R,c.G,c.B));
  }
  tile.RotateFlip(new[]{RotateFlipType.RotateNoneFlipNone,RotateFlipType.Rotate90FlipNone,RotateFlipType.Rotate180FlipNone,RotateFlipType.Rotate270FlipNone}[direction]);return tile;
 }
 public static Bitmap Cliff(Bitmap stone,string shape,int mask){
  int n=stone.Width,half=n/2;var tile=new Bitmap(n,n);var g=Graphics.FromImage(tile);
  if(shape=="fill"){g.DrawImageUnscaled(stone,0,0);g.Dispose();return tile;}
  var path=new GraphicsPath();int rotation=0;
  if(shape=="outer_corner"||shape=="split_corners"){
   rotation=mask==2||mask==10?1:mask==4?2:mask==8?3:0;
   path.AddLines(new[]{new Point(0,half),new Point(half,half),new Point(half,0)});
   if(shape=="split_corners"){path.StartFigure();path.AddLines(new[]{new Point(n,half),new Point(half,half),new Point(half,n)});}
  }else if(shape=="inner_corner"){
   rotation=mask==14?1:mask==13?2:mask==11?3:0;
   path.AddLines(new[]{new Point(0,half),new Point(half,half),new Point(half,n)});
  }else{
   rotation=mask==6?1:mask==12?2:mask==9?3:0;
   path.AddLine(0,half,shape=="endcap"?n-40:n,half);
  }
  var brush=new TextureBrush(stone);var pen=new Pen(brush,170);pen.LineJoin=LineJoin.Miter;
  g.DrawPath(pen,path);pen.Dispose();brush.Dispose();path.Dispose();g.Dispose();
  tile.RotateFlip(new[]{RotateFlipType.RotateNoneFlipNone,RotateFlipType.Rotate90FlipNone,RotateFlipType.Rotate180FlipNone,RotateFlipType.Rotate270FlipNone}[rotation]);return tile;
 }
 public static Bitmap Corner(Bitmap source,int cornerMask){
  int n=source.Width;var tile=new Bitmap(n,n);var g=Graphics.FromImage(tile);
  int rotation=cornerMask==2||cornerMask==14?1:cornerMask==4||cornerMask==13?2:cornerMask==8||cornerMask==11?3:0;
  int first=0,second=3;
  // Two non-overlapping triangular native sprite regions form a dedicated corner tile.
  for(int part=0;part<2;part++){
   var s=Straight(source,part==0?first:second,"center");
   var path=new GraphicsPath();
   bool slash=((first==0&&second==3)||(first==1&&second==2));
   Point[] points=slash?(part==0?new[]{new Point(0,0),new Point(n,0),new Point(0,n)}:new[]{new Point(n,0),new Point(n,n),new Point(0,n)}):(part==0?new[]{new Point(0,0),new Point(n,0),new Point(n,n)}:new[]{new Point(0,0),new Point(n,n),new Point(0,n)});
   path.AddPolygon(points);g.SetClip(path);g.DrawImageUnscaled(s,0,0);path.Dispose();s.Dispose();
  }
  g.Dispose();tile.RotateFlip(new[]{RotateFlipType.RotateNoneFlipNone,RotateFlipType.Rotate90FlipNone,RotateFlipType.Rotate180FlipNone,RotateFlipType.Rotate270FlipNone}[rotation]);return tile;
 }
}
