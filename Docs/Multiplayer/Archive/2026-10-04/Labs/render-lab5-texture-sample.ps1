$ErrorActionPreference='Stop'
$assetDir=Join-Path $PSScriptRoot '../../../Assets/Art/Terrain/Lab5FlatYTextures_v2'
$data=Get-Content -LiteralPath (Join-Path $assetDir 'sample_10x10.json') -Raw | ConvertFrom-Json
Add-Type -AssemblyName System.Drawing
Add-Type -Path (Join-Path $PSScriptRoot 'Lab5RampSpriteAtlas.cs') -ReferencedAssemblies System.Drawing
$size=418 # 1254 / 3: native sprite pixels, never resampled.
$atlasDir=Join-Path $assetDir 'NativeSpriteAtlas'
[System.IO.Directory]::CreateDirectory($atlasDir) | Out-Null
$manifest=[System.Collections.Generic.List[object]]::new()
function NativeTile($name,$x,$y){
 $source=[System.Drawing.Bitmap]::new([string](Resolve-Path (Join-Path $assetDir "$name.png")).Path)
 $tile=[System.Drawing.Bitmap]::new($size,$size)
 $g=[System.Drawing.Graphics]::FromImage($tile)
 $g.DrawImageUnscaled($source,-$x,-$y)
 $g.Dispose();$source.Dispose()
 $manifest.Add(@{source="$name.png";x=$x;y=$y;width=$size;height=$size;resampling=$false})
 return $tile
}
function RotateTile($tile,$direction){
 $result=[System.Drawing.Bitmap]$tile.Clone()
 $rotation=@([System.Drawing.RotateFlipType]::RotateNoneFlipNone,[System.Drawing.RotateFlipType]::Rotate90FlipNone,[System.Drawing.RotateFlipType]::Rotate180FlipNone,[System.Drawing.RotateFlipType]::Rotate270FlipNone)
 $result.RotateFlip($rotation[$direction]);return $result
}
$grass=NativeTile 'ground_grass_soil' 400 400
$mountain=NativeTile 'mountain_rock' 400 400
$stone=NativeTile 'cliff_edge_overlay' 200 470
$straight=NativeTile 'cliff_edge_overlay' 200 430
$outer=NativeTile 'cliff_outer_corner' 450 450
$inner=NativeTile 'cliff_inner_corner' 836 836
$endcap=NativeTile 'cliff_endcap' 836 430
$rampSource=NativeTile 'ramp_user_reference' 418 400
function MakeCliff($face){
 return [Lab5RampSpriteAtlas]::Cliff($stone,[string]$face.shape,[int]$face.cornerMask)
}
$atlas=@{}
$bitmap=[System.Drawing.Bitmap]::new(10*$size,10*$size)
$graphics=[System.Drawing.Graphics]::FromImage($bitmap)
foreach($cell in $data.cells){
 $x=$cell.x*$size;$y=$cell.y*$size
 $ground=$grass;if($cell.mountain){$ground=$mountain}
 $graphics.DrawImageUnscaled($ground,$x,$y)
 $tile=$null
 if($cell.rampTile){
  $r=$cell.rampTile;$key=$r.spriteKey
  if(!$atlas.ContainsKey($key)){
   if($r.shape -eq 'middle_corner'){$atlas[$key]=[Lab5RampSpriteAtlas]::Corner($rampSource,[int]$r.cornerMask)}else{$atlas[$key]=[Lab5RampSpriteAtlas]::Straight($rampSource,[int]$r.direction,[string]$r.widthRole)}
  }
  $tile=$atlas[$key]
 }elseif($cell.face -and !$cell.face.open){
  $key=$cell.face.spriteKey
  if(!$atlas.ContainsKey($key)){$atlas[$key]=MakeCliff $cell.face}
  $tile=$atlas[$key]
 }
 if($tile){
  if($tile.Width -ne $size -or $tile.Height -ne $size){throw 'Sprite dimensions must equal cell dimensions'}
  $graphics.DrawImageUnscaled($tile,$x,$y)
 }
}
$bitmap.Save((Join-Path $assetDir 'sample_10x10.png'),[System.Drawing.Imaging.ImageFormat]::Png)
# Diagnostic overlay uses actual level boundaries, independent of cliff artwork.
$marked=[System.Drawing.Bitmap]$bitmap.Clone()
$mg=[System.Drawing.Graphics]::FromImage($marked)
$gridPen=[System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(90,255,255,255),2)
$boundaryPen=[System.Drawing.Pen]::new([System.Drawing.Color]::Cyan,12)
$rampPen=[System.Drawing.Pen]::new([System.Drawing.Color]::Red,10)
$arrowPen=[System.Drawing.Pen]::new([System.Drawing.Color]::Yellow,12)
$arrowPen.EndCap=[System.Drawing.Drawing2D.LineCap]::ArrowAnchor
$markFont=[System.Drawing.Font]::new('Consolas',32,[System.Drawing.FontStyle]::Bold)
$byXY=@{};foreach($c in $data.cells){$byXY["$($c.x),$($c.y)"]=$c}
foreach($c in $data.cells){
 $x=$c.x*$size;$y=$c.y*$size
 $mg.DrawRectangle($gridPen,$x,$y,$size-1,$size-1)
 $east=$byXY["$($c.x+1),$($c.y)"];$south=$byXY["$($c.x),$($c.y+1)"]
 if($east -and $east.level -ne $c.level){$mg.DrawLine($boundaryPen,$x+$size,$y,$x+$size,$y+$size)}
 if($south -and $south.level -ne $c.level){$mg.DrawLine($boundaryPen,$x,$y+$size,$x+$size,$y+$size)}
}
$number=0
foreach($c in $data.cells){
 if(!$c.rampTile){continue};$number++
 $x=$c.x*$size;$y=$c.y*$size;$r=$c.rampTile
 $mg.DrawRectangle($rampPen,$x+8,$y+8,$size-16,$size-16)
 $dx=$r.globalDirection[0];$dy=$r.globalDirection[1]
 $norm=[Math]::Sqrt($dx*$dx+$dy*$dy);if($norm -gt 0){$dx/=$norm;$dy/=$norm}
 $cx=$x+$size/2;$cy=$y+$size/2
 $mg.DrawLine($arrowPen,[single]$cx,[single]$cy,[single]($cx+$dx*150),[single]($cy+$dy*150))
 $mg.FillRectangle([System.Drawing.Brushes]::Black,$x+16,$y+16,240,104)
 $mg.DrawString("G$($r.groupId) W$($r.groupWidth)",$markFont,[System.Drawing.Brushes]::White,[single]($x+18),[single]($y+18))
 $mg.DrawString("L$($r.lowLevel)->$($r.highLevel)",$markFont,[System.Drawing.Brushes]::Yellow,[single]($x+18),[single]($y+68))
}
$marked.Save((Join-Path $assetDir 'sample_10x10_ramp_marked.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$mg.Dispose();$marked.Dispose();$gridPen.Dispose();$boundaryPen.Dispose();$rampPen.Dispose();$arrowPen.Dispose();$markFont.Dispose()
$font=[System.Drawing.Font]::new('Consolas',24)
$pen=[System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(160,255,255,255),3)
foreach($cell in $data.cells){
 $x=$cell.x*$size;$y=$cell.y*$size
 $graphics.DrawRectangle($pen,$x,$y,$size-1,$size-1)
 $label="L$($cell.level)"
 if($cell.rampTile){$label+=" $($cell.rampTile.shape)"}elseif($cell.face -and !$cell.face.open){$label+=" $($cell.face.shape)"}
 $graphics.FillRectangle([System.Drawing.Brushes]::Black,$x,$y,$size,48)
 $graphics.DrawString($label,$font,[System.Drawing.Brushes]::White,[single]($x+4),[single]($y+4))
}
$bitmap.Save((Join-Path $assetDir 'sample_10x10_debug.png'),[System.Drawing.Imaging.ImageFormat]::Png)
foreach($entry in $atlas.GetEnumerator()){
 $entry.Value.Save((Join-Path $atlasDir ($entry.Key.Replace('/','_')+'.png')),[System.Drawing.Imaging.ImageFormat]::Png)
 $entry.Value.Dispose()
}
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $atlasDir 'manifest.json') -Encoding UTF8
$font.Dispose();$pen.Dispose();$graphics.Dispose();$bitmap.Dispose()
foreach($image in @($grass,$mountain,$stone,$straight,$outer,$inner,$endcap,$rampSource)){$image.Dispose()}
Write-Output "Seed $($data.seed): native 418x418 sprite pieces; no resizing."
