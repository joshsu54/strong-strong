# 煉丹玩法 V2：交付報告

分支 `feature/alchemy-gameplay-v2`。環境盤點與設計見 `alchemy-gameplay-v2.md`。本報告只列實際執行過的檢查。

## 1. 執行方式

```bash
npm test
```

```powershell
./tools/start-local.ps1
```

先產生場景並驗證（Unity 6000.0.85f1），再建置 Windows 開發版：

```powershell
& "C:/Program Files/Unity/Hub/Editor/6000.0.85f1/Editor/Unity.exe" -batchmode -quit -projectPath apps/game -executeMethod YunJing.Editor.AlchemyRoomBuilder.Verify -logFile artifacts/unity-alchemy-verify.log
```

```powershell
& "C:/Program Files/Unity/Hub/Editor/6000.0.85f1/Editor/Unity.exe" -batchmode -quit -projectPath apps/game -executeMethod YunJing.Editor.AlchemyRoomBuilder.BuildWindows -logFile artifacts/unity-alchemy-build.log
```

伺服器啟動後，執行 `artifacts/alchemy-windows/YunJingAlchemy.exe`：
- 鍵盤：WASD 移動，E 互動，Esc 返回或開選單，右鍵拖曳轉鏡頭，滾輪調整距離。
- 觸控：左下虛擬搖桿，右下「互動」按鈕，右半螢幕拖曳轉鏡頭。

自動玩家路徑：

```powershell
artifacts/alchemy-windows/YunJingAlchemy.exe -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -alchemyPlaytest -alchemyAlias playtest-001 -alchemyOut artifacts/alchemy-playtest
```

在 Editor 中也可用選單「Yun Jing/Generate alchemy room」產生 `Assets/YunJing/Generated/AlchemyRoom.unity` 後按 Play。

## 2. 變更檔案

| 檔案 | 內容 |
|---|---|
| `packages/content/alchemy.json` | 藥材性質、處理方式、回春丹與聚氣丹、火候常數、品質權重、熟練度、天賦／靈根／宗門加成、丹經三卷 |
| `packages/content/world.json` | 新物品：松柴、回春丹、聚氣丹、丹渣 |
| `packages/content/qingxi.json` | 商人阿禾新增松柴 5 根 1 靈石 |
| `packages/contracts/types.ts` | `AlchemyState`、`AlchemySession`、`AlchemyResult`、`AlchemyView` |
| `services/api/src/alchemy.ts` | 決定性的爐火重算、紀錄檢查、品質判定、熟練度、加成 |
| `services/api/src/game.ts` | `alchemy-identify／begin／charge／finish／claim／abandon／quick／use`，快照新增 `alchemy` 視圖 |
| `tests/alchemy.test.ts`、`tests/alchemy-autopilot.ts`、`tests/fixtures/alchemy-trace.json` | 9 項伺服器測試、測試用玩家代理、TS／C# 對照資料 |
| `apps/game/Assets/YunJing/Runtime/GameApi.cs` | 新增煉丹資料模型、操作結果回呼、`Refresh` |
| `apps/game/Assets/YunJing/Runtime/Alchemy/*.cs` | 爐火模擬（C#）、多鏡頭、行走、工作台互動、竹簡與介面、程序化音效與粒子、自動玩家路徑 |
| `apps/game/Assets/YunJing/Editor/AlchemyRoomBuilder.cs` | 匯入 B01–B12、產生丹房、驗證、建置 |
| `docs/execution/alchemy-v2-screenshots/` | 實機截圖與自動玩家報告（`report_*.json`） |

本分支不提交 Unity 自動產生的 `ProjectSettings`、`Packages`、IrrigationV2／CollaboratorWorldV1 等 `.meta` 變動。建置時由 `AlchemyRoomBuilder.IncludeUiShaders` 把 `UI/Default`、`Sprites/Default` 加入必含著色器，所以不必提交 `GraphicsSettings.asset`。

## 3. B01–B12 使用方式

