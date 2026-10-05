const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const E=require('./lab5-engine.js'),V=require('./lab5-visual-tiles.js');
const rows=[];
for(const players of [2,4,10])for(const seed of [300,30000,1337,2000]){
 const m=E.generate({seed,players,radius:.2,mainCount:0,secondaryCount:0,advantageCount:0,contestedCount:0});
 const v=V.compile(m),sourceDirections=new Map();
 for(const c of v.crossings){assert(Number.isInteger(c.direction)&&c.direction>=0&&c.direction<4);if(!sourceDirections.has(c.sourceRampId))sourceDirections.set(c.sourceRampId,new Set());sourceDirections.get(c.sourceRampId).add(c.direction);}
 const mixed=[...sourceDirections].filter(([id,d])=>d.size>1);
 for(const g of v.rampGroups){
  assert.equal(Math.abs(g.directionVector[0])+Math.abs(g.directionVector[1]),1);
  assert(g.edgeIndices.every(e=>v.crossings[e].direction===g.direction));
  assert(g.cells.every(i=>v.rampTileMap[i].groupId===g.id&&v.rampTileMap[i].direction===g.direction));
 }
 assert.equal(m.islandCount,1);
 rows.push({seed,players,accepted:m.accepted,islands:m.islandCount,primaryMultiDirectionCorridors:mixed.filter(([id])=>!m.ramps[id].connectivityRepair).length,repairMultiDirectionCorridors:mixed.filter(([id])=>m.ramps[id].connectivityRepair).length,cardinalRampFaces:v.rampGroups.length,diagonalRampFaces:v.rampGroups.filter(g=>g.directionVector[0]&&g.directionVector[1]).length});
}
fs.writeFileSync(path.resolve(__dirname,'../../../Assets/Art/Terrain/Lab5FlatYTextures_v2/ramp_direction_audit.json'),JSON.stringify(rows,null,2));
console.log(JSON.stringify({maps:rows.length,diagonalRampFaces:rows.reduce((sum,r)=>sum+r.diagonalRampFaces,0),allAccepted:rows.every(r=>r.accepted),allSingleIsland:rows.every(r=>r.islands===1)}));
