import { after, before, test } from 'node:test';
import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';
import { mkdtemp, readFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { Database } from '../services/api/src/database';
import { Game, drawBirth } from '../services/api/src/game';
import { AlchemyEvent, alchemyContent, judge, levelOf, modsFor, profileFor, simulate, validateLog, xpGain } from '../services/api/src/alchemy';
import { autopilot, BrewPlan } from './alchemy-autopilot';

let directory: string, game: Game, now = 1_900_000_000_000;
before(async () => {
  directory = await mkdtemp(join(tmpdir(), 'yunjing-alchemy-'));
  // Ordinary birth (roll 500000 lands in the ordinary tier) with a single 木 root.
  game = new Game(new Database({ directory }), { now: () => now, speed: 3600, birth: () => drawBirth(now, 500000, 1) });
  await game.initialize();
});
after(async () => { await game.db.close(); await rm(directory, { recursive: true }); });
const act = (id: string, kind: string, extras: Record<string, unknown> = {}, requestId: string = randomUUID()) => game.act(id, requestId, { kind, ...extras });
const count = (snap: { player: { inventory: { item: string; quantity: number }[] } }, item: string) =>
  snap.player.inventory.filter(x => x.item === item).reduce((sum, x) => sum + x.quantity, 0);
const HERBS = ['qingling-herb', 'water-lotus'];
const HUICHUN: Omit<BrewPlan, 'fuelBudget'> = { herbs: HERBS, prep: { 'qingling-herb': 'ground', 'water-lotus': 'dried' }, order: ['water', 'water-lotus', 'qingling-herb'], water: 2, condenseDelay: 3 };
const JUQI: Omit<BrewPlan, 'fuelBudget'> = { herbs: HERBS, prep: { 'qingling-herb': 'dried', 'water-lotus': 'ground' }, order: ['water', 'qingling-herb', 'water-lotus'], water: 1, condenseDelay: 2 };

async function give(id: string, item: string, quantity: number) {
  const row = (await game.db.query<{ state: any }>('SELECT state FROM players WHERE id=$1', [id])).rows[0].state;
  row.inventory.push({ item, quantity, bound: false, potency: 100 });
  await game.db.query('UPDATE players SET state=$2 WHERE id=$1', [id, JSON.stringify(row)]);
}
async function stocked(name: string) {
  const id = await game.playerFor(randomUUID(), name);
  // 10 spirit stones at creation: 2 herb bundles (8) + 2 firewood bundles (2).
  await act(id, 'buy', { offer: 'qingling-bundle', packs: 1 });
  await act(id, 'buy', { offer: 'lotus-bundle', packs: 1 });
  await act(id, 'buy', { offer: 'firewood-bundle', packs: 2 });
  return id;
}
async function brew(id: string, quantities: [number, number], plan: Omit<BrewPlan, 'fuelBudget'>, fuel = 8) {
  const herbs = [{ item: 'qingling-herb', quantity: quantities[0] }, { item: 'water-lotus', quantity: quantities[1] }];
  const begun = await act(id, 'alchemy-begin', { herbs, fuel });
  const profile = profileFor(herbs);
  const log = autopilot(profile, { ...plan, fuelBudget: fuel });
  await act(id, 'alchemy-charge');
  now += log[log.length - 1].t * 100; // real seconds elapse at the furnace
  const finished = await act(id, 'alchemy-finish', { log });
  return { begun, finished, log };
}

test('辨識與丹方解鎖分開；完整手動煉製到裝瓶入包，熟練與丹方永久記錄', async () => {
  const id = await stocked('丹房學徒');
  const fresh = await game.snapshot(id);
  assert.equal(count(fresh, 'qingling-herb'), 3); assert.equal(count(fresh, 'water-lotus'), 2); assert.equal(count(fresh, 'pine-firewood'), 10);
  assert.deepEqual(fresh.alchemy!.recipes.map(r => r.label), ['？', '？']);
  assert.equal(fresh.alchemy!.herbs[0].nature, null);
  await assert.rejects(act(id, 'alchemy-begin', { herbs: [{ item: 'qingling-herb', quantity: 2 }, { item: 'water-lotus', quantity: 1 }], fuel: 4 }), (e: any) => e.code === 'HERB_UNIDENTIFIED');
  await act(id, 'alchemy-identify', { item: 'qingling-herb' });
  const identified = await act(id, 'alchemy-identify', { item: 'water-lotus' });
  assert.ok(identified.alchemy!.herbs.every(h => h.identified && h.nature));
  assert.ok(identified.alchemy!.recipes.every(r => !r.known), 'identifying herbs never reveals a recipe');
  await assert.rejects(act(id, 'alchemy-identify', { item: 'water-lotus' }), (e: any) => e.code === 'ALREADY_IDENTIFIED');

  const { begun, finished, log } = await brew(id, [2, 1], HUICHUN);
  assert.equal(count(begun, 'qingling-herb'), 1, 'herbs are deducted when the brew begins');
  assert.equal(count(begun, 'pine-firewood'), 2);
  assert.equal(begun.alchemy!.profile!.recipe, 'huichun');
  const result = finished.player.alchemy!.session!.result!;
  assert.equal(result.success, true); assert.equal(result.grade, 'ji'); assert.ok(result.score >= 92);
  assert.equal(result.firstUnlock, true);
  const fuelUsed = log.filter(e => e.k === 'fuel').length;
  assert.equal(result.fuelUsed, fuelUsed); assert.equal(count(finished, 'pine-firewood'), 2 + 8 - fuelUsed, 'unused firewood returns');
  assert.equal(count(finished, 'huichun-pill'), 0, 'pill waits in the furnace until bottled');
  // success 10 + first unlock 20 + first perfect 5 for an ordinary birth at level 1
  assert.equal(finished.player.alchemy!.xp, 35); assert.equal(result.xpGain, 35);
  const key = randomUUID();
  const claimed = await act(id, 'alchemy-claim', {}, key);
  assert.equal(count(claimed, 'huichun-pill'), 1);
  assert.equal(claimed.player.inventory.find(x => x.item === 'huichun-pill')!.potency, 150);
  assert.deepEqual(await act(id, 'alchemy-claim', {}, key), claimed, 'network retry returns the original receipt');
  await assert.rejects(act(id, 'alchemy-claim'), (e: any) => e.code === 'NOTHING_TO_CLAIM');
  assert.equal(count(await game.snapshot(id), 'huichun-pill'), 1, 'double claim never duplicates');
  const view = claimed.alchemy!;
  const rec = view.recipes.find(r => r.id === 'huichun')!;
  assert.equal(rec.known, true); assert.equal(rec.label, '回春丹'); assert.equal(rec.quick, true);
  assert.deepEqual(rec.record!.ratio, { 'qingling-herb': 2, 'water-lotus': 1 });
  assert.deepEqual(rec.record!.prep, { 'qingling-herb': 'ground', 'water-lotus': 'dried' });
  assert.deepEqual(rec.record!.order, ['water', 'water-lotus', 'qingling-herb']);
  assert.equal(rec.record!.bestGrade, 'ji');
  assert.equal(view.recipes.find(r => r.id === 'juqi')!.known, false);
  assert.ok(claimed.player.milestones.includes('alchemy-recipe:huichun'));
  // Relogin: a fresh snapshot keeps the unlock and proficiency.
  const reloaded = await game.snapshot(id);
  assert.equal(reloaded.alchemy!.recipes.find(r => r.id === 'huichun')!.known, true);
  assert.equal(reloaded.alchemy!.xp, 35);
  const used = await act(id, 'alchemy-use', { item: 'huichun-pill' });
  assert.equal(used.player.body - claimed.player.body, 12);
  assert.equal(count(used, 'huichun-pill'), 0);
});

test('材料不足、柴不足、未投料不能完成；同一 requestId 不重複扣料', async () => {
  const id = await stocked('缺料者');
  await act(id, 'alchemy-identify', { item: 'qingling-herb' }); await act(id, 'alchemy-identify', { item: 'water-lotus' });
  await assert.rejects(act(id, 'alchemy-begin', { herbs: [{ item: 'qingling-herb', quantity: 4 }, { item: 'water-lotus', quantity: 2 }], fuel: 4 }), (e: any) => e.code === 'MATERIALS');
  await assert.rejects(act(id, 'alchemy-begin', { herbs: [{ item: 'qingling-herb', quantity: 2 }, { item: 'water-lotus', quantity: 1 }], fuel: 11 }), (e: any) => e.code === 'MATERIALS');
  const intact = await game.snapshot(id);
  assert.equal(count(intact, 'qingling-herb'), 3, 'a rejected begin leaves inventory untouched');
  const key = randomUUID();
  const herbs = [{ item: 'qingling-herb', quantity: 2 }, { item: 'water-lotus', quantity: 1 }];
  const first = await act(id, 'alchemy-begin', { herbs, fuel: 4 }, key);
  const again = await act(id, 'alchemy-begin', { herbs, fuel: 4 }, key);
  assert.deepEqual(again, first);
  assert.equal(count(await game.snapshot(id), 'qingling-herb'), 1);
  await assert.rejects(act(id, 'alchemy-begin', { herbs, fuel: 4 }, key.replace(/.$/, 'x')), (e: any) => e.code === 'ALCHEMY_BUSY');
  await assert.rejects(act(id, 'alchemy-begin', { herbs: [{ item: 'qingling-herb', quantity: 1 }, { item: 'water-lotus', quantity: 1 }], fuel: 4 }, key), (e: any) => e.code === 'IDEMPOTENCY_CONFLICT');
  await assert.rejects(act(id, 'alchemy-finish', { log: [{ t: 0, k: 'water' }] }), (e: any) => e.code === 'NOT_CHARGED');
  // Cancel before anything entered the furnace: everything returns.
  const cancelled = await act(id, 'alchemy-abandon');
  assert.equal(count(cancelled, 'qingling-herb'), 3); assert.equal(count(cancelled, 'water-lotus'), 2); assert.equal(count(cancelled, 'pine-firewood'), 10);
  assert.equal(cancelled.player.alchemy!.session, null);
});

test('投料後放棄或重登斷爐：材料作廢、得丹渣；紀錄偽造與時間過快被拒', async () => {
  const id = await stocked('斷爐者');
  await act(id, 'alchemy-identify', { item: 'qingling-herb' }); await act(id, 'alchemy-identify', { item: 'water-lotus' });
  const herbs = [{ item: 'qingling-herb', quantity: 2 }, { item: 'water-lotus', quantity: 1 }];
  await act(id, 'alchemy-begin', { herbs, fuel: 6 });
  await act(id, 'alchemy-charge');
  const log = autopilot(profileFor(herbs), { ...HUICHUN, fuelBudget: 6 });
  await assert.rejects(act(id, 'alchemy-finish', { log: [...log, { t: log[log.length - 1].t, k: 'charge', item: 'water-lotus' }] }), (e: any) => e.code === 'INVALID_LOG');
  await assert.rejects(act(id, 'alchemy-finish', { log: [...log.slice(0, -1), ...Array.from({ length: 7 }, () => ({ t: log[log.length - 1].t, k: 'fuel' }))] }), (e: any) => e.code === 'INVALID_LOG');
  // Relogin mid-fire: the session is still there, charged; the client can only settle it.
  const relog = await game.snapshot(id);
  assert.equal(relog.player.alchemy!.session!.charged, true);
  const lost = await act(id, 'alchemy-abandon');
  assert.equal(count(lost, 'qingling-herb'), 1); assert.equal(count(lost, 'pine-firewood'), 4);
  assert.equal(count(lost, 'pill-residue'), 1);
  await assert.rejects(act(id, 'alchemy-abandon'), (e: any) => e.code === 'NO_BREW');
});

test('真實速度下，紀錄時間長於實際經過時間會被拒', async () => {
  const dir = await mkdtemp(join(tmpdir(), 'yunjing-alchemy-rt-'));
  let clock = now; const realtime = new Game(new Database({ directory: dir }), { now: () => clock, speed: 1, birth: () => drawBirth(clock, 500000, 1) });
  await realtime.initialize();
  try {
    const id = await realtime.playerFor(randomUUID(), '急躁者');
    const go = (kind: string, extras: Record<string, unknown> = {}) => realtime.act(id, randomUUID(), { kind, ...extras });
    for (const [offer, packs] of [['qingling-bundle', 1], ['lotus-bundle', 1], ['firewood-bundle', 1]] as const) await go('buy', { offer, packs });
    await go('alchemy-identify', { item: 'qingling-herb' }); await go('alchemy-identify', { item: 'water-lotus' });
    const herbs = [{ item: 'qingling-herb', quantity: 2 }, { item: 'water-lotus', quantity: 1 }];
    await go('alchemy-begin', { herbs, fuel: 5 }); await go('alchemy-charge');
    const log = autopilot(profileFor(herbs), { ...HUICHUN, fuelBudget: 5 });
    clock += 10_000;
    await assert.rejects(go('alchemy-finish', { log }), (e: any) => e.code === 'LOG_TOO_FAST');
    clock += log[log.length - 1].t * 100;
    assert.equal((await go('alchemy-finish', { log })).player.alchemy!.session!.result!.success, true);
  } finally { await realtime.db.close(); await rm(dir, { recursive: true }); }
});

test('失敗給原因、丹渣與少量熟練；等量配比不成丹；多加柴焦爐', async () => {
  const id = await stocked('試錯者');
  await act(id, 'alchemy-identify', { item: 'qingling-herb' }); await act(id, 'alchemy-identify', { item: 'water-lotus' });
  const { finished } = await brew(id, [1, 1], HUICHUN, 4);
  const r = finished.player.alchemy!.session!.result!;
  assert.equal(r.success, false); assert.equal(r.residue, 1); assert.equal(r.xpGain, 4);
  assert.match(r.reasons[0], /等量/);
  assert.equal(count(finished, 'pill-residue'), 1, 'residue is settled at once on failure');
  const closed = await act(id, 'alchemy-claim');
  assert.equal(closed.player.alchemy!.session, null);
  assert.equal(closed.alchemy!.recipes.every(r => !r.known), true);
  const herbs = [{ item: 'qingling-herb', quantity: 2 }, { item: 'water-lotus', quantity: 1 }];
  const burnt = judge(simulate(autopilot(profileFor(herbs), { ...HUICHUN, fuelBudget: 12, overfuel: true }), HERBS, profileFor(herbs)), herbs, profileFor(herbs));
  assert.equal(burnt.success, false); assert.match(burnt.reasons[0], /焦糊/);
});

test('快速煉製需已掌握；扣料與柴、產出凡品或良品、不經過爐子', async () => {
  const id = await stocked('熟手');
  await act(id, 'alchemy-identify', { item: 'qingling-herb' }); await act(id, 'alchemy-identify', { item: 'water-lotus' });
  await assert.rejects(act(id, 'alchemy-quick', { recipe: 'juqi' }), (e: any) => e.code === 'NOT_MASTERED');
  await brew(id, [1, 2], JUQI, 6); await act(id, 'alchemy-claim');
  await give(id, 'water-lotus', 2); await give(id, 'pine-firewood', 5); // test fixture top-up for one quick craft
  const before = await game.snapshot(id);
  assert.equal(count(before, 'juqi-pill'), 1);
  const quick = await act(id, 'alchemy-quick', { recipe: 'juqi' });
  assert.equal(count(quick, 'qingling-herb'), count(before, 'qingling-herb') - 1);
  assert.equal(count(quick, 'water-lotus'), count(before, 'water-lotus') - 2);
  assert.equal(count(quick, 'pine-firewood'), count(before, 'pine-firewood') - 5);
  const pills = quick.player.inventory.filter(x => x.item === 'juqi-pill');
  assert.ok(pills.some(x => x.potency === 80), 'below proficiency 30 quick craft yields 凡品');
  assert.equal(quick.player.alchemy!.session, null);
  await assert.rejects(act(id, 'alchemy-quick', { recipe: 'juqi' }), (e: any) => e.code === 'MATERIALS');
  const qi = quick.player.qi;
  const took = await act(id, 'alchemy-use', { item: 'juqi-pill', potency: 80 });
  assert.equal(took.player.qi - qi, 12);
});

test('熟練度等級、遞減與天賦／靈根／宗門加成皆為資料驅動', () => {
  assert.equal(levelOf(0).level, 1); assert.equal(levelOf(19).level, 1); assert.equal(levelOf(20).level, 2);
  const p = alchemyContent.proficiency;
  let xp = 0; for (let l = 1; l < 100; l++) xp += p.baseNeed + p.needPerLevel * (l - 1);
  assert.equal(levelOf(xp).level, 100); assert.equal(levelOf(xp * 2).level, 100);
  assert.equal(xpGain(10, 0, 1), 10);
  assert.ok(xpGain(10, xp, 1) < 10 && xpGain(10, xp, 1) >= 1, 'gains diminish at high level');
  const fire = modsFor('ordinary', ['火'], null), metal = modsFor('ordinary', ['金'], 'alchemy');
  assert.equal(fire.band, 4); assert.equal(fire.scorchSeconds, 2);
  assert.equal(metal.scoreBonus, 4); assert.equal(metal.band, 3); assert.equal(metal.condenseSeconds, 1);
  assert.equal(modsFor('mortal', [], 'body').scorchSeconds, 1.5);
  // A root helps but never guarantees: a 1:1 mix still fails with every bonus stacked.
  const herbs = [{ item: 'qingling-herb', quantity: 2 }, { item: 'water-lotus', quantity: 2 }];
  const all = modsFor('chosen', ['火', '木', '水', '土', '金'], 'alchemy');
  assert.equal(judge(simulate(autopilot(profileFor(herbs), { ...HUICHUN, fuelBudget: 8 }), HERBS, profileFor(herbs), all), herbs, profileFor(herbs), all).success, false);
});

test('紀錄格式檢查', () => {
  assert.equal(validateLog([], HERBS, 4), 'LOG_SIZE');
  assert.equal(validateLog([{ t: 5, k: 'water' }, { t: 3, k: 'water' }], HERBS, 4), 'LOG_TIME');
  assert.equal(validateLog([{ t: 0, k: 'teleport' }], HERBS, 4), 'LOG_KIND');
  assert.equal(validateLog([{ t: 0, k: 'charge', item: 'spirit-stone' }], HERBS, 4), 'LOG_ITEM');
  assert.equal(validateLog([{ t: 0, k: 'charge', item: 'water-lotus' }, { t: 1, k: 'grind', item: 'water-lotus' }], HERBS, 4), 'LOG_PREP_AFTER_CHARGE');
  assert.equal(validateLog([{ t: 0, k: 'vent', v: 0.7 }], HERBS, 4), 'LOG_VENT');
  assert.equal(validateLog([{ t: 0, k: 'fuel' }, { t: 0, k: 'fuel' }], HERBS, 1), 'LOG_FUEL');
});

test('C# 火候模擬對照資料與 TypeScript 結果一致', async () => {
  // The Unity port (AlchemySim.cs) is checked against the same fixture in batch mode.
  const fixture = JSON.parse(await readFile(join(__dirname, '..', '..', 'tests', 'fixtures', 'alchemy-trace.json'), 'utf8')) as { herbs: { item: string; quantity: number }[]; log: AlchemyEvent[]; trace: number[]; completeTick: number; score: number };
  const profile = profileFor(fixture.herbs);
  const sim = simulate(fixture.log, HERBS, profile);
  assert.deepEqual(sim.trace, fixture.trace);
  assert.equal(sim.completeTick, fixture.completeTick);
  assert.equal(judge(sim, fixture.herbs, profile).score, fixture.score);
});
