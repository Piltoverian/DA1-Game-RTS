/* Pure visual compiler: no RNG, seed exceptions, sprite scaling or gameplay writes. */
(function(host){
'use strict';
const dirs=[[0,-1],[1,0],[0,1],[-1,0]];
const bitCount=n=>{let c=0;for(;n;n&=n-1)c++;return c;};
function rampRoles(length){if(!Number.isInteger(length)||length<1)throw Error('Ramp run must contain cells');return length===1?['single']:Array.from({length},(_,i)=>i===0?'start':i===length-1?'end':'middle');}
function cliffShape(mask){const n=bitCount(mask);return n===1?'outer_corner':n===3?'inner_corner':mask===5||mask===10?'split_corners':n===2?'straight':'fill';}
function rotateMask(mask,r){return ((mask<<r)|(mask>>(4-r)))&15;}
function connectionShape(mask){
 const directions=[0,1,2,3].filter(d=>mask&(1<<d));
 if(directions.length!==2)return null;
 return (directions[0]+2)%4===directions[1]?'straight':'corner';
}
// Occupancy bits are NW, NE, SE, SW. Connection bits are N, E, S, W.
// These are separate coordinate domains; neither substitutes for the other.
function surfaceSides(part,highLevel,lowLevel){
 return {highQuadrants:part.cornerMask,lowQuadrants:15^part.cornerMask,
  highLevel,lowLevel,downhillDirections:[0,1,2,3].filter(d=>part.lowEdges&(1<<d)),
  downhillDiagonalQuadrants:part.shape==='inner_corner'?15^part.cornerMask:0};
}
function matchCliffAsset(part,assets){
 return assets.find(a=>a.shape===part.shape&&a.connectionMask===part.ports&&
  a.highQuadrants===part.surfaceSides.highQuadrants&&
  a.lowQuadrants===part.surfaceSides.lowQuadrants)??null;
}
// Directions are LOW-facing normals. Ports are edges connecting adjacent sprites.
function cliffParts(lowEdges,diagonalMask=0){
 const parts=[],straightMasks=[12,9,3,6];
 const add=(shape,mask,ports,normals)=>{
  const base=shape==='outer_corner'?1:shape==='inner_corner'?14:3;
  const rotation=[0,1,2,3].find(r=>rotateMask(base,r)===mask);
  if(rotation===undefined)throw Error('No authored orientation for cliff mask '+mask);
  parts.push({shape,cornerMask:mask,rotation,ports,lowEdges:normals,source:'cliff_'+shape});
 };
 const normals=[0,1,2,3].filter(d=>lowEdges&(1<<d));
 if(normals.length===2&&((normals[0]+2)%4!==normals[1])){
  // Intersecting normal rays meet on the HIGH side of the junction.
  const a=normals[0],b=normals[1];
  const mask=({3:8,6:1,12:2,9:4})[lowEdges];
  add('outer_corner',mask,(1<<((a+2)%4))|(1<<((b+2)%4)),lowEdges);
 }else for(const d of normals)add('straight',straightMasks[d],(1<<((d+1)%4))|(1<<((d+3)%4)),1<<d);
 if(!lowEdges)for(let c=0;c<4;c++)if(diagonalMask&(1<<c)){
  // Concave connector goes toward the two high neighbours facing this low diagonal.
  const portPairs=[9,3,6,12];add('inner_corner',15^(1<<c),portPairs[c],0);
 }
 return parts;
}
function compile(map){
 const {W,H,levels,cliffMask,mountainMask,walk,rampCells}=map,T=W*H;
 for(const a of [levels,cliffMask,mountainMask,walk,rampCells])if(!a||a.length!==T)throw Error('Invalid visual input dimensions');
 const index=(x,y)=>x<0||y<0||x>=W||y>=H?-1:y*W+x;
 const neighbour=(i,d)=>index(i%W+dirs[d][0],Math.floor(i/W)+dirs[d][1]);
 const cliffFaceMap=Array(T).fill(null),rampTileMap=Array(T).fill(null),cliffJoinMap=Array(T).fill(null),diagnostics=[];
 for(let i=0;i<T;i++){
  if(mountainMask[i])continue;
  const x=i%W,y=Math.floor(i/W),high=levels[i];let low=high,lowEdges=0,diagonalMask=0;const lowerNeighbours=[];
  for(let d=0;d<4;d++){const j=neighbour(i,d);if(j>=0&&!mountainMask[j]&&levels[j]<high){lowEdges|=1<<d;lowerNeighbours.push(j);low=Math.min(low,levels[j]);}}
  if(!lowEdges)for(let c=0;c<4;c++){
   const dx=[-1,1,1,-1][c],dy=[-1,-1,1,1][c];
   const j=index(x+dx,y+dy),a=index(x+dx,y),b=index(x,y+dy);
   if(j>=0&&a>=0&&b>=0&&!mountainMask[j]&&!mountainMask[a]&&!mountainMask[b]&&levels[j]<high&&levels[a]===high&&levels[b]===high){diagonalMask|=1<<c;low=Math.min(low,levels[j]);}
  }
  const parts=cliffParts(lowEdges,diagonalMask);if(!parts.length)continue;
  const primary=parts[0];
  cliffFaceMap[i]={cell:i,ownerSide:'high',level:high,shape:primary.shape,cornerMask:primary.cornerMask,
   rotation:primary.rotation,ports:parts.reduce((m,p)=>m|p.ports,0),parts,
   lowEdges,highEdges:lowEdges,lowerNeighbours,lowLevel:low,highLevel:high,delta:high-low,
   connector:lowEdges===0,diagonalMask,bandDepth:0,open:false,spriteKey:`cliff/${primary.shape}/${primary.rotation}`};
  const face=cliffFaceMap[i];
  face.surfaceSides=parts.map(p=>surfaceSides(p,high,low));
  face.heightNeighbours=[0,1,2,3].map(direction=>{
   const cell=neighbour(i,direction);
   return {direction,cell,level:cell<0?null:levels[cell],
    relation:cell<0?'outside':levels[cell]<high?'lower':levels[cell]>high?'higher':'same'};
  });
  for(const p of parts)p.surfaceSides=surfaceSides(p,high,low);
 }
 // The slope replaces the HIGH-side face at its actual low/high interface.
 const crossings=[],lanes=[],anchors=new Map();
 const legal=i=>i>=0&&walk[i]&&!mountainMask[i];
 for(let i=0;i<T;i++)for(const d of [1,2]){
  const j=neighbour(i,d);
  if(!legal(i)||!legal(j)||levels[i]===levels[j]||(rampCells[i]<0&&rampCells[j]<0))continue;
  const low=levels[i]<levels[j]?i:j,high=low===i?j:i,direction=low===i?d:(d+2)%4;
  // A slope must replace an existing cliff face, never a ground landing.
  if(!cliffMask[low])continue;
  const face=cliffFaceMap[high];
  if(!face||face.parts.length!==1||face.parts[0].shape!=='straight'){
   diagnostics.push({kind:'illegal_corner_ramp',cell:high,lowCell:low});continue;
  }
  const crossing={lowCell:low,highCell:high,direction,lowLevel:levels[low],highLevel:levels[high]};
  crossings.push(crossing);lanes.push({id:lanes.length,cells:[low,high],slopeCells:[high],crossing});
  if(!anchors.has(low))anchors.set(low,[]);anchors.get(low).push(crossing);
 }
 // Build connected interface-edge groups BEFORE selecting any cell sprite.
 // A shared grid vertex joins stair-step boundary segments without claiming
 // the intervening diagonal cliff cell as a ramp cell.
 const rampGroups=[],vertexEdges=new Map();
 for(let e=0;e<crossings.length;e++){
  const c=crossings[e],x=c.lowCell%W,y=Math.floor(c.lowCell/W);
  const endpoints=c.direction===0?[[x,y],[x+1,y]]:c.direction===1?[[x+1,y],[x+1,y+1]]:c.direction===2?[[x,y+1],[x+1,y+1]]:[[x,y],[x,y+1]];
  c.vertices=endpoints.map(v=>v.join(','));
  c.sourceRampId=rampCells[c.lowCell]>=0?rampCells[c.lowCell]:rampCells[c.highCell];
  for(const vertex of c.vertices){if(!vertexEdges.has(vertex))vertexEdges.set(vertex,[]);vertexEdges.get(vertex).push(e);}
 }
 // Each anchor belongs to one cardinal face. Secondary edges describe its
 // corner, rather than creating another overlapping or diagonal ramp.
 const sourceVotes=new Map();
 for(const c of crossings){if(!sourceVotes.has(c.sourceRampId))sourceVotes.set(c.sourceRampId,[0,0,0,0]);sourceVotes.get(c.sourceRampId)[c.direction]++;}
 const anchorDirection=new Map();
 for(const [cell,list] of anchors){const votes=sourceVotes.get(list[0].sourceRampId);const preferred=map.rampFaceDirection?map.rampFaceDirection[cell]:-1;anchorDirection.set(cell,list.find(c=>c.direction===preferred)?.direction??list.slice().sort((a,b)=>votes[b.direction]-votes[a.direction]||a.direction-b.direction)[0].direction);}
 const assigned=new Uint8Array(crossings.length);
 for(let e=0;e<crossings.length;e++)if(crossings[e].direction!==anchorDirection.get(crossings[e].lowCell))assigned[e]=1;
 for(let root=0;root<crossings.length;root++){
  if(assigned[root])continue;
  const edges=[root],seed=crossings[root];assigned[root]=1;
  for(let head=0;head<edges.length;head++){
   const c=crossings[edges[head]];
   for(const vertex of c.vertices)for(const e of vertexEdges.get(vertex)){
    const n=crossings[e];
    if(assigned[e]||n.sourceRampId!==seed.sourceRampId||n.lowLevel!==seed.lowLevel||n.highLevel!==seed.highLevel||n.direction!==seed.direction)continue;
    assigned[e]=1;edges.push(e);
   }
  }
  const votes=[0,0,0,0];for(const e of edges)votes[crossings[e].direction]++;
  const direction=votes.indexOf(Math.max(...votes));
  const vector=dirs[direction].slice();
  const tangent=dirs[(direction+1)%4];
  const cells=[...new Set(edges.map(e=>crossings[e].lowCell))].filter(i=>!rampTileMap[edges.map(e=>crossings[e]).find(c=>c.lowCell===i).highCell]).sort((a,b)=>(a%W-b%W)*tangent[0]+(Math.floor(a/W)-Math.floor(b/W))*tangent[1]||a-b);
  if(!cells.length)continue;
  const id=rampGroups.length;
  const group={id,sourceRampId:seed.sourceRampId,cells,edgeIndices:edges,width:cells.length,interfaceEdgeCount:edges.length,direction,directionVector:vector,directionVotes:votes,lowLevel:seed.lowLevel,highLevel:seed.highLevel,slopeLength:1};
  rampGroups.push(group);
  for(let k=0;k<cells.length;k++){
   const i=cells[k];
   if(rampTileMap[i]){diagnostics.push({kind:'shared_ramp_anchor',cell:i,groups:[rampTileMap[i].groupId,id]});continue;}
   const highCell=edges.map(e=>crossings[e]).find(c=>c.lowCell===i).highCell;
   const face=cliffFaceMap[highCell],cornerMask=face.cornerMask,cornerType=cliffShape(cornerMask);
   const list=edges.map(e=>crossings[e]).filter(c=>c.lowCell===i);
   const highEdges=anchors.get(i).reduce((mask,c)=>mask|(1<<c.direction),0);
   let diagonalCliffMask=0;const x=i%W,y=Math.floor(i/W);
   for(let c=0;c<4;c++){const j=index(x+([1,2].includes(c)?1:-1),y+(c>=2?1:-1));if(j>=0&&cliffMask[j]&&!walk[j])diagonalCliffMask|=1<<c;}
   const widthRole=cells.length===1?'single':k===0?'left':k===cells.length-1?'right':'center';
   // The contour can bend, but the traversable slope has one global direction.
   // Its corner decoration is emitted separately in cliffJoinMap below.
   const isCorner=false;
   const shape=isCorner?'middle_corner':'straight';
   const exposedSides=(k===0?1<<((direction+3)%4):0)|(k===cells.length-1?1<<((direction+1)%4):0);
   face.open=true;
   rampTileMap[highCell]={cell:highCell,lowCell:i,highCell,ownerSide:'high',groupId:id,groupWidth:group.width,globalDirection:group.directionVector,widthIndex:k,role:widthRole,widthRole,shape,direction,spriteRotation:isCorner?face.rotation:direction,highEdges,ports:highEdges,exposedSides,level:levels[highCell],cornerMask,cornerType,diagonalCliffMask,slopeLength:1,lowLevel:group.lowLevel,highLevel:group.highLevel,spriteKey:'ramp/'+shape+'/'+(isCorner?cornerMask:direction)+'/'+widthRole};
  }
  group.lowCells=cells.slice();group.cells=cells.map(i=>edges.map(e=>crossings[e]).find(c=>c.lowCell===i).highCell);
 }
 // Cliff terminals beside an opening have authored end pieces. Preserve corner
 // types; only plain straight strips can become end caps.
 for(const f of cliffFaceMap){
  if(!f||f.open||f.shape!=='straight')continue;
  let ends=0;for(let d=0;d<4;d++){
   const j=neighbour(f.cell,d);if(j>=0&&rampTileMap[j]&&walk[j])ends|=1<<d;
  }
  if(ends){f.shape='endcap';f.endMask=ends;f.spriteKey=`cliff/endcap/${f.cornerMask}/${ends}`;}
 }
 // Resolve ports against the actual neighbouring contour cells, including
 // faces replaced by ramps. A direction alone is not a connection.
 for(const f of cliffFaceMap){
  if(!f)continue;
  f.connections=[];
  for(let d=0;d<4;d++){
   if(!(f.ports&(1<<d)))continue;
   const j=neighbour(f.cell,d),other=j>=0?cliffFaceMap[j]:null;
   if(other&&other.highLevel===f.highLevel&&other.lowLevel===f.lowLevel&&(other.ports&(1<<((d+2)%4))))
    f.connections.push({direction:d,cell:j,kind:rampTileMap[j]?'ramp':'cliff'});
  }
  f.connectionMask=f.connections.reduce((mask,c)=>mask|(1<<c.direction),0);
  for(const part of f.parts){
   part.connections=f.connections.filter(c=>part.ports&(1<<c.direction));
   part.connectionMask=part.connections.reduce((mask,c)=>mask|(1<<c.direction),0);
   const pathShape=connectionShape(part.connectionMask);
   // Connections determine the path, while height occupancy determines
   // convex/concave orientation. Never overwrite occupancy from path alone.
   if(pathShape==='corner'&&part.shape==='straight')
    diagnostics.push({kind:'path_surface_conflict',cell:f.cell,connectionMask:part.connectionMask,highQuadrants:part.surfaceSides.highQuadrants});
   part.pathShape=pathShape;
   part.source='cliff_'+part.shape;
   part.assetRequirement={shape:part.shape,connectionMask:part.ports,
    highQuadrants:part.surfaceSides.highQuadrants,lowQuadrants:part.surfaceSides.lowQuadrants};
   // Authored base tiles connect W/E (straight) or N/W (both corners).
   // Keep this independent of high/low corner occupancy masks.
   const basePorts=part.shape==='straight'?10:9;
   part.connectionRotation=[0,1,2,3].find(r=>rotateMask(basePorts,r)===part.ports);
   if(part.connectionRotation===undefined)throw Error('Unsupported cliff connection ports '+part.ports);
   part.heightRotation=part.rotation;
  }
  const bend=connectionShape(f.connectionMask)==='corner';
  f.pathShape=bend?'corner':connectionShape(f.connectionMask);
  if(bend&&f.parts.length===1){
   f.shape=f.parts[0].shape;
   const ramp=rampTileMap[f.cell];
   if(ramp){
    // Turning contour decoration must not rotate the cardinal ramp surface.
    ramp.shape='straight';ramp.spriteRotation=ramp.direction;
    cliffJoinMap[f.cell]={cell:f.cell,shape:f.parts[0].shape,
     rotation:f.parts[0].connectionRotation,connections:f.connections,
     connectionMask:f.connectionMask,walkable:!!walk[f.cell],
     collision:false,overlayOnly:true,preserveRampSurface:true,
     surfaceSides:f.parts[0].surfaceSides,assetRequirement:f.parts[0].assetRequirement,
     assetRequired:'transparent cliff corner trim'};
   }
  }
  if(f.connector&&!f.open)cliffJoinMap[f.cell]={cell:f.cell,shape:f.parts[0].shape,
   rotation:f.parts[0].connectionRotation,connections:f.connections,
   connectionMask:f.connectionMask,walkable:!!walk[f.cell],
   collision:false,overlayOnly:true,preserveRampSurface:false,
   surfaceSides:f.parts[0].surfaceSides,assetRequirement:f.parts[0].assetRequirement,
   assetRequired:'transparent cliff corner trim'};
 }
 return {W,H,cliffFaceMap,rampTileMap,cliffJoinMap,rampGroups,lanes,crossings,diagnostics,cliffPlacementPolicy:'high-side boundary only; low collision band is separate',tileSizePolicy:'fixed: sprite width and height must equal cellPixels; no destination scaling'};
}
host.RTSVisualTiles={compile,rampRoles,cliffShape,cliffParts,rotateMask,connectionShape,surfaceSides,matchCliffAsset};
if(typeof module!=='undefined'&&module.exports)module.exports=host.RTSVisualTiles;
})(typeof window!=='undefined'?window:globalThis);

