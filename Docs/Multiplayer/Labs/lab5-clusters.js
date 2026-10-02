(function(host){
// Cluster radius is sampling/debug data only. Occupancy comes exclusively
// from individual prefab footprints; terrain remains a separate source.
function bakeResourceOccupancy(terrainPhysical,resources,W,H){
const physical=terrainPhysical.slice(),resourceMask=new Uint8Array(W*H);
for(const p of resources)for(let dy=0;dy<2;dy++)for(let dx=0;dx<2;dx++){const x=p.x+dx,y=p.y+dy;if(x<0||y<0||x>=W||y>=H)throw Error('Resource footprint outside map');const i=y*W+x;resourceMask[i]=1;physical[i]=0;}
return {physical,resourceMask};
}
function generate(map,input,E){
const cfg={...input},W=map.W,H=map.H,T=W*H,roles=['main','secondary','advantage','contested'],min=Math.max(1,Math.round(cfg.clusterMin)),max=Math.max(min,Math.round(cfg.clusterMax));let state=(cfg.seed^0x947a53e1)>>>0;
function random(){state^=state<<13;state^=state>>>17;state^=state<<5;return (state>>>0)/4294967296;}
function source(b,walk){let best=-1,d=Infinity;for(let dy=-4;dy<=4;dy++)for(let dx=-4;dx<=4;dx++){const x=b.x+dx,y=b.y+dy,i=y*W+x;if(x<0||y<0||x>=W||y>=H||!walk[i]||map.levels[i]!==map.levels[b.y*W+b.x])continue;if(dx*dx+dy*dy<d){best=i;d=dx*dx+dy*dy;}}return best;}
function access(p,walk){const cells=[],reach=Math.ceil(cfg.radius+1);for(let y=p.y-reach;y<=p.y+1+reach;y++)for(let x=p.x-reach;x<=p.x+1+reach;x++){if(x<0||y<0||x>=W||y>=H||(x>=p.x&&x<p.x+2&&y>=p.y&&y<p.y+2))continue;const i=y*W+x;if(walk[i]&&map.levels[i]===p.level)cells.push(i);}return cells;}
const initial=map.bases.map(b=>E.dijkstra(map.walk,W,H,source(b,map.walk)).dist),clusters=[],requests=[];for(const b of map.bases)for(const role of roles)for(let n=0;n<cfg[role+'Clusters'];n++)requests.push({owner:role==='contested'?-1:b.id,sector:b.id,role,count:min+Math.floor(random()*(max-min+1))});
function audit(list,physical){const nav=E.compileWalk(physical,W,H,cfg.radius),parts=E.components(nav.walk,W,H),sources=map.bases.map(b=>source(b,nav.walk)),labels=sources.map(i=>i<0?0:parts.labels[i]);if(labels.some(l=>!l)||(cfg.rampEnabled!==false&&parts.counts.length!==1))return null;
// Preserve every base-to-base connection that existed before this candidate.
for(let i=0;i<labels.length;i++)for(let j=0;j<i;j++)if(baseLabels[i]&&baseLabels[i]===baseLabels[j]&&labels[i]!==labels[j])return null;
for(const c of list){let common=new Set(labels.filter(Boolean));if(c.owner>=0)common=new Set([labels[c.owner]]);for(const p of c.resources){const possible=new Set(access(p,nav.walk).map(i=>parts.labels[i]).filter(Boolean));common=new Set([...common].filter(l=>possible.has(l)));if(!common.size)return null;}if(c.owner<0&&labels.filter(l=>common.has(l)).length<2)return null;c.accessiblePlayers=labels.map((l,i)=>common.has(l)?i:-1).filter(i=>i>=0);}
return {nav,parts};}
const originalParts=E.components(map.walk,W,H),baseLabels=map.bases.map(b=>{const s=source(b,map.walk);return s>=0?originalParts.labels[s]:0;});let physical=map.physical.slice(),last=null;
for(const req of requests){const b=map.bases[req.sector],radius=Math.max(4,Math.sqrt(req.count)*2.3);let accepted=null;
for(let attempt=0;attempt<100&&!accepted;attempt++){
const angle=req.role==='contested'?Math.atan2(b.y-H/2,b.x-W/2)+(random()-.5)*Math.PI*2/map.bases.length:random()*Math.PI*2;
const lo=req.role==='main'?5:req.role==='secondary'?W*.11:req.role==='advantage'?W*.23:W*.07,hi=req.role==='main'?Math.max(11,W*.09):req.role==='secondary'?W*.22:req.role==='advantage'?W*.4:W*.28,r=Math.sqrt(lo*lo+random()*(hi*hi-lo*lo)),cx=Math.round((req.role==='contested'?W/2:b.x)+Math.cos(angle)*r),cy=Math.round((req.role==='contested'?H/2:b.y)+Math.sin(angle)*r);
if(cx<radius+3||cy<radius+3||cx>=W-radius-3||cy>=H-radius-3||clusters.some(c=>Math.hypot(cx-c.x,cy-c.y)<radius+c.radius+3))continue;const center=cy*W+cx;if(!physical[center]||!map.walk[center]||map.mountainMask?.[center]||map.rampCells[center]>=0)continue;const level=map.levels[center],resources=[];
for(let q=0;q<160&&resources.length<req.count;q++){const a=random()*Math.PI*2,rr=Math.sqrt(random())*radius,x=Math.round(cx+Math.cos(a)*rr),y=Math.round(cy+Math.sin(a)*rr),ids=[y*W+x,y*W+x+1,(y+1)*W+x,(y+1)*W+x+1];if(x<3||y<3||x>=W-4||y>=H-4||ids.some(i=>!physical[i]||map.mountainMask?.[i]||map.levels[i]!==level||map.rampCells[i]>=0)||resources.some(p=>Math.hypot(x-p.x,y-p.y)<3)||map.bases.some(b=>Math.hypot(x+.5-b.x,y+.5-b.y)<3))continue;resources.push({x,y,level,type:resources.length%2});}
if(resources.length!==req.count)continue;
if(req.owner>=0){const ds=resources.map(p=>Math.min(...access(p,map.walk).map(i=>initial[req.owner][i])));if(ds.some(d=>!Number.isFinite(d)))continue;const avg=ds.reduce((a,b)=>a+b,0)/ds.length;if(req.role==='main'&&avg>W*.15)continue;if(req.role==='secondary'&&(avg<W*.08||avg>W*.3))continue;if(req.role==='advantage'){const ns=clusters.filter(c=>c.owner===req.owner&&c.role==='secondary').flatMap(c=>c.resources.map(p=>Math.min(...access(p,map.walk).map(i=>initial[req.owner][i]))));if(avg>W*.5||Math.min(...ds)<(ns.length?Math.max(...ns)+4:W*.18))continue;}}
const c={...req,id:clusters.length,x:cx,y:cy,radius,level,resources},trial=bakeResourceOccupancy(physical,resources,W,H).physical;const result=audit([...clusters,c],trial);if(!result)continue;accepted=c;physical=trial;last=result;}
if(accepted)clusters.push(accepted);
}
// Re-audit ALL resources against the final occupancy, not pre-placement navigation.
const occupancy=bakeResourceOccupancy(map.physical,clusters.flatMap(c=>c.resources),W,H);
physical=occupancy.physical;map.terrainPhysical=map.physical.slice();map.resourceMask=occupancy.resourceMask;
const final=audit(clusters,physical);if(!final)throw Error('Final resource reachability audit failed');map.physical=physical;map.walk=final.nav.walk;map.clearance=final.nav.clearance;map.islandCount=final.parts.counts.length;map.clusters=clusters;map.clusterRequested=requests.length;map.clusterMissing=requests.length-clusters.length;map.resources=clusters.flatMap(c=>c.resources.map(p=>({...p,cluster:c.id,role:c.role,owner:c.owner})));map.resourceAuditPassed=true;return map;
}
host.RTSClusters={generate,bakeResourceOccupancy};if(typeof module!=='undefined'&&module.exports)module.exports=host.RTSClusters;
})(typeof window!=='undefined'?window:globalThis);
