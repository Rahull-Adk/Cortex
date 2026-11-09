const fs=require('fs');
try{
const inp=JSON.parse(fs.readFileSync(process.argv[2],'utf8'));
const nodes=inp.fileNodes, imp=inp.importEdges||[], all=inp.allEdges||[];
const byId={};nodes.forEach(n=>byId[n.id]=n);
const fp=n=>(n.filePath||n.id.replace(/^[a-z]+:/,'').split(':')[0]).split(String.fromCharCode(92)).join('/');
const grp=n=>{const p=fp(n).split('/');return p.length>1?p[0]:'root'};
const G={},T={};nodes.forEach(n=>{(G[grp(n)]=G[grp(n)]||[]).push(n.id);(T[n.type]=T[n.type]||[]).push(n.id)});
const fin={},fout={},ig={},intra={};
imp.forEach(e=>{fout[e.source]=(fout[e.source]||0)+1;fin[e.target]=(fin[e.target]||0)+1;
 const a=byId[e.source],b=byId[e.target];if(!a||!b)return;const ga=grp(a),gb=grp(b);
 intra[ga]=intra[ga]||{internalEdges:0,totalEdges:0};intra[gb]=intra[gb]||{internalEdges:0,totalEdges:0};
 if(ga===gb){intra[ga].internalEdges++;intra[ga].totalEdges++}else{intra[ga].totalEdges++;intra[gb].totalEdges++;const k=ga+'->'+gb;ig[k]=(ig[k]||0)+1}});
Object.values(intra).forEach(v=>v.density=v.totalEdges?+(v.internalEdges/v.totalEdges).toFixed(2):0);
const cc={};all.forEach(e=>{const a=byId[e.source],b=byId[e.target];if(!a||!b)return;const k=a.type+'|'+b.type+'|'+e.type;cc[k]=(cc[k]||0)+1});
const pat=g=>/Api$/.test(g)?'api':/Application$/.test(g)?'service':/Domain$/.test(g)?'data':/Infrastructure$/.test(g)?'infrastructure':'root';
const pm={};Object.keys(G).forEach(g=>pm[g]=pat(g));
const igl=Object.entries(ig).map(([k,c])=>{const[a,b]=k.split('->');return{from:a,to:b,count:c}});
const dd=[];igl.forEach(x=>{const r=ig[x.to+'->'+x.from]||0;if(x.count>r)dd.push({dependent:x.from,dependsOn:x.to})});
const paths=nodes.map(fp);
const out={scriptCompleted:true,directoryGroups:G,nodeTypeGroups:T,
 crossCategoryEdges:Object.entries(cc).map(([k,c])=>{const[a,b,t]=k.split('|');return{fromType:a,toType:b,edgeType:t,count:c}}),
 interGroupImports:igl,intraGroupDensity:intra,patternMatches:pm,
 deploymentTopology:{hasDockerfile:paths.some(p=>/Dockerfile/.test(p)),hasCompose:paths.some(p=>/docker-compose/.test(p)),hasK8s:false,hasTerraform:paths.some(p=>/\.tf$/.test(p)),hasCI:paths.some(p=>/\.github\/workflows|gitlab-ci|Jenkinsfile/.test(p)),infraFiles:[...new Set(paths.filter(p=>/docker|Dockerfile/i.test(p)))]},
 dataPipeline:{schemaFiles:[],migrationFiles:[...new Set(paths.filter(p=>/Migrations\//.test(p)))],dataModelFiles:paths.filter(p=>/Entities\//.test(p)),apiHandlerFiles:paths.filter(p=>/Controllers\//.test(p))},
 docCoverage:(()=>{const w=Object.keys(G).filter(g=>G[g].some(id=>byId[id].type==='document'));return{groupsWithDocs:w.length,totalGroups:Object.keys(G).length,coverageRatio:+(w.length/Object.keys(G).length).toFixed(2),undocumentedGroups:Object.keys(G).filter(g=>!w.includes(g))}})(),
 dependencyDirection:dd,
 fileStats:{totalFileNodes:nodes.length,filesPerGroup:Object.fromEntries(Object.entries(G).map(([k,v])=>[k,v.length])),nodeTypeCounts:Object.fromEntries(Object.entries(T).map(([k,v])=>[k,v.length]))},
 fileFanIn:fin,fileFanOut:fout};
fs.writeFileSync(process.argv[3],JSON.stringify(out,null,2));
}catch(e){console.error(e);process.exit(1)}
