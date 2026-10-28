# 煉丹玩法 V2：環境盤點與開發計畫

分支 `feature/alchemy-gameplay-v2`，起點 `origin/main` ebe57d4。本文件先記錄實際盤點結果，再列出開發方式；完成狀態與測試結果寫在 `alchemy-gameplay-v2-report.md`。

## 1. 實際環境（2026-10-28 盤點）

| 項目 | 實況 |
|---|---|
| 引擎 | Unity 6000.0.85f1（URP 17.0.4、Input System、uGUI、FishNet）。`ProjectVersion.txt` 在 main 上寫 82f1，本機只有 85f1；以 85f1 批次執行 `ProjectBuilder.Generate` 成功（return code 0，無 CS 錯誤）。 |
| 語言 | C#（客戶端）、TypeScript／NestJS（伺服器，PGlite） |
| 場景 | 由編輯器程式碼生成：`Generated/Qingxi.unity`（開發場景）、`QingxiHuntGraybox.unity`（獨立灰盒） |
| 玩家模型 | 膠囊體加袍色材質（`Cultivator.prefab`），沒有骨架角色 |
| 第三人稱控制 | `Cultivator`：FishNet 預測移動，WASD 與 `TouchInput`；`CameraRelativeDirection` 為靜態函式 |
| 鏡頭 | `QingxiOrbitCamera`：右鍵／右半觸控環繞、滾輪距離、SphereCast 避障；沒有 Cinemachine |
| 互動 | 沒有 3D 互動系統；玩法透過 `QingxiClient` 的 IMGUI 面板送 API |
| 背包 | 伺服器 `player.inventory: Stack{item,quantity,bound,potency}`，`grant`／`spend` |
| 天賦、靈根 | `births` 表：tier（mortal…chosen）、efficiency、roots（金木水火土）。創角滿 10 日測資前不公開 |
| 宗門 | `world.json` 的 `sects`（id、label、path）。可入：太玄道院（spell）、玄岳宗（body）；丹霞谷（alchemy）有定義但未開放 |
| 秘笈與熟練 | `techniques.json`（靈植辨識、辨石尋脈、行氣步法），以現實日研習。煉丹沒有既有熟練度 |
| 存檔 | 伺服器權威：`POST /v1/actions`，requestId 冪等收據、ledger、每角色鎖 |
| UI | IMGUI 開發面板；沒有正式 uGUI 介面，也沒有中文字型資產 |
| 輸入 | Input System；觸控搖桿只有 `TouchInput` 靜態欄位 |
| 特效、動畫、音效 | 專案內沒有現成的煉丹特效、動畫或音效 |

## 2. B01–B12 素材

路徑：`art/exports/qingxi-alchemy-b/<ID>/v001/`，每件有 FBX、GLB、`textures/`（BaseColor、Normal、ORM，加熱態另有 Emissive）、`model_info.json`、`validation.json`。單位為公尺，原點在底部中心。

| ID | 名稱 | 尺寸（寬×深×高 m） | 插槽與可動部位 | 玩法用途 |
|---|---|---|---|---|
| B01 | 青銅丹爐（冷／加熱） | 0.736×0.765×0.892 | lid_seat z0.746、mouth_center、flue_outlet | 煉製主體，冷熱兩態切換 |
| B02 | 爐蓋（一般／加熱） | 0.544×0.544×0.278 | grip、座面 | 開蓋投料、合蓋保溫 |
| B03 | 石臼 | 0.342×0.318×0.209 | grind_point | 研磨 |
| B04 | 竹編分料盤 | 0.626×0.466×0.665 | tray_upper、tray_lower | 君臣配伍 |
| B05 | 藥櫃 | 0.76×0.48×0.943 | Drawer1–3 沿 -Y 滑出 0.28 m | 取藥、辨材 |
| B06 | 陶水缸 | 0.549×0.468×0.553 | water_surface | 取水 |
| B07 | 柴箱 | 0.695×0.432×0.46 | take_point | 取柴 |
| B08 | 透氣晾藥籃 | 0.727×0.654×0.211 | herb_surface | 陰乾（計時） |
| B09 | 濾布架 | 0.624×0.42×0.934 | pour_point | 本版只作場景陳設 |
| B10 | 蠟封藥瓶（空／滿） | 0.112×0.076×0.109 | grip | 裝瓶領取 |
| B11 | 竹簡架 | 0.572×0.36×0.455 | read_focus | 竹簡四篇 |
| B12 | 排煙爐囪 | 0.763×0.739×1.945 | flue_inlet、smoke_outlet | 通風檔位 |

