$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$assets=Join-Path $PSScriptRoot '../../../Assets/Art/Terrain/Lab5TextureReview_v8/compiled'
$data=Get-Content -LiteralPath (Join-Path $PSScriptRoot '../../../Assets/Art/Terrain/Lab5TextureReview_v8/sample_10x10.json') -Raw | ConvertFrom-Json
$size=418
$tiles=@{}
# Native V8 sprites and a native 418px ground tile: no destination scaling.
foreach($name in @('ground_grass','cliff_straight','cliff_outer_corner','cliff_inner_corner','ramp_single','ramp_left','ramp_middle','ramp_right','ramp_middle_outer_corner','ramp_middle_inner_corner')){
 $tile=[System.Drawing.Bitmap]::new([string](Resolve-Path (Join-Path $assets ($name+'.png'))).Path)
 if($tile.Width -ne $size -or $tile.Height -ne $size){throw "Non-native tile size: $name"}
 $tiles[$name]=$tile
}
$rotated=@{}
function Sprite($name,$direction){
 $key="$name/$direction"
 if(!$rotated.ContainsKey($key)){
  $tile=[System.Drawing.Bitmap]$tiles[$name].Clone()
  $types=@([System.Drawing.RotateFlipType]::RotateNoneFlipNone,[System.Drawing.RotateFlipType]::Rotate90FlipNone,[System.Drawing.RotateFlipType]::Rotate180FlipNone,[System.Drawing.RotateFlipType]::Rotate270FlipNone)
  $tile.RotateFlip($types[$direction]);$rotated[$key]=$tile
 }
 return $rotated[$key]
}
$bitmap=[System.Drawing.Bitmap]::new(10*$size,10*$size)
$graphics=[System.Drawing.Graphics]::FromImage($bitmap)
$draws=[System.Collections.Generic.List[object]]::new()
# Three complete passes: ground, cliff faces, ramp faces.
foreach($cell in $data.cells){$graphics.DrawImageUnscaled($tiles['ground_grass'],$cell.x*$size,$cell.y*$size)}
foreach($cell in $data.cells){
 $face=$cell.face;if(!$face -or $face.open){continue}
 foreach($part in $face.parts){
  $name='cliff_'+$part.shape;$direction=[int]$part.connectionRotation
  if(!$tiles.ContainsKey($name)){throw "Missing authored cliff part: $name"}
  $graphics.DrawImageUnscaled((Sprite $name $direction),$cell.x*$size,$cell.y*$size)
  $draws.Add(@{cell=$cell.globalCell;pass='cliff';source=$name;direction=$direction;ports=$part.ports;lowEdges=$part.lowEdges})
 }
}
foreach($cell in $data.cells){
 $r=$cell.rampTile;if(!$r){continue}
 $name=@{single='ramp_single';left='ramp_left';center='ramp_middle';right='ramp_right'}[$r.widthRole]
 if($r.shape -eq 'middle_corner'){$name=if($r.cornerType -eq 'inner_corner'){'ramp_middle_inner_corner'}else{'ramp_middle_outer_corner'}}
 $graphics.DrawImageUnscaled((Sprite $name ([int]$r.spriteRotation)),$cell.x*$size,$cell.y*$size)
 $draws.Add(@{cell=$cell.globalCell;pass='ramp';source=$name;group=$r.groupId;width=$r.groupWidth;direction=$r.direction})
}
$bitmap.Save((Join-Path $assets 'sample_10x10_v8.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$grid=[System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(100,255,255,255),1)
$edge=[System.Drawing.Pen]::new([System.Drawing.Color]::Cyan,5)
$outline=[System.Drawing.Pen]::new([System.Drawing.Color]::Red,5)
$arrow=[System.Drawing.Pen]::new([System.Drawing.Color]::Yellow,6);$arrow.EndCap=[System.Drawing.Drawing2D.LineCap]::ArrowAnchor
$font=[System.Drawing.Font]::new('Consolas',17,[System.Drawing.FontStyle]::Bold)
$byXY=@{};foreach($c in $data.cells){$byXY["$($c.x),$($c.y)"]=$c}
foreach($c in $data.cells){
 $x=$c.x*$size;$y=$c.y*$size;$graphics.DrawRectangle($grid,$x,$y,$size-1,$size-1)
 $graphics.DrawString("L$($c.level)",$font,[System.Drawing.Brushes]::White,[single]($x+10),[single]($y+$size-35))
 $east=$byXY["$($c.x+1),$($c.y)"];$south=$byXY["$($c.x),$($c.y+1)"]
 if($east -and $east.level -ne $c.level){$graphics.DrawLine($edge,$x+$size,$y,$x+$size,$y+$size)}
 if($south -and $south.level -ne $c.level){$graphics.DrawLine($edge,$x,$y+$size,$x+$size,$y+$size)}
}
foreach($c in $data.cells){
 $r=$c.rampTile;if(!$r){continue};$x=$c.x*$size;$y=$c.y*$size
 $graphics.DrawRectangle($outline,$x+5,$y+5,$size-10,$size-10)
 $graphics.FillRectangle([System.Drawing.Brushes]::Black,$x+8,$y+8,200,53)
 $graphics.DrawString("G$($r.groupId) W$($r.groupWidth) $($r.widthRole)",$font,[System.Drawing.Brushes]::White,[single]($x+10),[single]($y+10))
 $graphics.DrawString("L$($r.lowLevel)->$($r.highLevel)",$font,[System.Drawing.Brushes]::Yellow,[single]($x+10),[single]($y+34))
 $cx=$x+$size/2;$cy=$y+$size/2
 $graphics.DrawLine($arrow,[single]$cx,[single]$cy,[single]($cx+$r.globalDirection[0]*90),[single]($cy+$r.globalDirection[1]*90))
}
$bitmap.Save((Join-Path $assets 'sample_10x10_v8_marked.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$connectionPen=[System.Drawing.Pen]::new([System.Drawing.Color]::Magenta,8)
$connectionPen.EndCap=[System.Drawing.Drawing2D.LineCap]::ArrowAnchor
$directions=@(@(0,-1),@(1,0),@(0,1),@(-1,0))
foreach($c in $data.cells){
 if(!$c.face){continue}
 $cx=$c.x*$size+$size/2;$cy=$c.y*$size+$size/2
 foreach($connection in $c.face.connections){
  $delta=$directions[[int]$connection.direction]
  $graphics.DrawLine($connectionPen,[single]$cx,[single]$cy,[single]($cx+$delta[0]*$size*.7),[single]($cy+$delta[1]*$size*.7))
 }
}
$bitmap.Save((Join-Path $assets 'sample_10x10_v8_connections.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$connectionPen.Dispose()
$draws | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $assets 'sample_draw_manifest.json') -Encoding UTF8
foreach($obj in @($grid,$edge,$outline,$arrow,$font,$graphics,$bitmap)){$obj.Dispose()}
foreach($obj in $tiles.Values){$obj.Dispose()};foreach($obj in $rotated.Values){$obj.Dispose()}
Write-Output "V8 seed $($data.seed), 10x10, ground -> cliffs -> ramps, native 418-square tiles; no resampling."
