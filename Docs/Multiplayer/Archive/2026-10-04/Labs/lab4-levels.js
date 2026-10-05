/* Height tiers are node data. Ramp permission is edge data. */
(function(host){
const dirs=[[1,0],[-1,0],[0,1],[0,-1]],key=(a,b,T)=>Math.min(a,b)*T+Math.max(a,b);
function attach(walk,levels,links){walk.levels=levels;walk.rampLinks=links;return walk;}
function build(rawWalk,rawHeight,W,H,cfg,seed,components){
const T=W*H,V=W+1,levels=new Int16Array(T),surface=new Float64Array(T),rampCells=new Int32Array(T).fill(-1),links=new Set(),ramps=[];
for(let y=0;y<H;y++)for(let x=0;x<W;x++){const i=y*W+x,v=y*V+x,h=(rawHeight[v]+rawHeight[v+1]+rawHeight[v+V]+rawHeight[v+V+1])/4;levels[i]=Math.round(h/cfg.levelStep);surface[i]=levels[i]*cfg.levelStep;}
attach(rawWalk,levels,links);const regions=components(rawWalk,W,H),groups=new Map();
for(let y=1;y<H-1;y++)for(let x=1;x<W-1;x++){const i=y*W+x;if(!rawWalk[i])continue;for(const [dx,dy]of [[1,0],[0,1]]){const j=(y+dy)*W+x+dx;if(!rawWalk[j]||Math.abs(levels[i]-levels[j])!==1)continue;const a=regions.labels[i],b=regions.labels[j],k=Math.min(a,b)+':'+Math.max(a,b);if(!groups.has(k))groups.set(k,[]);groups.get(k).push({x:x+.5+dx*.5,y:y+.5+dy*.5,dx,dy,a,b});}}
let state=(seed^0x93a58c19)>>>0;function random(){state^=state<<13;state^=state>>>17;state^=state<<5;return (state>>>0)/4294967296;}
const length=Math.ceil(1.875*cfg.levelStep/Math.tan(cfg.slope*Math.PI/180))+4,half=Math.ceil(length/2),width=Math.max(2,Math.floor(cfg.rampWidth/2)),inner=Math.max(0,width-Math.ceil(cfg.radius+.75));
if(cfg.rampEnabled)for(const candidates of groups.values()){
for(let i=candidates.length-1;i>0;i--){const j=Math.floor(random()*(i+1));[candidates[i],candidates[j]]=[candidates[j],candidates[i]];}
let made=0;for(const c of candidates){if(made>=cfg.rampsPerBorder)break;if(ramps.some(r=>Math.hypot(r.x-c.x,r.y-c.y)<length+cfg.rampWidth))continue;
const low=Math.min(levels[Math.floor(c.y-c.dy*.5)*W+Math.floor(c.x-c.dx*.5)],levels[Math.floor(c.y+c.dy*.5)*W+Math.floor(c.x+c.dx*.5)]),high=low+1,cells=[];let valid=true;
for(let t=-half;t<half&&valid;t++)for(let side=-width;side<=width;side++){
const x=Math.floor(c.x+c.dx*(t+.5)-c.dy*side),y=Math.floor(c.y+c.dy*(t+.5)+c.dx*side),i=y*W+x;
if(x<2||y<2||x>=W-2||y>=H-2||!rawWalk[i]||rampCells[i]>=0||levels[i]<low||levels[i]>high){valid=false;break;}cells.push({i,t,side});}
if(!valid)continue;
// Every longitudinal lane must have stable opposite-tier landings and only one transition.
for(let side=-width;side<=width&&valid;side++){const lane=cells.filter(p=>p.side===side);if(levels[lane[0].i]===levels[lane[lane.length-1].i]){valid=false;break;}let crossings=0;for(let n=1;n<lane.length;n++)if(levels[lane[n].i]!==levels[lane[n-1].i])crossings++;if(crossings!==1)valid=false;}
if(!valid)continue;
const id=ramps.length,start=levels[cells.find(p=>p.t===-half&&p.side===0).i],end=low+high-start;
for(const p of cells){const u=(p.t+half)/(2*half-1),f=u*u*u*(u*(u*6-15)+10);surface[p.i]=(start+(end-start)*f)*cfg.levelStep;rampCells[p.i]=id;}
for(let side=-inner;side<=inner;side++){const lane=cells.filter(p=>p.side===side);for(let n=1;n<lane.length;n++){const a=lane[n-1].i,b=lane[n].i;if(levels[a]!==levels[b])links.add(key(a,b,T));}}
ramps.push({...c,id,low,high,length:2*half-1,width:2*width+1,cells:cells.map(p=>p.i)});made++;
}}
return {levels,surface,rampCells,links,ramps,regionCount:regions.counts.length,attach:walk=>attach(walk,levels,links)};
}
host.RTSTiers={build,attach,key};if(typeof module!=='undefined'&&module.exports)module.exports=host.RTSTiers;
})(typeof window!=='undefined'?window:globalThis);