| 編號 | 遊戲內用途 | 互動 |
|---|---|---|
| B01 青銅丹爐 | 煉製主體，冷態與加熱態依爐溫切換材質發光 | 過肩鏡頭；開合蓋、注水、投料、添柴、點火、收丹 |
| B02 爐蓋 | 開蓋時移到爐邊石板上，合蓋時回到 lid_seat | 開蓋會散熱（`lossLidOpen`），合蓋才能點火保溫 |
| B03 石臼 | 研磨 | 拖曳藥材入臼，在研磨區畫圈 12 次 |
| B04 分料盤 | 君臣配伍 | 拖曳到「君」「臣」兩格 |
| B05 藥櫃 | 取藥、辨識、選份量 | 特寫鏡頭；抽屜依操作滑出 |
| B06 陶水缸 | 舀水（手上最多 3 瓢） | 特寫鏡頭 |
| B07 柴箱 | 取柴後開始本爐，伺服器扣藥扣柴 | 特寫鏡頭 |
| B08 晾藥籃 | 陰乾計時，8–24 息為合格，過久焦乾 | 拖曳放入、按鈕取出 |
| B09 濾布架 | 本版只作場景陳設，沒有互動 | — |
| B10 蠟封藥瓶 | 裝瓶動畫，領取後入背包 | 結果視窗「以蠟封瓶裝瓶領取」 |
| B11 竹簡架 | 竹簡四篇：藥材錄、丹方錄、丹道推演、煉丹紀錄 | 特寫鏡頭；已掌握丹方可快速煉製 |
| B12 排煙爐囪 | 排煙檔閉／半／全開，決定火力與耗柴 | 爐火面板 |

## 4. 藥材與丹方

只使用兩味真實藥材：青靈草（`qingling-herb`）、淨水蓮（`water-lotus`）。

- 回春丹：淨水蓮 1、青靈草 2；淨水蓮陰乾、青靈草研末；順序為注水 → 淨水蓮 → 青靈草；注水 2 瓢；文火 22–40 維持 10 息，再中火 45–62 維持 12 息；丹香起後 2–6 息收丹。
- 聚氣丹：青靈草 1、淨水蓮 2；青靈草陰乾、淨水蓮研末；順序為注水 → 青靈草 → 淨水蓮；注水 1 瓢；中火 45–62 維持 10 息，再武火 68–85 維持 10 息；丹香起後 1–4 息收丹。
- 1：1 時兩味藥性互抵，必定失敗。

辨識藥材只揭示藥性，不會解鎖丹方。第一次成丹時才「領悟新丹方」，並永久記入丹方錄。

## 5. 鏡頭

- 探索：沿用 `QingxiOrbitCamera`。
- 特寫：藥櫃、柴箱、水缸、竹簡。主體偏左，右側三分之一留給面板。
- 工作台：約 41° 斜俯視，可拖放藥材。
- 爐前過肩：跟隨玩家。
- 爐口特寫：可看到爐膛與炭火。
- 開爐演出：3.2 秒環繞推近，可略過。

過渡為 smoothstep，限制在 0.35–0.8 秒。鏡頭以 SphereCast 避免穿模。取消時回到進入特寫前的探索位置。開啟「減少鏡頭運動」後直接切換，不播放過渡。

## 6. 成長系統

- 煉丹熟練 Lv1–100，經驗遞減，最低保留 25%。
  - 失敗 +4、成功 +10、首次領悟 +20、首次極品 +5、快速煉製 +2。
- 品質權重：配比 25%、處理 25%、火候 35%、收丹 15%。
  - 品級門檻 45／65／80／92，對應凡品、良品、上品、極品。
  - 品級寫入丹藥的 potency：80／100／120／150。
- 失敗時列出原因，得丹渣一份，仍得少量熟練。
- 天賦與靈根加成只讀取既有角色資料。宗門加成只讀取既有的三條路線（alchemy、spell、body），沒有改寫宗門資料。
- 丹經三卷（丹道推演）只提供方向提示，不會解鎖丹方。
  - 第 2 卷：Lv10 或已學藥材辨識技藝。
  - 第 3 卷：Lv30。
- 快速煉製：該丹方最佳品質達良品以上才開放，熟練 Lv30 以上有機會得良品。

