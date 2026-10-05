$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=Join-Path $PSScriptRoot '../../../Assets/Art/Terrain/Lab5TextureReview_v17'
$atlas=[System.Drawing.Bitmap]::new((Join-Path $root 'atlas.png'))
if($atlas.Width -ne 1254 -or $atlas.Height -ne 1254){throw 'Atlas must contain native 418px tiles; no resampling allowed'}
$n=418;$tiles=@{};$names=@('cliff_straight','cliff_outer_corner','cliff_inner_corner','ramp_single','ramp_left','ramp_middle','ramp_right','ground_grass','ground_soil')
for($k=0;$k -lt 9;$k++){
 $rect=[System.Drawing.Rectangle]::new(($k%3)*$n,[int][Math]::Floor($k/3)*$n,$n,$n)
 $tile=$atlas.Clone($rect,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
 $tile.Save((Join-Path $root ($names[$k]+'.png')));$tiles[$names[$k]]=$tile
}
$atlas.Dispose()
function RotMask($mask,$r){return (($mask -shl $r) -bor ($mask -shr (4-$r))) -band 15}
$cache=@{}
function Sprite($name,$r){
 $key="$name/$r"
 if(!$cache.ContainsKey($key)){
  $tile=[System.Drawing.Bitmap]$tiles[$name].Clone()
  $types=@([System.Drawing.RotateFlipType]::RotateNoneFlipNone,[System.Drawing.RotateFlipType]::Rotate90FlipNone,[System.Drawing.RotateFlipType]::Rotate180FlipNone,[System.Drawing.RotateFlipType]::Rotate270FlipNone)
  $tile.RotateFlip($types[$r]);$cache[$key]=$tile
 }
 return $cache[$key]
}
$data=Get-Content (Join-Path $root 'sample_10x10.json') -Raw | ConvertFrom-Json
$b=[System.Drawing.Bitmap]::new(10*$n,10*$n);$g=[System.Drawing.Graphics]::FromImage($b)
$manifest=[System.Collections.Generic.List[object]]::new()
foreach($c in $data.cells){$g.DrawImageUnscaled($tiles.ground_grass,$c.x*$n,$c.y*$n)}
foreach($c in $data.cells){
 if(!$c.face -or $c.face.open){continue}
 foreach($p in $c.face.parts){
  $name='cliff_'+$p.shape;$r=[int]$p.heightRotation
  $baseHigh=@{straight=3;outer_corner=1;inner_corner=14}[$p.shape]
  $basePorts=if($p.shape -eq 'straight'){10}else{9}
  if((RotMask $baseHigh $r) -ne $p.surfaceSides.highQuadrants -or (RotMask $basePorts $r) -ne $p.ports){throw "Path/height mismatch at $($c.globalCell)"}
  $g.DrawImageUnscaled((Sprite $name $r),$c.x*$n,$c.y*$n)
  $manifest.Add(@{cell=$c.globalCell;source=$name;rotation=$r;highQuadrants=$p.surfaceSides.highQuadrants;lowQuadrants=$p.surfaceSides.lowQuadrants;ports=$p.ports;joinWalkable=$c.cliffJoin.walkable})
 }
}
foreach($c in $data.cells){
 $r=$c.rampTile;if(!$r){continue}
 $name=@{single='ramp_single';left='ramp_left';center='ramp_middle';right='ramp_right'}[$r.widthRole]
 $g.DrawImageUnscaled((Sprite $name ([int]$r.direction)),$c.x*$n,$c.y*$n)
 $manifest.Add(@{cell=$c.globalCell;source=$name;uphill=$r.direction;lowCell=$r.lowCell;highCell=$r.highCell;joinPending=!!$c.cliffJoin})
}
$b.Save((Join-Path $root 'sample_10x10.png'))
$pen=[System.Drawing.Pen]::new([System.Drawing.Color]::Red,5)
foreach($c in $data.cells){if($c.rampTile){$g.DrawRectangle($pen,$c.x*$n+3,$c.y*$n+3,$n-6,$n-6)}}
$b.Save((Join-Path $root 'sample_10x10_marked.png'))
$manifest | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $root 'draw_manifest.json') -Encoding utf8
foreach($o in @($g,$b,$pen)){$o.Dispose()};foreach($o in $tiles.Values){$o.Dispose()};foreach($o in $cache.Values){$o.Dispose()}
Write-Output 'Native 418px sample rendered; every cliff draw checked against both path ports and high/low occupancy.'




