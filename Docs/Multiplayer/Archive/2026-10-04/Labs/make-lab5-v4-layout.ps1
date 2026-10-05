Add-Type -AssemblyName System.Drawing
$s=256;$b=[Drawing.Bitmap]::new(1024,1024);$g=[Drawing.Graphics]::FromImage($b)
$grass=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(78,103,39))
$dirt=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(166,130,81))
$rock=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(93,91,84))
$g.FillRectangle($grass,0,0,1024,1024)
function Cliff($x,$y){$g.FillRectangle($dirt,$x,$y+128,256,88);$g.FillRectangle($rock,$x,$y+128,256,52)}
Cliff 256 0;Cliff 512 0;$g.FillRectangle($dirt,768,0,256,256)
# Both corner ports are fixed on the right/bottom; inner has inverted material sides.
foreach($x in @(0)){
 $g.FillRectangle($dirt,$x+128,384,128,128)
 $g.FillRectangle($rock,$x+128,384,128,52)
 $g.FillRectangle($rock,$x+128,384,52,128)
}
$g.FillRectangle($dirt,256,256,128,128)
$g.FillRectangle($rock,256,332,128,52)
$g.FillRectangle($rock,332,256,52,128)
Cliff 512 256;Cliff 768 256
$g.FillRectangle($dirt,640,346,128,124);$g.FillRectangle($dirt,768,346,128,124)
# One-cell slope has both shoulders, wide pieces only have their exterior shoulder.
for($c=0;$c -lt 4;$c++){
 $x=$c*256;$y=512
 if($c -ne 2){Cliff $x $y}
 if($c -eq 0){$g.FillRectangle($dirt,$x+64,$y+90,128,124)}
 if($c -eq 1){$g.FillRectangle($dirt,$x+64,$y+90,192,124)}
 if($c -eq 2){$g.FillRectangle($dirt,$x,$y+90,256,124)}
 if($c -eq 3){$g.FillRectangle($dirt,$x,$y+90,192,124)}
}
foreach($x in @(0,256)){$g.FillRectangle($dirt,$x+90,858,166,124);$g.FillRectangle($dirt,$x+90,858,124,166)}
$g.FillRectangle($dirt,512,858,256,124)
Cliff 768 768;$g.FillRectangle($dirt,832,858,128,124)
$b.Save((Join-Path $PSScriptRoot '../../../Assets/Art/Terrain/Lab5TextureReview_v4/layout.png'),[Drawing.Imaging.ImageFormat]::Png)
foreach($o in @($g,$b,$grass,$dirt,$rock)){$o.Dispose()}
