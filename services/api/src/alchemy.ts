import alchemyContent from '../../../packages/content/alchemy.json';

// Deterministic alchemy rules shared by the server and (as a C# port) the Unity client.
// The client only replays this for presentation; quality is always recomputed here.
export { alchemyContent };
export type AlchemyRecipe = typeof alchemyContent.recipes[number];
export interface AlchemyEvent { t: number; k: string; item?: string; v?: number }
export interface AlchemyPhase { label: string; fire: string; low: number; high: number; seconds: number }
export interface AlchemyProfile { recipe: string | null; label: string; phases: AlchemyPhase[]; condense: { best: number[]; zeroAt: number[]; overcookAt: number } }
export interface AlchemyMods { band: number; scorchSeconds: number; condenseSeconds: number; dryBonus: number; prepFloor: number; ratioTolerance: number; orderForgive: number; scoreBonus: number }
export const NO_MODS: AlchemyMods = { band: 0, scorchSeconds: 0, condenseSeconds: 0, dryBonus: 0, prepFloor: 0, ratioTolerance: 1, orderForgive: 0, scoreBonus: 0 };
export interface SimResult {
  endTick: number; water: number; order: string[]; prep: Record<string, string>; drySeconds: Record<string, number>;
  fuelUsed: number; inBand: number; activeSeconds: number; scorch: number; peak: number;
  completeTick: number | null; condenseTick: number | null; phaseProgress: number[]; trace: number[]; error: string | null; temperature: number; burning: number; phase: number;
}
const KINDS = new Set(['grind', 'dry-in', 'dry-out', 'water', 'charge', 'lid', 'fuel', 'vent', 'ignite', 'condense']);
const round = (value: number, digits = 4) => Math.round(value * 10 ** digits) / 10 ** digits;

export function profileFor(herbs: { item: string; quantity: number }[]): AlchemyProfile {
  const q = (id: string) => herbs.find(x => x.item === id)?.quantity ?? 0;
  const a = q('qingling-herb'), b = q('water-lotus');
  const recipe = a > b ? alchemyContent.recipes.find(x => x.id === 'huichun') : a < b ? alchemyContent.recipes.find(x => x.id === 'juqi') : undefined;
  if (!recipe) return { recipe: null, label: alchemyContent.unmatched.label, phases: alchemyContent.unmatched.phases, condense: alchemyContent.unmatched.condense };
  return { recipe: recipe.id, label: recipe.label, phases: recipe.phases, condense: recipe.condense };
}

/** Validates the event log shape against the locked session; returns an error code or null. */
export function validateLog(log: unknown, herbs: string[], fuel: number): string | null {
  if (!Array.isArray(log) || log.length === 0 || log.length > 4000) return 'LOG_SIZE';
  let last = 0, fuelUsed = 0; const charged = new Set<string>();
  for (const raw of log) {
    if (!raw || typeof raw !== 'object') return 'LOG_EVENT';
    const e = raw as AlchemyEvent;
    if (!Number.isInteger(e.t) || e.t < last || e.t > alchemyContent.fire.maxLogSeconds * 10) return 'LOG_TIME';
    if (!KINDS.has(e.k)) return 'LOG_KIND';
    if (['grind', 'dry-in', 'dry-out', 'charge'].includes(e.k)) {
      if (typeof e.item !== 'string' || !herbs.includes(e.item)) return 'LOG_ITEM';
      if (e.k === 'charge') { if (charged.has(e.item)) return 'LOG_DOUBLE_CHARGE'; charged.add(e.item); }
      else if (charged.has(e.item)) return 'LOG_PREP_AFTER_CHARGE';
    }
    if (e.k === 'vent' && ![0, 0.5, 1].includes(e.v as number)) return 'LOG_VENT';
    if (e.k === 'lid' && ![0, 1].includes(e.v as number)) return 'LOG_LID';
    if (e.k === 'fuel' && ++fuelUsed > fuel) return 'LOG_FUEL';
    last = e.t;
  }
  return null;
}

