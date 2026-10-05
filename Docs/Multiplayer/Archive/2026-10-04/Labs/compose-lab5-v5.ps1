$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$dir=Join-Path $PSScriptRoot '../../../Assets/Art/Terrain/Lab5TextureReview_v5'
$src=[Drawing.Bitmap]::new((Join-Path $dir 'source_atlas.png'))
$s=[int]($src.Width/3);if($s%2){throw 'Even cell resolution required'}
$half=[int]($s/2);$rim=$half;$toe=[int]($s*.7);$bottom=[int]($s*.82);$landing=[int]($s*.35)
$source=@{}
foreach($entry in @(@('grass',0,0),@('straight',1,0),@('dirt',2,1),@('middle',2,2))){
 $source[$entry[0]]=$src.Clone([Drawing.Rectangle]::new([int]$entry[1]*$s,[int]$entry[2]*$s,$s,$s),[Drawing.Imaging.PixelFormat]::Format32bppArgb)
}
function Periodic($inputTile){
 $t=[Drawing.Bitmap]::new($s,$s);$g=[Drawing.Graphics]::FromImage($t)
 # Native copies with reflection make opposite borders identical; no resizing.
 $q=$inputTile.Clone([Drawing.Rectangle]::new(0,0,$half,$half),[Drawing.Imaging.PixelFormat]::Format32bppArgb)
 foreach($xy in @(@(0,0),@(1,0),@(0,1),@(1,1))){
  $copy=[Drawing.Bitmap]$q.Clone()
  if($xy[0]){$copy.RotateFlip([Drawing.RotateFlipType]::RotateNoneFlipX)}
  if($xy[1]){$copy.RotateFlip([Drawing.RotateFlipType]::RotateNoneFlipY)}
  $g.DrawImageUnscaled($copy,$xy[0]*$half,$xy[1]*$half);$copy.Dispose()
 }
 $q.Dispose();$g.Dispose();return $t
}
$grass=Periodic $source.grass;$dirt=Periodic $source.dirt
# Shared short slope shading and landing alpha: identical for every ramp edge.
for($y=0;$y -lt $s;$y++){for($x=0;$x -lt $s;$x++){
 $c=$dirt.GetPixel($x,$y);$alpha=255
 if($y -ge $landing -and $y -lt $landing+12){$alpha=[int](255*($y-$landing)/12)}
 if($y -ge $bottom-12 -and $y -lt $bottom){$alpha=[int](255*($bottom-1-$y)/12)}
 $shade=if($y -ge $rim -and $y -lt $toe){[int](22*($y-$rim)/($toe-$rim))}else{0}
 $dirt.SetPixel($x,$y,[Drawing.Color]::FromArgb($alpha,[Math]::Max(0,$c.R-$shade),[Math]::Max(0,$c.G-$shade),[Math]::Max(0,$c.B-$shade)))
}}

