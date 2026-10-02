const assert=require('node:assert/strict'),E=require('./lab5-engine.js'),C=require('./lab5-clusters.js');
for(const seed of [30000,1337,2000]){
const cfg={seed,players:10,radius:.2,rampWidthMin:3,rampWidthMax:7,mainCount:0,secondaryCount:0,advantageCount:0,contestedCount:0,clusterMin:2,clusterMax:4,mainClusters:3,secondaryClusters:3,advantageClusters:6,contestedClusters:6};
const m=E.generate(cfg),before=m.physical.slice(),mountains=m.mountainMask.slice();C.generate(m,cfg,E);
const expected=new Uint8Array(m.W*m.H);for(const p of m.resources)for(let dy=0;dy<2;dy++)for(let dx=0;dx<2;dx++)expected[(p.y+dy)*m.W+p.x+dx]=1;
let emptyGround=0;for(let i=0;i<expected.length;i++){assert.equal(m.resourceMask[i],expected[i]);assert.equal(m.physical[i],expected[i]?0:before[i]);assert.equal(m.terrainPhysical[i],before[i]);assert.equal(m.mountainMask[i],mountains[i]);}
for(const c of m.clusters)for(let y=Math.max(0,Math.floor(c.y-c.radius));y<=Math.min(m.H-1,Math.ceil(c.y+c.radius));y++)for(let x=Math.max(0,Math.floor(c.x-c.radius));x<=Math.min(m.W-1,Math.ceil(c.x+c.radius));x++){const i=y*m.W+x;if(Math.hypot(x-c.x,y-c.y)<=c.radius&&!expected[i]&&before[i]){assert(m.physical[i]);emptyGround++;}}
assert(emptyGround>0);assert.equal(E.components(m.walk,m.W,m.H).counts.length,1);assert(m.resourceAuditPassed);console.log(JSON.stringify({seed,resources:m.resources.length,resourceCells:expected.reduce((a,b)=>a+b,0),emptyClusterGround:emptyGround,onlyFootprintsBlocked:true}));
}
