$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
public static class FixedViewTerrain {
 public static Bitmap Periodic(Bitmap source,int sx,int sy,int n){
  int h=n/2;var b=new Bitmap(n,n);
  for(int y=0;y<n;y++)for(int x=0;x<n;x++){
   int px=x<h?x:n-1-x,py=y<h?y:n-1-y;
   b.SetPixel(x,y,source.GetPixel(sx+px,sy+py));
  }
  return b;
 }
 public static Bitmap Paint(Bitmap grass,Bitmap rock,Bitmap dirt,string kind,int rotation,bool semantic){
  int n=rock.Width;var b=new Bitmap(n,n);
  for(int y=0;y<n;y++)for(int x=0;x<n;x++){
   // Rotate coordinates of the topology ONLY. Material samples remain in screen space.
   int u=x,v=y;
   if(rotation==1){u=y;v=n-1-x;}else if(rotation==2){u=n-1-x;v=n-1-y;}else if(rotation==3){u=n-1-y;v=x;}
   double t=v/(double)(n-1),q=u/(double)(n-1);
   if(kind.Contains("outer_corner"))t=Math.Max(q,t);
   if(kind.Contains("inner_corner"))t=1-Math.Max(q,t);
   bool isRamp=kind.StartsWith("ramp_");
   bool walking=isRamp;
   if(kind=="ramp_single")walking=q>=.12&&q<=.88;
   else if(kind=="ramp_left")walking=q>=.12;
   else if(kind=="ramp_right")walking=q<=.88;
   Color c;
   if(walking){
    c=dirt.GetPixel(x,y);
    int shade=(int)Math.Round(18*t*(1-t)*4);
    c=Color.FromArgb(255,Math.Max(0,c.R-shade),Math.Max(0,c.G-shade),Math.Max(0,c.B-shade));
   }else if(t<.05)c=grass.GetPixel(x,y);
   else if(t<=.95){
    c=rock.GetPixel(x,y);double shade=1-.24*t;
    c=Color.FromArgb(255,(int)Math.Round(c.R*shade),(int)Math.Round(c.G*shade),(int)Math.Round(c.B*shade));
   }
   else c=dirt.GetPixel(x,y);
   if(semantic)c=walking?Color.Yellow:t<.05?Color.Green:t<=.95?Color.Gray:Color.Brown;
   b.SetPixel(x,y,c);
  }
  return b;
 }
}
'@
$dir=Join-Path $PSScriptRoot '../../../Assets/Art/Terrain/Lab5FixedView_v9'
New-Item -ItemType Directory -Force -Path $dir | Out-Null
$old=Join-Path $PSScriptRoot '../../../Assets/Art/Terrain/Lab5TextureReview_v8/compiled'
$rockSource=[Drawing.Bitmap]::new((Join-Path $old 'cliff_straight.png'))
$dirtSource=[Drawing.Bitmap]::new((Join-Path $old 'ramp_middle.png'))
$grassSource=[Drawing.Bitmap]::new((Join-Path $old 'ground_grass.png'))
$s=$rockSource.Width
$rock=[FixedViewTerrain]::Periodic($rockSource,20,75,$s)
$dirt=[FixedViewTerrain]::Periodic($dirtSource,100,100,$s)
$grass=[FixedViewTerrain]::Periodic($grassSource,0,0,$s)
foreach($entry in @(@('ground_grass',$grass),@('material_stone',$rock),@('material_dirt',$dirt))){$entry[1].Save((Join-Path $dir ($entry[0]+'.png')),[Drawing.Imaging.ImageFormat]::Png)}
$names=@('cliff_straight','cliff_outer_corner','cliff_inner_corner','ramp_single','ramp_left','ramp_middle','ramp_right','ramp_middle_outer_corner','ramp_middle_inner_corner')
foreach($name in $names){for($r=0;$r -lt 4;$r++){
 $tile=[FixedViewTerrain]::Paint($grass,$rock,$dirt,$name,$r,$false)
 $tile.Save((Join-Path $dir ($name+'_'+$r+'.png')),[Drawing.Imaging.ImageFormat]::Png);$tile.Dispose()
 $mask=[FixedViewTerrain]::Paint($grass,$rock,$dirt,$name,$r,$true);$mask.Save((Join-Path $dir ('semantic_'+$name+'_'+$r+'.png')),[Drawing.Imaging.ImageFormat]::Png);$mask.Dispose()
}}
foreach($t in @($rock,$dirt,$grass,$rockSource,$dirtSource,$grassSource)){$t.Dispose()}
Write-Output '36 native fixed-view sprites: topology rotates, stone texture never rotates.'
