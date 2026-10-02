const assert=require('node:assert/strict'),E=require('./lab4-engine.js');
const a=E.generate({},3),b=E.generate({},3);assert.equal(a.hash,b.hash);assert(a.accepted);assert(a.maxSlope<=a.cfg.slope);assert.equal(a.placements.length,a.expectedPlacements);
for(const [role,key] of [['starter','mainCount'],['natural','secondaryCount'],['advantage','advantageCount'],['contested','contestedCount']])assert.equal(a.placements.filter(p=>p.role===role).length,a.cfg.players*a.cfg[key]);
// The only globally guaranteed constant-height patch is each protected core.
for(const base of a.bases){const level=Math.round(base.level*256)/256;for(let y=base.y-6;y<=base.y+6;y++)for(let x=base.x-6;x<=base.x+6;x++)if(Math.hypot(x-base.x,y-base.y)<=a.cfg.protectedRadius)assert.equal(a.height[y*(a.W+1)+x],level);assert(base.ramp.points.some(p=>Math.abs((p.x-base.ramp.a.x)*base.ey-(p.y-base.ramp.a.y)*base.ex)>.1));}
for(const side of ['top','bottom','left','right']){const values=[];for(let k=5;k<a.W-5;k++){const x=side==='left'?5:side==='right'?a.W-5:k,y=side==='top'?5:side==='bottom'?a.H-5:k;values.push(a.height[y*(a.W+1)+x]);}assert(Math.max(...values)-Math.min(...values)>.25);}
const custom=E.generate({mainCount:2,secondaryCount:1,advantageCount:1,contestedCount:2},3);assert.equal(custom.expectedPlacements,24);assert.equal(custom.placements.length,24);
const empty=E.generate({mainCount:0,secondaryCount:0,advantageCount:0,contestedCount:0},3);assert.equal(empty.placements.length,0);assert(empty.errors.every(Number.isFinite));
for(const row of a.rows)assert(Math.min(...row.advantage)>Math.max(...row.natural)+4);
for(const p of a.placements.filter(p=>p.role==='contested')){assert.equal(p.player,-1);const radius=Math.hypot(p.x-a.W/2,p.y-a.H/2);assert(radius>=a.W*.075-1&&radius<a.W*.28+1);}
// Independent regional samples must not collapse back into fixed-site clusters.
for(const base of a.bases)for(const role of ['natural','advantage']){const ps=a.placements.filter(p=>p.player===base.id&&p.role===role);const span=Math.max(...ps.flatMap((p,i)=>ps.slice(i+1).map(q=>Math.hypot(p.x-q.x,p.y-q.y))));assert(span>12);}
const contested=a.placements.filter(p=>p.role==='contested');const contestedSpan=Math.max(...contested.flatMap((p,i)=>contested.slice(i+1).map(q=>Math.hypot(p.x-q.x,p.y-q.y))));assert(contestedSpan>a.W*.3);
for(const seed of [1,42,1337,7654321]){const m=E.generate({seed});for(const base of m.bases){assert(Math.hypot(base.x-m.W/2,base.y-m.H/2)>=m.W*.30);const angle=((Math.atan2(base.y-m.H/2,base.x-m.W/2)-base.sectorAngle+Math.PI*3)%(Math.PI*2))-Math.PI;assert(Math.abs(angle)<m.sectorWidth/2);}}
const r=E.route(a,a.bases[0],a.bases[1]);assert(r.path.length>0);for(let i=1;i<r.path.length;i++){const p=r.path[i-1],q=r.path[i];assert(E.canStep(a.walk,a.W,a.H,Math.floor(p.x),Math.floor(p.y),Math.floor(q.x),Math.floor(q.y)));}
const blocked=E.generate({rampEnabled:false},3);assert.notEqual(a.hash,blocked.hash);assert(!blocked.kind.includes(3));
const stress=E.generate({amplitude:4,wavelength:12,certified:false});assert(stress.steepCells>0);const certified=E.generate({amplitude:4,wavelength:12,certified:true});assert(certified.alpha<1);assert.equal(certified.steepCells,0);assert(certified.maxSlope<=certified.cfg.slope);
let mismatch=0;for(let y=0;y<a.H;y++)for(let x=0;x<a.W;x++)if(a.walk[y*a.W+x]!==a.walk[y*a.W+a.W-1-x])mismatch++;assert(mismatch>100);
console.log(JSON.stringify({checks:'passed',defaultHash:a.hash,routeLength:r.length,mirrorMismatch:mismatch,stressSteepCells:stress.steepCells,certifiedAlpha:certified.alpha},null,2));