/** Fixed-step furnace replay. Integer ticks of fire.step seconds; identical arithmetic in AlchemySim.cs. */
export function simulate(log: AlchemyEvent[], herbs: string[], profile: AlchemyProfile, mods: AlchemyMods = NO_MODS): SimResult {
  const f = alchemyContent.fire, dt = f.step;
  const ground = new Set<string>(), dryIn: Record<string, number> = {}, drySeconds: Record<string, number> = {};
  const order: string[] = []; const charged = new Set<string>(); const trace: number[] = [];
  let T = f.ambient, vent = 0.5, lidClosed = false, ignited = false, water = 0, fuelUsed = 0, loaded = 0;
  let burning: number[] = []; let idx = 0; const progress = profile.phases.map(() => 0);
  let inBand = 0, active = 0, warmed = false, scorch = 0, peak = T, completeTick: number | null = null, condenseTick: number | null = null, error: string | null = null;
  const endTick = log.length ? log[log.length - 1].t : 0;
  let e = 0;
  for (let tick = 0; tick <= endTick; tick++) {
    while (e < log.length && log[e].t === tick) {
      const ev = log[e++];
      switch (ev.k) {
        case 'grind': ground.add(ev.item!); break;
        case 'dry-in': dryIn[ev.item!] = tick; break;
        case 'dry-out': if (dryIn[ev.item!] !== undefined) { drySeconds[ev.item!] = (drySeconds[ev.item!] ?? 0) + (tick - dryIn[ev.item!]) / 10; delete dryIn[ev.item!]; } break;
        case 'water': if (!order.includes('water')) order.push('water'); water++; break;
        case 'charge': if (lidClosed) { error ??= 'LID_CLOSED_CHARGE'; break; } charged.add(ev.item!); order.push(ev.item!); break;
        case 'lid': lidClosed = ev.v === 1; break;
        case 'fuel': fuelUsed++; if (ignited && burning.length > 0 && burning.length < f.maxBurning) burning.push(f.burnSeconds); else loaded++; break;
        case 'vent': vent = ev.v!; break;
        case 'ignite': if (loaded > 0) { ignited = true; while (loaded > 0 && burning.length < f.maxBurning) { burning.push(f.burnSeconds); loaded--; } } break;
        case 'condense': if (condenseTick === null) condenseTick = tick; break;
      }
    }
    if (condenseTick !== null && tick >= condenseTick) break;
    const power = burning.length * f.piecePower * (f.ventPowerBase + f.ventPowerScale * vent);
    const loss = f.lossBase + f.lossVent * vent + (lidClosed ? 0 : f.lossLidOpen);
    T = T + (power - loss * (T - f.ambient)) * dt;
    const burn = dt * (f.ventBurnBase + f.ventBurnScale * vent);
    burning = burning.map(x => x - burn).filter(x => x > 1e-9);
    if (T > peak) peak = T;
    const allCharged = herbs.every(h => charged.has(h));
    if (allCharged && T > f.scorchLine) scorch += dt;
    if (allCharged && ignited && idx < profile.phases.length) {
      // Preheat is free: stability counts from the first time the furnace reaches the first band.
      const p = profile.phases[idx]; const inside = T >= p.low - mods.band && T <= p.high + mods.band;
      if (inside) warmed = true;
      if (warmed) active += dt;
      if (inside) {
        inBand += dt;
        if (lidClosed) { progress[idx] += dt; if (progress[idx] >= p.seconds - 1e-9) { idx++; if (idx === profile.phases.length) completeTick = tick; } }
      }
    }
    if (tick % 10 === 0) trace.push(round(T, 3));
  }
  for (const h of Object.keys(dryIn)) drySeconds[h] = (drySeconds[h] ?? 0) + (endTick - dryIn[h]) / 10;
  const prep: Record<string, string> = {};
  for (const h of herbs) {
    const d = drySeconds[h] ?? 0; const g = ground.has(h);
    const dried = d >= alchemyContent.drying.minSeconds; const scorched = d > alchemyContent.drying.maxSeconds + mods.dryBonus;
    prep[h] = scorched ? 'scorched' : g && dried ? 'ground+dried' : g ? 'ground' : dried ? 'dried' : 'raw';
  }
  return { endTick, water, order, prep, drySeconds, fuelUsed, inBand: round(inBand), activeSeconds: round(active), scorch: round(scorch), peak: round(peak, 3), completeTick, condenseTick, phaseProgress: progress.map(x => round(x)), trace, error, temperature: round(T, 3), burning: burning.length, phase: idx };
}