## 7. 伺服器權威與防刷

- 所有資產變動走 `POST /v1/actions`。
  - 以 requestId 去重：重送同一請求只回原收據。
  - 每位玩家有鎖，處理失敗時整筆回滾。
- `alchemy-begin` 鎖定並扣除藥材與柴。
- `alchemy-charge` 寫入「已投料」檢查點。
- `alchemy-finish` 由伺服器以同一套整數 tick 規則重算整份操作紀錄。
  - 紀錄時間長於實際經過時間（加 1.5 秒容差）就拒收（`LOG_TOO_FAST`）。
- `alchemy-claim` 只能領一次。
- 中途放棄：投料前全數退回，投料後材料作廢，只得丹渣。
- TS 與 C# 爐火模擬的一致性，以 `tests/fixtures/alchemy-trace.json` 的 53 個取樣點對照。

## 8. 實際執行的檢查

| 檢查 | 結果 |
|---|---|
| `npm test` | 64 項通過、0 失敗；含 9 項煉丹測試，涵蓋重複領取、並行、放棄退款、紀錄過快、品質與熟練 |
| Unity `AlchemyRoomBuilder.Verify` | `ALCHEMY_VERIFY_PASS`：B01–B12 匯入，尺寸與 model_info 相符 |
| Unity `VerifySim` | `ALCHEMY_SIM_PARITY_PASS 53 samples, completeTick 491` |
| Windows 開發版建置 | `ALCHEMY_BUILD_PASS`，171,832,361 bytes |
| 自動玩家路徑（1920×1080，實機執行檔連本機伺服器） | 20／20 步通過，見下表 |
| 版面檢查（1280×720、2340×1080） | 探索、藥櫃、工作台、丹爐、竹簡、互動提示各一張，已逐張檢視 |

自動玩家路徑結果（最後一次執行）：

1. 登入本機伺服器，進入丹房。
2. 向商人阿禾購得青靈草 4、淨水蓮 2。
3. 辨識兩味藥材，丹方仍顯示「？」。
4. 選取 2：1。
5. 取消特寫，回到探索鏡頭。
6. 取柴 6 根開始本爐，伺服器扣除藥材與柴。
7. 查閱竹簡：未知丹方顯示「？」。
8. 青靈草在石臼研磨完成。
9. 淨水蓮陰乾後取出。
10. 君臣配伍。
11. 舀水 2 瓢。
12. 開蓋注水、依序投料，伺服器記錄 `session.charged=True`。
13. 兩段火候：峰值 58.4，在所需火候內 22.0／25.3 息，用柴 3 根。
14. 開爐演出，伺服器判定極品 99 分，首次領悟，熟練 +35。
15. 以 B10 裝瓶，回春丹 1 枚入背包。
16. 重複按領取不會重複發放。
17. 丹方錄記下配比、處理、順序、注水、火候、收丹與最佳品質。
18. 重新登入後丹方與熟練（Lv2，35 xp）仍在。
19. 快速煉製：扣料扣柴，得凡品或良品。
20. 減少鏡頭運動：直接切換。

自動玩家依實際影格時間操作，所以每次分數略有不同。三次執行分別得 94、95、99 分，都是極品。

## 9. 截圖

所有截圖都是 Windows 執行檔的實機畫面，存放在 `docs/execution/alchemy-v2-screenshots/`：

- 探索與藥櫃：`1920x1080_01_explore.png`、`1920x1080_01_cabinet_closeup.png`
- 工作台與陰乾：`1920x1080_07_table_topdown.png`、`1920x1080_08_drying.png`
- 水缸：`1920x1080_10_water_jar.png`
- 爐火：`1920x1080_11_brew_shoulder.png`、`1920x1080_12_furnace_heating.png`、`1920x1080_12_furnace_mouth.png`
- 開爐與結果：`1920x1080_13_reveal_cinematic.png`、`1920x1080_14_result.png`
- 裝瓶：`1920x1080_15_bottled.png`
- 竹簡與快速煉製：`1920x1080_16_scroll_recipe_unlocked.png`、`1920x1080_18_quick_craft.png`
- 其他解析度：`1280x720_*`、`2340x1080_*`

