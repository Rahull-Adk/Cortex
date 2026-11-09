const fs=require("fs");const path=require("path");
try{
const inp=JSON.parse(fs.readFileSync(process.argv[2],"utf8"));
const nodes=inp.nodes,ids=new Set(nodes.map(n=>n.id));
const edges=inp.edges.filter(e=>ids.has(e.source)&&ids.has(e.target)&&e.source!==e.target);
const fi={},fo={};nodes.forEach(n=>{fi[n.id]=new Set();fo[n.id]=new Set();});
edges.forEach(e=>{fi[e.target].add(e.source);fo[e.source].add(e.target);});
const byId=Object.fromEntries(nodes.map(n=>[n.id,n]));
const fanIn=nodes.map(n=>({id:n.id,fanIn:fi[n.id].size,name:n.name})).sort((a,b)=>b.fanIn-a.fanIn);
const fanOut=nodes.map(n=>({id:n.id,fanOut:fo[n.id].size,name:n.name})).sort((a,b)=>b.fanOut-a.fanOut);
const foSorted=fanOut.map(x=>x.fanOut),fiSorted=fanIn.map(x=>x.fanIn).slice().sort((a,b)=>a-b);
const foTh=foSorted[Math.max(0,Math.floor(nodes.length*0.1)-1)],fiTh=fiSorted[Math.floor(nodes.length*0.25)];
const entryNames=["index.ts","index.js","main.ts","main.js","app.ts","app.js","server.ts","server.js","mod.rs","main.go","main.py","main.rs","manage.py","app.py","wsgi.py","asgi.py","run.py","__main__.py","Application.java","Main.java","Program.cs","config.ru","index.php","App.swift","Application.kt","main.cpp","main.c"];
const cands=[];
for(const n of nodes){let s=0;const fp=n.filePath||"";const depth=fp.split("/").length-1;
 if(n.type==="file"){if(entryNames.includes(n.name))s+=3;if(depth<=1)s+=1;if(fo[n.id].size>=foTh&&foTh>0)s+=1;if(fi[n.id].size<=fiTh)s+=1;}
 else if(n.type==="document"){if(/^readme\.md$/i.test(fp))s+=5;else if(depth===0&&fp.endsWith(".md"))s+=2;}
 if(s>0)cands.push({id:n.id,score:s,name:n.name,summary:n.summary,type:n.type});}
cands.sort((a,b)=>b.score-a.score);
const start=cands.find(c=>c.type==="file");
const adj={};edges.filter(e=>e.type==="imports"||e.type==="calls").forEach(e=>{(adj[e.source]=adj[e.source]||[]).push(e.target);});
const order=[],depthMap={},byDepth={};
if(start){const q=[start.id];depthMap[start.id]=0;while(q.length){const c=q.shift();order.push(c);(byDepth[depthMap[c]]=byDepth[depthMap[c]]||[]).push(c);for(const t of adj[c]||[])if(!(t in depthMap)){depthMap[t]=depthMap[c]+1;q.push(t);}}}
const cat=t=>t==="document"?"documentation":["service","pipeline","resource"].includes(t)?"infrastructure":["table","schema","endpoint"].includes(t)?"data":t==="config"?"config":null;
const nonCode={documentation:[],infrastructure:[],data:[],config:[]};
nodes.forEach(n=>{const c=cat(n.type);if(c)nonCode[c].push({id:n.id,name:n.name,type:n.type,summary:n.summary});});
const rel=new Set(edges.filter(e=>e.type==="imports"||e.type==="calls").map(e=>e.source+"|"+e.target+"|"+e.type));
const und={};edges.forEach(e=>{(und[e.source]=und[e.source]||new Set()).add(e.target);(und[e.target]=und[e.target]||new Set()).add(e.source);});
const clusters=[];const seen=new Set();
for(const k of rel){const[a,b,t]=k.split("|");if(rel.has(b+"|"+a+"|"+t)){const key=[a,b].sort().join("|");if(seen.has(key))continue;seen.add(key);
 const cl=new Set([a,b]);for(const n of nodes){if(cl.size>=5)break;if(cl.has(n.id))continue;let c=0;for(const m of cl)if(und[n.id]&&und[n.id].has(m))c++;if(c>=2)cl.add(n.id);}
 const arr=[...cl];const ec=edges.filter(e=>cl.has(e.source)&&cl.has(e.target)).length;clusters.push({nodes:arr,edgeCount:ec});}}
if(clusters.length<3){// fallback: dense neighborhoods by shared edges
 for(const n of fanIn.slice(0,10)){const nb=[...(und[n.id]||[])].sort((x,y)=>(und[y]?.size||0)-(und[x]?.size||0)).slice(0,4);const cl=new Set([n.id,...nb]);clusters.push({nodes:[...cl],edgeCount:edges.filter(e=>cl.has(e.source)&&cl.has(e.target)).length,fallback:true});}}
clusters.sort((a,b)=>b.edgeCount-a.edgeCount);
const idx={};nodes.forEach(n=>idx[n.id]={name:n.name,type:n.type,summary:n.summary});
const edgeTypes={};edges.forEach(e=>edgeTypes[e.type]=(edgeTypes[e.type]||0)+1);
fs.writeFileSync(process.argv[3],JSON.stringify({scriptCompleted:true,entryPointCandidates:cands.slice(0,5),fanInRanking:fanIn.slice(0,20),fanOutRanking:fanOut.slice(0,20),bfsTraversal:{startNode:start&&start.id,order,depthMap,byDepth},nonCodeFiles:nonCode,clusters:clusters.slice(0,10),layers:{count:inp.layers.length,list:inp.layers},nodeSummaryIndex:idx,totalNodes:nodes.length,totalEdges:inp.edges.length,fileLevelEdgeTypes:edgeTypes},null,1));
}catch(e){console.error(e.stack);process.exit(1);}
