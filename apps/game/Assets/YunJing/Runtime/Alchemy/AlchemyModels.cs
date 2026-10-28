using System;

namespace YunJing.Alchemy
{
    // JsonUtility mirrors of packages/contracts/types.ts (Alchemy*). Keyed records are read through the
    // server's plain-text summary arrays because JsonUtility cannot parse dictionaries.
    [Serializable] public class AlchemyHerbLock { public string item; public int quantity; public bool bound; }
    [Serializable] public class AlchemyParts { public float ratio, prep, fire, condense; }
    [Serializable] public class AlchemyResult
    {
        public bool success, bound, firstUnlock; public string grade, label, output; public AlchemyParts parts; public string[] reasons;
        public int score, quantity, potency, residue, xpGain, level, fuelUsed, fuelReturned;
    }
    [Serializable] public class AlchemySession { public string id, recipe; public AlchemyHerbLock[] herbs; public int fuel; public bool fuelBound, charged; public double startedAt; public AlchemyResult result; }
    [Serializable] public class AlchemyHistory { public double at; public string recipe, label; public int score; public bool quick; public string[] reasons; }
    [Serializable] public class AlchemyState { public int xp; public string[] identified, perfect; public AlchemySession session; public AlchemyHistory[] history; }
    [Serializable] public class AlchemyHintLines { public string recipe; public string[] lines; }
    [Serializable] public class AlchemyManual { public int tier; public string label, requirement; public bool unlocked; public AlchemyHintLines[] lines; }
    [Serializable] public class AlchemyHerbView { public string id, label, nature, use, identifyHint; public bool identified; public int owned; }
    [Serializable] public class AlchemyRecipeView { public string id, label, effect; public bool known, quick; public string[] summary; }
    [Serializable] public class AlchemyDrying { public float minSeconds, maxSeconds; }
    [Serializable] public class AlchemyView
    {
        public int level, xp, into, need, manualTier, grindStrokes, maxHerbEach, maxFuel, fuel;
        public AlchemyManual[] manuals; public AlchemyHerbView[] herbs; public AlchemyRecipeView[] recipes;
        public AlchemyFire fire; public AlchemyDrying drying; public AlchemyProfileView profile;
    }
}
