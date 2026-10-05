const fs=require('fs'),path=require('path'),E=require('./lab5-engine.js'),V=require('./lab5-visual-tiles.js');
const cfg={seed:30000,players:4,size:192,radius:.2,protectedRadius:6,rampWidthMin:3,rampWidthMax:7,mainCount:0,secondaryCount:0,advantageCount:0,contestedCount:0};
const m=E.generate(cfg),v=V.compile(m),tiles=new Map(),cells=[],missing=new Set();
function descriptor(i){
 let source='ground_grass',direction=0,kind='ground',unsupported=null;
 const r=v.rampTileMap[i],f=v.cliffFaceMap[i];
 if(m.mountainMask[i]){source='mountain_rock';kind='mountain';}
 else if(r){kind='ramp';direction=r.direction;source={single:'ramp_single',left:'ramp_left',center:'ramp_middle',right:'ramp_right'}[r.widthRole];if(r.shape==='middle_corner')source=r.cornerType==='inner_corner'?'ramp_middle_inner_corner':'ramp_middle_outer_corner';}
 else if(f&&!f.open){kind='cliff';const mask=f.cornerMask;
  if(f.shape==='outer_corner'){source='cliff_outer_corner';direction={1:0,2:1,4:2,8:3}[mask];}
  else if(f.shape==='inner_corner'){source='cliff_inner_corner';direction={14:0,13:1,11:2,7:3}[mask];}
  else {source='cliff_straight';direction={3:0,6:1,12:2,9:3}[mask]??0;if(['fill','split_corners','endcap'].includes(f.shape)){unsupported=f.shape;missing.add(f.shape);}}
 }
 const key=[source,direction,unsupported||''].join('/');if(!tiles.has(key))tiles.set(key,{key,source,direction,kind,unsupported});return key;
}
for(let i=0;i<m.W*m.H;i++)cells.push(descriptor(i));
const cases=new Map();function add(type,w,h,keys,x,y){const signature=[type,...keys].join('|');if(cases.has(signature)){cases.get(signature).count++;return;}cases.set(signature,{id:cases.size,type,width:w,height:h,tileKeys:keys,count:1,first:{x,y}});}
for(let y=0;y<m.H;y++)for(let x=0;x<m.W;x++){
 const i=y*m.W+x;
 if(x+1<m.W)add('pair_E',2,1,[cells[i],cells[i+1]],x,y);
 if(y+1<m.H)add('pair_S',1,2,[cells[i],cells[i+m.W]],x,y);
 if(x+1<m.W&&y+1<m.H){const keys=[cells[i],cells[i+1],cells[i+m.W],cells[i+m.W+1]];add('corner_2x2',2,2,keys,x,y);}
}
// Also inspect wide-ramp recipes even when this specific seed has no such width.
for(let d=0;d<4;d++)for(let width=1;width<=7;width++){
 const keys=Array.from({length:width},(_,i)=>{const source=width===1?'ramp_single':i===0?'ramp_left':i===width-1?'ramp_right':'ramp_middle';const key=source+'/'+d+'/';if(!tiles.has(key))tiles.set(key,{key,source,direction:d,kind:'ramp',unsupported:null});return key;});
 // Preserve left -> right relative to the low -> high travel direction.
 const ordered=d===2||d===3?keys.slice().reverse():keys;
 add('recipe_width_'+width+'_dir_'+d,d%2?1:width,d%2?width:1,ordered,-1,-1);
}
for(const source of ['ramp_middle_outer_corner','ramp_middle_inner_corner'])for(let d=0;d<4;d++){
 const key=source+'/'+d+'/';if(!tiles.has(key))tiles.set(key,{key,source,direction:d,kind:'ramp',unsupported:null});
 const keys=['ramp_left/'+d+'/',key,'ramp_right/'+d+'/'];
 add('recipe_corner_'+source+'_dir_'+d,d%2?1:3,d%2?3:1,d===2||d===3?keys.slice().reverse():keys,-1,-1);
}
// Cliff-to-ramp shoulders: both ends plus widths 1..7, all cardinal headings.
const keyFor=(source,d)=>{const key=source+'/'+d+'/';if(!tiles.has(key))tiles.set(key,{key,source,direction:d,kind:source.startsWith('cliff')?'cliff':source==='ground_grass'?'ground':'ramp',unsupported:null});return key;};
for(let d=0;d<4;d++){
 const straight=keyFor('cliff_straight',d);
 add('recipe_cliff_repeat_'+d,d%2?1:2,d%2?2:1,[straight,straight],-1,-1);
 for(let width=1;width<=7;width++){
  const keys=[straight,...Array.from({length:width},(_,i)=>keyFor(width===1?'ramp_single':i===0?'ramp_left':i===width-1?'ramp_right':'ramp_middle',d)),straight];
  add('recipe_cliff_ramp_'+width+'_dir_'+d,d%2?1:width+2,d%2?width+2:1,d===2||d===3?keys.slice().reverse():keys,-1,-1);
 }
 for(const type of ['outer','inner']){
  const cells=Array(9).fill(keyFor('ground_grass',0));
  const parts=type==='outer'?[{x:1,y:1,s:'cliff_outer_corner',d:0},{x:0,y:1,s:'cliff_straight',d:0},{x:1,y:0,s:'cliff_straight',d:3}]:[{x:1,y:1,s:'cliff_inner_corner',d:0},{x:0,y:1,s:'cliff_straight',d:2},{x:1,y:0,s:'cliff_straight',d:1}];
  for(const p of parts){let {x,y}=p;for(let r=0;r<d;r++){const nx=2-y;y=x;x=nx;}cells[y*3+x]=keyFor(p.s,(p.d+d)%4);}
  add('recipe_cliff_corner_'+type+'_dir_'+d,3,3,cells,-1,-1);
 }
}
const pairOccurrences=[...cases.values()].filter(c=>c.type.startsWith('pair')).reduce((n,c)=>n+c.count,0);
if(pairOccurrences!==m.H*(m.W-1)+m.W*(m.H-1))throw Error('Incomplete adjacency coverage');
const cornerOccurrences=[...cases.values()].filter(c=>c.type==='corner_2x2').reduce((n,c)=>n+c.count,0);
if(cornerOccurrences!==(m.W-1)*(m.H-1))throw Error('Incomplete 2x2 coverage');
const out={seed:cfg.seed,cfg,attempt:m.attempt,mapSize:m.W,missingVariants:[...missing],cornerOccurrences,expectedCornerOccurrences:(m.W-1)*(m.H-1),pairOccurrences,expectedPairOccurrences:m.H*(m.W-1)+m.W*(m.H-1),tiles:[...tiles.values()],cases:[...cases.values()],rampGroups:v.rampGroups};
const dir=path.resolve(__dirname,'../../../Assets/Art/Terrain/Lab5TextureReview_v3/seam-audit');fs.mkdirSync(dir,{recursive:true});fs.writeFileSync(path.join(dir,'cases.json'),JSON.stringify(out,null,2));
console.log(JSON.stringify({seed:cfg.seed,pairs:out.cases.filter(c=>c.type.startsWith('pair')).length,corners:out.cases.filter(c=>c.type==='corner_2x2').length,recipes:out.cases.filter(c=>c.type.startsWith('recipe')).length,missingVariants:out.missingVariants,groups:v.rampGroups.length}));
