$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=Join-Path $PSScriptRoot '../../../Assets/Art/Terrain/Lab5TextureReview_v8'
$data=Get-Content (Join-Path $root 'sample_10x10.json') -Raw | ConvertFrom-Json
$n=120;$b=[System.Drawing.Bitmap]::new(1200,1200);$g=[System.Drawing.Graphics]::FromImage($b)
$high=[System.Drawing.SolidBrush]::new([System.Drawing.Color]::ForestGreen)
$low=[System.Drawing.SolidBrush]::new([System.Drawing.Color]::SaddleBrown)
$ground=[System.Drawing.SolidBrush]::new([System.Drawing.Color]::DarkOliveGreen)
$line=[System.Drawing.Pen]::new([System.Drawing.Color]::White,5)
$grid=[System.Drawing.Pen]::new([System.Drawing.Color]::Gray,1)
$font=[System.Drawing.Font]::new('Consolas',10)
$d=@(@(0,-1),@(1,0),@(0,1),@(-1,0))
$q=@(@(0,0),@(1,0),@(1,1),@(0,1))
foreach($c in $data.cells){
 $x=$c.x*$n;$y=$c.y*$n;$g.FillRectangle($ground,$x,$y,$n,$n)
 if($c.face){
  foreach($part in $c.face.parts){
   $s=$part.surfaceSides
   for($k=0;$k -lt 4;$k++){
    $brush=if(([int]$s.highQuadrants -band (1 -shl $k)) -ne 0){$high}else{$low}
    $g.FillRectangle($brush,$x+$q[$k][0]*60,$y+$q[$k][1]*60,60,60)
   }
  }
  foreach($a in $c.face.connections){$v=$d[[int]$a.direction];$g.DrawLine($line,$x+60,$y+60,$x+60+$v[0]*60,$y+60+$v[1]*60)}
  $g.DrawString("H$($c.face.highLevel) / L$($c.face.lowLevel)",$font,[System.Drawing.Brushes]::White,[single]($x+4),[single]($y+4))
 }
 if($c.rampTile){
  $r=$c.rampTile;$v=$r.globalDirection
  $g.DrawRectangle([System.Drawing.Pens]::Red,$x+2,$y+2,116,116)
  $g.DrawLine([System.Drawing.Pens]::Yellow,$x+60,$y+60,$x+60+$v[0]*40,$y+60+$v[1]*40)
  $g.DrawString('RAMP UP',$font,[System.Drawing.Brushes]::Yellow,[single]($x+4),[single]($y+100))
 }
 if($c.cliffJoin){$g.DrawString('JOIN walk',$font,[System.Drawing.Brushes]::Cyan,[single]($x+4),[single]($y+82))}
 $g.DrawRectangle($grid,$x,$y,$n-1,$n-1)
}
$b.Save((Join-Path $root 'compiled/sample_10x10_surface_contract.png'))
foreach($o in @($g,$b,$high,$low,$ground,$line,$grid,$font)){$o.Dispose()}
