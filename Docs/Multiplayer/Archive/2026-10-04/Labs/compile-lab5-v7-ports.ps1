param([string]$Version="v7")
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
public static class TerrainPortAuthor {
 public static Color[] Profile(Bitmap b,int x) {var a=new Color[b.Height];for(int y=0;y<b.Height;y++)a[y]=b.GetPixel(x,y);return a;}
 public static Color[] Reverse(Color[] a) {var b=(Color[])a.Clone();Array.Reverse(b);return b;}
 public static Bitmap Build(Bitmap source,Color[] left,Color[] right,Color[] top,int width) {
  int n=source.Width;var output=(Bitmap)source.Clone();
  for(int y=0;y<n;y++)for(int x=0;x<n;x++){
   // Fixed native border authoring. No resampling, sprite stretching or runtime seam filter.
   double sum=0,red=0,green=0,blue=0,opacity=0,minDistance=width;
   int[] distances={x,n-1-x,y};Color[][] ports={left,right,top};int[] offsets={y,y,x};
   bool exact=false;Color edge=Color.Empty;
   for(int p=0;p<3;p++)if(ports[p]!=null && distances[p]<width){
    int d=distances[p];Color c=ports[p][offsets[p]];
    if(d==0){if(exact&&edge.ToArgb()!=c.ToArgb())throw new Exception("Incompatible intersecting ports");edge=c;exact=true;}
    double w=1.0/((d+1.0)*(d+1.0));sum+=w;red+=w*c.R;green+=w*c.G;blue+=w*c.B;opacity+=w*c.A;minDistance=Math.Min(minDistance,d);
   }
   if(exact){output.SetPixel(x,y,edge);continue;}
   if(sum==0)continue;
   Color old=source.GetPixel(x,y);double amount=Math.Pow(1-minDistance/width,2);
   output.SetPixel(x,y,Color.FromArgb((int)Math.Round(old.A*(1-amount)+opacity/sum*amount),(int)Math.Round(old.R*(1-amount)+red/sum*amount),(int)Math.Round(old.G*(1-amount)+green/sum*amount),(int)Math.Round(old.B*(1-amount)+blue/sum*amount)));
  }
  return output;
 }
}
'@
$dir=Join-Path $PSScriptRoot ("../../../Assets/Art/Terrain/Lab5TextureReview_"+$Version)
$destination=Join-Path $dir 'compiled';New-Item -ItemType Directory -Force -Path $destination | Out-Null
$inputDir=if($Version -eq "v8"){Join-Path $dir "authored"}else{$dir}
$stone=[Drawing.Bitmap]::new((Join-Path $inputDir 'cliff_straight.png'))
$soil=[Drawing.Bitmap]::new((Join-Path $inputDir 'ramp_middle.png'))
$p=[TerrainPortAuthor]::Profile($stone,0);$d=[TerrainPortAuthor]::Profile($soil,0)
$pr=[TerrainPortAuthor]::Reverse($p);$dr=[TerrainPortAuthor]::Reverse($d)
$spec=@(
 @{name='cliff_straight';left=$p;right=$p;top=$null},
 @{name='cliff_outer_corner';left=$p;right=$null;top=$p},
 @{name='cliff_inner_corner';left=$pr;right=$null;top=$pr},
 @{name='ramp_single';left=$p;right=$p;top=$null},
 @{name='ramp_left';left=$p;right=$d;top=$null},
 @{name='ramp_middle';left=$d;right=$d;top=$null},
 @{name='ramp_right';left=$d;right=$p;top=$null},
 @{name='ramp_middle_outer_corner';left=$d;right=$null;top=$d},
 @{name='ramp_middle_inner_corner';left=$dr;right=$null;top=$dr}
)
$s=$stone.Width;$atlas=[Drawing.Bitmap]::new($s*3,$s*3);$g=[Drawing.Graphics]::FromImage($atlas)
for($i=0;$i -lt $spec.Count;$i++){
 $entry=$spec[$i];$raw=[Drawing.Bitmap]::new((Join-Path $inputDir ($entry.name+'.png')))
 $result=if($Version -eq "v8" -and $entry.name.StartsWith("cliff_")){[Drawing.Bitmap]$raw.Clone()}else{[TerrainPortAuthor]::Build($raw,$entry.left,$entry.right,$entry.top,32)}
 $result.Save((Join-Path $destination ($entry.name+'.png')),[Drawing.Imaging.ImageFormat]::Png)
 $g.DrawImageUnscaled($result,($i%3)*$s,[int][Math]::Floor($i/3)*$s)
 $result.Dispose();$raw.Dispose()
}
$atlas.Save((Join-Path $destination 'atlas_candidate.png'),[Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$atlas.Dispose();$stone.Dispose();$soil.Dispose()
Write-Output '9 sprites authored with shared 32-pixel ports, native resolution.'
