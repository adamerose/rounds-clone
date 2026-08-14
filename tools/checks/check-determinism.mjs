import { readFile, readdir } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const RULES = [
  ['DET001', /\busing\s+Godot\s*;|Godot\.NET\.Sdk|GodotSharp/i, 'Godot references are forbidden in Rounds.Sim.'],
  ['DET002', /\bfloat\b|System\.Single/, '`double` is the only floating-point type allowed in Rounds.Sim.'],
  ['DET003', /\bMath\.(?:Sin|Cos|Tan|Atan|Atan2|Pow|Exp|Log)\s*\(/, 'Unpinned math calls belong only in Math/Trig.cs.'],
  ['DET004', /\bSystem\.Random\b|\bnew\s+Random\s*\(/, 'Use the world-owned PCG instead of System.Random.'],
  ['DET005', /\b(?:Dictionary|HashSet)\s*</, 'Unordered collections are forbidden in Rounds.Sim.'],
  ['DET006', /\b(?:DateTime|DateTimeOffset|Stopwatch|Environment\.TickCount)\b/, 'Wall-clock APIs are forbidden in Rounds.Sim.'],
  ['DET007', /\b(?:async|await|Task|Thread|Parallel)\b/, 'Concurrency is forbidden in the simulation step.'],
];

async function filesBelow(root) {
  const entries = await readdir(root, { withFileTypes: true });
  const files = [];
  for (const entry of entries.sort((left, right) => left.name.localeCompare(right.name))) {
    const fullPath = path.join(root, entry.name);
    if (entry.isDirectory()) files.push(...await filesBelow(fullPath));
    else if (entry.isFile() && (entry.name.endsWith('.cs') || entry.name.endsWith('.csproj'))) files.push(fullPath);
  }
  return files;
}

export async function checkSimulation(simRoot) {
  const failures = [];
  for (const file of await filesBelow(simRoot)) {
    const relative = path.relative(simRoot, file).replaceAll('\\', '/');
    const body = await readFile(file, 'utf8');
    for (const [id, expression, message] of RULES) {
      if (id === 'DET003' && relative === 'Math/Trig.cs') continue;
      if (expression.test(body)) failures.push(`${id} ${relative}: ${message}`);
    }
  }
  return failures;
}

async function main() {
  const repository = path.resolve(process.argv[2] ?? '.');
  const failures = await checkSimulation(path.join(repository, 'src', 'Rounds.Sim'));
  if (failures.length > 0) {
    console.error(failures.join('\n'));
    process.exitCode = 1;
    return;
  }
  console.log('determinism boundary check passed');
}

const isMain = process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url);
if (isMain) await main();
