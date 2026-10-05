/* Generation-only repair: open cliff cells, never mountain occupancy. */
(function(host){
function repair(baked,solid,W,H,cfg,bases,E){
const Rules=typeof module!=='undefined'&&module.exports?require('./lab5-bake.js'):host.RTSBake;
const T=W*H,width=cfg.rampWidthMin,left=-Math.floor((width-1)/2),right=left+width-1;
const legal=new Uint8Array(T);
for(let y=0;y<H;y++)for(let x=0;x<W;x++){let ok=true;for(let dy=left;dy<=right&&ok;dy++)for(let dx=left;dx<=right;dx++){const xx=x+dx,yy=y+dy;if(xx<0||yy<0||xx>=W||yy>=H||!solid[yy*W+xx]){ok=false;break;}}legal[y*W+x]=ok?1:0;}
function navigation(){const nav=E.compileWalk(baked.walk,W,H),parts=E.components(nav.walk,W,H);return {nav,parts,labels:bases.map(b=>parts.labels[b.y*W+b.x])};}
let current=navigation(),repairs=0,sealed=0,rejectedPaths=0;const rejectedCenters=new Set();
const maxPocketCells=64,maxSealedCells=Math.floor(T*.02);
for(let pass=0;pass<T;pass++){
const target=current.labels[0];if(!target||current.labels.some(l=>!l))return {connected:false,repairs,sealed};
if(current.parts.counts.length===1){for(const r of baked.ramps)r.cells=r.cells.filter(i=>baked.walk[i]);return {connected:true,repairs,sealed};}
// Connect every island, largest first. Base islands can never be sealed.
let sourceLabel=0;for(let l=1;l<=current.parts.counts.length;l++)if(l!==target&&(!sourceLabel||current.parts.counts[l-1]>current.parts.counts[sourceLabel-1]))sourceLabel=l;
// Existing face cells reserve a one-cell halo. Walking over an existing
// passage is allowed, but opening new cliff beside its face is not.
const preferredFaces=new Set(Rules.rampFaces(baked,W,H).flatMap(g=>[...g.cells,...g.highCells]));
const forbidden=new Uint8Array(T);
if(baked.levels)for(let i=0;i<T;i++)if(baked.cliff[i]){
 const x=i%W,y=Math.floor(i/W);
 for(const [dx,dy]of [[0,-1],[1,0],[0,1],[-1,0]]){
  const xx=x+dx,yy=y+dy;if(xx<0||yy<0||xx>=W||yy>=H)continue;
  const j=yy*W+xx;
  if(baked.levels[j]>baked.levels[i]&&!Rules.straightRampFace(baked.levels,W,H,j))forbidden[i]=1;
 }
}
for(const i of preferredFaces){const x=i%W,y=Math.floor(i/W);for(let dy=-1;dy<=1;dy++)for(let dx=-1;dx<=1;dx++)if(x+dx>=0&&y+dy>=0&&x+dx<W&&y+dy<H)forbidden[(y+dy)*W+x+dx]=1;}
const passLegal=legal.slice();for(const i of rejectedCenters)passLegal[i]=0;
for(let i=0;i<T;i++)if(passLegal[i]){const x=i%W,y=Math.floor(i/W);for(let dy=left;dy<=right;dy++)for(let dx=left;dx<=right;dx++){const k=(y+dy)*W+x+dx;if((baked.cliff[k]&&!baked.walk[k]&&forbidden[k])||(cfg.mountainMask&&cfg.mountainMask[k]))passLegal[i]=0;}}
const dist=new Float64Array(T).fill(Infinity),prev=new Int32Array(T).fill(-1),heap=[];
function sealOrReject(){if(current.labels.includes(sourceLabel)||current.parts.counts[sourceLabel-1]>maxPocketCells||sealed+current.parts.counts[sourceLabel-1]>maxSealedCells)return false;for(let i=0;i<T;i++)if(current.parts.labels[i]===sourceLabel){baked.walk[i]=0;baked.rampCells[i]=-1;baked.cliff[i]=0;if(cfg.mountainMask)cfg.mountainMask[i]=1;sealed++;}current=navigation();return true;}
function push(i,d){let k=heap.length;heap.push([i,d]);while(k){const p=(k-1)>>1;if(heap[p][1]<=d)break;heap[k]=heap[p];k=p;}heap[k]=[i,d];}
function pop(){const out=heap[0],last=heap.pop();if(heap.length){let k=0;while(k*2+1<heap.length){let j=k*2+1;if(j+1<heap.length&&heap[j+1][1]<heap[j][1])j++;if(heap[j][1]>=last[1])break;heap[k]=heap[j];k=j;}heap[k]=last;}return out;}
for(let i=0;i<T;i++)if(passLegal[i]&&current.parts.labels[i]===sourceLabel){dist[i]=0;push(i,0);}let end=-1;
while(heap.length){const [i,d]=pop();if(d!==dist[i])continue;if(current.parts.labels[i]===target){end=i;break;}const x=i%W,y=Math.floor(i/W);for(const [dx,dy]of [[1,0],[-1,0],[0,1],[0,-1]]){const xx=x+dx,yy=y+dy,j=yy*W+xx;if(xx<0||yy<0||xx>=W||yy>=H||!passLegal[j])continue;let cost=1;for(let oy=left;oy<=right;oy++)for(let ox=left;ox<=right;ox++){const k=(yy+oy)*W+xx+ox;if(!baked.walk[k])cost+=12;}const nd=d+cost;if(nd<dist[j]){dist[j]=nd;prev[j]=i;push(j,nd);}}}
if(end<0){if(sealOrReject())continue;return {connected:false,repairs,sealed};}const path=[];for(let i=end;i>=0;i=prev[i])path.push(i);const source=path[path.length-1];
const cells=new Set();for(const i of path){const x=i%W,y=Math.floor(i/W);for(let dy=left;dy<=right;dy++)for(let dx=left;dx<=right;dx++){const k=(y+dy)*W+x+dx;if(!baked.walk[k])cells.add(k);}}
// Include landings adjacent to each opened band, so cross-tier edges are ramps.
const opened=[...cells];for(const i of opened){const x=i%W,y=Math.floor(i/W);for(const [dx,dy]of [[1,0],[-1,0],[0,1],[0,-1]]){const xx=x+dx,yy=y+dy,j=yy*W+xx;if(xx>=0&&yy>=0&&xx<W&&yy<H&&solid[j]&&baked.walk[j])cells.add(j);}}
if(!opened.length){if(sealOrReject())continue;return {connected:false,repairs,sealed};}const previousPhysical=baked.walk.slice(),previousRampCells=baked.rampCells.slice(),previousDirections=baked.rampFaceDirection?baked.rampFaceDirection.slice():undefined;
const id=baked.ramps.length;for(const i of cells){baked.walk[i]=1;baked.rampCells[i]=id;}
baked.ramps.push({id,x:(end%W)+.5,y:Math.floor(end/W)+.5,width,length:path.length,dx:1,dy:0,cells:[...cells],connectivityRepair:true});Rules.enforceRampGap(baked,W,H,preferredFaces);const next=navigation();
if(next.parts.labels[source]!==next.labels[0]||!next.labels[0]){
 const closed=opened.filter(i=>!baked.walk[i]);
 for(const i of path){const x=i%W,y=Math.floor(i/W);if(closed.some(k=>Math.abs(k%W-x)<=Math.ceil(width/2)&&Math.abs(Math.floor(k/W)-y)<=Math.ceil(width/2)))rejectedCenters.add(i);}
 if(!closed.length)rejectedCenters.add(path[Math.floor(path.length/2)]);
 baked.walk.set(previousPhysical);baked.rampCells.set(previousRampCells);baked.rampFaceDirection=previousDirections;baked.ramps.pop();
 if(++rejectedPaths>=24)return {connected:false,repairs,sealed};continue;
}
repairs++;rejectedPaths=0;rejectedCenters.clear();current=next;
}
return {connected:false,repairs};
}
host.RTSConnectivity={repair};if(typeof module!=='undefined'&&module.exports)module.exports=host.RTSConnectivity;
})(typeof window!=='undefined'?window:globalThis);

