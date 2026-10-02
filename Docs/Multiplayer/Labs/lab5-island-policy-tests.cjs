const assert=require('node:assert/strict'),E=require('./lab5-engine.js'),R=require('./lab5-connectivity.js');
function scene(type){const W=48,T=W*W,solid=new Uint8Array(T),physical=new Uint8Array(T),cliff=new Uint8Array(T),mountainMask=new Uint8Array(T),rampCells=new Int32Array(T).fill(-1);for(let y=2;y<W-2;y++)for(let x=2;x<W-2;x++)solid[y*W+x]=physical[y*W+x]=1;
if(type==='cliff')for(let y=2;y<W-2;y++){physical[y*W+24]=0;cliff[y*W+24]=1;}
else if(type==='large')for(let y=2;y<W-2;y++){solid[y*W+24]=physical[y*W+24]=0;mountainMask[y*W+24]=1;}
else for(let y=20;y<=25;y++)for(let x=20;x<=25;x++)if(x===20||y===20||x===25||y===25){solid[y*W+x]=physical[y*W+x]=0;mountainMask[y*W+x]=1;}
const before=mountainMask.reduce((a,b)=>a+b,0),baked={physical,cliff,rampCells,ramps:[]},result=R.repair(baked,solid,W,W,{radius:.2,rampWidthMin:3,mountainMask},[{x:10,y:10}],E);return {result,added:mountainMask.reduce((a,b)=>a+b,0)-before,islands:E.components(E.compileWalk(physical,W,W,.2).walk,W,W).counts.length};}
const cliff=scene('cliff');assert(cliff.result.connected);assert(cliff.result.repairs>0);assert.equal(cliff.added,0);assert.equal(cliff.islands,1);
const large=scene('large');assert(!large.result.connected);assert.equal(large.added,0);
const small=scene('small');assert(small.result.connected);assert.equal(small.added,16);assert.equal(small.islands,1);
console.log(JSON.stringify({cliff,large,small}));
