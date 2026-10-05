param([ValidateRange(8,32)][int]$CellPixels=16)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=Join-Path $PSScriptRoot '../../../../Assets/Art/Terrain/Lab6'
$plan=Get-Content (Join-Path $root 'render_plan.json') -Raw | ConvertFrom-Json
$terrain=Get-Content (Join-Path $root 'terrain.json') -Raw | ConvertFrom-Json
$atlas=[System.Drawing.Bitmap]::new((Join-Path $root 'atlas.png'))
if($atlas.Width -ne 1254 -or $atlas.Height -ne 1254){throw 'Invalid atlas'}
$names=@('cliff_straight','cliff_outer_corner','cliff_inner_corner','ramp_single','ramp_left','ramp_middle','ramp_right','ground_grass','ground_soil')
$tiles=@{}
$rotations=@([System.Drawing.RotateFlipType]::RotateNoneFlipNone,[System.Drawing.RotateFlipType]::Rotate90FlipNone,[System.Drawing.RotateFlipType]::Rotate180FlipNone,[System.Drawing.RotateFlipType]::Rotate270FlipNone)
for($k=0;$k -lt 9;$k++){
 for($r=0;$r -lt 4;$r++){
  $native=$atlas.Clone([System.Drawing.Rectangle]::new(($k%3)*418,[int][Math]::Floor($k/3)*418,418,418),[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $native.RotateFlip($rotations[$r])
  $small=[System.Drawing.Bitmap]::new($CellPixels,$CellPixels)
  $sg=[System.Drawing.Graphics]::FromImage($small)
  $sg.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $sg.DrawImage($native,0,0,$CellPixels,$CellPixels)
  $sg.Dispose();$native.Dispose();$tiles["$($names[$k])/$r"]=$small
 }
}
$atlas.Dispose()
$b=[System.Drawing.Bitmap]::new([int]($plan.W*$CellPixels),[int]($plan.H*$CellPixels))
$g=[System.Drawing.Graphics]::FromImage($b)
$mountain=[System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(90,96,91))
$water=[System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(41,64,76))
for($i=0;$i -lt $plan.W*$plan.H;$i++){
 $x=($i%$plan.W)*$CellPixels;$y=[int][Math]::Floor($i/$plan.W)*$CellPixels
 $g.DrawImageUnscaled($tiles['ground_grass/0'],$x,$y)
 if($terrain.kind[$i] -eq 4){$g.FillRectangle($water,$x,$y,$CellPixels,$CellPixels)}
 if($terrain.mountainMask[$i]){$g.FillRectangle($mountain,$x,$y,$CellPixels,$CellPixels)}
}
foreach($d in $plan.draws){
 $x=($d.cell%$plan.W)*$CellPixels;$y=[int][Math]::Floor($d.cell/$plan.W)*$CellPixels
 $g.DrawImageUnscaled($tiles["$($d.source)/$($d.rotation)"],$x,$y)
}
$b.Save((Join-Path $root 'full_map.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$pen=[System.Drawing.Pen]::new([System.Drawing.Color]::Red,2)
foreach($d in $plan.draws){if($d.layer -eq 2){$g.DrawRectangle($pen,($d.cell%$plan.W)*$CellPixels,[int][Math]::Floor($d.cell/$plan.W)*$CellPixels,$CellPixels,$CellPixels)}}
$font=[System.Drawing.Font]::new('Arial',18,[System.Drawing.FontStyle]::Bold)
foreach($base in $terrain.bases){
 $x=($base.x+.5)*$CellPixels;$y=($base.y+.5)*$CellPixels
 $g.FillEllipse([System.Drawing.Brushes]::Black,$x-20,$y-20,40,40)
 $g.DrawString("P$([int]$base.id+1)",$font,[System.Drawing.Brushes]::White,$x-17,$y-14)
}
$b.Save((Join-Path $root 'full_map_marked.png'),[System.Drawing.Imaging.ImageFormat]::Png)
@{seed=$terrain.seed;grid=@($plan.W,$plan.H);imagePixels=@($b.Width,$b.Height);previewCellPixels=$CellPixels;nativeCellPixels=418;mountainArt='gray diagnostic fill';waterArt='blue diagnostic fill';artWarnings=$plan.warnings.Count} | ConvertTo-Json | Set-Content (Join-Path $root 'full_map_review.json') -Encoding utf8
foreach($o in @($g,$b,$pen,$font,$mountain,$water)){$o.Dispose()}
foreach($o in $tiles.Values){$o.Dispose()}
Write-Output "Full seeded map rendered: $($plan.W)x$($plan.H), preview $CellPixels pixels per cell; mountains/water use diagnostic fills."

