$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$dir=Join-Path $PSScriptRoot '../../../Assets/Art/Terrain/Lab5TextureReview_v4'
$atlas=[Drawing.Bitmap]::new((Join-Path $dir 'atlas.png'))
if($atlas.Width -ne $atlas.Height){throw 'Atlas must be square'}
$s=[int][Math]::Floor($atlas.Width/4)
$names=@('ground_grass','cliff_straight','cliff_straight_repeat','ground_dirt','cliff_outer_corner','cliff_inner_corner','cliff_end_left','cliff_end_right','ramp_single','ramp_left','ramp_middle','ramp_right','ramp_middle_outer_corner','ramp_middle_inner_corner','ramp_middle_repeat','ramp_single_repeat')
$tiles=@{};$rects=@()
for($i=0;$i -lt 16;$i++){
 $x=[int][Math]::Round(($i%4)*$atlas.Width/4);$y=[int][Math]::Round([Math]::Floor($i/4)*$atlas.Height/4)
 $rect=[Drawing.Rectangle]::new($x,$y,$s,$s)
 $tile=$atlas.Clone($rect,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
 $tile.Save((Join-Path $dir ($names[$i]+'.png')),[Drawing.Imaging.ImageFormat]::Png)
 $tiles[$names[$i]]=$tile;$rects+=@{name=$names[$i];x=$x;y=$y;width=$s;height=$s}
}
$rects | ConvertTo-Json | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $dir 'atlas_rects.json')
$recipes=@(
 @{label='STRAIGHT REPEAT';keys=@('cliff_straight','cliff_straight','cliff_straight','cliff_straight','cliff_straight')},
 @{label='CLIFF + SINGLE RAMP + CLIFF';keys=@('cliff_straight','cliff_straight','ramp_single','cliff_straight','cliff_straight')},
 @{label='CLIFF + LEFT / MIDDLE / RIGHT + CLIFF';keys=@('cliff_straight','ramp_left','ramp_middle','ramp_right','cliff_straight')},
 @{label='WIDE RAMP: LEFT + 3 MIDDLE + RIGHT';keys=@('ramp_left','ramp_middle','ramp_middle','ramp_middle','ramp_right')},
 @{label='TERMINALS + SINGLE + TERMINALS';keys=@('cliff_straight','cliff_end_left','ramp_single','cliff_end_right','cliff_straight')}
)
$font=[Drawing.Font]::new('Consolas',16)
$board=[Drawing.Bitmap]::new(5*$s,5*($s+30));$g=[Drawing.Graphics]::FromImage($board);$g.Clear([Drawing.Color]::FromArgb(26,26,26))
for($r=0;$r -lt $recipes.Count;$r++){
 $g.DrawString($recipes[$r].label,$font,[Drawing.Brushes]::White,5,[single]($r*($s+30)))
 for($c=0;$c -lt 5;$c++){$g.DrawImageUnscaled($tiles[$recipes[$r].keys[$c]],$c*$s,$r*($s+30)+30)}
}
$board.Save((Join-Path $dir 'joins_straight_ramp.png'),[Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$board.Dispose()
# Corner mosaics show every orientation without stretching any sprite.
$cornerBoard=[Drawing.Bitmap]::new(12*$s,6*$s+60);$g=[Drawing.Graphics]::FromImage($cornerBoard);$g.Clear([Drawing.Color]::FromArgb(26,26,26))
$rotations=@([Drawing.RotateFlipType]::RotateNoneFlipNone,[Drawing.RotateFlipType]::Rotate90FlipNone,[Drawing.RotateFlipType]::Rotate180FlipNone,[Drawing.RotateFlipType]::Rotate270FlipNone)
for($kind=0;$kind -lt 2;$kind++){
 $name=@('cliff_outer_corner','cliff_inner_corner')[$kind]
 $g.DrawString($name+' + straight, rotations 0 / 90 / 180 / 270',$font,[Drawing.Brushes]::White,5,[single]($kind*(3*$s+30)))
 $m=[Drawing.Bitmap]::new(3*$s,3*$s);$mg=[Drawing.Graphics]::FromImage($m)
 for($y=0;$y -lt 3;$y++){for($x=0;$x -lt 3;$x++){$mg.DrawImageUnscaled($tiles['ground_grass'],$x*$s,$y*$s)}}
 $mg.DrawImageUnscaled($tiles[$name],$s,$s)
 $horizontal=[Drawing.Bitmap]$tiles['cliff_straight'].Clone();if($kind -eq 1){$horizontal.RotateFlip([Drawing.RotateFlipType]::Rotate180FlipNone)}
 $hx=if($kind -eq 0){2*$s}else{0};$mg.DrawImageUnscaled($horizontal,$hx,$s);$horizontal.Dispose()
 $vertical=[Drawing.Bitmap]$tiles['cliff_straight'].Clone();$turn=if($kind -eq 0){[Drawing.RotateFlipType]::Rotate270FlipNone}else{[Drawing.RotateFlipType]::Rotate90FlipNone};$vertical.RotateFlip($turn)
 $vy=if($kind -eq 0){2*$s}else{0};$mg.DrawImageUnscaled($vertical,$s,$vy);$vertical.Dispose();$mg.Dispose()
 for($d=0;$d -lt 4;$d++){$copy=[Drawing.Bitmap]$m.Clone();$copy.RotateFlip($rotations[$d]);$g.DrawImageUnscaled($copy,$d*3*$s,$kind*(3*$s+30)+30);$copy.Dispose()}
 $m.Dispose()
}
$cornerBoard.Save((Join-Path $dir 'joins_corners.png'),[Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$cornerBoard.Dispose();$font.Dispose();$atlas.Dispose()
foreach($tile in $tiles.Values){$tile.Dispose()}
Write-Output "16 native $s-square slices; raw joins, no scaling, blending or color fixes."
