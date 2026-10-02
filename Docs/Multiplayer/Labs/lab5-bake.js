/* Common Y. Levels affect only cell occupancy and rendering. */
(function(host){
function build(basePhysical,height,W,H,cfg,seed,components,bases=[]){
cfg={...cfg,rampWidthMin:cfg.rampWidthMin??cfg.rampWidth,rampWidthMax:cfg.rampWidthMax??cfg.rampWidth};
const T=W*H,V=W+1,levels=new Int16Array(T),cliff=new Uint8Array(T),rampCells=new Int32Array(T).fill(-1),physical=basePhysical.slice(),ramps=[],cardinal=[[1,0],[-1,0],[0,1],[0,-1]];
for(let y=0;y<H;y++)for(let x=0;x<W;x++){const v=y*V+x;levels[y*W+x]=Math.round((height[v]+height[v+1]+height[v+V]+height[v+V+1])/4/cfg.levelStep);}
// Smooth threshold speckles, keeping an integer level map throughout.
for(let pass=0;pass<3;pass++){const copy=levels.slice();for(let y=1;y<H-1;y++)for(let x=1;x<W-1;x++){const votes=new Map();for(let dy=-1;dy<=1;dy++)for(let dx=-1;dx<=1;dx++){const l=copy[(y+dy)*W+x+dx];votes.set(l,(votes.get(l)||0)+1);}let best=copy[y*W+x],n=votes.get(best);for(const [l,c]of votes)if(c>n){best=l;n=c;}levels[y*W+x]=best;}}
if(cfg.levelOverride)levels.set(cfg.levelOverride);
for(const b of bases){const l=Math.round(height[b.y*V+b.x]/cfg.levelStep);b.level=l;for(let y=Math.max(0,b.y-cfg.protectedRadius-2);y<=Math.min(H-1,b.y+cfg.protectedRadius+2);y++)for(let x=Math.max(0,b.x-cfg.protectedRadius-2);x<=Math.min(W-1,b.x+cfg.protectedRadius+2);x++)if(Math.hypot(x+.5-b.x,y+.5-b.y)<=cfg.protectedRadius+2)levels[y*W+x]=l;}
// Treat each connected mountain as one solid, uncut visual mass.
if(cfg.mountainMask){const seen=new Uint8Array(T),queue=new Int32Array(T);for(let root=0;root<T;root++){if(!cfg.mountainMask[root]||seen[root])continue;let head=0,tail=0;queue[tail++]=root;seen[root]=1;const values=[];while(head<tail){const i=queue[head++],x=i%W,y=Math.floor(i/W);values.push(levels[i]);for(const [dx,dy]of cardinal){const xx=x+dx,yy=y+dy,j=yy*W+xx;if(xx<0||yy<0||xx>=W||yy>=H||!cfg.mountainMask[j]||seen[j])continue;seen[j]=1;queue[tail++]=j;}}values.sort((a,b)=>a-b);const level=values[Math.floor(values.length/2)];for(let k=0;k<tail;k++)levels[queue[k]]=level;}}
// Region labels are generation-only. They never reach pathfinding.
const regionMask=basePhysical.slice();regionMask.levels=levels;const regionalComponents=(mask)=>{const labels=new Int32Array(T),counts=[],queue=new Int32Array(T);let id=0;for(let i=0;i<T;i++){if(!mask[i]||labels[i])continue;let head=0,tail=0;queue[tail++]=i;labels[i]=++id;while(head<tail){const k=queue[head++],x=k%W,y=Math.floor(k/W);for(const [dx,dy]of cardinal){const nx=x+dx,ny=y+dy,j=ny*W+nx;if(nx<0||ny<0||nx>=W||ny>=H||!mask[j]||labels[j]||levels[j]!==levels[k])continue;labels[j]=id;queue[tail++]=j;}}counts.push(tail);}return {labels,counts};};
const regions=regionalComponents(basePhysical),groups=new Map();
for(let y=1;y<H-1;y++)for(let x=1;x<W-1;x++){const i=y*W+x;for(const [dx,dy]of [[1,0],[0,1]]){const j=(y+dy)*W+x+dx,delta=Math.abs(levels[i]-levels[j]);if(!delta||!basePhysical[i]||!basePhysical[j])continue;
// Thickness = delta * configured cells per level, on the lower side.
const low=levels[i]<levels[j]?i:j,lowLevel=levels[low],thickness=Math.max(1,delta*cfg.cliffCells),lx=low%W,ly=Math.floor(low/W);
for(let oy=-(thickness-1);oy<=thickness-1;oy++)for(let ox=-(thickness-1);ox<=thickness-1;ox++){if(Math.abs(ox)+Math.abs(oy)>=thickness)continue;const xx=lx+ox,yy=ly+oy,k=yy*W+xx;if(xx>=0&&yy>=0&&xx<W&&yy<H&&levels[k]===lowLevel&&basePhysical[k])cliff[k]=1;}
if(basePhysical[i]&&basePhysical[j]){const a=regions.labels[i],b=regions.labels[j],key=Math.min(a,b)+':'+Math.max(a,b);if(!groups.has(key))groups.set(key,[]);groups.get(key).push({x:x+.5+dx*.5,y:y+.5+dy*.5,dx,dy,delta,lowLevel,highLevel:lowLevel+delta});}
}}
for(let i=0;i<T;i++)if(cliff[i])physical[i]=0;
let state=(seed^0x64e123ab)>>>0;function random(){state^=state<<13;state^=state>>>17;state^=state<<5;return (state>>>0)/4294967296;}
if(cfg.rampEnabled)for(const candidates of groups.values()){
for(let i=candidates.length-1;i>0;i--){const j=Math.floor(random()*(i+1));[candidates[i],candidates[j]]=[candidates[j],candidates[i]];}
let made=0;for(const c of candidates){if(made>=cfg.rampsPerBorder)break;const half=c.delta*cfg.cliffCells+3,width=made===0?cfg.rampWidthMin:made===1?cfg.rampWidthMax:cfg.rampWidthMin+Math.floor(random()*(cfg.rampWidthMax-cfg.rampWidthMin+1)),left=-Math.floor((width-1)/2),right=left+width-1;if(ramps.some(r=>Math.hypot(r.x-c.x,r.y-c.y)<half+width+4))continue;const cells=[];let valid=true;
for(let side=left;side<=right&&valid;side++){const lane=[];for(let t=-half;t<half;t++){const x=Math.floor(c.x+c.dx*(t+.5)-c.dy*side),y=Math.floor(c.y+c.dy*(t+.5)+c.dx*side),i=y*W+x;if(x<2||y<2||x>=W-2||y>=H-2||!basePhysical[i]||rampCells[i]>=0||(levels[i]!==c.lowLevel&&levels[i]!==c.highLevel)||bases.some(b=>Math.hypot(x+.5-b.x,y+.5-b.y)<=cfg.protectedRadius)){valid=false;break;}lane.push(i);cells.push(i);}if(!valid)break;if(cliff[lane[0]]||cliff[lane[lane.length-1]]||levels[lane[0]]===levels[lane[lane.length-1]]){valid=false;break;}let changes=0;for(let i=1;i<lane.length;i++)if(levels[lane[i]]!==levels[lane[i-1]])changes++;if(changes!==1)valid=false;}
if(!valid)continue;const id=ramps.length;for(const i of cells){physical[i]=1;rampCells[i]=id;}ramps.push({...c,id,width,length:2*half,cells});made++;
}}
return {levels,cliff,rampCells,physical,ramps,regionCount:regions.counts.length,attach:walk=>walk};
}
host.RTSBake={build};if(typeof module!=='undefined'&&module.exports)module.exports=host.RTSBake;
})(typeof window!=='undefined'?window:globalThis);