export interface AlchemyOutcome {
  success: boolean; grade: string | null; label: string; score: number;
  parts: { ratio: number; prep: number; fire: number; condense: number }; reasons: string[]; potency: number;
}
const herbLabel = (id: string) => alchemyContent.herbs.find(x => x.id === id)?.label ?? id;

export function judge(sim: SimResult, herbs: { item: string; quantity: number }[], profile: AlchemyProfile, mods: AlchemyMods = NO_MODS): AlchemyOutcome {
  const reasons: string[] = []; const recipe = alchemyContent.recipes.find(x => x.id === profile.recipe);
  const fail = (reason: string, parts = { ratio: 0, prep: 0, fire: 0, condense: 0 }): AlchemyOutcome => ({ success: false, grade: null, label: '失敗', score: 0, parts, reasons: [reason, ...reasons], potency: 0 });
  if (sim.error === 'LID_CLOSED_CHARGE') return fail('爐蓋未開就投料，藥材散落爐外。');
  if (!recipe) return fail('青靈草與淨水蓮等量，藥性互抵，未能成丹。可改用二比一或一比二。');
  const q = (id: string) => herbs.find(x => x.item === id)?.quantity ?? 0;
  const ratioTarget = recipe.ratio['qingling-herb'] / recipe.ratio['water-lotus'];
  const ratioActual = q('qingling-herb') / q('water-lotus');
  const ratio = Math.max(0, 1 - Math.abs(Math.log(ratioActual / ratioTarget)) / (Math.log(2) * mods.ratioTolerance));
  if (ratio < 0.999) reasons.push(`配比 ${q('qingling-herb')}:${q('water-lotus')} 偏離此丹的理想比例。`);
  let herbScore = 0;
  for (const h of Object.keys(recipe.prep)) {
    const want = (recipe.prep as Record<string, string>)[h]; const got = sim.prep[h];
    const s = got === want ? 1 : got === 'ground+dried' ? 0.5 : 0;
    if (s < 1) reasons.push(got === 'scorched' ? `${herbLabel(h)}晾過頭，藥性焦散。` : `${herbLabel(h)}的處理方式不合此丹。`);
    herbScore += s / Object.keys(recipe.prep).length;
  }
  herbScore = Math.max(herbScore, mods.prepFloor);
  let mismatch = 0; recipe.order.forEach((x, i) => { if (sim.order[i] !== x) mismatch++; });
  const order = Math.max(0, 1 - mismatch / recipe.order.length * (1 - mods.orderForgive));
  if (mismatch) reasons.push('投料順序與藥性相生次序不合。');
  const water = sim.water === recipe.water ? 1 : Math.abs(sim.water - recipe.water) === 1 ? 0.5 : 0;
  if (water < 1) reasons.push(`注水 ${sim.water} 瓢，與此丹所需水量不同。`);
  const pp = alchemyContent.prepParts;
  const prep = herbScore * pp.herbs + order * pp.order + water * pp.water;
  const held = sim.activeSeconds > 0 ? Math.min(1, sim.inBand / sim.activeSeconds) : 0;
  const F = alchemyContent.fire;
  const fire = Math.max(0, Math.min(1, (held - F.stabilityFloor) / (F.stabilityFull - F.stabilityFloor)));
  const partial = { ratio: round(ratio), prep: round(prep), fire: round(fire), condense: 0 };
  if (sim.water === 0) return fail('爐中無水，藥材乾燒焦黑。', partial);
  if (sim.scorch > alchemyContent.fire.scorchLimitSeconds + mods.scorchSeconds) return fail(`爐溫超過 ${alchemyContent.fire.scorchLine} 達 ${sim.scorch.toFixed(1)} 息，藥材焦糊。柴多不等於火好。`, partial);
  if (sim.completeTick === null) {
    if (sim.condenseTick !== null) return fail('火候未足便收丹，藥未成形。', partial);
    return fail('火候未完成，爐中藥液未凝。', partial);
  }
  const c = recipe.condense; const w = mods.condenseSeconds;
  if (sim.condenseTick === null) return fail('丹香已起卻遲遲未收，丹藥過火焦化。', partial);
  const d = (sim.condenseTick - sim.completeTick) / 10;
  if (d >= c.overcookAt) return fail('收丹太遲，丹藥過火焦化。', partial);
  const condense = d >= c.best[0] - w && d <= c.best[1] + w ? 1
    : d < c.best[0] - w ? Math.max(0, (d - c.zeroAt[0]) / (c.best[0] - w - c.zeroAt[0]))
    : Math.max(0, 1 - (d - c.best[1] - w) / (c.zeroAt[1] - c.best[1] - w));
  if (condense < 1) reasons.push(d < c.best[0] ? '收丹略早，丹體未圓。' : '收丹略遲，丹紋暗沉。');
  if (held < 0.9) reasons.push(`火候不穩：${round(sim.activeSeconds - sim.inBand, 1)} 息不在所需火候內。`);
  const W = alchemyContent.weights;
  const score = Math.round(W.ratio * ratio + W.prep * prep + W.fire * fire + W.condense * condense + mods.scoreBonus);
  const parts = { ratio: round(ratio), prep: round(prep), fire: round(fire), condense: round(condense) };
  const grade = [...alchemyContent.grades].reverse().find(g => score >= g.min);
  if (!grade) return { success: false, grade: null, label: '失敗', score, parts, reasons: ['藥力未能凝聚成丹。', ...reasons], potency: 0 };
  return { success: true, grade: grade.id, label: grade.label, score, parts, reasons, potency: grade.potency };
}

