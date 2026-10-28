using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace YunJing.Alchemy
{
    // Scripted player path for the built game (-alchemyPlaytest). It walks with the walker's input vector and
    // presses the same buttons, drop handler and grind handler a player uses; it is not physical keyboard,
    // mouse or touch input. Screenshots come from the running game via ScreenCapture.
    public sealed class AlchemyPlaytest : MonoBehaviour
    {
        [Serializable] class Step { public int index; public string name; public bool pass; public string note; public float seconds; public string shot; }
        [Serializable] class Report { public string alias, started, resolution; public bool pass; public List<Step> steps = new List<Step>(); public string[] inventory; public string result; }

        AlchemyWorkshop ws; string outDir; Report report = new Report(); float t0; int index;

        public static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
        public static bool Flag(string name) => Environment.GetCommandLineArgs().Contains(name);

        void Start()
        {
            bool full = Flag("-alchemyPlaytest"), layout = Flag("-alchemyLayoutCheck");
            if (!full && !layout) { enabled = false; return; }
            Application.runInBackground = true; // keep rendering (and capturing) when the test window loses focus
            ws = GetComponent<AlchemyWorkshop>(); outDir = Arg("-alchemyOut") ?? Path.Combine(Application.persistentDataPath, "playtest");
            Directory.CreateDirectory(outDir);
            report.alias = ws.Alias; report.started = DateTime.Now.ToString("s"); report.resolution = Screen.width + "x" + Screen.height;
            StartCoroutine(full ? Run() : Layout());
        }

        // ReadPixels after the frame (UI overlay included); the project has no ScreenCapture module.
        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            var file = $"{report.resolution}_{index:00}_{name}.png";
            var tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0); tex.Apply();
            File.WriteAllBytes(Path.Combine(outDir, file), tex.EncodeToPNG()); Destroy(tex);
            yield return null;
            if (report.steps.Count > 0) report.steps[report.steps.Count - 1].shot = file;
        }

        void Mark(string name, bool pass, string note = "")
        {
            report.steps.Add(new Step { index = ++index, name = name, pass = pass, note = note, seconds = Time.realtimeSinceStartup - t0 });
            Debug.Log($"PLAYTEST {(pass ? "PASS" : "FAIL")} {index:00} {name} {note}");
        }

        IEnumerator Until(Func<bool> condition, float timeout)
        {
            float end = Time.realtimeSinceStartup + timeout; while (!condition() && Time.realtimeSinceStartup < end) yield return null;
        }

        AlchemyStation Station(string id) => FindObjectsByType<AlchemyStation>(FindObjectsSortMode.None).First(s => s.Id == id);

        IEnumerator WalkTo(string id)
        {
            var station = Station(id); var approach = station.Point - station.transform.forward * .85f; approach.y = 0;
            foreach (var goal in new[] { new Vector3(.2f, 0, -.1f), approach })
            {
                float end = Time.realtimeSinceStartup + 12;
                while (Time.realtimeSinceStartup < end)
                {
                    var p = ws.Walker.transform.position; var d = goal - new Vector3(p.x, 0, p.z);
                    if (d.magnitude < .12f) break;
                    var cam = Camera.main.transform; var f = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized; var r = Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized;
                    var dir = d.normalized * Mathf.Clamp01(d.magnitude * 2.5f + .2f);
                    ws.Walker.ScriptedInput = new Vector2(Vector3.Dot(dir, r), Vector3.Dot(dir, f));
                    yield return null;
                }
            }
            ws.Walker.ScriptedInput = Vector2.zero; yield return null;
        }

        IEnumerator Interact(string id)
        {
            yield return WalkTo(id);
            yield return Until(() => ws.Near != null && ws.Near.Id == id, 2);
            bool near = ws.Near != null && ws.Near.Id == id;
            ws.TryInteract(); // same entry point as the E key and the touch 互動 button
            yield return Until(() => !ws.Director.Blending, 2); yield return new WaitForSecondsRealtime(.2f);
            if (!near) Mark("走到 " + id, false, "未進入互動範圍");
        }

        IEnumerator Click(string key)
        {
            yield return Until(() => !ws.Waiting && ws.Buttons.ContainsKey(key) && ws.Buttons[key] != null, 6);
            if (!ws.Buttons.TryGetValue(key, out var b) || b == null) { Debug.LogWarning("PLAYTEST missing button " + key); yield break; }
            b.onClick.Invoke(); yield return null;
            yield return Until(() => !ws.Waiting, 10);
        }

        IEnumerator Leave() { yield return Click("Leave"); yield return Until(() => !ws.Director.Blending, 2); }

        int Owned(string item) => ws.Owned(item);

        IEnumerator Run()
        {
            t0 = Time.realtimeSinceStartup;
            yield return Until(() => ws.Api.State != null && ws.Api.State.alchemy != null && !ws.Api.Busy, 30);
            Mark("登入本機伺服器並進入丹房（第三人稱）", ws.Api.State?.alchemy != null, ws.Api.Status);
            yield return new WaitForSecondsRealtime(1); yield return Capture("explore");
            bool gathered = false; ws.Api.Act(new GameAction { kind = "gather", node = "herb-bank" }, (ok, e) => gathered = ok);
            yield return Until(() => !ws.Api.Busy && !ws.Api.HasPending, 5); float gatherAt = Time.realtimeSinceStartup;

            // 2–4 cabinet: buy, identify, choose 2:1
            yield return Interact("B05"); yield return new WaitForSecondsRealtime(.5f);
            yield return Capture("cabinet_closeup");
            if (Owned("qingling-herb") < 3) yield return Click("Buy_qingling-herb");
            yield return Click("Buy_water-lotus");
            var cam = Camera.main.transform.position;
            Mark("藥櫃特寫：向阿禾購得藥材（測試角色前置，靈石購買）", Owned("water-lotus") >= 2 && Owned("qingling-herb") >= 3, $"青靈草 {Owned("qingling-herb")}・淨水蓮 {Owned("water-lotus")}・採集 {gathered}");
            yield return Click("Identify_qingling-herb"); yield return Click("Identify_water-lotus");
            bool identified = ws.Api.State.alchemy.herbs.All(h => h.identified) && ws.Api.State.alchemy.recipes.All(r => !r.known);
            Mark("辨識兩味藥材（丹方仍為？）", identified); yield return Capture("identified");
            ws.Pick[0] = 2; ws.Pick[1] = 1; ws.BuildPanel(); yield return Click("ChooseHerbs");
            Mark("選取份量 2：1 並取出", ws.HerbsChosen, $"{ws.Pick[0]}:{ws.Pick[1]}");
            yield return Leave();
            Mark("取消特寫回到探索鏡頭", Vector3.Distance(Camera.main.transform.position, cam) > .2f && ws.Director.Current == Shot.Explore);

            // 5 fuel box: buy firewood and begin (materials deducted)
            yield return Interact("B07"); yield return Click("BuyFuel"); yield return Click("BuyFuel");
            ws.FuelPick = 6; ws.BuildPanel(); yield return Capture("fuelbox");
            int herbsBefore = Owned("qingling-herb");
            yield return Click("Begin");
            Mark("柴箱取柴 6 根，開始煉製（伺服器扣除藥材與柴）", ws.Stage == Stage.Prep && Owned("qingling-herb") == herbsBefore - 2, $"剩青靈草 {Owned("qingling-herb")}・松柴 {ws.Api.State.alchemy.fuel}");

            // 6 scroll
            yield return Interact("B11"); yield return Click("Tab_manuals"); yield return Capture("scroll_manuals");
            yield return Click("Tab_recipes"); Mark("竹簡：丹道推演與丹方錄（未知丹方顯示？）", ws.Api.State.alchemy.recipes.All(r => r.label == "？"));
            yield return Leave();

            // 7–9 tabletop: grind, dry, compound
            yield return Interact("TABLE"); yield return new WaitForSecondsRealtime(.3f);
            var chips = FindObjectsByType<HerbChip>(FindObjectsSortMode.None).ToDictionary(c => c.Herb);
            chips["water-lotus"].Dropped(chips["water-lotus"], ws.ZoneScreen("basket"));
            chips["qingling-herb"].Dropped(chips["qingling-herb"], ws.ZoneScreen("mortar"));
            yield return Capture("table_topdown"); Check("工作台操作");
            var pad = FindAnyObjectByType<GrindPad>();
            while (!ws.Ground.Contains("qingling-herb") && Time.realtimeSinceStartup - t0 < 600) { pad.Stroke(.25f); yield return new WaitForSecondsRealtime(.08f); }
            Mark("工作台俯視：青靈草入石臼，研磨完成", ws.Ground.Contains("qingling-herb"));
            yield return Until(() => ws.Drying("water-lotus") >= 11, 20); yield return Capture("drying");
            chips["water-lotus"].Dropped(chips["water-lotus"], ws.ZoneScreen("basket"));
            Mark("淨水蓮陰乾後取出", ws.PrepLabel("water-lotus").StartsWith("陰乾"), ws.PrepLabel("water-lotus"));
            chips["qingling-herb"].Dropped(chips["qingling-herb"], ws.ZoneScreen("main"));
            chips["water-lotus"].Dropped(chips["water-lotus"], ws.ZoneScreen("aux"));
            yield return Click("Compound"); Mark("分料盤君臣配伍", ws.Compounded, $"君 {ws.MainHerb}・臣 {ws.AuxHerb}");
            yield return Leave();

            // 10 water
            yield return Interact("B06"); yield return Click("Ladle"); yield return Click("Ladle"); yield return Capture("water_jar");
            Mark("陶水缸舀水 2 瓢", ws.Ladles == 2); yield return Leave();

            // 11–13 furnace
            yield return Interact("B01"); yield return Capture("brew_shoulder");
            yield return Click("Water"); yield return Click("Water");
            yield return Click("Charge_water-lotus"); yield return Click("Charge_qingling-herb");
            yield return Until(() => ws.Api.State.player.alchemy.session.charged, 5);
            Mark("過肩：開蓋注水、依序投料（伺服器記錄已投料）", ws.Furnace.Water == 2 || ws.Log.Count(x => x.k == "water") == 2, $"session.charged={ws.Api.State.player.alchemy.session.charged}");
            yield return Click("Lid");
            var f = ws.Furnace; var fire = ws.Api.State.alchemy.fire; var phases = ws.Api.State.alchemy.profile.phases;
            double perPiece = fire.piecePower * (fire.ventPowerBase + fire.ventPowerScale * .5) / (fire.lossBase + fire.lossVent * .5);
            int start = (int)Math.Ceiling(((phases[0].low + phases[0].high) / 2 - fire.ambient) / perPiece);
            for (int i = 0; i < start; i++) yield return Click("Fuel");
            yield return Click("Ignite");
            float vent = .5f; bool mouthShot = false, hotShot = false; float condenseAt = -1;
            while (Time.realtimeSinceStartup - t0 < 900)
            {
                yield return new WaitForSecondsRealtime(1);
                if (f.CompleteTick != null && condenseAt < 0) condenseAt = Time.realtimeSinceStartup + 3;
                if (condenseAt > 0 && Time.realtimeSinceStartup >= condenseAt) break;
                if (f.CompleteTick != null) continue;
                var p = phases[Math.Min(f.Phase, phases.Length - 1)]; double target = (p.low + p.high) / 2;
                if (!hotShot && f.T > 40) { hotShot = true; yield return Capture("furnace_heating"); Check("控火"); }
                if (!mouthShot && f.Phase == 1) { mouthShot = true; yield return Click("Mouth"); yield return new WaitForSecondsRealtime(.9f); yield return Capture("furnace_mouth"); yield return Click("Mouth"); }
                float next = f.T < target - 3 ? 1 : f.T > target + 3 ? 0 : .5f;
                if (next != vent) { vent = next; yield return Click(next == 0 ? "Vent0" : next == 1 ? "Vent1" : "Vent5"); }
                if (f.Burning == 0) { yield return Click("Fuel"); yield return Click("Ignite"); continue; }
                int want = (int)Math.Ceiling((target - fire.ambient) / perPiece);
                if (f.T < target - 2 && f.Burning < want) yield return Click("Fuel");
            }
            Mark("合蓋添柴點火、以排煙檔控火完成兩段火候", f.CompleteTick != null, $"峰值 {f.Peak:0.0}・在火候內 {f.InBand:0.0}/{f.Active:0.0} 息・用柴 {f.FuelUsed}");
            yield return Capture("dan_aroma");
            yield return Click("Condense");
            yield return new WaitForSecondsRealtime(1.2f); yield return Capture("reveal_cinematic");
            yield return Until(() => !ws.RevealPlaying, 8); yield return new WaitForSecondsRealtime(.5f);
            var r = ws.Api.State.player.alchemy.session?.result;
            Mark("丹香起後收丹、開爐演出、伺服器判定品質", r != null && r.success, r == null ? ws.Api.Status : $"{r.label} {r.score} 分・首次領悟 {r.firstUnlock}・熟練 +{r.xpGain}");
            report.result = r == null ? null : JsonUtility.ToJson(r);
            yield return Capture("result"); Check("結果");
            yield return Click("Claim"); yield return new WaitForSecondsRealtime(.8f);
            Mark("以 B10 蠟封瓶裝瓶領取入包", Owned("huichun-pill") >= 1, $"回春丹 {Owned("huichun-pill")}"); yield return Capture("bottled");
            yield return Click("Claim");
            Mark("重複按領取不會重複發放", Owned("huichun-pill") == (r != null ? r.quantity : 1));

            // 15 scroll record, relogin, quick craft
            yield return Interact("B11"); yield return Click("Tab_recipes"); yield return Capture("scroll_recipe_unlocked");
            var known = ws.Api.State.alchemy.recipes.First(x => x.id == "huichun");
            Mark("丹方錄記下配比、處理、順序、火候與最佳品質", known.known && known.summary.Length >= 6, string.Join(" / ", known.summary ?? new string[0]));
            yield return Leave();
            ws.Api.Login(ws.Alias); yield return Until(() => !ws.Api.Busy, 10);
            Mark("重新登入後丹方與熟練仍在", ws.Api.State.alchemy.recipes.First(x => x.id == "huichun").known, $"Lv{ws.Api.State.alchemy.level} xp {ws.Api.State.alchemy.xp}");
            yield return Until(() => Time.realtimeSinceStartup - gatherAt > 16, 20);
            ws.Api.Act(new GameAction { kind = "gather", node = "herb-bank" }, (ok, e) => { }); yield return Until(() => !ws.Api.Busy && !ws.Api.HasPending, 5);
            yield return Interact("B11"); yield return Click("Tab_recipes");
            int before = Owned("huichun-pill");
            yield return Click("Quick_huichun"); yield return new WaitForSecondsRealtime(.6f); yield return Capture("quick_craft");
            Mark("已掌握丹方快速煉製（扣料扣柴，得凡品或良品）", Owned("huichun-pill") == before + 1, $"青靈草 {Owned("qingling-herb")}・淨水蓮 {Owned("water-lotus")}・松柴 {ws.Api.State.alchemy.fuel}");
            yield return Leave();
            ws.SetReducedMotion(true); yield return Interact("B05");
            Mark("減少鏡頭運動：直接切換不播放過渡", !ws.Director.Blending && ws.Director.Current == Shot.Cabinet);
            yield return Leave(); ws.SetReducedMotion(false);
            report.inventory = ws.Api.State.player.inventory.Select(s => $"{s.item}×{s.quantity}（potency {s.potency}{(s.bound ? "，綁定" : "")}）").ToArray();
            Mark("各畫面版面無重疊、未超出安全區", overlapIssues.Count == 0, string.Join("；", overlapIssues)); Finish();
        }

        IEnumerator Layout()
        {
            t0 = Time.realtimeSinceStartup;
            yield return Until(() => ws.Api.State != null && ws.Api.State.alchemy != null && !ws.Api.Busy, 30);
            Mark("登入", ws.Api.State?.alchemy != null); yield return new WaitForSecondsRealtime(1); yield return Capture("explore");
            yield return WalkTo("B05"); yield return Capture("prompt");
            Check("探索");
            yield return Interact("B05"); yield return new WaitForSecondsRealtime(.4f); yield return Capture("cabinet"); Check("藥櫃");
            yield return Leave(); yield return Interact("B11"); yield return Click("Tab_manuals"); yield return Capture("scroll"); Check("竹簡");
            yield return Leave(); yield return Interact("TABLE"); yield return Capture("table"); Check("工作台");
            yield return Leave(); yield return Interact("B01"); yield return Capture("furnace"); Check("丹爐");
            yield return Leave();
            // tap-to-walk: tap the cabinet from the far side of the room; the walker should arrive and open it
            var target = Station("B05"); var screen = Camera.main.WorldToScreenPoint(target.transform.position + Vector3.up * .5f);
            ws.Tap(screen); yield return Until(() => ws.Active != null && ws.Active.Id == "B05" && !ws.Director.Blending, 12);
            Mark("點擊藥櫃：自動走過去並打開", ws.Active != null && ws.Active.Id == "B05"); yield return Capture("tap_cabinet");
            yield return Leave();
            Mark("各解析度版面截圖與重疊檢查", overlapIssues.Count == 0, string.Join("；", overlapIssues)); Finish();
        }

        readonly List<string> overlapIssues = new List<string>();
        void Check(string where) { foreach (var o in ws.LayoutOverlaps()) { overlapIssues.Add(where + "：" + o); Debug.Log("LAYOUT_OVERLAP " + where + " " + o); } }

        void Finish()
        {
            report.pass = report.steps.All(s => s.pass);
            File.WriteAllText(Path.Combine(outDir, $"report_{report.resolution}.json"), JsonUtility.ToJson(report, true));
            Debug.Log("PLAYTEST_DONE " + (report.pass ? "PASS" : "FAIL") + " " + outDir);
            Application.Quit(report.pass ? 0 : 3);
        }
    }
}
