param([switch]$Compiled,[string]$Version="v7")
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$dir=Join-Path $PSScriptRoot '../../../Assets/Art/Terrain/Lab5FixedView_v9'
$probe=[Drawing.Bitmap]::new((Join-Path $dir 'material_stone.png'));$s=$probe.Width;$probe.Dispose()
$names=@('cliff_straight','cliff_outer_corner','cliff_inner_corner','ramp_single','ramp_left','ramp_middle','ramp_right','ramp_middle_outer_corner','ramp_middle_inner_corner')
$tiles=@{};$lookup=@{}
foreach($name in $names){$tile=[Drawing.Bitmap]::new((Join-Path $dir ($name+'_0.png')));$tiles[$name]=$tile;$lookup[$tile.GetHashCode()]=$name}
function Turn($t,$d){return [Drawing.Bitmap]::new((Join-Path $dir ($lookup[$t.GetHashCode()]+'_'+$d+'.png')))}
$pairs=@(
 @('cliff_straight',0,1,'cliff_straight',0,3),
 @('cliff_straight',0,1,'ramp_single',0,3),
 @('ramp_single',0,1,'cliff_straight',0,3),
 @('cliff_straight',0,1,'ramp_left',0,3),
 @('ramp_left',0,1,'ramp_middle',0,3),
 @('ramp_middle',0,1,'ramp_middle',0,3),
 @('ramp_middle',0,1,'ramp_right',0,3),
 @('ramp_right',0,1,'cliff_straight',0,3),
 @('cliff_outer_corner',0,3,'cliff_straight',0,1),
 @('cliff_outer_corner',0,0,'cliff_straight',3,2),
 @('cliff_inner_corner',0,3,'cliff_straight',2,1),
 @('cliff_inner_corner',0,0,'cliff_straight',1,2),
 @('ramp_middle_outer_corner',0,3,'ramp_middle',0,1),
 @('ramp_middle_outer_corner',0,0,'ramp_middle',3,2),
 @('ramp_middle_inner_corner',0,3,'ramp_middle',2,1),
 @('ramp_middle_inner_corner',0,0,'ramp_middle',1,2)
)
function Pixel($t,$side,$k,$depth){
 switch($side){0{return $t.GetPixel($k,$depth)}1{return $t.GetPixel($s-1-$depth,$k)}2{return $t.GetPixel($k,$s-1-$depth)}3{return $t.GetPixel($depth,$k)}}
}
$results=@()
foreach($pair in $pairs){for($r=0;$r -lt 4;$r++){
 $a=Turn $tiles[$pair[0]] (($pair[1]+$r)%4);$b=Turn $tiles[$pair[3]] (($pair[4]+$r)%4)
 $sideA=($pair[2]+$r)%4;$sideB=($pair[5]+$r)%4;$mismatch=0;$delta=0L;$stripDelta=0L
 for($k=0;$k -lt $s;$k++){
  $ca=Pixel $a $sideA $k 0;$cb=Pixel $b $sideB $k 0
  if($ca.ToArgb() -ne $cb.ToArgb()){$mismatch++}
  $delta+=[Math]::Abs([int]$ca.R-$cb.R)+[Math]::Abs([int]$ca.G-$cb.G)+[Math]::Abs([int]$ca.B-$cb.B)
  for($depth=0;$depth -lt 8;$depth++){
   $ca=Pixel $a $sideA $k $depth;$cb=Pixel $b $sideB $k $depth
   $stripDelta+=[Math]::Abs([int]$ca.R-$cb.R)+[Math]::Abs([int]$ca.G-$cb.G)+[Math]::Abs([int]$ca.B-$cb.B)
  }
 }
 $results+=[pscustomobject]@{a=$pair[0];aRotation=($pair[1]+$r)%4;aSide=$sideA;b=$pair[3];bRotation=($pair[4]+$r)%4;bSide=$sideB;mismatchedPixels=$mismatch;pixels=$s;meanRGBJump=$delta/($s*3);eightPixelStripJump=$stripDelta/($s*3*8)}
 $a.Dispose();$b.Dispose()
}}
$results | ConvertTo-Json | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $dir 'pixel_audit.json')
Write-Output (@{tests=$results.Count;passed=@($results | Where-Object mismatchedPixels -eq 0).Count;failed=@($results | Where-Object mismatchedPixels -gt 0).Count}|ConvertTo-Json -Compress)
foreach($t in $tiles.Values){$t.Dispose()}
