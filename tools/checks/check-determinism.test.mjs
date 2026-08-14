import assert from 'node:assert/strict';
import { mkdtemp, mkdir, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import test from 'node:test';
import { checkSimulation } from './check-determinism.mjs';

test('each locked rule rejects a representative violation', async () => {
  const root = await mkdtemp(path.join(tmpdir(), 'rounds-check-'));
  try {
    await mkdir(path.join(root, 'Math'));
    await writeFile(path.join(root, 'Bad.cs'), `
using Godot;
class Bad {
  float value;
  Dictionary<int, int> values = new();
  System.Random random = new();
  DateTime now;
  Task Work() => Task.CompletedTask;
  double Curve(double x) => Math.Sin(x);
}`);
    const failures = await checkSimulation(root);
    assert.deepEqual(
      failures.map((failure) => failure.slice(0, 6)),
      ['DET001', 'DET002', 'DET003', 'DET004', 'DET005', 'DET006', 'DET007']);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test('the dedicated trig file owns the unpinned math boundary', async () => {
  const root = await mkdtemp(path.join(tmpdir(), 'rounds-check-'));
  try {
    await mkdir(path.join(root, 'Math'));
    await writeFile(path.join(root, 'Math', 'Trig.cs'), 'class Trig { double Sin(double x) => Math.Sin(x); }');
    assert.deepEqual(await checkSimulation(root), []);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});
