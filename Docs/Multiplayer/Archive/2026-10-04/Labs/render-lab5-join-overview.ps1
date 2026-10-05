$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$out=Join-Path $PSScriptRoot '../../../Assets/Art/Terrain/Lab5TextureReview_v3/seam-audit'
$data=Get-Content -LiteralPath (Join-Path $out 'results.json') -Raw | ConvertFrom-Json
$items=@(
 @('recipe_cliff_repeat_0','01 Straight cliff repeat'),
 @('recipe_cliff_corner_outer_dir_0','02 Outer corner + straight'),
 @('recipe_cliff_corner_inner_dir_0','03 Inner corner + straight'),
 @('recipe_cliff_ramp_3_dir_0','04 Cliff + ramp + cliff'),
 @('recipe_width_3_dir_0','05 Left + middle + right'),
 @('recipe_corner_ramp_middle_inner_corner_dir_0','06 Inner middle + shoulders')
)
$board=[System.Drawing.Bitmap]::new(1440,1120);$g=[System.Drawing.Graphics]::FromImage($board)
$g.Clear([System.Drawing.Color]::FromArgb(30,32,34));$g.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$font=[System.Drawing.Font]::new('Segoe UI',18);$title=[System.Drawing.Font]::new('Segoe UI',26,[System.Drawing.FontStyle]::Bold)
$g.DrawString('V3 - RAW TEXTURE JOIN CHECK',$title,[System.Drawing.Brushes]::White,20,10)
$g.DrawString("$($data.Count) cases in the full gallery. These joins are not repaired or blended.",$font,[System.Drawing.Brushes]::White,20,58)
for($i=0;$i -lt $items.Count;$i++){
 $item=$data | Where-Object {$_.type -eq $items[$i][0]} | Select-Object -First 1
 if(!$item){throw "Missing review case $($items[$i][0])"}
 $x=20+($i%3)*470;$y=110+[Math]::Floor($i/3)*500
 $image=[System.Drawing.Bitmap]::new((Join-Path $out $item.file))
 $ratio=[Math]::Min(440.0/$image.Width,400.0/$image.Height);$w=[int]($image.Width*$ratio);$h=[int]($image.Height*$ratio)
 $g.DrawImage($image,[System.Drawing.Rectangle]::new([int]($x+(440-$w)/2),[int]($y+(400-$h)/2),$w,$h));$image.Dispose()
 $g.DrawString($items[$i][1],$font,[System.Drawing.Brushes]::White,[single]$x,[single]($y+410))
 $g.DrawString("Case #$($item.id) / edge RGB jump: $($item.meanEdgeRgbJump)",$font,[System.Drawing.Brushes]::Yellow,[single]$x,[single]($y+442))
}
$board.Save((Join-Path $out 'join_overview.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$font.Dispose();$title.Dispose();$g.Dispose();$board.Dispose()
