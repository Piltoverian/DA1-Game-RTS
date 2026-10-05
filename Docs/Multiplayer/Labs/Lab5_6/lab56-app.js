'use strict';
const $=id=>document.getElementById(id),canvas=$('map'),ctx=canvas.getContext('2d');
let map,plan,scale=6,ox=0,oy=0,selected=-1,drag=null,routeStart=null,path=[],ready=false;
const atlas=new Image(),sprites=new Map();
const names=['cliff_straight','cliff_outer_corner','cliff_inner_corner','ramp_single','ramp_left','ramp_middle','ramp_right','ground_grass','ground_soil'];
atlas.src=window.LAB56_ATLAS;
function sprite(name,r){
 const key=name+'/'+r;if(sprites.has(key))return sprites.get(key);
 const k=names.indexOf(name);if(k<0)throw Error('Missing sprite '+name);
 const b=document.createElement('canvas');b.width=b.height=418;const g=b.getContext('2d');
 g.translate(209,209);g.rotate(r*Math.PI/2);g.drawImage(atlas,k%3*418,Math.floor(k/3)*418,418,418,-209,-209,418,418);sprites.set(key,b);return b;
}
function fit(){scale=Math.min(canvas.width/map.W,canvas.height/map.H);ox=oy=0;render();}
function draw(){
 ctx.fillStyle='#17211d';ctx.fillRect(0,0,canvas.width,canvas.height);if(!map)return;
 const mode=$('mode').value,minX=Math.max(0,Math.floor(-ox/scale)),minY=Math.max(0,Math.floor(-oy/scale)),maxX=Math.min(map.W,Math.ceil((canvas.width-ox)/scale)),maxY=Math.min(map.H,Math.ceil((canvas.height-oy)/scale));
 for(let y=minY;y<maxY;y++)for(let x=minX;x<maxX;x++){
  const i=y*map.W+x,px=ox+x*scale,py=oy+y*scale;
  if(mode==='texture'){
   ctx.drawImage(sprite('ground_grass',0),px,py,scale,scale);
   if(map.kind[i]===4||map.mountainMask[i]){ctx.fillStyle=map.mountainMask[i]?'#656b65':'#294553';ctx.fillRect(px,py,scale,scale);}
  }else{ctx.fillStyle=mode==='walk'?(map.walk[i]?'#819b65':'#252d28'):`hsl(${95-map.levels[i]*13} 28% ${28+map.levels[i]*12}%)`;ctx.fillRect(px,py,scale,scale);}
 }
 if(mode==='texture')for(const d of plan.draws){const x=d.cell%map.W,y=Math.floor(d.cell/map.W);if(x>=minX&&x<maxX&&y>=minY&&y<maxY)ctx.drawImage(sprite(d.source,d.rotation),ox+x*scale,oy+y*scale,scale,scale);}
 if($('blocked').checked)for(let y=minY;y<maxY;y++)for(let x=minX;x<maxX;x++){const i=y*map.W+x;if(!map.walk[i]){ctx.fillStyle=map.cliffMask[i]?'#ff773a88':map.mountainMask[i]?'#bbc0c077':'#cc44cc66';ctx.fillRect(ox+x*scale,oy+y*scale,scale,scale);}}
 if($('ramps').checked)for(const d of plan.draws)if(d.layer===2){
  const x=ox+(d.cell%map.W)*scale,y=oy+Math.floor(d.cell/map.W)*scale;
  ctx.strokeStyle='#ff524b';ctx.lineWidth=1.5;ctx.strokeRect(x,y,scale,scale);
  if(scale>=24){const v=[[0,-1],[1,0],[0,1],[-1,0]][d.rotation],cx=x+scale/2,cy=y+scale/2;ctx.beginPath();ctx.moveTo(cx-v[0]*scale*.25,cy-v[1]*scale*.25);ctx.lineTo(cx+v[0]*scale*.25,cy+v[1]*scale*.25);ctx.lineTo(cx+v[0]*scale*.1-v[1]*scale*.1,cy+v[1]*scale*.1+v[0]*scale*.1);ctx.stroke();}
 }
 if($('grid').checked&&scale>=24){ctx.strokeStyle='#ffffff44';ctx.lineWidth=1;ctx.font='12px sans-serif';for(let y=minY;y<maxY;y++)for(let x=minX;x<maxX;x++){ctx.strokeRect(ox+x*scale,oy+y*scale,scale,scale);ctx.fillStyle='white';ctx.fillText('H'+map.levels[y*map.W+x],ox+x*scale+3,oy+y*scale+14);}}
 ctx.fillStyle='white';ctx.font='bold 15px sans-serif';for(const b of map.bases){const x=ox+(b.x+.5)*scale,y=oy+(b.y+.5)*scale;ctx.beginPath();ctx.arc(x,y,12,0,Math.PI*2);ctx.fillStyle='black';ctx.fill();ctx.fillStyle='white';ctx.fillText('P'+(b.id+1),x-9,y+5);}
 if(path.length){ctx.strokeStyle='#65dfff';ctx.lineWidth=3;ctx.beginPath();path.forEach((p,k)=>ctx[k?'lineTo':'moveTo'](ox+p.x*scale,oy+p.y*scale));ctx.stroke();}
 if(selected>=0){ctx.strokeStyle='#ffe776';ctx.lineWidth=3;ctx.strokeRect(ox+selected%map.W*scale,oy+Math.floor(selected/map.W)*scale,scale,scale);}
}
function render(){try{draw();}catch(e){$('status').textContent=e.message;}}
async function generate(){
 $('gen').disabled=true;$('status').textContent='Đang gen terrain và biên dịch tile…';await new Promise(r=>requestAnimationFrame(()=>requestAnimationFrame(r)));
 try{
  const seed=Number($('seed').value),players=Number($('players').value);
  if(!Number.isSafeInteger(seed)||players<2||players>10)throw Error('Seed phải nguyên; 2–10 người chơi.');
  const next=RTSLab5.generate({seed,players,size:256,fullMap:true,rampWidthMin:Number($('width').value),rampWidthMax:Number($('width').value),protectedRadius:6,mainCount:0,secondaryCount:0,advantageCount:0,contestedCount:0});
  const nextPlan=RTSLab6.compile(next);map=next;plan=nextPlan;selected=-1;path=[];routeStart=null;fit();
  const limited=map.rampQuota.filter(s=>s.limited).length;
  $('status').textContent=`Seed ${seed} · ${map.W}×${map.H} · candidate ${map.attempt} · ${map.islandCount} vùng đi được · ${plan.rampGroups.length} nhóm ramp · ${limited} đoạn không đủ vị trí ramp hợp lệ · ${plan.warnings.length} ô cliff nhiều mảnh cần art riêng`;
 }catch(e){$('status').textContent='Gen thất bại: '+e.message;}finally{$('gen').disabled=false;}
}
function point(e){const r=canvas.getBoundingClientRect();return {x:(e.clientX-r.left)*canvas.width/r.width,y:(e.clientY-r.top)*canvas.height/r.height};}
canvas.onpointerdown=e=>{const p=point(e);drag={...p,ox,oy,moved:false};canvas.setPointerCapture(e.pointerId);};
canvas.onpointermove=e=>{if(!drag)return;const p=point(e),dx=p.x-drag.x,dy=p.y-drag.y;if(Math.abs(dx)+Math.abs(dy)>4)drag.moved=true;ox=drag.ox+dx;oy=drag.oy+dy;render();};
canvas.onpointerup=e=>{
 if(!drag)return;const moved=drag.moved;drag=null;if(moved||!map)return;
 const p=point(e),x=Math.floor((p.x-ox)/scale),y=Math.floor((p.y-oy)/scale);if(x<0||y<0||x>=map.W||y>=map.H)return;
 selected=y*map.W+x;const r=map.visual.rampTileMap[selected],f=map.visual.cliffFaceMap[selected];
 const blockedReason=map.walk[selected]?'walkable':map.mountainMask[selected]?'mountain':map.cliffMask[selected]&&!map.walk[selected]?'low-side cliff collision':map.kind[selected]===4?'water / outside terrain':'other occupancy';
 $('inspect').textContent=JSON.stringify({cell:selected,x,y,HeightLevel:map.levels[selected],walk:!!map.walk[selected],blockedReason,cliffCollision:!!map.cliffMask[selected],mountain:!!map.mountainMask[selected],cliff:f?{open:f.open,parts:f.parts.map(p=>({shape:p.shape,ports:p.ports,surfaceSides:p.surfaceSides}))}:null,ramp:r},null,2);
 if(e.shiftKey){if(routeStart===null){routeStart={x,y};$('route').textContent='Đã chọn đầu đường. Shift+click ô đích.';}else{const result=RTSLab5.route(map,routeStart,{x,y});path=result.path;$('route').textContent=Number.isFinite(result.length)?`Đường đi: ${result.length.toFixed(1)} ô`:'Không tìm thấy đường đi.';routeStart=null;}}
 render();
};
canvas.onpointercancel=()=>drag=null;
canvas.addEventListener('wheel',e=>{if(!map)return;e.preventDefault();const p=point(e),wx=(p.x-ox)/scale,wy=(p.y-oy)/scale;scale=Math.max(3,Math.min(418,scale*Math.exp(-e.deltaY*.0015)));ox=p.x-wx*scale;oy=p.y-wy*scale;render();},{passive:false});
$('gen').onclick=generate;$('fit').onclick=()=>map&&fit();$('detail').onclick=()=>{if(!map)return;const c=selected>=0?{x:selected%map.W,y:Math.floor(selected/map.W)}:RTSLab6.crop(map,plan).crop;scale=canvas.width/10;ox=-c.x*scale;oy=-c.y*scale;render();};
for(const id of ['mode','ramps','grid','blocked'])$(id).onchange=render;
$('export').onclick=()=>{if(!map)return;const blob=new Blob([JSON.stringify({seed:map.cfg.seed,plan,levels:Array.from(map.levels),walk:Array.from(map.walk)})],{type:'application/json'}),url=URL.createObjectURL(blob),a=document.createElement('a');a.href=url;a.download='lab56-seed-'+map.cfg.seed+'.json';a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);};
atlas.onload=()=>{ready=true;$('gen').disabled=false;generate();};atlas.onerror=()=>$('status').textContent='Không đọc được atlas';


