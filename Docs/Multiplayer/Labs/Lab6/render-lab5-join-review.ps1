param([string]$Version='v15')
$ErrorActionPreference='Stop';Add-Type -AssemblyName System.Drawing
$root=Join-Path $PSScriptRoot "../../../../Assets/Art/Terrain/Lab5TextureReview_$Version"
$atlas=[System.Drawing.Bitmap]::new((Join-Path $root 'atlas.png'))
if($atlas.Width -ne 1254 -or $atlas.Height -ne 1254){throw 'Expected native 418px cells'}
$n=418;$tiles=@{};$names=@('cliff_straight','cliff_outer_corner','cliff_inner_corner','ramp_single','ramp_left','ramp_middle','ramp_right','ground_grass','ground_soil')
for($k=0;$k -lt 9;$k++){$t=$atlas.Clone([System.Drawing.Rectangle]::new(($k%3)*$n,[int][Math]::Floor($k/3)*$n,$n,$n),[System.Drawing.Imaging.PixelFormat]::Format32bppArgb);$tiles[$names[$k]]=$t;$t.Save((Join-Path $root ($names[$k]+'.png')))}
$atlas.Dispose();$cache=@{}
function Tile($name,$r){$key="$name/$r";if(!$cache.ContainsKey($key)){$t=[System.Drawing.Bitmap]$tiles[$name].Clone();$types=@([System.Drawing.RotateFlipType]::RotateNoneFlipNone,[System.Drawing.RotateFlipType]::Rotate90FlipNone,[System.Drawing.RotateFlipType]::Rotate180FlipNone,[System.Drawing.RotateFlipType]::Rotate270FlipNone);$t.RotateFlip($types[$r]);$cache[$key]=$t};return $cache[$key]}
$b=[System.Drawing.Bitmap]::new(6*$n,6*$n);$g=[System.Drawing.Graphics]::FromImage($b)
for($y=0;$y -lt 6;$y++){for($x=0;$x -lt 6;$x++){$g.DrawImageUnscaled($tiles.ground_grass,$x*$n,$y*$n)}}
$placements=@(
 @('cliff_straight',0,0,1),@('cliff_straight',0,1,1),@('cliff_straight',0,2,1),
 @('ramp_left',0,3,1),@('ramp_middle',0,4,1),@('ramp_right',0,5,1),
 @('cliff_straight',3,1,3),@('cliff_outer_corner',0,1,4),@('cliff_straight',0,0,4),
 @('cliff_straight',1,4,3),@('cliff_inner_corner',0,4,4),@('cliff_straight',2,3,4)
)
foreach($p in $placements){$g.DrawImageUnscaled((Tile $p[0] $p[1]),$p[2]*$n,$p[3]*$n)}
$b.Save((Join-Path $root 'join_review.png'))
$results=[System.Collections.Generic.List[object]]::new()
$pairs=@(@('cliff_straight',0,'cliff_straight',0),@('ramp_left',0,'ramp_middle',0),@('ramp_middle',0,'ramp_right',0),@('cliff_straight',0,'cliff_outer_corner',0),@('cliff_straight',2,'cliff_inner_corner',0))
foreach($p in $pairs){$a=Tile $p[0] $p[1];$z=Tile $p[2] $p[3];$sum=0.0;$different=0
 for($y=0;$y -lt $n;$y++){$u=$a.GetPixel($n-1,$y);$v=$z.GetPixel(0,$y);if($u.ToArgb() -ne $v.ToArgb()){$different++};$sum+=[Math]::Abs([int]$u.R-$v.R)+[Math]::Abs([int]$u.G-$v.G)+[Math]::Abs([int]$u.B-$v.B)}
 $results.Add(@{left=$p[0];leftRotation=$p[1];right=$p[2];rightRotation=$p[3];mismatchedPixels=$different;edgeMeanRgbDifference=$sum/(3*$n)})
}
$results | ConvertTo-Json | Set-Content (Join-Path $root 'join_pixel_audit.json') -Encoding utf8
$results | ConvertTo-Json -Compress | Write-Output
$all=[System.Collections.Generic.List[object]]::new()
$cases=@(
 @('cliff_straight',0,'cliff_straight',0,1),@('ramp_left',0,'ramp_middle',0,1),@('ramp_middle',0,'ramp_right',0,1),
 @('cliff_straight',0,'cliff_outer_corner',0,1),@('cliff_straight',2,'cliff_inner_corner',0,1),
 @('cliff_straight',3,'cliff_outer_corner',0,2),@('cliff_straight',1,'cliff_inner_corner',0,2),
 @('ground_grass',0,'ground_grass',0,1),@('ground_grass',0,'ground_grass',0,2),
 @('ground_grass',0,'cliff_straight',0,2),@('cliff_straight',0,'ground_grass',0,2)
)
foreach($c in $cases){for($r=0;$r -lt 4;$r++){
 $a=Tile $c[0] (($c[1]+$r)%4);$z=Tile $c[2] (($c[3]+$r)%4);$d=($c[4]+$r)%4;$count=0
 for($t=0;$t -lt $n;$t++){
  switch($d){0{$u=$a.GetPixel($t,0);$v=$z.GetPixel($t,$n-1)}1{$u=$a.GetPixel($n-1,$t);$v=$z.GetPixel(0,$t)}2{$u=$a.GetPixel($t,$n-1);$v=$z.GetPixel($t,0)}3{$u=$a.GetPixel(0,$t);$v=$z.GetPixel($n-1,$t)}}
  if($u.ToArgb() -ne $v.ToArgb()){$count++}
 }
 $all.Add(@{first=$c[0];second=$c[2];rotation=$r;direction=$d;mismatchedPixels=$count})
}}
$all | ConvertTo-Json | Set-Content (Join-Path $root 'all_rotation_audit.json') -Encoding utf8
Write-Output "All rotations: $($all.Count) cases; failed: $(@($all | Where-Object mismatchedPixels -gt 0).Count)"
foreach($o in @($g,$b)){$o.Dispose()};foreach($o in $tiles.Values){$o.Dispose()};foreach($o in $cache.Values){$o.Dispose()}