export function levelOf(xp: number) {
  const p = alchemyContent.proficiency; let level = 1, left = xp;
  while (level < p.maxLevel) { const need = p.baseNeed + p.needPerLevel * (level - 1); if (left < need) return { level, into: left, need }; left -= need; level++; }
  return { level, into: 0, need: 0 };
}
export function xpGain(base: number, xp: number, talentXp: number) {
  const p = alchemyContent.proficiency; const level = levelOf(xp).level;
  return Math.max(1, Math.round(base * talentXp * Math.max(p.diminishFloor, 1 - p.diminishPerLevel * (level - 1))));
}
export function modsFor(tier: string, roots: string[], sectPath: string | null): AlchemyMods {
  const m = { ...NO_MODS };
  const t = (alchemyContent.talents as Record<string, { band: number }>)[tier]; if (t) m.band += t.band;
  const R = alchemyContent.roots as Record<string, Record<string, unknown>>;
  for (const r of roots) {
    const x = R[r]; if (!x) continue;
    m.band += (x.band as number) ?? 0; m.scorchSeconds += (x.scorchSeconds as number) ?? 0; m.condenseSeconds += (x.condenseSeconds as number) ?? 0;
    if (x.dryBonus) m.dryBonus += alchemyContent.drying.woodRootBonusSeconds; m.prepFloor = Math.max(m.prepFloor, (x.prepFloor as number) ?? 0);
    if (x.ratioTolerance) m.ratioTolerance = x.ratioTolerance as number; m.orderForgive = Math.max(m.orderForgive, (x.orderForgive as number) ?? 0);
    m.scoreBonus += (x.scoreBonus as number) ?? 0;
  }
  const s = sectPath ? (alchemyContent.sectPaths as unknown as Record<string, Record<string, number>>)[sectPath] : undefined;
  if (s) { m.band += s.band ?? 0; m.scorchSeconds += s.scorchSeconds ?? 0; m.condenseSeconds += s.condenseSeconds ?? 0; }
  return m;
}