## 10. 尚未驗證／未完成（如實列出）

- 自動玩家透過與滑鼠、鍵盤相同的處理函式操作，沒有用實體鍵鼠走完全程。
- 沒有在實體觸控裝置上測試。觸控搖桿與互動鍵已實作，但未實測。
- 沒有做 Android／iOS 建置，也沒有手機效能數據。
- 玩家角色是灰模膠囊，不是最終角色。
- 丹房外殼（牆、地板、屋樑、門、工作台桌面）是灰模佔位。B01–B12 使用既有正式模型。
- 音效是程序化生成的佔位音效，不是最終音效。
- B09 濾布架只作陳設，沒有互動。
- 燃燒中途重新登入無法接續該爐：視同投料後放棄，材料作廢、得丹渣。
- 沒有正式 Firebase，也沒有多人即時驗證。測試只用 local 模式與 127.0.0.1。

## 11. 已知問題

- 建置後 URP 的 `VolumeProfileOverridesAnalytic` 會丟出 NullReferenceException。這是 Unity 套件的分析回呼問題，不影響建置結果（`Build Finished, Result: Success`）。
- 進入過肩鏡頭時，膠囊玩家會遮住部分爐身，換成正式角色後需要重新調整鏡頭。

## 12. 手機版（介面與觸控）

**版面：**
- 手機橫向（寬高比 16:10 以上）改用精簡版面：畫布以 1280×720 為基準、依高度縮放，所以介面在手機上放大約 1.5 倍。
- 按鈕高 64、字級 22。
- 爐火按鈕依面板寬度自動排成兩欄。
- 平板（寬高比小於 16:10）沿用電腦版排列，但以觸控尺寸顯示。
- 所有介面放在 `Screen.safeArea` 內，避開瀏海、圓角和底部手勢列。
- 工作台、丹爐等面板內容超出時，可以上下捲動。
- 在工作台時，提示訊息移到藥材卡上方，不會蓋住放置區。

**觸控：**
- 點擊場景物件會自動走過去並打開，已在旁邊時直接打開。也可以用左下搖桿加「互動」鍵。
- 虛擬搖桿半徑跟著畫面密度縮放。

**電腦預覽：**
- 加上 `-alchemyMobile` 參數，就能在電腦上用手機版面執行。
- 加上 `-alchemySafeInset <px>` 可模擬瀏海的內縮。

**實際執行的檢查（Windows 執行檔＋`-alchemyMobile`）：**

| 檢查 | 結果 |
|---|---|
| 2340×1080，模擬兩側內縮 90 px，跑完整自動玩家路徑 | 21／21 通過；第 21 項是每個畫面都檢查版面無重疊、沒有超出安全區 |
| 1600×720 版面檢查＋點擊藥櫃自動走過去 | 通過 |
| 2532×1170（模擬內縮 120 px）版面檢查＋點擊 | 通過 |
| 2048×1536 平板版面檢查＋點擊 | 通過（螢幕限制，實際視窗為 2048×1440） |
| 電腦版 1920×1080 完整路徑（回歸） | 21／21 通過 |

截圖放在 `docs/execution/alchemy-v2-mobile-screenshots/`。

**手機版尚未完成或未驗證：**
- 沒有 Android 或 iOS 安裝檔：這台電腦的 Unity 只裝了 Windows 模組，要加裝 Android Build Support 才能出 APK。iOS 必須用 Mac 和 Xcode。
- 沒有在實體手機上測試，所以觸控手感、效能、發熱和實際安全區都還沒驗證。上面的測試都是在電腦上模擬手機解析度。
- 手機要連電腦上的本機伺服器。依專案規則，測試身分只能用 loopback，所以建議用 USB 加 `adb reverse tcp:4310 tcp:4310`，不要開放區網位址。
- 共用的 `QingxiOrbitCamera` 會把右半螢幕的拖曳當成轉鏡頭，按右下「互動」鍵時鏡頭可能會稍微轉一下。這次沒有修改這個共用檔。
