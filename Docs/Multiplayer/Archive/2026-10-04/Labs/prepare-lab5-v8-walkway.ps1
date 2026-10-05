$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
public static class OpenRampSurface {
 public static Bitmap Soil(Bitmap source){
  int n=source.Width,half=n/2,start=n/4;var b=new Bitmap(n,n);
  // Native soil pixels from the clear interior, mirrored transversely only.
  // The north/south slope gradient and image size are unchanged.
  for(int y=0;y<n;y++)for(int x=0;x<n;x++){
   int sx=start+(x<half?x:n-1-x);b.SetPixel(x,y,source.GetPixel(sx,y));
  }
  return b;
 }
 public static Bitmap Shoulders(Bitmap raw,Bitmap soil,string role){
  int n=raw.Width,edge=(int)Math.Round(n*.12);var output=(Bitmap)raw.Clone();
  int left=(role=="single"||role=="left")?edge:0;
  int right=(role=="single"||role=="right")?n-edge:n;
  for(int y=0;y<n;y++)for(int x=left;x<right;x++)output.SetPixel(x,y,soil.GetPixel(x,y));
  return output;
 }
}
'@
$dir=Join-Path $PSScriptRoot '../../../Assets/Art/Terrain/Lab5TextureReview_v8'
$authored=Join-Path $dir 'authored';New-Item -ItemType Directory -Force -Path $authored | Out-Null
foreach($name in @('cliff_straight','cliff_outer_corner','cliff_inner_corner')){
 Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('../../../Assets/Art/Terrain/Lab5TextureReview_v7/compiled/'+$name+'.png')) -Destination (Join-Path $authored ($name+'.png'))
}
$source=[Drawing.Bitmap]::new((Join-Path $dir 'ramp_middle.png'))
$soil=[OpenRampSurface]::Soil($source);$source.Dispose()
$soil.Save((Join-Path $authored 'ramp_middle.png'),[Drawing.Imaging.ImageFormat]::Png)
foreach($role in @('single','left','right')){
 $raw=[Drawing.Bitmap]::new((Join-Path $dir ('ramp_'+$role+'.png')))
 $result=[OpenRampSurface]::Shoulders($raw,$soil,$role)
 $result.Save((Join-Path $authored ('ramp_'+$role+'.png')),[Drawing.Imaging.ImageFormat]::Png);$result.Dispose();$raw.Dispose()
}
foreach($name in @('ramp_middle_outer_corner','ramp_middle_inner_corner')){Copy-Item -LiteralPath (Join-Path $dir ($name+'.png')) -Destination (Join-Path $authored ($name+'.png'))}
$soil.Dispose()
Write-Output 'Native walkway authored: 12% exterior shoulders; open soil interior; cliffs reused exactly.'