$canonical=[Drawing.Bitmap]::new($s,$s);$g=[Drawing.Graphics]::FromImage($canonical)
# Copy the GENERATED stone band at native size into the shared face strip.
$wall=$source.straight.Clone([Drawing.Rectangle]::new(0,$rim,$half,$toe-$rim),[Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g.DrawImageUnscaled($wall,0,$rim);$wall.RotateFlip([Drawing.RotateFlipType]::RotateNoneFlipX);$g.DrawImageUnscaled($wall,$half,$rim);$wall.Dispose()
$g.SetClip([Drawing.Rectangle]::new(0,$toe,$s,$bottom-$toe));$g.DrawImageUnscaled($dirt,0,0);$g.ResetClip();$g.Dispose()
$rot=@();foreach($r in @([Drawing.RotateFlipType]::RotateNoneFlipNone,[Drawing.RotateFlipType]::Rotate90FlipNone,[Drawing.RotateFlipType]::Rotate180FlipNone,[Drawing.RotateFlipType]::Rotate270FlipNone)){$t=[Drawing.Bitmap]$canonical.Clone();$t.RotateFlip($r);$rot+=,$t}
$tiles=@{ground_grass=$grass;ground_dirt=$dirt;cliff_straight=$canonical}
function NewTile(){return [Drawing.Bitmap]::new($s,$s)}
function Region($g,$image,$x,$y,$w,$h){$g.SetClip([Drawing.Rectangle]::new($x,$y,$w,$h));$g.DrawImageUnscaled($image,0,0);$g.ResetClip()}
$t=NewTile;$g=[Drawing.Graphics]::FromImage($t)
Region $g $rot[0] 0 $rim $half ($bottom-$rim)
Region $g $rot[3] $rim 0 ($bottom-$rim) $half
# Shared intersection, low southeast of convex high northwest corner.
Region $g $dirt $half $half ($bottom-$half) ($bottom-$half)
Region $g $canonical $half $half ($toe-$half) ($toe-$half)
$g.Dispose();$tiles.cliff_outer_corner=$t
$t=NewTile;$g=[Drawing.Graphics]::FromImage($t)
Region $g $rot[2] 0 ($s-$bottom) $half ($bottom-$rim)
Region $g $rot[1] ($s-$bottom) 0 ($bottom-$rim) $half
$g.Dispose();$tiles.cliff_inner_corner=$t
foreach($role in @('single','left','middle','right')){
 $t=NewTile;$g=[Drawing.Graphics]::FromImage($t)
 if($role -ne 'middle'){$g.DrawImageUnscaled($canonical,0,0)}
 $left=if($role -in @('single','left')){[int]($s*.25)}else{0}
 $right=if($role -in @('single','right')){[int]($s*.75)}else{$s}
 $g.CompositingMode=[Drawing.Drawing2D.CompositingMode]::SourceCopy
 $g.FillRectangle([Drawing.Brushes]::Transparent,$left,0,$right-$left,$s)
 $g.CompositingMode=[Drawing.Drawing2D.CompositingMode]::SourceOver
 Region $g $dirt $left $landing ($right-$left) ($bottom-$landing)
 $g.Dispose();$tiles['ramp_'+$role]=$t
}
foreach($kind in @('outer','inner')){
 $t=NewTile;$g=[Drawing.Graphics]::FromImage($t);$g.CompositingMode=[Drawing.Drawing2D.CompositingMode]::SourceCopy
 $h=[Drawing.Bitmap]$tiles.ramp_middle.Clone();$v=[Drawing.Bitmap]$tiles.ramp_middle.Clone()
 if($kind -eq 'outer'){$v.RotateFlip([Drawing.RotateFlipType]::Rotate270FlipNone)}
 else{$h.RotateFlip([Drawing.RotateFlipType]::Rotate180FlipNone);$v.RotateFlip([Drawing.RotateFlipType]::Rotate90FlipNone)}
 for($y=0;$y -lt $s;$y++){for($x=0;$x -lt $half;$x++){$t.SetPixel($x,$y,$h.GetPixel($x,$y))}}
 for($y=0;$y -lt $half;$y++){for($x=$half;$x -lt $s;$x++){$t.SetPixel($x,$y,$v.GetPixel($x,$y))}}
 $g.Dispose();$h.Dispose();$v.Dispose();$tiles['ramp_middle_'+$kind+'_corner']=$t
}
# Boundary test compares RGBA exactly; no tolerance or concealed blending.
$checks=@()
function Check($a,$sideA,$b,$sideB,$label){
 $errors=0
 for($k=0;$k -lt $s;$k++){
  $ca=if($sideA -eq 'left'){$a.GetPixel(0,$k)}elseif($sideA -eq 'right'){$a.GetPixel($s-1,$k)}elseif($sideA -eq 'top'){$a.GetPixel($k,0)}else{$a.GetPixel($k,$s-1)}
  $cb=if($sideB -eq 'left'){$b.GetPixel(0,$k)}elseif($sideB -eq 'right'){$b.GetPixel($s-1,$k)}elseif($sideB -eq 'top'){$b.GetPixel($k,0)}else{$b.GetPixel($k,$s-1)}
  if($ca.ToArgb() -ne $cb.ToArgb()){$errors++}
 }
 if($errors){throw "Border mismatch $label : $errors"}
 return @{join=$label;pixels=$s;mismatches=$errors}
}
$checks+=Check $canonical right $tiles.ramp_single left 'cliff -> single'
$checks+=Check $tiles.ramp_single right $canonical left 'single -> cliff'
$checks+=Check $canonical right $tiles.ramp_left left 'cliff -> left'
$checks+=Check $tiles.ramp_left right $tiles.ramp_middle left 'left -> middle'
$checks+=Check $tiles.ramp_middle right $tiles.ramp_middle left 'middle -> middle'
$checks+=Check $tiles.ramp_middle right $tiles.ramp_right left 'middle -> right'
$checks+=Check $tiles.ramp_right right $canonical left 'right -> cliff'
$checks+=Check $tiles.cliff_outer_corner left $canonical right 'outer left port'
$checks+=Check $tiles.cliff_outer_corner top $rot[3] bottom 'outer top port'
$checks+=Check $tiles.cliff_inner_corner left $rot[2] right 'inner left port'
$checks+=Check $tiles.cliff_inner_corner top $rot[1] bottom 'inner top port'
$checks+=Check $tiles.ramp_middle_outer_corner left $tiles.ramp_middle right 'outer ramp middle port'
$reverse=[Drawing.Bitmap]$tiles.ramp_middle.Clone();$reverse.RotateFlip([Drawing.RotateFlipType]::Rotate180FlipNone)
$checks+=Check $tiles.ramp_middle_inner_corner left $reverse right 'inner ramp middle port';$reverse.Dispose()
foreach($name in $tiles.Keys){$tiles[$name].Save((Join-Path $dir ($name+'.png')),[Drawing.Imaging.ImageFormat]::Png)}
$checks | ConvertTo-Json | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $dir 'edge_checks.json')
$rows=@(@('cliff_straight','cliff_straight','ramp_single','cliff_straight','cliff_straight'),@('cliff_straight','ramp_left','ramp_middle','ramp_right','cliff_straight'),@('ramp_left','ramp_middle','ramp_middle','ramp_middle','ramp_right'))
$board=[Drawing.Bitmap]::new(5*$s,3*$s);$g=[Drawing.Graphics]::FromImage($board)
for($y=0;$y -lt 3;$y++){for($x=0;$x -lt 5;$x++){$g.DrawImageUnscaled($grass,$x*$s,$y*$s);$g.DrawImageUnscaled($tiles[$rows[$y][$x]],$x*$s,$y*$s)}}
$board.Save((Join-Path $dir 'joined_preview.png'),[Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$board.Dispose()
$cornerBoard=[Drawing.Bitmap]::new(6*$s,3*$s);$g=[Drawing.Graphics]::FromImage($cornerBoard)
for($y=0;$y -lt 3;$y++){for($x=0;$x -lt 6;$x++){$g.DrawImageUnscaled($grass,$x*$s,$y*$s)}}
$g.DrawImageUnscaled($tiles.cliff_outer_corner,$s,$s);$g.DrawImageUnscaled($canonical,0,$s);$g.DrawImageUnscaled($rot[3],$s,0)
$g.DrawImageUnscaled($tiles.cliff_inner_corner,4*$s,$s);$g.DrawImageUnscaled($rot[2],3*$s,$s);$g.DrawImageUnscaled($rot[1],4*$s,0)
$cornerBoard.Save((Join-Path $dir 'corner_preview.png'),[Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$cornerBoard.Dispose()
Write-Output "13 shared RGBA edge tests passed; native $s-pixel sprites; source normals N-high to S-low."
foreach($t in $tiles.Values){$t.Dispose()};foreach($t in $rot){$t.Dispose()};foreach($t in $source.Values){$t.Dispose()};$src.Dispose()
