$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=Join-Path $PSScriptRoot '../../../Assets/Art/Terrain'
$data=Get-Content -Raw -LiteralPath (Join-Path $root 'Lab5FlatYTextures_v2/sample_10x10.json') | ConvertFrom-Json
$s=90;$bitmap=[Drawing.Bitmap]::new(900,960);$g=[Drawing.Graphics]::FromImage($bitmap)
$g.Clear([Drawing.Color]::FromArgb(25,28,32))
$font=[Drawing.Font]::new('Consolas',12);$small=[Drawing.Font]::new('Consolas',9)
$boundary=[Drawing.Pen]::new([Drawing.Color]::Cyan,4)
$cliff=[Drawing.Pen]::new([Drawing.Color]::Orange,5)
$ports=[Drawing.Pen]::new([Drawing.Color]::White,3)
$ramp=[Drawing.Pen]::new([Drawing.Color]::Red,5)
$arrow=[Drawing.Pen]::new([Drawing.Color]::Yellow,3);$arrow.EndCap=[Drawing.Drawing2D.LineCap]::ArrowAnchor
$by=@{};foreach($c in $data.cells){$by["$($c.x),$($c.y)"]=$c}
foreach($c in $data.cells){
 $x=$c.x*$s;$y=$c.y*$s
 $shade=60+([int]$c.level*25)%140;$brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb($shade,$shade+15,$shade))
 $g.FillRectangle($brush,$x,$y,$s,$s);$brush.Dispose()
 $g.DrawRectangle([Drawing.Pens]::Gray,$x,$y,$s,$s)
 $g.DrawString("L$($c.level) #$($c.globalCell)",$small,[Drawing.Brushes]::White,[single]($x+7),[single]($y+7))
 if($c.face){
  $g.DrawRectangle($cliff,$x+4,$y+4,$s-8,$s-8)
  $label="R$($c.face.rotation) P$($c.face.ports)"
  for($d=0;$d -lt 4;$d++){if(([int]$c.face.ports -band (1 -shl $d)) -ne 0){
   $dx=@(0,1,0,-1)[$d];$dy=@(-1,0,1,0)[$d]
   $g.DrawLine($ports,[single]($x+45),[single]($y+45),[single]($x+45+$dx*45),[single]($y+45+$dy*45))
  }}
  $g.DrawString($label,$small,[Drawing.Brushes]::Orange,[single]($x+7),[single]($y+24))
 }
 if($c.rampTile){
  $r=$c.rampTile;$g.DrawRectangle($ramp,$x+8,$y+8,$s-16,$s-16)
  $g.DrawString("R G$($r.groupId)",$font,[Drawing.Brushes]::Red,[single]($x+12),[single]($y+42))
  $g.DrawLine($arrow,[single]($x+45),[single]($y+72),[single]($x+45+$r.globalDirection[0]*22),[single]($y+72+$r.globalDirection[1]*22))
 }
}
foreach($c in $data.cells){
 $x=$c.x*$s;$y=$c.y*$s;$e=$by["$($c.x+1),$($c.y)"];$b=$by["$($c.x),$($c.y+1)"]
 if($e -and $e.level -ne $c.level){$g.DrawLine($boundary,$x+$s,$y,$x+$s,$y+$s)}
 if($b -and $b.level -ne $c.level){$g.DrawLine($boundary,$x,$y+$s,$x+$s,$y+$s)}
}
$g.DrawString('CYAN = height boundary | ORANGE = high cliff | RED = ramp | WHITE = cliff ports',$font,[Drawing.Brushes]::White,8,910)
$g.DrawString("Seed $($data.seed), crop $($data.crop.x),$($data.crop.y); arrows: low -> high",$font,[Drawing.Brushes]::White,8,935)
$bitmap.Save((Join-Path $root 'Lab5TextureReview_v3/high_side_cell_audit.png'),[Drawing.Imaging.ImageFormat]::Png)
foreach($o in @($g,$bitmap,$font,$small,$boundary,$cliff,$ramp,$arrow,$ports)){$o.Dispose()}
