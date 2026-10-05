param([switch]$Compiled,[string]$Version="v7")
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$dir=Join-Path $PSScriptRoot ("../../../Assets/Art/Terrain/Lab5TextureReview_"+$Version)
if($Compiled){$dir=Join-Path $dir 'compiled'}
$atlas=[Drawing.Bitmap]::new((Join-Path $dir 'atlas_candidate.png'))
if($atlas.Width%3 -or $atlas.Height -ne $atlas.Width){throw 'Atlas dimensions not divisible by 3'}
$s=[int]($atlas.Width/3)
$names=@('cliff_straight','cliff_outer_corner','cliff_inner_corner','ramp_single','ramp_left','ramp_middle','ramp_right','ramp_middle_outer_corner','ramp_middle_inner_corner')
$tiles=@{};for($i=0;$i -lt 9;$i++){
 $tile=$atlas.Clone([Drawing.Rectangle]::new(($i%3)*$s,[int][Math]::Floor($i/3)*$s,$s,$s),[Drawing.Imaging.PixelFormat]::Format32bppArgb)
 $tile.Save((Join-Path $dir ($names[$i]+'.png')),[Drawing.Imaging.ImageFormat]::Png);$tiles[$names[$i]]=$tile
}
function Turn($t,$d){$copy=[Drawing.Bitmap]$t.Clone();$copy.RotateFlip(@([Drawing.RotateFlipType]::RotateNoneFlipNone,[Drawing.RotateFlipType]::Rotate90FlipNone,[Drawing.RotateFlipType]::Rotate180FlipNone,[Drawing.RotateFlipType]::Rotate270FlipNone)[$d]);return $copy}
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
$rows=@(@('cliff_straight','cliff_straight','ramp_single','cliff_straight','cliff_straight'),@('cliff_straight','ramp_left','ramp_middle','ramp_right','cliff_straight'),@('ramp_left','ramp_middle','ramp_middle','ramp_middle','ramp_right'))
$board=[Drawing.Bitmap]::new(5*$s,3*$s);$g=[Drawing.Graphics]::FromImage($board)
for($y=0;$y -lt 3;$y++){for($x=0;$x -lt 5;$x++){$g.DrawImageUnscaled($tiles[$rows[$y][$x]],$x*$s,$y*$s)}}
$board.Save((Join-Path $dir 'raw_joined_preview.png'),[Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$board.Dispose()
$corner=[Drawing.Bitmap]::new(12*$s,6*$s);$g=[Drawing.Graphics]::FromImage($corner);$g.Clear([Drawing.Color]::FromArgb(45,45,45))
for($kind=0;$kind -lt 2;$kind++){
 $tile=[Drawing.Bitmap]::new(3*$s,3*$s);$tg=[Drawing.Graphics]::FromImage($tile);$tg.Clear([Drawing.Color]::FromArgb(45,45,45))
 $key=@('cliff_outer_corner','cliff_inner_corner')[$kind];$tg.DrawImageUnscaled($tiles[$key],$s,$s)
 $h=Turn $tiles.cliff_straight ($kind*2);$v=Turn $tiles.cliff_straight (@(3,1)[$kind])
 $tg.DrawImageUnscaled($h,0,$s);$tg.DrawImageUnscaled($v,$s,0);$tg.Dispose();$h.Dispose();$v.Dispose()
 for($r=0;$r -lt 4;$r++){$copy=Turn $tile $r;$g.DrawImageUnscaled($copy,$r*3*$s,$kind*3*$s);$copy.Dispose()};$tile.Dispose()
}
$corner.Save((Join-Path $dir 'raw_corner_preview.png'),[Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$corner.Dispose()
Write-Output (@{tests=$results.Count;passed=@($results | Where-Object mismatchedPixels -eq 0).Count;failed=@($results | Where-Object mismatchedPixels -gt 0).Count;worstMeanRGBJump=($results | Measure-Object -Property meanRGBJump -Maximum).Maximum}|ConvertTo-Json -Compress)
foreach($t in $tiles.Values){$t.Dispose()};$atlas.Dispose()
