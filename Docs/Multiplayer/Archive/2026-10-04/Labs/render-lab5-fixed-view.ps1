$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$assets=Join-Path $PSScriptRoot '../../../Assets/Art/Terrain/Lab5FixedView_v9'
$data=Get-Content -LiteralPath (Join-Path $PSScriptRoot '../../../Assets/Art/Terrain/Lab5TextureReview_v8/sample_10x10.json') -Raw | ConvertFrom-Json
$size=418
$tiles=@{}
$tiles['ground_grass']=[Drawing.Bitmap]::new((Join-Path $assets 'ground_grass.png'))
$rotated=@{}
function Sprite($name,$direction){
 $key="$name/$direction"
 if(!$rotated.ContainsKey($key)){
  $path=Join-Path $assets ($name+'_'+$direction+'.png')
  $rotated[$key]=[Drawing.Bitmap]::new($path)
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
  $name='cliff_'+$part.shape;$direction=[int]$part.rotation
  if(!(Test-Path -LiteralPath (Join-Path $assets ($name+'_'+$direction+'.png')))){throw "Missing authored cliff part: $name"}
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
$bitmap.Save((Join-Path $assets 'sample_10x10_fixed_view.png'),[System.Drawing.Imaging.ImageFormat]::Png)
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
$normal=[System.Drawing.Pen]::new([System.Drawing.Color]::Orange,7);$normal.EndCap=[System.Drawing.Drawing2D.LineCap]::ArrowAnchor
foreach($c in $data.cells){
 $f=$c.face;if(!$f -or $c.rampTile){continue}
 $x=$c.x*$size;$y=$c.y*$size
 $graphics.FillRectangle([System.Drawing.Brushes]::Black,$x+8,$y+8,260,36)
 $graphics.DrawString("HIGH $($f.highLevel) -> LOW $($f.lowLevel)",$font,[System.Drawing.Brushes]::Orange,[single]($x+10),[single]($y+10))
 for($d=0;$d -lt 4;$d++){if(([int]$f.lowEdges -band (1 -shl $d)) -ne 0){
  $dx=@(0,1,0,-1)[$d];$dy=@(-1,0,1,0)[$d]
  $graphics.DrawLine($normal,[single]($x+$size/2),[single]($y+$size/2),[single]($x+$size/2+$dx*145),[single]($y+$size/2+$dy*145))
 }}
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
$bitmap.Save((Join-Path $assets 'sample_10x10_fixed_view_marked.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$draws | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $assets 'sample_draw_manifest.json') -Encoding UTF8
foreach($obj in @($grid,$edge,$outline,$arrow,$normal,$font,$graphics,$bitmap)){$obj.Dispose()}
foreach($obj in $tiles.Values){$obj.Dispose()};foreach($obj in $rotated.Values){$obj.Dispose()}
Write-Output "Fixed-view seed $($data.seed), 10x10, ground -> cliffs -> ramps, native 418-square tiles; no resampling."
