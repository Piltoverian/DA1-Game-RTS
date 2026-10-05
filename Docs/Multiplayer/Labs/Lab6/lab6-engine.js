/* Lab 6: deterministic render plan, independent from gameplay occupancy. */
(function(host){
'use strict';
const V=typeof module!=='undefined'&&module.exports?require('../Lab5/lab5-visual-tiles.js'):host.RTSVisualTiles;
const base={straight:{high:3,ports:10},outer_corner:{high:1,ports:9},inner_corner:{high:14,ports:9}};
const rampNames={single:'ramp_single',left:'ramp_left',center:'ramp_middle',right:'ramp_right'};
function compile(map){
 const v=map.visual||V.compile(map),draws=[],warnings=[];
 if(v.diagnostics.length)throw Error('Visual contract failed: '+JSON.stringify(v.diagnostics));
 for(let cell=0;cell<map.W*map.H;cell++){
  const face=v.cliffFaceMap[cell],ramp=v.rampTileMap[cell];
  if(face&&!face.open){
   if(face.parts.length>1)warnings.push({cell,code:'compound_opaque_parts',message:'Needs a transparent compound asset; current preview layers opaque parts.'});
   for(const p of face.parts){
    const b=base[p.shape],rotation=p.heightRotation;
    if(!b||V.rotateMask(b.high,rotation)!==p.surfaceSides.highQuadrants||V.rotateMask(b.ports,rotation)!==p.ports)throw Error('Cliff orientation mismatch at '+cell);
    draws.push({cell,layer:1,source:'cliff_'+p.shape,rotation,ports:p.ports,highQuadrants:p.surfaceSides.highQuadrants,lowQuadrants:p.surfaceSides.lowQuadrants});
   }
  }
  if(ramp){
   if(!face||!face.open||face.parts.length!==1||face.parts[0].shape!=='straight'||!Number.isInteger(ramp.direction)||ramp.direction<0||ramp.direction>3)throw Error('Illegal ramp cell '+cell);
   if(map.levels[ramp.highCell]<=map.levels[ramp.lowCell])throw Error('Ramp high/low reversed '+cell);
   const dx=ramp.highCell%map.W-ramp.lowCell%map.W,dy=Math.floor(ramp.highCell/map.W)-Math.floor(ramp.lowCell/map.W);
   const d=[[0,-1],[1,0],[0,1],[-1,0]][ramp.direction];
   if(dx!==d[0]||dy!==d[1])throw Error('Ramp direction differs from crossing '+cell);
   if(!rampNames[ramp.widthRole])throw Error('Unknown ramp width role');
   draws.push({cell,layer:2,source:rampNames[ramp.widthRole],rotation:ramp.direction,lowCell:ramp.lowCell,highCell:ramp.highCell,group:ramp.groupId});
  }
 }
 draws.sort((a,b)=>a.layer-b.layer||a.cell-b.cell);
 return {version:6,W:map.W,H:map.H,cellPixels:418,ground:'ground_grass',draws,warnings,diagnostics:v.diagnostics,rampGroups:v.rampGroups};
}
function crop(map,plan,side=10){
 if(!Number.isInteger(side)||side<1||side>Math.min(map.W,map.H))throw Error('Invalid crop size');
 let best={x:0,y:0,score:-1};
 for(let y=0;y<=map.H-side;y++)for(let x=0;x<=map.W-side;x++){
  let score=0,ramps=0,cliffs=0;
  for(let dy=0;dy<side;dy++)for(let dx=0;dx<side;dx++){
   const i=(y+dy)*map.W+x+dx,f=map.visual.cliffFaceMap[i];
   if(map.visual.rampTileMap[i]){score+=8;ramps++;}
   if(f&&!f.open){cliffs++;score+=f.parts.some(p=>p.shape.includes('corner'))?4:1;}
  }
  if(ramps&&cliffs&&score>best.score)best={x,y,score};
 }
 const cells=[];
 for(let y=0;y<side;y++)for(let x=0;x<side;x++){
  const i=(best.y+y)*map.W+best.x+x;
  cells.push({x,y,globalCell:i,level:map.levels[i],walk:!!map.walk[i],face:map.visual.cliffFaceMap[i],cliffJoin:map.visual.cliffJoinMap[i],rampTile:map.visual.rampTileMap[i]});
 }
 return {crop:{x:best.x,y:best.y,width:side,height:side},cells};
}
host.RTSLab6={compile,crop};
if(typeof module!=='undefined'&&module.exports)module.exports=host.RTSLab6;
})(typeof window!=='undefined'?window:globalThis);

