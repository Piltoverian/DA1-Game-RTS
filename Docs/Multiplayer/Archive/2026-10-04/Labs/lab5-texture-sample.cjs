const fs=require('node:fs'),path=require('node:path'),E=require('./lab5-engine.js'),V=require('./lab5-visual-tiles.js');
const cfg={seed:Number(process.argv[2]??30000),players:Number(process.argv[3]??4),size:192,radius:.2,protectedRadius:6,rampWidthMin:3,rampWidthMax:7,mainCount:0,secondaryCount:0,advantageCount:0,contestedCount:0};
const m=E.generate(cfg),v=m.visual,side=10;
let best=null;
for(let y=1;y<=m.H-side-1;y++)for(let x=1;x<=m.W-side-1;x++){
 let ramps=0,cliffs=0,corners=0;
 for(let dy=0;dy<side;dy++)for(let dx=0;dx<side;dx++){
  const i=(y+dy)*m.W+x+dx,r=v.rampTileMap[i],f=v.cliffFaceMap[i];
  if(r)ramps++;
  if(f&&!f.open){cliffs++;if(f.shape.includes('corner'))corners++;}
 }
 const crossings=v.crossings.filter(c=>[c.lowCell,c.highCell].every(i=>i%m.W>=x&&i%m.W<x+side&&Math.floor(i/m.W)>=y&&Math.floor(i/m.W)<y+side)).length;
 if(!crossings||!cliffs)continue;
 const score=crossings*8+Math.min(corners,10)*3+Math.min(cliffs,20)-Math.max(0,ramps-35);
 if(!best||score>best.score)best={x,y,score};
}
if(!best)throw Error('No terrain crop with cliff and an open level crossing');
const cells=[];
for(let y=0;y<side;y++)for(let x=0;x<side;x++){
 const i=(best.y+y)*m.W+best.x+x;
 cells.push({x,y,globalCell:i,level:m.levels[i],mountain:!!m.mountainMask[i],cliff:!!m.cliffMask[i],ramp:m.rampCells[i],walk:!!m.walk[i],face:v.cliffFaceMap[i],cliffJoin:v.cliffJoinMap[i],rampTile:v.rampTileMap[i]});
}
const dir=path.resolve(__dirname,'../../../Assets/Art/Terrain/Lab5FlatYTextures_v2');
fs.writeFileSync(path.join(dir,'sample_10x10.json'),JSON.stringify({seed:cfg.seed,cfg,attempt:m.attempt,mapSize:m.W,crop:{x:best.x,y:best.y,width:side,height:side},fullTerrainIslandCount:m.islandCount,visualDiagnostics:v.diagnostics,tileSizePolicy:v.tileSizePolicy,rampGroups:v.rampGroups.filter(g=>g.cells.some(i=>cells.some(c=>c.globalCell===i))),cells},null,2));
fs.writeFileSync(path.join(dir,'cliff_ramp_face_map.json'),JSON.stringify({seed:cfg.seed,cfg,...v},null,2));
console.log(JSON.stringify({seed:cfg.seed,crop:best,cliffFaces:v.cliffFaceMap.filter(Boolean).length,rampTiles:v.rampTileMap.filter(Boolean).length,diagnostics:v.diagnostics.length}));
