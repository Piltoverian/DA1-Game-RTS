$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
public static class RampBodyAudit {
 public static int Compare(Bitmap ramp,Bitmap soil,string role){
  int n=ramp.Width,edge=(int)Math.Round(n*.12),errors=0;
  int left=(role=="single"||role=="left")?edge:0;
  int right=(role=="single"||role=="right")?n-edge:n;
  for(int y=0;y<n;y++)for(int x=left;x<right;x++)if(ramp.GetPixel(x,y).ToArgb()!=soil.GetPixel(x,y).ToArgb())errors++;
  return errors;
 }
 public static int Same(Bitmap a,Bitmap b){int errors=0;for(int y=0;y<a.Height;y++)for(int x=0;x<a.Width;x++)if(a.GetPixel(x,y).ToArgb()!=b.GetPixel(x,y).ToArgb())errors++;return errors;}
}
'@
$dir=Join-Path $PSScriptRoot '../../../Assets/Art/Terrain/Lab5TextureReview_v8/compiled'
$soil=[Drawing.Bitmap]::new((Join-Path $dir 'ramp_middle.png'));$results=@()
foreach($role in @('single','left','right')){
 $r=[Drawing.Bitmap]::new((Join-Path $dir ('ramp_'+$role+'.png')))
 $errors=[RampBodyAudit]::Compare($r,$soil,$role);if($errors){throw "Interior differs from walking soil: $role ($errors pixels)"}
 $results+=@{role=$role;interiorMismatches=$errors;exteriorShoulderFraction=.12};$r.Dispose()
}
foreach($name in @('cliff_straight','cliff_outer_corner','cliff_inner_corner')){
 $a=[Drawing.Bitmap]::new((Join-Path $dir ($name+'.png')))
 $b=[Drawing.Bitmap]::new((Join-Path $PSScriptRoot ('../../../Assets/Art/Terrain/Lab5TextureReview_v7/compiled/'+$name+'.png')))
 $errors=[RampBodyAudit]::Same($a,$b);if($errors){throw "Cliff changed: $name"}
 $results+=@{sprite=$name;changedPixels=$errors};$a.Dispose();$b.Dispose()
}
$soil.Dispose();$results | ConvertTo-Json | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $dir 'walkway_audit.json')
Write-Output '3 ramp bodies exactly match clear soil; 3 cliff sprites preserved pixel-for-pixel.'
