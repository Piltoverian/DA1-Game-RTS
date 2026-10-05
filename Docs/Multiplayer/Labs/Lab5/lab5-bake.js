/* Common Y. Levels affect only cell occupancy and rendering. */
(function(host){
// Ramp faces, not corridors/landings, define the spacing constraint.
function straightRampFace(levels,W,H,high){
 const x=high%W,y=Math.floor(high/W);let lower=0;
 for(const [dx,dy]of [[0,-1],[1,0],[0,1],[-1,0]]){
  const xx=x+dx,yy=y+dy;if(xx>=0&&yy>=0&&xx<W&&yy<H&&levels[yy*W+xx]<levels[high])lower++;
 }
 return lower===1;
}
function rampFaces(baked,W,H){
 const {levels,cliff,walk,rampCells}=baked,dirs=[[0,-1],[1,0],[0,1],[-1,0]],T=W*H;
 const at=(x,y)=>x<0||y<0||x>=W||y>=H?-1:y*W+x;
 const next=(i,d)=>at(i%W+dirs[d][0],Math.floor(i/W)+dirs[d][1]);
 if(!levels)return [];
 const anchors=new Map(),votes=new Map();
 for(let i=0;i<T;i++){
  if(!cliff[i]||!walk[i])continue;
  const edges=[];for(let d=0;d<4;d++){const j=next(i,d);if(j>=0&&walk[j]&&levels[j]>levels[i])edges.push({direction:d,highLevel:levels[j]});}
  if(!edges.length)continue;
  const source=rampCells[i];if(!votes.has(source))votes.set(source,[0,0,0,0]);for(const e of edges)votes.get(source)[e.direction]++;
  anchors.set(i,{cell:i,source,edges});
 }
 for(const a of anchors.values()){
  const preferred=baked.rampFaceDirection?baked.rampFaceDirection[a.cell]:-1;
  const sorted=a.edges.slice().sort((x,y)=>votes.get(a.source)[y.direction]-votes.get(a.source)[x.direction]||x.direction-y.direction);
  const e=a.edges.find(e=>e.direction===preferred)||sorted[0];
  a.direction=e.direction;a.highLevel=e.highLevel;a.lowLevel=levels[a.cell];
 }
 const seen=new Set(),groups=[];
 for(const root of anchors.values()){
  if(seen.has(root.cell))continue;const cells=[root.cell];seen.add(root.cell);
  for(let head=0;head<cells.length;head++)for(const d of [(root.direction+1)%4,(root.direction+3)%4]){
   const j=next(cells[head],d),a=anchors.get(j);
   if(!a||seen.has(j)||a.source!==root.source||a.direction!==root.direction||a.lowLevel!==root.lowLevel||a.highLevel!==root.highLevel)continue;
   seen.add(j);cells.push(j);
  }
  groups.push({id:groups.length,sourceRampId:root.source,direction:root.direction,lowLevel:root.lowLevel,highLevel:root.highLevel,width:cells.length,cells,highCells:cells.map(i=>next(i,root.direction))});
 }
 return groups;
}
function rampGapViolations(baked,W,H){
 const groups=rampFaces(baked,W,H),owners=new Int32Array(W*H).fill(-1),pairs=new Set();
 for(const g of groups)for(const i of [...g.cells,...g.highCells])owners[i]=g.id;
 for(const g of groups)for(const i of [...g.cells,...g.highCells]){const x=i%W,y=Math.floor(i/W);for(let dy=-1;dy<=1;dy++)for(let dx=-1;dx<=1;dx++){
  if(x+dx<0||y+dy<0||x+dx>=W||y+dy>=H)continue;const other=owners[(y+dy)*W+x+dx];
  if(other>=0&&other!==g.id)pairs.add(Math.min(g.id,other)+':'+Math.max(g.id,other));
 }}
 return [...pairs].map(pair=>pair.split(':').map(Number));
}
function enforceRampGap(baked,W,H,preferredCells=new Set()){
 if(!baked.rampFaceDirection)baked.rampFaceDirection=new Int8Array(W*H).fill(-1);
 let closed=0;
 // Do not hide an opened corner by choosing its majority direction.
 // Close the actual LOW-side portal before spacing and connectivity checks.
 if(baked.levels)for(let i=0;i<W*H;i++){
  if(!baked.cliff[i]||!baked.walk[i])continue;
  const x=i%W,y=Math.floor(i/W);let invalid=false;
  for(const [dx,dy]of [[0,-1],[1,0],[0,1],[-1,0]]){
   const xx=x+dx,yy=y+dy;if(xx<0||yy<0||xx>=W||yy>=H)continue;
   const j=yy*W+xx;
   if(baked.walk[j]&&baked.levels[j]>baked.levels[i]&&!straightRampFace(baked.levels,W,H,j))invalid=true;
  }
  if(invalid){baked.walk[i]=0;baked.rampCells[i]=-1;baked.rampFaceDirection[i]=-1;closed++;}
 }
 for(let pass=0;pass<W*H;pass++){
  const groups=rampFaces(baked,W,H),reserved=new Uint8Array(W*H);let changed=false;
  groups.sort((a,b)=>b.cells.filter(i=>preferredCells.has(i)).length-a.cells.filter(i=>preferredCells.has(i)).length||b.width-a.width||a.id-b.id);
  for(const g of groups){
   if([...g.cells,...g.highCells].some(i=>reserved[i])){
    // Remove an entire conflicting face run, never punch a one-cell hole in it.
    for(const i of g.cells){baked.walk[i]=0;baked.rampCells[i]=-1;baked.rampFaceDirection[i]=-1;closed++;}changed=true;
   }else{
    for(const i of g.cells)baked.rampFaceDirection[i]=g.direction;
    for(const i of [...g.cells,...g.highCells]){const x=i%W,y=Math.floor(i/W);for(let dy=-1;dy<=1;dy++)for(let dx=-1;dx<=1;dx++)if(x+dx>=0&&y+dy>=0&&x+dx<W&&y+dy<H)reserved[(y+dy)*W+x+dx]=1;}
   }
  }
  if(!changed)return {closed,groups:rampFaces(baked,W,H)};
 }
 throw Error('Ramp spacing did not converge');
}
// Quotas count HIGH-side face cells, not corridor or landing cells.
function ensureStraightRampQuota(baked,solid,W,H,cfg,bases=[]){
 const dirs=[[0,-1],[1,0],[0,1],[-1,0]],at=(x,y)=>x<0||y<0||x>=W||y>=H?-1:y*W+x;
 const next=(i,d)=>at(i%W+dirs[d][0],Math.floor(i/W)+dirs[d][1]);
 const faces=new Map();
 for(let i=0;i<W*H;i++){
  if(!solid[i]||cfg.mountainMask?.[i]||!straightRampFace(baked.levels,W,H,i))continue;
  const d=dirs.findIndex((_,d)=>{const j=next(i,d);return j>=0&&baked.levels[j]<baked.levels[i];}),low=next(i,d);
  if(low<0||!solid[low]||cfg.mountainMask?.[low]||!baked.cliff[low])continue;
  faces.set(i,{cell:i,d,low,highLevel:baked.levels[i],lowLevel:baked.levels[low]});
 }
 const seen=new Set(),segments=[];
 for(const root of faces.values()){
  if(seen.has(root.cell))continue;const cells=[root.cell];seen.add(root.cell);
  for(let k=0;k<cells.length;k++)for(const d of [(root.d+1)%4,(root.d+3)%4]){
   const j=next(cells[k],d),f=faces.get(j);
   if(!f||seen.has(j)||f.d!==root.d||f.highLevel!==root.highLevel||f.lowLevel!==root.lowLevel)continue;
   seen.add(j);cells.push(j);
  }
  cells.sort((a,b)=>a-b);segments.push({...root,cells,required:Math.max(1,Math.ceil(cells.length/12))});
 }
 const opened=s=>s.cells.filter(i=>baked.walk[i]&&baked.walk[faces.get(i).low]&&baked.rampCells[faces.get(i).low]>=0).length;
 function lane(i,s){
  if(!baked.walk[i]||bases.some(b=>Math.hypot(i%W+.5-b.x,Math.floor(i/W)+.5-b.y)<=cfg.protectedRadius))return null;
  const cells=[i];let j=next(i,s.d);
  for(let depth=0;depth<(s.highLevel-s.lowLevel)*cfg.cliffCells+3;depth++,j=next(j,s.d)){
   if(j<0||!solid[j]||cfg.mountainMask?.[j]||baked.levels[j]!==s.lowLevel||baked.rampCells[j]>=0)return null;
   cells.push(j);
   if(!baked.cliff[j])return baked.walk[j]?cells:null;
  }
  return null;
 }
 for(const s of segments){
  // Prefer configured width; shrink only when no wider legal placement fits.
  for(let width=Math.min(cfg.rampWidthMin,s.cells.length);width>=1&&opened(s)<s.required;width--){
   for(let start=0;start+width<=s.cells.length&&opened(s)<s.required;start++){
    const selected=s.cells.slice(start,start+width),lanes=selected.map(i=>lane(i,s));if(lanes.some(l=>!l))continue;
    const before=opened(s),oldWalk=baked.walk.slice(),oldIds=baked.rampCells.slice(),oldDirections=baked.rampFaceDirection?.slice(),id=baked.ramps.length;
    const cells=[...new Set(lanes.flat())];for(const i of cells){baked.walk[i]=1;baked.rampCells[i]=id;}
    const anchor=selected[Math.floor(width/2)];baked.ramps.push({id,x:anchor%W+.5,y:Math.floor(anchor/W)+.5,width,cells,lowLevel:s.lowLevel,highLevel:s.highLevel,delta:s.highLevel-s.lowLevel,quotaRamp:true});
    const result=enforceRampGap(baked,W,H);
    if(result.closed||opened(s)<=before){
     baked.walk.set(oldWalk);baked.rampCells.set(oldIds);baked.rampFaceDirection=oldDirections;baked.ramps.pop();
    }
   }
  }
 }
 return segments.map(s=>({cell:s.cell,direction:s.d,length:s.cells.length,required:s.required,opened:opened(s),limited:opened(s)<s.required,reason:opened(s)<s.required?'geometry_or_ramp_spacing':null}));
}
function build(baseWalk,height,W,H,cfg,seed,components,bases=[]){
cfg={...cfg,rampWidthMin:cfg.rampWidthMin??cfg.rampWidth,rampWidthMax:cfg.rampWidthMax??cfg.rampWidth};
const T=W*H,V=W+1,levels=new Int16Array(T),cliff=new Uint8Array(T),rampCells=new Int32Array(T).fill(-1),walk=baseWalk.slice(),ramps=[],cardinal=[[1,0],[-1,0],[0,1],[0,-1]];
for(let y=0;y<H;y++)for(let x=0;x<W;x++){const v=y*V+x;levels[y*W+x]=Math.round((height[v]+height[v+1]+height[v+V]+height[v+V+1])/4/cfg.levelStep);}
// Smooth threshold speckles, keeping an integer level map throughout.
for(let pass=0;pass<3;pass++){const copy=levels.slice();for(let y=1;y<H-1;y++)for(let x=1;x<W-1;x++){const votes=new Map();for(let dy=-1;dy<=1;dy++)for(let dx=-1;dx<=1;dx++){const l=copy[(y+dy)*W+x+dx];votes.set(l,(votes.get(l)||0)+1);}let best=copy[y*W+x],n=votes.get(best);for(const [l,c]of votes)if(c>n){best=l;n=c;}levels[y*W+x]=best;}}
// Retry fallback: regularise contour runs before baking collision, rather
// than forcing a portal through a corner of an already baked contour.
if(cfg.contourRunCells&&!cfg.levelOverride){
 const original=levels.slice(),step=cfg.contourRunCells;
 for(let by=0;by<H;by+=step)for(let bx=0;bx<W;bx+=step){
  const votes=new Map();for(let y=by;y<Math.min(H,by+step);y++)for(let x=bx;x<Math.min(W,bx+step);x++){
   const i=y*W+x;if(cfg.mountainMask&&cfg.mountainMask[i])continue;
   votes.set(original[i],(votes.get(original[i])||0)+1);
  }
  const chosen=[...votes].sort((a,b)=>b[1]-a[1]||a[0]-b[0])[0];if(!chosen)continue;
  for(let y=by;y<Math.min(H,by+step);y++)for(let x=bx;x<Math.min(W,bx+step);x++){
   const i=y*W+x;if(!cfg.mountainMask||!cfg.mountainMask[i])levels[i]=chosen[0];
  }
 }
}
if(cfg.levelOverride)levels.set(cfg.levelOverride);
for(const b of bases){const l=Math.round(height[b.y*V+b.x]/cfg.levelStep);b.level=l;for(let y=Math.max(0,b.y-cfg.protectedRadius-2);y<=Math.min(H-1,b.y+cfg.protectedRadius+2);y++)for(let x=Math.max(0,b.x-cfg.protectedRadius-2);x<=Math.min(W-1,b.x+cfg.protectedRadius+2);x++)if(Math.hypot(x+.5-b.x,y+.5-b.y)<=cfg.protectedRadius+2)levels[y*W+x]=l;}
// Treat each connected mountain as one solid, uncut visual mass.
if(cfg.mountainMask){const seen=new Uint8Array(T),queue=new Int32Array(T);for(let root=0;root<T;root++){if(!cfg.mountainMask[root]||seen[root])continue;let head=0,tail=0;queue[tail++]=root;seen[root]=1;const values=[];while(head<tail){const i=queue[head++],x=i%W,y=Math.floor(i/W);values.push(levels[i]);for(const [dx,dy]of cardinal){const xx=x+dx,yy=y+dy,j=yy*W+xx;if(xx<0||yy<0||xx>=W||yy>=H||!cfg.mountainMask[j]||seen[j])continue;seen[j]=1;queue[tail++]=j;}}values.sort((a,b)=>a-b);const level=values[Math.floor(values.length/2)];for(let k=0;k<tail;k++)levels[queue[k]]=level;}}
// Region labels are generation-only. They never reach pathfinding.
const regionMask=baseWalk.slice();regionMask.levels=levels;const regionalComponents=(mask)=>{const labels=new Int32Array(T),counts=[],queue=new Int32Array(T);let id=0;for(let i=0;i<T;i++){if(!mask[i]||labels[i])continue;let head=0,tail=0;queue[tail++]=i;labels[i]=++id;while(head<tail){const k=queue[head++],x=k%W,y=Math.floor(k/W);for(const [dx,dy]of cardinal){const nx=x+dx,ny=y+dy,j=ny*W+nx;if(nx<0||ny<0||nx>=W||ny>=H||!mask[j]||labels[j]||levels[j]!==levels[k])continue;labels[j]=id;queue[tail++]=j;}}counts.push(tail);}return {labels,counts};};
const regions=regionalComponents(baseWalk),groups=new Map();
for(let y=1;y<H-1;y++)for(let x=1;x<W-1;x++){const i=y*W+x;for(const [dx,dy]of [[1,0],[0,1]]){const j=(y+dy)*W+x+dx,delta=Math.abs(levels[i]-levels[j]);if(!delta||!baseWalk[i]||!baseWalk[j])continue;
// Thickness = delta * configured cells per level, on the lower side.
const low=levels[i]<levels[j]?i:j,lowLevel=levels[low],thickness=Math.max(1,delta*cfg.cliffCells),lx=low%W,ly=Math.floor(low/W);
for(let oy=-(thickness-1);oy<=thickness-1;oy++)for(let ox=-(thickness-1);ox<=thickness-1;ox++){if(Math.abs(ox)+Math.abs(oy)>=thickness)continue;const xx=lx+ox,yy=ly+oy,k=yy*W+xx;if(xx>=0&&yy>=0&&xx<W&&yy<H&&levels[k]===lowLevel&&baseWalk[k])cliff[k]=1;}
if(baseWalk[i]&&baseWalk[j]){const a=regions.labels[i],b=regions.labels[j],key=Math.min(a,b)+':'+Math.max(a,b);if(!groups.has(key))groups.set(key,[]);groups.get(key).push({x:x+.5+dx*.5,y:y+.5+dy*.5,dx,dy,delta,lowLevel,highLevel:lowLevel+delta});}
}}
for(let i=0;i<T;i++)if(cliff[i])walk[i]=0;
let state=(seed^0x64e123ab)>>>0;function random(){state^=state<<13;state^=state>>>17;state^=state<<5;return (state>>>0)/4294967296;}
if(cfg.rampEnabled)for(const candidates of groups.values()){
for(let i=candidates.length-1;i>0;i--){const j=Math.floor(random()*(i+1));[candidates[i],candidates[j]]=[candidates[j],candidates[i]];}
let made=0;for(const c of candidates){if(made>=cfg.rampsPerBorder)break;const half=c.delta*cfg.cliffCells+3,width=made===0?cfg.rampWidthMin:made===1?cfg.rampWidthMax:cfg.rampWidthMin+Math.floor(random()*(cfg.rampWidthMax-cfg.rampWidthMin+1)),left=-Math.floor((width-1)/2),right=left+width-1;if(ramps.some(r=>Math.hypot(r.x-c.x,r.y-c.y)<half+width+4))continue;const cells=[];let valid=true;
for(let side=left;side<=right&&valid;side++){const lane=[];for(let t=-half;t<half;t++){const x=Math.floor(c.x+c.dx*(t+.5)-c.dy*side),y=Math.floor(c.y+c.dy*(t+.5)+c.dx*side),i=y*W+x;if(x<2||y<2||x>=W-2||y>=H-2||!baseWalk[i]||rampCells[i]>=0||(levels[i]!==c.lowLevel&&levels[i]!==c.highLevel)||bases.some(b=>Math.hypot(x+.5-b.x,y+.5-b.y)<=cfg.protectedRadius)){valid=false;break;}lane.push(i);cells.push(i);}if(!valid)break;if(cliff[lane[0]]||cliff[lane[lane.length-1]]||levels[lane[0]]===levels[lane[lane.length-1]]){valid=false;break;}let changes=0;for(let i=1;i<lane.length;i++)if(levels[lane[i]]!==levels[lane[i-1]])changes++;if(changes!==1)valid=false;}
if(!valid)continue;const id=ramps.length;for(const i of cells){walk[i]=1;rampCells[i]=id;}ramps.push({...c,id,width,length:2*half,cells});made++;
}}
const result={levels,cliff,rampCells,walk,ramps,regionCount:regions.counts.length,attach:walk=>walk};
if(cfg.rampEnabled)result.rampGapResult=enforceRampGap(result,W,H);
return result;
}
host.RTSBake={build,rampFaces,rampGapViolations,enforceRampGap,straightRampFace,ensureStraightRampQuota};if(typeof module!=='undefined'&&module.exports)module.exports=host.RTSBake;
})(typeof window!=='undefined'?window:globalThis);

