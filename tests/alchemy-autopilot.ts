import { AlchemyEvent, AlchemyProfile, alchemyContent, simulate } from '../services/api/src/alchemy';

// Test-only player stand-in: builds an event log the way a careful player would act at the furnace,
// one decision per second, reading only what the furnace UI shows (temperature, burning pieces, phase).
export interface BrewPlan {
  herbs: string[]; prep: Record<string, 'ground' | 'dried' | 'raw' | 'scorched'>; order: string[]; water: number;
  condenseDelay: number; fuelBudget: number; overfuel?: boolean; skipCondense?: boolean; lidOpenAt?: number;
}
export function autopilot(profile: AlchemyProfile, plan: BrewPlan): AlchemyEvent[] {
  const log: AlchemyEvent[] = []; let t = 0;
  for (const h of plan.herbs) {
    const p = plan.prep[h];
    if (p === 'ground') log.push({ t: t += 5, k: 'grind', item: h });
    if (p === 'dried' || p === 'scorched') log.push({ t: t += 1, k: 'dry-in', item: h });
  }
  const dryEnd = t + (plan.herbs.some(h => plan.prep[h] === 'scorched') ? 300 : 110);
  t = dryEnd;
  for (const h of plan.herbs) if (plan.prep[h] === 'dried' || plan.prep[h] === 'scorched') log.push({ t, k: 'dry-out', item: h });
  t += 20;
  for (const step of plan.order) {
    if (step === 'water') for (let i = 0; i < plan.water; i++) log.push({ t: t += 5, k: 'water' });
    else log.push({ t: t += 10, k: 'charge', item: step });
  }
  log.push({ t: t += 10, k: 'lid', v: 1 });
  let used = 0;
  const fuel = () => { if (used < plan.fuelBudget) { log.push({ t, k: 'fuel' }); used++; } };
  const f0 = alchemyContent.fire; const first = profile.phases[0];
  const startPieces = Math.ceil(((first.low + first.high) / 2 - f0.ambient) / (f0.piecePower * (f0.ventPowerBase + f0.ventPowerScale * 0.5) / (f0.lossBase + f0.lossVent * 0.5)));
  for (let i = 0; i < (plan.overfuel ? 6 : startPieces); i++) fuel();
  log.push({ t: t += 5, k: 'ignite' });
  let vent = 0.5; let condenseAt: number | null = null; let lidOpened = false;
  for (let guard = 0; guard < 400; guard++) {
    t += 10;
    const probe = simulate([...log, { t, k: 'vent', v: vent }], plan.herbs, profile);
    if (plan.lidOpenAt !== undefined && !lidOpened && guard === plan.lidOpenAt) { log.push({ t, k: 'lid', v: 0 }); lidOpened = true; continue; }
    if (lidOpened && guard === (plan.lidOpenAt ?? 0) + 4) log.push({ t, k: 'lid', v: 1 });
    if (probe.completeTick !== null && condenseAt === null) condenseAt = Math.max(t, probe.completeTick + plan.condenseDelay * 10);
    if (condenseAt !== null && t >= condenseAt) { if (!plan.skipCondense) log.push({ t, k: 'condense' }); else log.push({ t, k: 'vent', v: vent }); break; }
    const phase = profile.phases[Math.min(probe.phase, profile.phases.length - 1)];
    const target = (phase.low + phase.high) / 2;
    if (plan.overfuel) { fuel(); log.push({ t, k: 'vent', v: 1 }); continue; }
    const next = probe.temperature < target - 3 ? 1 : probe.temperature > target + 3 ? 0 : 0.5;
    if (next !== vent) { vent = next; log.push({ t, k: 'vent', v: vent }); }
    const f = alchemyContent.fire;
    const perPiece = f.piecePower * (f.ventPowerBase + f.ventPowerScale * 0.5) / (f.lossBase + f.lossVent * 0.5);
    const want = Math.ceil((target - f.ambient) / perPiece);
    if (probe.burning === 0) { fuel(); log.push({ t, k: 'ignite' }); continue; }
    if (probe.temperature < target - 2 && probe.burning < want) fuel();
  }
  return log;
}
