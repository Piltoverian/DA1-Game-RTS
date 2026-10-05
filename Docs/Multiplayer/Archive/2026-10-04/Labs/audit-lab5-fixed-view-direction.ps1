$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$dir=Join-Path $PSScriptRoot '../../../Assets/Art/Terrain/Lab5FixedView_v9'
$stone=[Drawing.Bitmap]::new((Join-Path $dir 'material_stone.png'));$n=$stone.Width
$x=[int]($n*.32);$y=[int]($n*.44);$base=$stone.GetPixel($x,$y);$checks=0
function RotPoint($x,$y,$r){switch($r){0{return @($x,$y)}1{return @(($n-1-$y),$x)}2{return @(($n-1-$x),($n-1-$y))}3{return @($y,($n-1-$x))}}}
function Expect($mask,$point,$color,$message){if($mask.GetPixel($point[0],$point[1]).ToArgb() -ne $color.ToArgb()){throw $message}}
for($r=0;$r -lt 4;$r++){
 $sprite=[Drawing.Bitmap]::new((Join-Path $dir ('cliff_straight_'+$r+'.png')))
 $t=@($y,($n-1-$x),($n-1-$y),$x)[$r]/[double]($n-1);$factor=1-.24*$t
 $expected=[Drawing.Color]::FromArgb(255,[int][Math]::Round($base.R*$factor),[int][Math]::Round($base.G*$factor),[int][Math]::Round($base.B*$factor))
 if($sprite.GetPixel($x,$y).ToArgb() -ne $expected.ToArgb()){throw "Stone was rotated instead of sampled in fixed view: $r"};$checks++;$sprite.Dispose()
 $mask=[Drawing.Bitmap]::new((Join-Path $dir ('semantic_cliff_straight_'+$r+'.png')))
 Expect $mask (RotPoint ([int]($n/2)) 4 $r) ([Drawing.Color]::Green) 'High-side lip reversed'
 Expect $mask (RotPoint ([int]($n/2)) ($n-5) $r) ([Drawing.Color]::Brown) 'Low-side foot reversed'
 $checks+=2;$mask.Dispose()
 foreach($kind in @('outer','inner')){
  $mask=[Drawing.Bitmap]::new((Join-Path $dir ('semantic_cliff_'+$kind+'_corner_'+$r+'.png')))
  $color=if($kind -eq 'outer'){[Drawing.Color]::Green}else{[Drawing.Color]::Brown}
  Expect $mask (RotPoint 4 4 $r) $color 'Corner high/low quadrant reversed';$checks++;$mask.Dispose()
 }
}
$stone.Dispose()
@{assertions=$checks;errors=0;stoneRotates=$false;highLowAndCornersVerified=$true}|ConvertTo-Json|Set-Content -Encoding UTF8 -LiteralPath (Join-Path $dir 'direction_audit.json')
Write-Output "$checks direction/material assertions passed."
