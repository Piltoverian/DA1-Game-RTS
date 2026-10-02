/* Standalone algorithm experiment. Grid units, not Unity world units. */
(function (host) {
  'use strict';
  const Tiers=typeof module!=='undefined'&&module.exports?require('./lab5-bake.js'):host.RTSBake;
  const Connectivity=typeof module!=='undefined'&&module.exports?require('./lab5-connectivity.js'):host.RTSConnectivity;
  const clamp=(x,a,b)=>Math.max(a,Math.min(b,x)), mix=(a,b,t)=>a+(b-a)*t;
  const fade=t=>{t=clamp(t,0,1);return t*t*t*(t*(t*6-15)+10);};
  function hash(x){x=(x+0x9e3779b9)>>>0;x=Math.imul(x^(x>>>16),0x85ebca6b);x=Math.imul(x^(x>>>13),0xc2b2ae35);return (x^(x>>>16))>>>0;}
  class RNG{constructor(seed){this.s=hash(seed)||0x6d2b79f5;}uint(){let x=this.s;x^=x<<13;x^=x>>>17;x^=x<<5;return this.s=x>>>0;}next(){return (this.uint()>>>8)/16777216;}range(a,b){return a+(b-a)*this.next();}}
  class Noise{
    constructor(seed){const r=new RNG(seed),p=Array.from({length:256},(_,i)=>i);for(let i=255;i;i--){const j=Math.floor(r.next()*(i+1));[p[i],p[j]]=[p[j],p[i]];}this.p=Array.from({length:512},(_,i)=>p[i&255]);}
    grad(h,x,y){const g=[[1,0],[0,1],[-1,0],[0,-1],[1,1],[-1,1],[-1,-1],[1,-1]][h&7];return g[0]*x+g[1]*y;}
    sample(x,y){const X=Math.floor(x)&255,Y=Math.floor(y)&255;const u=x-Math.floor(x),v=y-Math.floor(y),p=this.p;return mix(mix(this.grad(p[X+p[Y]],u,v),this.grad(p[X+1+p[Y]],u-1,v),fade(u)),mix(this.grad(p[X+p[Y+1]],u,v-1),this.grad(p[X+1+p[Y+1]],u-1,v-1),fade(u)),fade(v));}
    fbm(x,y,wavelength){let a=1,f=1/wavelength,sum=0,total=0;for(let k=0;k<3;k++){sum+=a*this.sample(x*f,y*f);total+=a;a*=.5;f*=2;}return clamp(sum/total,-1,1);}
  }
  class Heap{
    constructor(){this.a=[];}
    push(i,d){const a=this.a;let k=a.length;a.push([i,d]);while(k){const p=(k-1)>>1;if(a[p][1]<=d)break;a[k]=a[p];k=p;}a[k]=[i,d];}
    pop(){const a=this.a;if(!a.length)return null;const out=a[0],last=a.pop();if(a.length){let k=0;while(2*k+1<a.length){let j=2*k+1;if(j+1<a.length&&a[j+1][1]<a[j][1])j++;if(a[j][1]>=last[1])break;a[k]=a[j];k=j;}a[k]=last;}return out;}
  }
  const dirs=[[1,0],[-1,0],[0,1],[0,-1],[1,1],[-1,1],[1,-1],[-1,-1]];
  function canStep(walk,W,H,x,y,nx,ny){if(x<0||y<0||x>=W||y>=H||nx<0||ny<0||nx>=W||ny>=H||Math.abs(nx-x)>1||Math.abs(ny-y)>1||!walk[y*W+x]||!walk[ny*W+nx])return false;return x===nx||y===ny||!!(walk[y*W+nx]&&walk[ny*W+x]);}
  function dijkstra(walk,W,H,source,parents=false){const dist=new Float64Array(W*H);dist.fill(Infinity);const prev=parents?new Int32Array(W*H).fill(-1):null,h=new Heap();if(source<0||!walk[source])return {dist,prev};dist[source]=0;h.push(source,0);for(let item;(item=h.pop());){const [i,d]=item;if(d!==dist[i])continue;const x=i%W,y=Math.floor(i/W);for(let k=0;k<8;k++){const nx=x+dirs[k][0],ny=y+dirs[k][1];if(!canStep(walk,W,H,x,y,nx,ny))continue;const j=ny*W+nx,nd=d+(k<4?1:Math.SQRT2);if(nd<dist[j]){dist[j]=nd;if(prev)prev[j]=i;h.push(j,nd);}}}return {dist,prev};}
  function components(walk,W,H){const labels=new Int32Array(W*H),counts=[],q=new Int32Array(W*H);let id=0;for(let i=0;i<walk.length;i++){if(!walk[i]||labels[i])continue;id++;let head=0,tail=0;q[tail++]=i;labels[i]=id;while(head<tail){const k=q[head++],x=k%W,y=Math.floor(k/W);for(const [dx,dy]of dirs){const nx=x+dx,ny=y+dy;if(!canStep(walk,W,H,x,y,nx,ny))continue;const j=ny*W+nx;if(!labels[j]){labels[j]=id;q[tail++]=j;}}}counts.push(tail);}return {labels,counts};}
  // Exact Euclidean distance transform to blocked cell centers; conservative
  // square-distance and whole-cell corrections are applied by compileWalk.
  function edt(mask,W,H){const tmp=new Float64Array(W*H),out=new Float64Array(W*H),f=new Float64Array(Math.max(W,H));
    function line(n){const v=new Int32Array(n),z=new Float64Array(n+1),d=new Float64Array(n);let k=0;v[0]=0;z[0]=-Infinity;z[1]=Infinity;for(let q=1;q<n;q++){let s;do{s=((f[q]+q*q)-(f[v[k]]+v[k]*v[k]))/(2*(q-v[k]));if(s<=z[k])k--;else break;}while(k>=0);if(k<0){k=0;v[0]=q;z[0]=-Infinity;z[1]=Infinity;}else{k++;v[k]=q;z[k]=s;z[k+1]=Infinity;}}k=0;for(let q=0;q<n;q++){while(z[k+1]<q)k++;d[q]=(q-v[k])**2+f[v[k]];}return d;}
    for(let y=0;y<H;y++){for(let x=0;x<W;x++)f[x]=mask[y*W+x]?1e12:0;const d=line(W);for(let x=0;x<W;x++)tmp[y*W+x]=d[x];}
    for(let x=0;x<W;x++){for(let y=0;y<H;y++)f[y]=tmp[y*W+x];const d=line(H);for(let y=0;y<H;y++)out[y*W+x]=Math.max(0,Math.sqrt(d[y])-Math.SQRT1_2);}
    return out;
  }
  function compileWalk(physical,W,H,radius){const clearance=edt(physical,W,H),walk=new Uint8Array(physical.length);for(let i=0;i<walk.length;i++)walk[i]=physical[i]&&clearance[i]>=radius+.05?1:0;return {walk,clearance};}
  function segment(p,a,b){const dx=b.x-a.x,dy=b.y-a.y,L=Math.hypot(dx,dy),t=L?((p.x-a.x)*dx+(p.y-a.y)*dy)/(L*L):0;return {t,d:Math.hypot(p.x-a.x-clamp(t,0,1)*dx,p.y-a.y-clamp(t,0,1)*dy),L};}
  function relSpread(a){if(!a.length||a.some(x=>!Number.isFinite(x)))return Infinity;const s=[...a].sort((a,b)=>a-b),med=(s[(s.length-1)>>1]+s[s.length>>1])/2;return Math.max(...a.map(x=>Math.abs(x-med)))/Math.max(med,.01);}
  function normalize(input){return {seed:(Number(input.seed)||1337)>>>0,players:clamp(Math.round(Number(input.players)||4),2,10),size:clamp(Math.round(Number(input.size)||192),96,256),amplitude:clamp(Number(input.amplitude??.8),0,4),wavelength:clamp(Number(input.wavelength??36),12,96),height:clamp(Number(input.height??3),1,6),rampWidth:clamp(Math.round(Number(input.rampWidth??5)),1,12),slope:clamp(Number(input.slope??25),10,45),radius:clamp(Number(input.radius??.65),.2,2),tolerance:clamp(Number(input.tolerance??15),5,30),protectedRadius:clamp(Number(input.protectedRadius??6),4,16),mainCount:clamp(Math.round(Number(input.mainCount??4)),0,12),secondaryCount:clamp(Math.round(Number(input.secondaryCount??3)),0,12),advantageCount:clamp(Math.round(Number(input.advantageCount??3)),0,12),contestedCount:clamp(Math.round(Number(input.contestedCount??4)),0,12),rampWidthMin:clamp(Math.round(Number(input.rampWidthMin??input.rampWidth??3)),1,12),rampWidthMax:clamp(Math.round(Number(input.rampWidthMax??input.rampWidth??7)),1,12),cliffCells:clamp(Math.round(Number(input.cliffCells??1)),1,3),levelStep:clamp(Number(input.levelStep??1),.5,3),rampsPerBorder:clamp(Math.round(Number(input.rampsPerBorder??2)),1,4),rampEnabled:input.rampEnabled!==false,certified:input.certified!==false};}
  function generate(input,attempt=0){
    const cfg=normalize(input),W=cfg.size,H=W,V=W+1,T=W*H,seed=hash(cfg.seed^Math.imul(attempt+1,0x45d9f3b));
    const rng=new RNG(seed),boundary=new Noise(seed^0x12345),detail=new Noise(seed^0x67890),rocks=new Noise(seed^0xf001);
    function landscape(x,y){const wx=x+14*boundary.fbm(x+81,y-39,75),wy=y+14*boundary.fbm(x-53,y+92,75);return cfg.height*(.65+1.5*boundary.fbm(wx+217,wy+151,W*.38)+.4*(1-Math.abs(2*rocks.fbm(wx-93,wy+163,W*.23))));}
    // One angular domain per player; independent jitter and radial position.
    const bases=[],sectorOffset=Math.PI/4+rng.range(-.12,.12),sectorWidth=2*Math.PI/cfg.players;
    for(let id=0;id<cfg.players;id++){const theta=sectorOffset+id*sectorWidth+rng.range(-.12,.12)*sectorWidth,ex=Math.cos(theta),ey=Math.sin(theta),edge=(W/2-27)/Math.max(Math.abs(ex),Math.abs(ey)),r=edge*rng.range(.88,.98);bases.push({x:W/2+ex*r,y:H/2+ey*r,sectorAngle:sectorOffset+id*sectorWidth});}
    bases.forEach((b,id)=>{b.id=id;b.x=Math.round(b.x);b.y=Math.round(b.y);b.level=landscape(b.x,b.y)+cfg.height*.24;const angle=Math.atan2(H/2-b.y,W/2-b.x)+rng.range(-.45,.45);b.angle=angle;b.ex=Math.cos(angle);b.ey=Math.sin(angle);const start=cfg.protectedRadius*.55,L=Math.max(22,Math.ceil(1.875*cfg.height/Math.tan((cfg.slope-2)*Math.PI/180))+5),bend=rng.range(-6,6);const points=[];for(let k=0;k<=8;k++){const t=k/8,side=bend*Math.sin(Math.PI*t)**2;points.push({x:b.x+b.ex*(start+L*t)-b.ey*side,y:b.y+b.ey*(start+L*t)+b.ex*side,t});}b.ramp={a:points[0],b:points[8],points,L,bend};b.ramp.endLevel=landscape(points[8].x,points[8].y);b.ramp.widthScale=rng.range(.85,1.15);b.natural={x:clamp(Math.round(b.ramp.b.x+b.ex*7),9,W-10),y:clamp(Math.round(b.ramp.b.y+b.ey*7),9,H-10)};const side=(rng.next()<.5?-1:1)*rng.range(58,68),inward=rng.range(4,10);b.advantage={x:clamp(Math.round(b.x+b.ex*inward-b.ey*side),12,W-13),y:clamp(Math.round(b.y+b.ey*inward+b.ex*side),12,H-13)};});
    const contested=Array.from({length:cfg.players},(_,id)=>{const t=sectorOffset+(id+.5)*sectorWidth+rng.range(-.15,.15),r=W*rng.range(.07,.12);return {x:Math.round(W/2+Math.cos(t)*r),y:Math.round(H/2+Math.sin(t)*r),id};});
    const economicSites=[...bases.flatMap(b=>[b.natural,b.advantage]),...contested];
    const minSpacing=Math.min(...bases.flatMap((b,i)=>bases.slice(i+1).map(o=>Math.hypot(b.x-o.x,b.y-o.y))));
    const nodes=bases.map(b=>b.natural),edges=[],joined=new Set([0]);
    while(joined.size<nodes.length){let best=null;for(const i of joined)for(let j=0;j<nodes.length;j++)if(!joined.has(j)){const d=Math.hypot(nodes[i].x-nodes[j].x,nodes[i].y-nodes[j].y);if(!best||d<best.d)best={i,j,d};}edges.push([best.i,best.j]);joined.add(best.j);}
    const ordered=nodes.map((p,i)=>({i,a:Math.atan2(p.y-H/2,p.x-W/2)})).sort((a,b)=>a.a-b.a).map(o=>o.i);
    if(nodes.length>2){for(let k=0;k<ordered.length;k++){const a=ordered[k],b=ordered[(k+1)%ordered.length];if(!edges.some(e=>e.includes(a)&&e.includes(b)))edges.push([a,b]);}}
    const lanes=[];for(const [i,j]of edges){const a=nodes[i],b=nodes[j],dx=b.x-a.x,dy=b.y-a.y,L=Math.hypot(dx,dy)||1,offset=rng.range(-10,10),mid={x:clamp((a.x+b.x)/2-dy/L*offset,8,W-8),y:clamp((a.y+b.y)/2+dx/L*offset,8,H-8)};lanes.push([a,mid],[mid,b]);}
    if(nodes.length===2){const a=nodes[0],b=nodes[1],dx=b.x-a.x,dy=b.y-a.y,L=Math.hypot(dx,dy)||1,mid={x:clamp((a.x+b.x)/2-dy/L*22,10,W-10),y:clamp((a.y+b.y)/2+dx/L*22,10,H-10)};lanes.push([a,mid],[mid,b]);}
    for(const b of bases){lanes.push([b.natural,b.advantage]);const near=contested.reduce((a,p)=>Math.hypot(p.x-b.advantage.x,p.y-b.advantage.y)<Math.hypot(a.x-b.advantage.x,a.y-b.advantage.y)?p:a);lanes.push([b.advantage,near]);}for(let i=0;i<contested.length;i++)lanes.push([contested[i],contested[(i+1)%contested.length]]);
    const kind=new Uint8Array(T),protectedMask=new Uint8Array(T),owner=new Int16Array(T).fill(-1);
    const height0=new Float64Array(V*V),residual=new Float64Array(V*V),vertexRamp=new Int16Array(V*V).fill(-1);
    function rampSample(x,y,b){let best={d:Infinity,t:0};for(let k=0;k<8;k++){const a=b.ramp.points[k],end=b.ramp.points[k+1],ss=segment({x,y},a,end);if(ss.d<best.d)best={d:ss.d,t:(k+clamp(ss.t,0,1))/8};}return best;}
    function surface(x,y){let h=landscape(x,y),mask=1,plateau=-1,dmin=Infinity,ri=-1;
      for(const b of bases){const r=Math.hypot(x-b.x,y-b.y),d=r-cfg.protectedRadius;dmin=Math.min(dmin,d);const outer=cfg.protectedRadius+13+3*boundary.fbm(x+47,y-38,28),weight=1-fade((r-cfg.protectedRadius)/(outer-cfg.protectedRadius));h=mix(h,b.level,weight);mask=Math.min(mask,1-weight);if(r<cfg.protectedRadius)plateau=b.id;}
      // Curved ramp core, variable width, feathered shoulders, matching end heights.
      if(false)for(const b of bases){const ss=rampSample(x,y,b),width=cfg.rampWidth*b.ramp.widthScale*(.88+.22*Math.sin(ss.t*Math.PI)+.08*boundary.fbm(x,y,18)),weight=(1-fade((ss.d-width/2)/6))*fade((1.12-ss.t)/.12);if(weight>0){const target=mix(b.level,b.ramp.endLevel,fade(ss.t));h=mix(h,target,weight);mask=Math.min(mask,1-weight);if(weight>.8&&ss.t>.12&&ss.t<.95)ri=b.id;}}
      // Only the spawn safety core is guaranteed to be exactly level.
      for(const b of bases)if(Math.hypot(x-b.x,y-b.y)<=cfg.protectedRadius){h=b.level;mask=0;plateau=b.id;ri=-1;}
      return {h,n:mask*detail.fbm(x,y,cfg.wavelength),d:dmin,plateau,ri};}
    for(let y=0;y<V;y++)for(let x=0;x<V;x++){const i=y*V+x,s=surface(x,y);height0[i]=s.h;residual[i]=cfg.amplitude*s.n;vertexRamp[i]=s.ri;}
    for(let y=0;y<H;y++)for(let x=0;x<W;x++){const i=y*W+x,s=surface(x+.5,y+.5);let reserved=false;for(const b of bases)if(Math.hypot(x+.5-b.x,y+.5-b.y)<cfg.protectedRadius+3||Math.hypot(x+.5-b.natural.x,y+.5-b.natural.y)<8||rampSample(x+.5,y+.5,b).d<cfg.rampWidth/2+3)reserved=true;for(const [a,b]of lanes)if(segment({x:x+.5,y:y+.5},a,b).d<6)reserved=true;for(const p of economicSites)if(Math.hypot(x+.5-p.x,y+.5-p.y)<9)reserved=true;protectedMask[i]=reserved?1:0;kind[i]=s.ri>=0?3:s.plateau>=0?1:0;owner[i]=s.h>0?s.plateau:-1;if(x<2||y<2||x>=W-2||y>=H-2)kind[i]=4;else if(kind[i]===0&&!reserved&&rocks.fbm(x+79,y-143,19)>.20)kind[i]=4;}
    // Mountain occupancy is generated before corridors: routes never carve it.
    const mountainMask=new Uint8Array(T);
    for(let y=2;y<H-2;y++)for(let x=2;x<W-2;x++){const i=y*W+x;if(!bases.some(b=>Math.hypot(x+.5-b.x,y+.5-b.y)<cfg.protectedRadius+4)&&rocks.fbm(x+79,y-143,19)>.20)mountainMask[i]=1;}
    // Close single-cell gaps without removing any mountain cells.
    const mountainSeed=mountainMask.slice();for(let y=3;y<H-3;y++)for(let x=3;x<W-3;x++){const i=y*W+x;if(!mountainSeed[i]&&((mountainSeed[i-1]&&mountainSeed[i+1])||(mountainSeed[i-W]&&mountainSeed[i+W]))&&!bases.some(b=>Math.hypot(x+.5-b.x,y+.5-b.y)<cfg.protectedRadius+4))mountainMask[i]=1;}
    for(let y=2;y<H-2;y++)for(let x=2;x<W-2;x++)kind[y*W+x]=mountainMask[y*W+x]?4:0;
    const basePhysical=new Uint8Array(T),slopeBase=new Float64Array(T),triangles=[],limit=Math.tan(Math.max(1,cfg.slope-1)*Math.PI/180);let alpha=1;
    function grads(field,x,y){const i=y*V+x,h00=field[i],h10=field[i+1],h01=field[i+V],h11=field[i+V+1];return [[h10-h00,h11-h10],[h11-h01,h01-h00]];}
    for(let y=0;y<H;y++)for(let x=0;x<W;x++){const i=y*W+x,g0=grads(height0,x,y),gn=grads(residual,x,y);slopeBase[i]=Math.max(...g0.map(g=>Math.hypot(...g)));if(kind[i]===2||kind[i]===4||slopeBase[i]>limit){if(kind[i]!==4)kind[i]=2;continue;}basePhysical[i]=1;for(let t=0;t<2;t++){const a=gn[t][0]**2+gn[t][1]**2,b=2*(g0[t][0]*gn[t][0]+g0[t][1]*gn[t][1]),d=g0[t][0]**2+g0[t][1]**2-limit**2;if(a>1e-18)alpha=Math.min(alpha,(-b+Math.sqrt(Math.max(0,b*b-4*a*d)))/(2*a));triangles.push([i,g0[t],gn[t]]);}}
    alpha=1;const height=new Float64Array(V*V);for(let i=0;i<height.length;i++)height[i]=Math.round((height0[i]+alpha*residual[i])*256)/256;
    const slope=new Float64Array(T);let physical=basePhysical.slice();let maxSlope=0,steepCells=0;
    for(let y=0;y<H;y++)for(let x=0;x<W;x++){const i=y*W+x;slope[i]=Math.atan(Math.max(...grads(height,x,y).map(g=>Math.hypot(...g))))*180/Math.PI;if(basePhysical[i]){maxSlope=Math.max(maxSlope,slope[i]);if(false){physical[i]=0;steepCells++;}}}
    let {walk,clearance}=compileWalk(physical,W,H,cfg.radius);
    const terrainSolid=kind.map(k=>k===4?0:1);
    const tierData=Tiers.build(terrainSolid,height,W,H,{...cfg,mountainMask},seed,components,bases);
    const connection=cfg.rampEnabled?Connectivity.repair(tierData,terrainSolid,W,H,{...cfg,mountainMask},bases,{compileWalk,components}):{connected:false,repairs:0};
    if(cfg.rampEnabled&&!connection.connected){if(attempt<15)return generate(input,attempt+1);throw Error('Không tạo được map nối mọi base sau 16 candidate; hãy giảm radius / rộng ramp hoặc đổi cấu hình. Núi được giữ nguyên.');}
    tierData.connectivityRepairs=connection.repairs;
    // Sealed pockets become actual mountains. Unify merged mountain masses
    // without changing any remaining ground level or navigation occupancy.
    const mountainSeen=new Uint8Array(T);
    for(let root=0;root<T;root++){if(!mountainMask[root]||mountainSeen[root])continue;const cells=[root];mountainSeen[root]=1;for(let head=0;head<cells.length;head++){const i=cells[head],x=i%W,y=Math.floor(i/W);for(const [dx,dy]of [[1,0],[-1,0],[0,1],[0,-1]]){const xx=x+dx,yy=y+dy,j=yy*W+xx;if(xx>=0&&yy>=0&&xx<W&&yy<H&&mountainMask[j]&&!mountainSeen[j]){mountainSeen[j]=1;cells.push(j);}}}const sorted=cells.map(i=>tierData.levels[i]).sort((a,b)=>a-b),level=sorted[Math.floor(sorted.length/2)];for(const i of cells){tierData.levels[i]=level;kind[i]=4;tierData.cliff[i]=0;tierData.rampCells[i]=-1;}}
    // Remove dead portals and place labels on actual surviving ground cells.
    const liveRamps=tierData.ramps.filter(r=>r.cells.some(i=>tierData.physical[i]&&!mountainMask[i]));
    const remap=new Map();liveRamps.forEach((r,id)=>{remap.set(r.id,id);r.id=id;r.cells=r.cells.filter(i=>tierData.physical[i]&&!mountainMask[i]);const center=r.cells.reduce((a,i)=>({x:a.x+i%W+.5,y:a.y+Math.floor(i/W)+.5}),{x:0,y:0});center.x/=r.cells.length;center.y/=r.cells.length;const anchor=r.cells.reduce((a,i)=>Math.hypot(i%W+.5-center.x,Math.floor(i/W)+.5-center.y)<Math.hypot(a%W+.5-center.x,Math.floor(a/W)+.5-center.y)?i:a,r.cells[0]);r.labelX=anchor%W+.5;r.labelY=Math.floor(anchor/W)+.5;});
    for(let i=0;i<T;i++)if(tierData.rampCells[i]>=0)tierData.rampCells[i]=remap.get(tierData.rampCells[i])??-1;
    tierData.ramps=liveRamps;
    // Include side landings of opened ramps; every open tier transition is marked.
    for(let y=0;y<H;y++)for(let x=0;x<W;x++){const i=y*W+x;for(const [dx,dy]of [[1,0],[0,1]]){const xx=x+dx,yy=y+dy,j=yy*W+xx;if(xx>=W||yy>=H||!tierData.physical[i]||!tierData.physical[j]||tierData.levels[i]===tierData.levels[j])continue;const id=Math.max(tierData.rampCells[i],tierData.rampCells[j]);if(id>=0)for(const k of [i,j])if(tierData.rampCells[k]<0){tierData.rampCells[k]=id;tierData.ramps[id].cells.push(k);}}}
    physical=tierData.physical;({walk,clearance}=compileWalk(physical,W,H,cfg.radius));for(let i=0;i<T;i++){if(tierData.cliff[i])kind[i]=2;if(tierData.rampCells[i]>=0)kind[i]=3;}
    for(const b of bases)b.level=tierData.levels[b.y*W+b.x]*cfg.levelStep;
    function nearest(x,y,mask=walk,max=16){let best=-1,d=Infinity;for(let dy=-max;dy<=max;dy++)for(let dx=-max;dx<=max;dx++){const nx=Math.round(x)+dx,ny=Math.round(y)+dy;if(nx<0||ny<0||nx>=W||ny>=H)continue;const i=ny*W+nx,dd=(nx-x)**2+(ny-y)**2;if(mask[i]&&dd<d){best=i;d=dd;}}return best;}
    const sources=bases.map(b=>nearest(b.x,b.y,walk,4)),initialDistances=sources.map(s=>dijkstra(walk,W,H,s).dist),placements=[],rngP=new RNG(seed^0x72a3);
    function standing(p,mask){const out=[],reach=Math.ceil(cfg.radius+2.5);for(let y=p.y-reach;y<=p.y+1+reach;y++)for(let x=p.x-reach;x<=p.x+1+reach;x++){if(x<0||y<0||x>=W||y>=H||x>=p.x&&x<p.x+2&&y>=p.y&&y<p.y+2)continue;const dx=Math.max(p.x-x,0,x-p.x-1),dy=Math.max(p.y-y,0,y-p.y-1);const i=y*W+x;if(mask[i]&&Math.hypot(dx,dy)<=reach)out.push(i);}return out;}
    const counts={starter:cfg.mainCount,natural:cfg.secondaryCount,advantage:cfg.advantageCount,contested:cfg.contestedCount},expectedPlacements=cfg.players*Object.values(counts).reduce((a,b)=>a+b,0);
    const requests=[...bases.flatMap(b=>['starter','natural','advantage'].map(role=>({player:b.id,role,center:role==='starter'?b:b[role]}))),...contested.map(center=>({player:-1,role:'contested',center}))];
    // Lab 3: sample each mine independently over annuli / radial sectors.
    // No shared center for the prefabs of a private or contested resource role.
    for(const req of requests)for(let number=0;number<counts[req.role];number++){
      const type=number%2,{role,player}=req,b=player>=0?bases[player]:bases[req.center.id];let best=null,bestScore=Infinity;
      for(let q=0;q<600;q++){
        let angle,r,cx=b.x,cy=b.y;
        if(role==='contested'){angle=b.sectorAngle+rngP.range(-.48,.48)*sectorWidth;const lo=W*.075,hi=W*.28;r=Math.sqrt(lo*lo+rngP.next()*(hi*hi-lo*lo));cx=W/2;cy=H/2;}
        else{angle=rngP.range(0,2*Math.PI);const lo=role==='starter'?cfg.protectedRadius+1:role==='natural'?cfg.protectedRadius+10:W*.25,hi=role==='starter'?cfg.protectedRadius+12:role==='natural'?W*.22:W*.40;r=Math.sqrt(lo*lo+rngP.next()*(hi*hi-lo*lo));if(role==='starter'&&Math.cos(angle)*b.ex+Math.sin(angle)*b.ey>(q<400?.25:.75))continue;}
        const p={x:Math.round(cx+Math.cos(angle)*r),y:Math.round(cy+Math.sin(angle)*r),player,role,type,site:role==='contested'?req.center.id:player};
        if(p.x<5||p.y<5||p.x>=W-6||p.y>=H-6)continue;
        const ids=[p.y*W+p.x,p.y*W+p.x+1,(p.y+1)*W+p.x,(p.y+1)*W+p.x+1];if(ids.some(i=>!physical[i]||slope[i]>8))continue;
        if(role!=='starter'&&bases.some(o=>Math.hypot(p.x-o.x,p.y-o.y)<cfg.protectedRadius+8))continue;
        if(ids.some(i=>tierData.rampCells[i]>=0)||ids.some(i=>tierData.levels[i]!==tierData.levels[ids[0]]))continue;
        const spacing=role==='starter'?5:Math.max(8,W*.045);if(placements.some(o=>Math.hypot(p.x-o.x,p.y-o.y)<Math.max(spacing,o.role==='starter'?5:8)))continue;
        const access=standing(p,walk);if(access.length<5)continue;
        const ds=initialDistances.map(field=>Math.min(...access.map(i=>field[i])));let score;
        const peers=placements.filter(o=>o.role===role&&(role==='contested'||o.player===player));const separation=peers.length?Math.min(...peers.map(o=>Math.hypot(p.x-o.x,p.y-o.y))):spacing;
        if(player>=0){const d=ds[player];if(!Number.isFinite(d))continue;const target=role==='starter'?cfg.protectedRadius+4:role==='natural'?W*.16:W*.30;const lo=target*.72,hi=target*1.24;if(d<lo||d>hi)continue;
          if(role==='advantage'){const natural=placements.filter(o=>o.player===player&&o.role==='natural').map(o=>Math.min(...standing(o,walk).map(i=>initialDistances[player][i])));if(natural.length!==counts.natural||d<=(natural.length?Math.max(...natural)+7:cfg.protectedRadius+14))continue;}
          score=Math.abs(d-target)*.32-Math.min(separation,W*.14)*.6+rngP.next()*2;
        }else{const sorted=ds.filter(Number.isFinite).sort((a,b)=>a-b);if(sorted.length!==cfg.players||sorted[0]<W*.285||sorted[0]>W*.44)continue;score=Math.abs(sorted[0]-sorted[1])*.12-Math.min(separation,W*.12)*.7+rngP.next()*3;}
        if(score<bestScore){bestScore=score;best=p;}
      }
      if(best){placements.push(best);for(let y=best.y;y<best.y+2;y++)for(let x=best.x;x<best.x+2;x++)physical[y*W+x]=0;}
    }
    ({walk,clearance}=compileWalk(physical,W,H,cfg.radius));tierData.attach(walk);
    // Only empty pockets without a base or natural role may be pruned.
    let islands=components(walk,W,H),pruned=0;const keep=new Set(bases.flatMap(b=>[nearest(b.x,b.y,walk,4),nearest(b.natural.x,b.natural.y,walk,6)]).filter(i=>i>=0).map(i=>islands.labels[i]));
    for(let i=0;i<T;i++)if(false&&walk[i]&&!keep.has(islands.labels[i])){physical[i]=0;pruned++;}
    ({walk,clearance}=compileWalk(physical,W,H,cfg.radius));tierData.attach(walk);islands=components(walk,W,H);
    const distances=bases.map(b=>dijkstra(walk,W,H,nearest(b.x,b.y,walk,4)).dist),rows=[];
    for(const b of bases){const ps=placements.filter(p=>p.player===b.id),access=role=>{const list=ps.filter(p=>p.role===role);return list.length===counts[role]?list.map(p=>Math.min(...standing(p,walk).map(i=>distances[b.id][i]))):[Infinity];};const starter=access('starter'),natural=access('natural'),advantage=access('advantage'),contest=placements.filter(p=>p.role==='contested').map(p=>Math.min(...standing(p,walk).map(i=>distances[b.id][i]))),contestedAccess=contest.length?Math.min(...contest):counts.contested?Infinity:0,near=nearest(b.x,b.y,walk,4);let area=0;for(let y=Math.max(0,b.y-13);y<Math.min(H,b.y+14);y++)for(let x=Math.max(0,b.x-13);x<Math.min(W,b.x+14);x++){const i=y*W+x;if(walk[i]&&slope[i]<=8&&Math.hypot(x-b.x,y-b.y)<12)area++;}const enemy=bases.filter(o=>o.id!==b.id).map(o=>distances[o.id][near]??Infinity).sort((a,b)=>a-b);rows.push({player:b.id,starter,natural,advantage,contested:contestedAccess,area,nearest:enemy[0],pressure:enemy.slice(0,2).reduce((s,d)=>s+Math.exp(-d/60),0),source:near});}
    function roleSpread(role){return counts[role]?relSpread(rows.map(r=>r[role].reduce((a,b)=>a+b,0)/Math.max(1,r[role].length))):0;}
    const starterError=roleSpread('starter'),naturalError=roleSpread('natural'),areaError=relSpread(rows.map(r=>r.area)),threatError=relSpread(rows.map(r=>r.nearest)),pressureError=relSpread(rows.map(r=>r.pressure));
    const advantageError=roleSpread('advantage'),contestError=relSpread(rows.map(r=>r.contested));const tol=cfg.tolerance/100,violations=[];if(bases.some(b=>Math.hypot(b.x-W/2,b.y-H/2)<W*.30))violations.push('Base quá gần tâm');if(advantageError>tol)violations.push('Mỏ lợi thế vượt budget');if(contestError>tol)violations.push('Tiếp cận tranh chấp vượt budget');if(rows.some(r=>r.advantage.length&&r.natural.length&&Math.min(...r.advantage)<=Math.max(...r.natural)+4))violations.push('Mỏ lợi thế quá gần so với mỏ phụ');if(minSpacing<43)violations.push('Base quá gần / private budget thiếu');if(placements.length!==expectedPlacements)violations.push('Thiếu resource prefab');if(islands.counts.length!==1)violations.push('Navigation có '+islands.counts.length+' đảo');if(steepCells)violations.push('Noise làm '+steepCells+' ô vượt dốc');if(starterError>tol)violations.push('Starter access vượt budget');if(naturalError>tol)violations.push('Natural access vượt budget');if(areaError>tol)violations.push('Đất xây vượt budget');if(threatError>tol)violations.push('Nearest-enemy vượt budget');if(pressureError>Math.max(.2,tol))violations.push('Áp lực hàng xóm vượt budget');if(rows.some(r=>r.area<Math.PI*cfg.protectedRadius**2*.45))violations.push('Thiếu đất xây tối thiểu');
    const errors=[starterError,naturalError,areaError,threatError,pressureError,advantageError,contestError],score=(errors.every(Number.isFinite)?Math.max(...errors)/tol:1e6)+violations.length*2;
    let digest=2166136261;for(let i=0;i<T;i++){digest=Math.imul(digest^kind[i],16777619);digest=Math.imul(digest^walk[i],16777619);}for(const n of [cfg.mainCount,cfg.secondaryCount,cfg.advantageCount,cfg.contestedCount,cfg.protectedRadius])digest=Math.imul(digest^n,16777619);for(const n of [cfg.levelStep*256,cfg.rampsPerBorder])digest=Math.imul(digest^n,16777619);for(const h of tierData.levels)digest=Math.imul(digest^h,16777619);for(const p of placements)for(const n of [p.x,p.y,p.type,p.player])digest=Math.imul(digest^n,16777619);
    return {cfg,attempt,W,H,bases,nodes,lanes,contested,sectorOffset,sectorWidth,kind,protectedMask,expectedPlacements,height:tierData.levels,levels:tierData.levels,cliffMask:tierData.cliff,mountainMask,mountainSeed,ramps:tierData.ramps,rampCells:tierData.rampCells,regionCount:tierData.regionCount,slope,physical,walk,clearance,placements,rows,distances,alpha,maxSlope,steepCells,pruned,islandCount:islands.counts.length,walkRatio:walk.reduce((s,x)=>s+x,0)/T,errors,score,violations,hash:(digest>>>0).toString(16).padStart(8,'0'),accepted:violations.length===0};
  }
  function route(map,start,end){const i=Math.floor(start.y)*map.W+Math.floor(start.x),j=Math.floor(end.y)*map.W+Math.floor(end.x);if(i<0||j<0||i>=map.walk.length||j>=map.walk.length||!map.walk[i]||!map.walk[j])return {path:[],length:Infinity};const {dist,prev}=dijkstra(map.walk,map.W,map.H,i,true),path=[];if(!Number.isFinite(dist[j]))return {path,length:Infinity};for(let k=j;k>=0;k=prev[k]){path.push({x:k%map.W+.5,y:Math.floor(k/map.W)+.5});if(k===i)break;}return {path:path.reverse(),length:dist[j]};}
  host.RTSLab5={generate,route,normalize,relSpread,canStep,dijkstra,components,compileWalk,Noise};
  if(typeof module!=='undefined'&&module.exports)module.exports=host.RTSLab5;
})(typeof window!=='undefined'?window:globalThis);
