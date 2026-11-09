const fs = require('fs');
const D = process.argv[2];
const read = p => JSON.parse(fs.readFileSync(p, 'utf8'));
const g = read(D + '/intermediate/assembled-graph.json');
const scan = read(D + '/intermediate/scan-result.json');
const ids = new Set(g.nodes.map(n => n.id));
const PFX = /^(file|config|document|service|pipeline|table|schema|resource|endpoint):/;
const fix = id => (PFX.test(id) ? id : 'file:' + id);

let layers = read(D + '/intermediate/layers.json');
if (!Array.isArray(layers)) layers = layers.layers;
layers = layers.map(l => {
  let nodeIds = l.nodeIds || l.nodes || [];
  nodeIds = nodeIds.map(x => (typeof x === 'object' ? x.id : x)).map(fix).filter(x => ids.has(x));
  const id = l.id || 'layer:' + l.name.toLowerCase().replace(/[^a-z0-9]+/g, '-');
  return { id, name: l.name, description: l.description, nodeIds };
});

let tour = read(D + '/intermediate/tour.json');
if (!Array.isArray(tour)) tour = tour.steps;
tour = tour.map(s => {
  const o = {
    order: s.order,
    title: s.title,
    description: s.description || s.whyItMatters,
    nodeIds: (s.nodeIds || s.nodesToInspect || []).map(fix).filter(x => ids.has(x)),
  };
  if (s.languageLesson) o.languageLesson = s.languageLesson;
  return o;
}).sort((a, b) => a.order - b.order);

const out = {
  version: '1.0.0',
  project: {
    name: scan.name || 'Cortex',
    languages: scan.languages,
    frameworks: scan.frameworks,
    description: scan.description,
    analyzedAt: new Date().toISOString(),
    gitCommitHash: process.argv[3],
  },
  nodes: g.nodes,
  edges: g.edges,
  layers,
  tour,
};
fs.writeFileSync(D + '/intermediate/assembled-graph.json', JSON.stringify(out, null, 2));
console.log('nodes', out.nodes.length, 'edges', out.edges.length, 'layers', layers.length, 'tour', tour.length);
