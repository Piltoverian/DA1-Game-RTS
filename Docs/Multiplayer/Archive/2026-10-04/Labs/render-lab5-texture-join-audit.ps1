$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @"
using System;using System.Drawing;
public static class Lab5JoinMetric {
 public static double RGB(Bitmap im,int s) {
  double sum=0;int count=0;
  for(int x=s;x<im.Width;x+=s)for(int y=0;y<im.Height;y++){Color a=im.GetPixel(x-1,y),b=im.GetPixel(x,y);sum+=Math.Abs(a.R-b.R)+Math.Abs(a.G-b.G)+Math.Abs(a.B-b.B);count+=3;}
  for(int y=s;y<im.Height;y+=s)for(int x=0;x<im.Width;x++){Color a=im.GetPixel(x,y-1),b=im.GetPixel(x,y);sum+=Math.Abs(a.R-b.R)+Math.Abs(a.G-b.G)+Math.Abs(a.B-b.B);count+=3;}
  return count>0?sum/count:0;
 }
}
"@ -ReferencedAssemblies System.Drawing
$assets=Join-Path $PSScriptRoot '../../../Assets/Art/Terrain/Lab5TextureReview_v3'
$out=Join-Path $assets 'seam-audit'
$data=Get-Content -LiteralPath (Join-Path $out 'cases.json') -Raw | ConvertFrom-Json
$caseDir=Join-Path $out 'cases';[System.IO.Directory]::CreateDirectory($caseDir) | Out-Null
$pageDir=Join-Path $out 'pages';[System.IO.Directory]::CreateDirectory($pageDir) | Out-Null
$size=128;$sources=@{};$tiles=@{}
foreach($d in $data.tiles){
 if(!$sources.ContainsKey($d.source)){
  $path=Join-Path $assets ($d.source+'.png')
  if($d.source -eq 'mountain_rock'){$path=Join-Path $PSScriptRoot '../../../Assets/Art/Terrain/Lab5FlatYTextures_v2/mountain_rock.png'}
  $src=[System.Drawing.Bitmap]::new([string](Resolve-Path $path).Path)
  $tile=[System.Drawing.Bitmap]::new($size,$size);$g=[System.Drawing.Graphics]::FromImage($tile)
  $g.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $attributes=[System.Drawing.Imaging.ImageAttributes]::new();$attributes.SetWrapMode([System.Drawing.Drawing2D.WrapMode]::TileFlipXY)
  $g.DrawImage($src,[System.Drawing.Rectangle]::new(0,0,$size,$size),0,0,$src.Width,$src.Height,[System.Drawing.GraphicsUnit]::Pixel,$attributes);$attributes.Dispose();$g.Dispose();$src.Dispose();$sources[$d.source]=$tile
 }
 $tile=[System.Drawing.Bitmap]$sources[$d.source].Clone()
 $rotation=@([System.Drawing.RotateFlipType]::RotateNoneFlipNone,[System.Drawing.RotateFlipType]::Rotate90FlipNone,[System.Drawing.RotateFlipType]::Rotate180FlipNone,[System.Drawing.RotateFlipType]::Rotate270FlipNone)
 $tile.RotateFlip($rotation[$d.direction]);$tiles[$d.key]=$tile
}
$results=[System.Collections.Generic.List[object]]::new()
foreach($c in $data.cases){
 $image=[System.Drawing.Bitmap]::new([int]($c.width*$size),[int]($c.height*$size));$g=[System.Drawing.Graphics]::FromImage($image)
 for($i=0;$i -lt $c.tileKeys.Count;$i++){
  $x=($i%$c.width)*$size;$y=[Math]::Floor($i/$c.width)*$size
  $g.DrawImageUnscaled($sources['ground_grass'],[int]$x,[int]$y)
  $g.DrawImageUnscaled($tiles[$c.tileKeys[$i]],[int]$x,[int]$y)
 }
 $g.Dispose();$delta=[Math]::Round([Lab5JoinMetric]::RGB($image,$size),2)
 $file=('{0:D4}.png' -f [int]$c.id)
 $image.Save((Join-Path $caseDir $file),[System.Drawing.Imaging.ImageFormat]::Png);$image.Dispose()
 $results.Add(@{id=$c.id;type=$c.type;file="cases/$file";tileKeys=$c.tileKeys;count=$c.count;first=$c.first;meanEdgeRgbJump=$delta})
}
$results | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $out 'results.json') -Encoding UTF8
$font=[System.Drawing.Font]::new('Consolas',12)
$title=[System.Drawing.Font]::new('Segoe UI',22,[System.Drawing.FontStyle]::Bold)
$pageIndex=[System.Collections.Generic.List[object]]::new()
foreach($category in @('pair','corner','recipe')){
 $items=@($results | Where-Object {$_.type.StartsWith($category)} | Sort-Object -Property @{Expression={[double]$_.meanEdgeRgbJump};Descending=$true})
 for($start=0;$start -lt $items.Count;$start+=16){
  $page=[int]($start/16+1);$board=[System.Drawing.Bitmap]::new(1360,1510);$g=[System.Drawing.Graphics]::FromImage($board)
  $g.Clear([System.Drawing.Color]::FromArgb(30,32,34));$g.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $g.DrawString("SEED 30000 / $category / PAGE $page",$title,[System.Drawing.Brushes]::White,20,12)
  $g.DrawString('RGB jump is an indicator, not a pass/fail test. Fallback variants are named in texture keys.',$font,[System.Drawing.Brushes]::White,20,53)
  for($k=0;$k -lt 16 -and $start+$k -lt $items.Count;$k++){
   $item=$items[$start+$k];$x=20+($k%4)*335;$y=85+[Math]::Floor($k/4)*350
   $image=[System.Drawing.Bitmap]::new((Join-Path $out $item.file))
   $ratio=[Math]::Min(310.0/$image.Width,255.0/$image.Height);$w=[int]($image.Width*$ratio);$h=[int]($image.Height*$ratio)
   $g.DrawImage($image,[System.Drawing.Rectangle]::new([int]($x+(310-$w)/2),[int]($y+(255-$h)/2),$w,$h));$image.Dispose()
   $displayType=$item.type;if($displayType.Length -gt 29){$displayType=$displayType.Substring(0,29)}
   $g.DrawString("#$($item.id) $displayType",$font,[System.Drawing.Brushes]::White,[single]$x,[single]($y+258))
   $g.DrawString("RGB=$($item.meanEdgeRgbJump) count=$($item.count)",$font,[System.Drawing.Brushes]::Yellow,[single]$x,[single]($y+280))
   $keys=($item.tileKeys -join ' | ').Replace('cliff_','C_').Replace('ramp_','R_').Replace('middle','mid').Replace('outer_corner','outer').Replace('inner_corner','inner');if($keys.Length -gt 85){$keys=$keys.Substring(0,85)}
   $g.DrawString($keys,$font,[System.Drawing.Brushes]::White,[System.Drawing.RectangleF]::new($x,$y+301,315,45))
  }
  $file="$category-$page.png";$board.Save((Join-Path $pageDir $file),[System.Drawing.Imaging.ImageFormat]::Png)
  $g.Dispose();$board.Dispose();$pageIndex.Add(@{category=$category;page=$page;file="pages/$file"})
 }
}
$pageIndex | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $out 'pages.json') -Encoding UTF8
$payload=$results | ConvertTo-Json -Depth 6 -Compress
$html=@'
<!doctype html><html lang="vi"><meta charset="utf-8"><title>Lab 5 texture join audit</title>
<style>body{background:#202225;color:#eee;font:16px system-ui;margin:24px}header{position:sticky;top:0;background:#202225;padding:12px;z-index:1}input,select{padding:8px;font:inherit}#grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(290px,1fr));gap:16px}.case{background:#303337;padding:12px;overflow-wrap:anywhere}.case img{width:100%;height:260px;object-fit:contain;background:#171819}small{display:block;color:#bbb;margin-top:8px}.fallback{color:#ffb174}</style>
<header><h1>Seed 30000 - texture join audit</h1><p>CASE_SUMMARY. Missing variants: fill, endcap. RGB jump is an indicator, not a compatibility certificate.</p><select id="type"><option value="">All</option><option value="pair">Edge pairs</option><option value="corner">2x2 corners</option><option value="recipe">Recipes</option></select> <input id="search" placeholder="ramp, cliff, endcap..."><span id="count"></span></header><main id="grid"></main>
<script>const data=PAYLOAD;function render(){const t=document.querySelector('#type').value,q=document.querySelector('#search').value.toLowerCase();const list=data.filter(c=>(!t||c.type.startsWith(t))&&JSON.stringify(c.tileKeys).toLowerCase().includes(q)).sort((a,b)=>b.meanEdgeRgbJump-a.meanEdgeRgbJump);document.querySelector('#count').textContent=' '+list.length+' cases';document.querySelector('#grid').innerHTML=list.map(c=>'<article class="case"><b>#'+c.id+' '+c.type+'</b><img loading="lazy" src="'+c.file+'"><div>RGB jump: '+c.meanEdgeRgbJump+' / count: '+c.count+'</div><small>Position: '+c.first.x+', '+c.first.y+'</small><small>'+c.tileKeys.join('<br>')+'</small></article>').join('')}document.querySelector('#type').onchange=render;document.querySelector('#search').oninput=render;render();</script></html>
'@
$summary="$(@($results | Where-Object {$_.type.StartsWith('pair')}).Count) edge pairs + $(@($results | Where-Object {$_.type -eq 'corner_2x2'}).Count) corners + $(@($results | Where-Object {$_.type.StartsWith('recipe')}).Count) recipes"
$html.Replace('PAYLOAD',$payload).Replace('CASE_SUMMARY',$summary) | Set-Content -LiteralPath (Join-Path $out 'index.html') -Encoding UTF8
$font.Dispose();$title.Dispose();foreach($image in $sources.Values){$image.Dispose()};foreach($image in $tiles.Values){$image.Dispose()}
Write-Output "Rendered $($results.Count) join cases; $($pageIndex.Count) contact sheets."