## 3. 兩種真實藥材

| id | 名稱 | 來源 | 既有用途 |
|---|---|---|---|
| `qingling-herb` | 青靈草 | 種子 `qingling-seed` 種植、`herb-bank` 採集、商人 3 株 4 靈石 | 淬體藥浴 ×3 |
| `water-lotus` | 淨水蓮 | 種子 `lotus-seed` 種植、商人 2 株 4 靈石 | 淨水藥劑 ×2 |

既有配方 `body-bath`、`water-purifier` 不更動。

## 4. 沿用與新增

沿用：
- 伺服器 `act()` 的收據、ledger、鎖。
- `grant`／`spend`、births 的天賦與靈根、宗門 path、靈植辨識秘笈。
- `QingxiOrbitCamera` 作為探索鏡頭，`Cultivator.CameraRelativeDirection` 與 `TouchInput`。
- `GameApi` 的登入與重送。

新增：
- `packages/content/alchemy.json`：藥材性質、處理方式、兩張新丹方、火候常數、品質權重、熟練度、天賦／靈根／宗門加成、丹經三卷。
- 伺服器 `services/api/src/alchemy.ts` 與 `alchemy-*` 操作。
- Unity：獨立生成的 `AlchemyRoom.unity`、本機行走、互動、多鏡頭、各工作站、竹簡 UI、火候模擬（與伺服器同公式）。

新增物品：
- `pine-firewood` 松柴（商人 5 根 1 靈石）。
- `huichun-pill` 回春丹、`juqi-pill` 聚氣丹、`pill-residue` 丹渣。

丹藥品級以 `potency` 表示：凡品 80、良品 100、上品 120、極品 150。

## 5. 伺服器權威流程

1. `alchemy-identify {item}`：在藥櫃辨識持有的藥材。這只是辨識，與丹方解鎖分開記錄。
2. `alchemy-begin {herbs:[{item,quantity}], fuel}`：鎖定並扣除藥材與柴薪，建立 `player.alchemy.session`。回傳本爐的火候輪廓，但不回傳配比、處理或順序的正解。
3. 客戶端在本機完成研磨、陰乾、配伍、取水、投料、控火、凝丹，並記錄事件。
4. `alchemy-charge`：第一次投料入爐的檢查點。在這之前放棄，材料全數退回；在這之後放棄，材料作廢並得到丹渣。
5. `alchemy-finish {log}`：伺服器以同一套公式重算整爐，並檢查實際經過時間不短於紀錄時間。接著計算品質、熟練度與首次解鎖，未用完的柴薪退回，結果放入 `session.result`。
6. `alchemy-claim`：B10 裝瓶入包。已領取時回 409。重送同一個 requestId 會得到原收據。
7. `alchemy-quick {recipe}`：已掌握（最佳品質達良品）的丹方可快速煉製，扣除標準材料與柴薪，產出凡品或良品。
8. `alchemy-abandon`：依是否已投料結算。

品質加權：比例 25%、處理 25%、火候穩定 35%、凝丹時機 15%。結果分為失敗／凡品／良品／上品／極品。

## 6. 鏡頭

全部在 `AlchemyCameraDirector`：
- 探索：沿用 QingxiOrbitCamera。
- 特寫：藥櫃、柴箱、爐口。
- 竹簡：近景。
- 桌面：45° 俯視，用於研磨、陰乾、配伍、拖放。
- 煉製：過肩。
- 開爐：運鏡，看過一次後可略過。

鏡頭轉換使用 0.35–0.8 s 平滑曲線，鏡位以 SphereCast 防穿模。可選擇減少運動（改成直接切換）；取消時回到原鏡位。

## 7. 驗證方式

- `npm test`：新增煉丹測試，涵蓋完整流程、重送、重複領取、材料不足、放棄結算、快速煉製、重登恢復，以及 TS 與 C# 火候對照資料。
- Unity 批次：生成 `AlchemyRoom.unity`；執行玩家路徑自動測試，在真實場景中透過與按鍵相同的互動入口操作；對本機 API 截圖。
- 無法實測的項目，例如手機實機、實體觸控、效能，在報告中標示未驗證。
