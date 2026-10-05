'use strict';
// A lossless checkpoint of compiler inputs; generation stays in the Lab 5 engine.
const E=require('../Lab5/lab5-engine.js'),L=require('./lab6-engine.js');
const names=['cliff_straight','cliff_outer_corner','cliff_inner_corner','ramp_single','ramp_left','ramp_middle','ramp_right','ground_grass','ground_soil'];
function buildSnapshot(input){
 const map=E.generate(input),plan=L.compile(map);
 const terrain={};
 for(const key of ['kind','levels','walk','mountainMask','cliffMask','rampCells','rampFaceDirection'])terrain[key]=Array.from(map[key]);
 return {schemaVersion:1,generator:'Lab5',compilerVersion:plan.version,cfg:map.cfg,
  W:map.W,H:map.H,attempt:map.attempt,contourRunCells:map.contourRunCells,
  islandCount:map.islandCount,terrain,bases:map.bases,ramps:map.ramps,rampQuota:map.rampQuota,
  // Keep observational balance results; they are not admission gates for this port.
  balance:{accepted:map.accepted,violations:map.violations},
  atlas:{assetPath:'Assets/Art/Terrain/Lab5TextureReview_v17/atlas.png',width:1254,height:1254,cellPixels:418,
   tiles:names.map((id,k)=>({id,x:k%3*418,y:Math.floor(k/3)*418,width:418,height:418}))},
  plan};
}
module.exports={buildSnapshot};
if(require.main===module){
 const fs=require('node:fs'),path=require('node:path');
 const seed=Number(process.argv[2]??30000),width=Number(process.argv[3]??3);
 if(!Number.isSafeInteger(seed)||!Number.isInteger(width)||width<1||width>12)throw Error('Expected integer seed and ramp width 1..12');
 const snapshot=buildSnapshot({seed,players:4,size:256,fullMap:true,protectedRadius:6,rampWidthMin:width,rampWidthMax:width,mainCount:0,secondaryCount:0,advantageCount:0,contestedCount:0});
 const directory=path.resolve(__dirname,'../../../../Assets/MapGen/Fixtures');
 fs.mkdirSync(directory,{recursive:true});
 const output=path.join(directory,`lab6-${seed}-width${width}.json`);
 fs.writeFileSync(output,JSON.stringify(snapshot));
 console.log(JSON.stringify({output,seed,width,islands:snapshot.islandCount,draws:snapshot.plan.draws.length,warnings:snapshot.plan.warnings.length}));
}
