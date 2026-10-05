param([string]$Version='v16')
$ErrorActionPreference='Stop';Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies @([Drawing.Bitmap].Assembly.Location,[Drawing.Color].Assembly.Location) -TypeDefinition @'
using System;
using System.Drawing;
public static class SharedTerrainEdges {
 public static void CopyToAtlas(Bitmap tile,Bitmap atlas,int ox,int oy){for(int y=0;y<tile.Height;y++)for(int x=0;x<tile.Width;x++)atlas.SetPixel(ox+x,oy+y,tile.GetPixel(x,y));}
 public static Bitmap RampBody(Bitmap b,Bitmap soil,int left,int right){
  var o=(Bitmap)b.Clone();for(int y=0;y<b.Height;y++)for(int x=left;x<right;x++)o.SetPixel(x,y,soil.GetPixel(x,y));return o;
 }
 public static double Centre(Bitmap b,int edge) {
  int n=b.Width;double total=0;int count=0;
  for(int t=90;t<n-98;t++){
   Color c=edge==0?b.GetPixel(t,12):edge==1?b.GetPixel(n-13,t):b.GetPixel(12,t);
   if(c.R>c.G*.92&&c.B>c.G*.75){total+=t;count++;}
  }
  if(count<10)throw new Exception("Cannot locate stone endpoint");return total/count;
 }
 public static Bitmap Align(Bitmap b,Bitmap ground,int dx,int dy){
  if(Math.Abs(dx)>64||Math.Abs(dy)>64)throw new Exception("Endpoint drift too large");
  var o=new Bitmap(b.Width,b.Height);
  for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++){
   int sx=x-dx,sy=y-dy;o.SetPixel(x,y,sx>=0&&sy>=0&&sx<b.Width&&sy<b.Height?b.GetPixel(sx,sy):ground.GetPixel(x,y));
  }return o;
 }
 public static Color[] Profile(Bitmap b) {
  int n=b.Width;var a=new Color[n];
  for(int k=0;k<n;k++){int p=Math.Min(k,n-1-k);a[k]=b.GetPixel(p,n/2);}
  return a;
 }
 public static Color[] StonePort(Bitmap b,Color[] grass) {
  int n=b.Width;var a=(Color[])grass.Clone();
  for(int k=n/2-32;k<n/2+32;k++)a[k]=b.GetPixel(n/2,k);
  return a;
 }
 public static Bitmap Build(Bitmap b,Color[][] edges,int band) {
  int n=b.Width;var output=(Bitmap)b.Clone();
  for(int y=0;y<n;y++)for(int x=0;x<n;x++){
   int[] ds={y,n-1-x,n-1-y,x},ts={x,y,x,y};
   double sum=0,r=0,g=0,bb=0,min=band;Color exact=Color.Empty;
   for(int e=0;e<4;e++)if(edges[e]!=null&&ds[e]<band){
    Color c=edges[e][ts[e]];
    if(ds[e]==0){if(!exact.IsEmpty&&exact.ToArgb()!=c.ToArgb())throw new Exception("Corner edge conflict");exact=c;}
    Color boundary=e==0?b.GetPixel(x,0):e==1?b.GetPixel(n-1,y):e==2?b.GetPixel(x,n-1):b.GetPixel(0,y);
    double w=1.0/Math.Pow(ds[e]+1,2);sum+=w;r+=w*(c.R-boundary.R);g+=w*(c.G-boundary.G);bb+=w*(c.B-boundary.B);min=Math.Min(min,ds[e]);
   }
   if(!exact.IsEmpty){output.SetPixel(x,y,exact);continue;}
   if(sum==0)continue;
   double a=Math.Pow(1-min/band,2);var old=b.GetPixel(x,y);
   output.SetPixel(x,y,Color.FromArgb(255,Math.Clamp((int)Math.Round(old.R+r/sum*a),0,255),Math.Clamp((int)Math.Round(old.G+g/sum*a),0,255),Math.Clamp((int)Math.Round(old.B+bb/sum*a),0,255)));
  }
  return output;
 }
}
'@
$root=Join-Path $PSScriptRoot "../../../../Assets/Art/Terrain/Lab5TextureReview_$Version"
$raw=Join-Path $root 'raw';New-Item -ItemType Directory -Force -Path $raw | Out-Null
$grass=[Drawing.Bitmap]::new((Join-Path $root 'ground_grass.png'))
$soil=[Drawing.Bitmap]::new((Join-Path $root 'ramp_middle.png'))
foreach($name in @('ramp_single','ramp_left','ramp_right')){
 $p=Join-Path $root ($name+'.png');$b=[Drawing.Bitmap]::new($p)
 $left=if($name -in @('ramp_single','ramp_left')){42}else{0}
 $right=if($name -in @('ramp_single','ramp_right')){376}else{418}
 $o=[SharedTerrainEdges]::RampBody($b,$soil,$left,$right);$b.Dispose();$tmp=$p+'.body.png';$o.Save($tmp,[Drawing.Imaging.ImageFormat]::Png);$o.Dispose();Move-Item -LiteralPath $tmp -Destination $p -Force
}
$alignment=[System.Collections.Generic.List[object]]::new()
foreach($name in @('cliff_straight','cliff_outer_corner','cliff_inner_corner')){
 $p=Join-Path $root ($name+'.png');$b=[Drawing.Bitmap]::new($p)
 $west=[SharedTerrainEdges]::Centre($b,3)
 if($name -eq 'cliff_straight'){$dx=0;$dy=[int][Math]::Round(209-($west+[SharedTerrainEdges]::Centre($b,1))/2)}
 else{$dx=[int][Math]::Round(209-[SharedTerrainEdges]::Centre($b,0));$dy=[int][Math]::Round(209-$west)}
 $o=[SharedTerrainEdges]::Align($b,$grass,$dx,$dy);$b.Dispose();$tmp=$p+'.aligned.png';$o.Save($tmp,[Drawing.Imaging.ImageFormat]::Png);$o.Dispose();Move-Item -LiteralPath $tmp -Destination $p -Force
 $alignment.Add(@{source=$name;dx=$dx;dy=$dy;resampled=$false})
}
$alignment | ConvertTo-Json | Set-Content (Join-Path $root 'endpoint_alignment.json') -Encoding utf8
$straight=[Drawing.Bitmap]::new((Join-Path $root 'cliff_straight.png'))
$gp=[SharedTerrainEdges]::Profile($grass);$dp=[SharedTerrainEdges]::Profile($soil)
$pp=[SharedTerrainEdges]::StonePort($straight,$gp)
$pr=[Drawing.Color[]]$pp.Clone();[Array]::Reverse($pr)
foreach($b in @($grass,$soil,$straight)){$b.Dispose()}
$spec=@(
 @{name='ground_grass';edges=@($gp,$gp,$gp,$gp)},
 @{name='ground_soil';edges=@($gp,$gp,$gp,$gp)},
 @{name='cliff_straight';edges=@($gp,$pp,$gp,$pp)},
 @{name='cliff_outer_corner';edges=@($pp,$gp,$gp,$pp)},
 @{name='cliff_inner_corner';edges=@($pr,$gp,$gp,$pr)},
 @{name='ramp_middle';edges=@($dp,$dp,$dp,$dp)},
 @{name='ramp_left';edges=@($null,$dp,$null,$null)},
 @{name='ramp_right';edges=@($null,$null,$null,$dp)},
 @{name='ramp_single';edges=@($null,$null,$null,$null)}
)
foreach($s in $spec){
 $p=Join-Path $root ($s.name+'.png');Copy-Item -LiteralPath $p -Destination (Join-Path $raw ($s.name+'.png'))
 $b=[Drawing.Bitmap]::new($p);$o=[SharedTerrainEdges]::Build($b,$s.edges,8);$b.Dispose();$tmp=$p+'.authored.png';$o.Save($tmp,[Drawing.Imaging.ImageFormat]::Png);$o.Dispose();Move-Item -LiteralPath $tmp -Destination $p -Force
}
# Rebuild authored atlas; the review slicer always consumes this exact grid.
$names=@('cliff_straight','cliff_outer_corner','cliff_inner_corner','ramp_single','ramp_left','ramp_middle','ramp_right','ground_grass','ground_soil')
$atlas=[Drawing.Bitmap]::new(1254,1254)
for($k=0;$k -lt 9;$k++){$t=[Drawing.Bitmap]::new((Join-Path $root ($names[$k]+'.png')));[SharedTerrainEdges]::CopyToAtlas($t,$atlas,($k%3)*418,[int][Math]::Floor($k/3)*418);$t.Dispose()}
$atlas.Save((Join-Path $root 'atlas.png'));$atlas.Dispose()

