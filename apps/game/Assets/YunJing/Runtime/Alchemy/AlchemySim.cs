using System;
using System.Collections.Generic;

namespace YunJing.Alchemy
{
    [Serializable] public class AlchemyEvent { public int t; public string k; public string item; public float v; }
    [Serializable] public class AlchemyPhase { public string label, fire; public float low, high, seconds; }
    [Serializable] public class AlchemyProfileView { public string recipe, label; public AlchemyPhase[] phases; }
    [Serializable] public class AlchemyFire
    {
        public double step = .1, ambient = 15, piecePower = 4.3, ventPowerBase = .45, ventPowerScale = .55, lossBase = .12, lossVent = .03,
            lossLidOpen = .25, burnSeconds = 20, ventBurnBase = .7, ventBurnScale = .6, scorchLine = 95, scorchLimitSeconds = 4;
        public int maxBurning = 6, maxLogSeconds = 900;
    }

    // Presentation port of services/api/src/alchemy.ts simulate(). Same tick order and arithmetic so the
    // furnace the player watches matches the server replay; tests/fixtures/alchemy-trace.json checks both.
    public sealed class Furnace
    {
        readonly AlchemyFire f; readonly AlchemyPhase[] phases; readonly string[] herbs;
        readonly List<double> burning = new List<double>();
        readonly HashSet<string> charged = new HashSet<string>();
        public double T, Peak, InBand, Active, Scorch; public double Vent = .5;
        public bool LidClosed, Ignited, Warmed; public int Loaded, FuelUsed, Water, Phase, Tick = -1;
        public double[] Progress; public int? CompleteTick, CondenseTick;
        public readonly List<string> Order = new List<string>();
        public readonly List<double> Trace = new List<double>();
        public string Error;
        public int Burning => burning.Count;
        public bool AllCharged { get { foreach (var h in herbs) if (!charged.Contains(h)) return false; return true; } }
        public bool IsCharged(string herb) => charged.Contains(herb);

        public Furnace(AlchemyFire fire, AlchemyPhase[] phases, string[] herbs)
        {
            f = fire; this.phases = phases ?? new AlchemyPhase[0]; this.herbs = herbs;
            T = f.ambient; Peak = T; Progress = new double[this.phases.Length];
        }

        public void Apply(AlchemyEvent ev)
        {
            switch (ev.k)
            {
                case "water": if (!Order.Contains("water")) Order.Add("water"); Water++; break;
                case "charge": if (LidClosed) { Error ??= "LID_CLOSED_CHARGE"; break; } charged.Add(ev.item); Order.Add(ev.item); break;
                case "lid": LidClosed = ev.v == 1; break;
                case "fuel": FuelUsed++; if (Ignited && burning.Count > 0 && burning.Count < f.maxBurning) burning.Add(f.burnSeconds); else Loaded++; break;
                case "vent": Vent = ev.v; break;
                case "ignite":
                    if (Loaded > 0) { Ignited = true; while (Loaded > 0 && burning.Count < f.maxBurning) { burning.Add(f.burnSeconds); Loaded--; } }
                    break;
                case "condense": if (CondenseTick == null) CondenseTick = Tick; break;
            }
        }

        /// One fire.step of physics for the current Tick (call Apply for this tick's events first).
        public void Step()
        {
            double dt = f.step;
            double power = burning.Count * f.piecePower * (f.ventPowerBase + f.ventPowerScale * Vent);
            double loss = f.lossBase + f.lossVent * Vent + (LidClosed ? 0 : f.lossLidOpen);
            T = T + (power - loss * (T - f.ambient)) * dt;
            double burn = dt * (f.ventBurnBase + f.ventBurnScale * Vent);
            for (int i = burning.Count - 1; i >= 0; i--) { burning[i] -= burn; if (!(burning[i] > 1e-9)) burning.RemoveAt(i); }
            if (T > Peak) Peak = T;
            bool all = AllCharged;
            if (all && T > f.scorchLine) Scorch += dt;
            if (all && Ignited && Phase < phases.Length)
            {
                var p = phases[Phase]; bool inside = T >= p.low && T <= p.high;
                if (inside) Warmed = true;
                if (Warmed) Active += dt;
                if (inside)
                {
                    InBand += dt;
                    if (LidClosed) { Progress[Phase] += dt; if (Progress[Phase] >= p.seconds - 1e-9) { Phase++; if (Phase == phases.Length) CompleteTick = Tick; } }
                }
            }
            if (Tick % 10 == 0) Trace.Add(Round(T, 3));
        }

        public static double Round(double value, int digits) { double s = Math.Pow(10, digits); return Math.Floor(value * s + .5) / s; }

        /// Full replay identical to the server's simulate(); used by the parity check.
        public static Furnace Replay(AlchemyFire fire, AlchemyPhase[] phases, string[] herbs, IList<AlchemyEvent> log)
        {
            var furnace = new Furnace(fire, phases, herbs);
            int end = log.Count > 0 ? log[log.Count - 1].t : 0, e = 0;
            for (int tick = 0; tick <= end; tick++)
            {
                furnace.Tick = tick;
                while (e < log.Count && log[e].t == tick) furnace.Apply(log[e++]);
                if (furnace.CondenseTick != null && tick >= furnace.CondenseTick) break;
                furnace.Step();
            }
            return furnace;
        }
    }
}
