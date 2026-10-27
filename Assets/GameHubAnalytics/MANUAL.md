# GameHub Analytics — Sổ Tay Tích Hợp & Hướng Dẫn Sử Dụng (Integration Manual)

Tài liệu này tổng hợp toàn bộ các hàm của **GameHub Analytics** kèm vị trí gọi cụ thể trong kiến trúc game để bạn hoặc bất kỳ developer nào khi import package sang project mới chỉ cần xem qua là tích hợp được ngay.

> **🤖 Dành cho AI / developer mới:** đây là file DUY NHẤT cần đọc để tích hợp. Mọi hàm public, nơi gọi, và các bẫy đều nằm ở đây.
> Ví dụ chạy được đầy đủ: [`Samples/GamePlay/LevelManager.cs`](Samples/GamePlay/LevelManager.cs) (vòng đời màn chơi) và [`Samples/GamePlay/GamePlayUI.cs`](Samples/GamePlay/GamePlayUI.cs) (popup tên + đánh giá sao).

## 🗂️ 0. Package Gồm Những Gì

| Đường dẫn | Nội dung |
| :--- | :--- |
| `Assets/GameHubAnalytics/Runtime/` | API chính: `AnalyticsManager` (namespace `GameHub.Analytics`), asmdef `GameHub.Analytics` |
| `Assets/GameHubAnalytics/Editor/` | Dashboard, Config Inspector, Package Exporter (menu `GameHub > Analytics`) |
| `Assets/GameHubAnalytics/UI/` | `NamePanel.cs`, `RatingPanel.cs` (namespace `GamePlay`) + builder `UI/Editor/GamePlayPanelBuilder.cs` |
| `Assets/GameHubAnalytics/UI/Resources/UI/` | Prefab `NamePanel` (nhập tên) và `RatingPanel` (đánh giá 1–5 sao), uGUI |
| `Assets/GameHubAnalytics/UI/Art/` | Sprite + font Baloo (kèm giấy phép `OFL.txt`) mà 2 prefab dùng, lấy từ pack Skyden Free Casual GUI |
| `Assets/GameHubAnalytics/Samples/GamePlay/` | Game mẫu tham chiếu (namespace `GamePlay`): `LevelManager`, `GamePlayUI` |
| `Assets/Resources/AnalyticsConfig.asset` | **KHÔNG nằm trong package** (package chỉ gồm thư mục `Assets/GameHubAnalytics`) — mỗi project tự tạo (chứa API key) |

**Quy tắc quan trọng:**
- `AnalyticsManager.Instance` **tự tạo GameObject và tự Initialize** từ `Resources/AnalyticsConfig` ở lần truy cập đầu tiên, tồn tại qua các scene (`DontDestroyOnLoad`). **Không cần** đặt vào scene, **không cần** gọi `Initialize()` (gọi thêm chỉ in cảnh báo "Already initialized").
- `missionId` là chuỗi tự đặt, nên thống nhất dạng `level_01`, `level_02`... (game mẫu dùng `$"level_{level:D2}"`).
- Trong Unity Editor **không có gì được gửi lên Firebase** (chỉ bản build mới gửi). Thấy log `Skipped '...' (Unity Editor)` là bình thường, không phải lỗi. `isEnabled = false` tắt hẳn Firebase và popup tên / đánh giá (xem mục 5).

---

## ⚡ 1. Cài Đặt Nhanh (Setup 1 Lần)

Sau khi import package vào dự án mới:
1. Trên thanh Menu Unity, bấm: **`GameHub > Analytics > ⚙️ Create Analytics Config`**.
2. Một file `AnalyticsConfig.asset` sẽ tự động được tạo tại `Assets/Resources/AnalyticsConfig.asset`, **đồng thời đường dẫn này được thêm vào `.gitignore` ở thư mục gốc project** để không đẩy API key lên git. (Nếu file đã từng được commit, chạy thêm `git rm --cached Assets/Resources/AnalyticsConfig.asset*`.)
3. Bấm vào file đó và điền thông tin:
   - **`API Key`**: Web API Key lấy từ *Firebase Console > Project Settings > General*.
   - **`Project ID`**: Project ID Firebase (ví dụ: `my-game-12345`).
   - **`Game ID`**: Tên định danh phân loại của game (ví dụ: `StickmanRunner`, `MyPuzzleGame`).
   - **`Is Enabled`**: `true` để bật. Dữ liệu chỉ được gửi từ bản build, không bao giờ từ Unity Editor (tránh làm bẩn data khi test).

---

## 🚀 2. Bảng Tra Cứu Nhanh — Gọi Hàm Nào? Ở Đâu?

| Hàm Cần Gọi | Vị Trí Gọi Trong Game | Mục Đích |
| :--- | :--- | :--- |
| *(không cần gọi)* | — | `Instance` tự khởi tạo ở lần truy cập đầu |
| `AnalyticsManager.Instance.GetPlayerName(cb)` | `Start()` của scene đầu tiên, **trước** khi bắt đầu màn | Lấy tên đã lưu; `""` = chưa có → hiện `NamePanel` |
| `AnalyticsManager.Instance.SetPlayerName(name)` | Khi người chơi bấm Xác nhận trên popup nhập tên | Lưu tên (PlayerPrefs + Firestore) |
| `AnalyticsManager.Instance.LogStartMission(...)` | Khi bấm nút Play, hoặc khi bắt đầu load màn chơi | Ghi nhận lượt bắt đầu chơi (`started++`) |
| `AnalyticsManager.Instance.LogCompleteMission(...)` | Khi người chơi vượt ải thành công (mở popup Win) | Ghi nhận chiến thắng, cập nhật kỷ lục thời gian |
| `AnalyticsManager.Instance.LogFailMission(...)` | Khi người chơi thua cuộc, hết giờ, game over | Ghi nhận thất bại, tính tỉ lệ WinRate |
| `AnalyticsManager.Instance.LogRateMission(...)` | Sau khi thắng, khi người chơi gửi đánh giá trên `RatingPanel` | Ghi số sao 1–5 cho màn chơi |
| `AnalyticsManager.Instance.LogRetryMission(...)` | Khi người chơi bấm nút "Chơi lại" / "Thử lại" | Ghi nhận lượt retry màn chơi |
| `AnalyticsManager.Instance.LogQuitMission(...)` | Khi bấm nút "Thoát ra Menu" khi đang chơi dở | Ghi nhận lượt bỏ ngang màn chơi |
| `AnalyticsManager.Instance.LogCustomEvent(...)` | Xem Ads, mua shop, nâng cấp, claim quà | Ghi nhận các sự kiện kinh tế / hành vi tùy chọn |
| `AnalyticsManager.Instance.GetPlayerMissionStats(...)` | Khi load UI màn chơi, xem profile cá nhân | Lấy WinRate, Best Time của người chơi hiện tại |

---

## 📖 3. Chi Tiết Từng Hàm & Code Mẫu Thực Tế

### 3.1. Khởi Tạo Hệ Thống (Tự Động)
- **Không cần gọi gì.** Lần đầu bất kỳ code nào truy cập `AnalyticsManager.Instance`, nó tự load `Resources/AnalyticsConfig` và khởi tạo.
- Muốn khởi tạo sớm (đồng bộ profile ngay khi mở game), chỉ cần chạm vào `Instance` trong scene đầu:
```csharp
using UnityEngine;
using GameHub.Analytics;

public class GameManager : MonoBehaviour
{
    private void Awake()
    {
        _ = AnalyticsManager.Instance; // tự Initialize từ Resources/AnalyticsConfig
    }
}
```
- Kiểm tra trạng thái: `AnalyticsManager.Instance.IsInitialized` (false nếu thiếu `AnalyticsConfig`).

---

### 3.2. Bắt Đầu Màn Chơi (Start Mission)
- **Vị trí gọi**: Ngay khi vào màn chơi và bắt đầu đếm giờ (`playTime = 0f`).
- **Cú pháp**: `AnalyticsManager.Instance.LogStartMission(string missionId, int retryCount = 0);`
- **Code mẫu**:
```csharp
public void StartLevel(int levelIndex)
{
    string missionId = $"level_{levelIndex:D2}"; // ví dụ: "level_01", "level_02"
    _playTime = 0f;

    // Gửi sự kiện bắt đầu lên Firebase
    AnalyticsManager.Instance.LogStartMission(missionId);
}
```

---

### 3.3. Hoàn Thành Màn Chơi (Complete Mission / Win)
- **Vị trí gọi**: Khi người chơi hoàn thành điều kiện thắng (chạm đích, hạ boss, giải xong câu đố) trước khi hiện popup Chiến Thắng.
- **Cú pháp**: `AnalyticsManager.Instance.LogCompleteMission(string missionId, float playTime, Dictionary<string, object> extraData = null);`
- **Code mẫu cơ bản**:
```csharp
public void OnPlayerWin()
{
    string missionId = $"level_{currentLevel:D2}";
    float finalPlayTime = _playTime; // số giây người chơi đã chơi trong màn này

    AnalyticsManager.Instance.LogCompleteMission(missionId, finalPlayTime);
}
```
- **Code mẫu kèm thông tin phụ (Số sao, điểm số)**:
```csharp
var extraData = new Dictionary<string, object>
{
    { "stars", 3 },
    { "score", 9500 },
    { "remaining_hp", 85 }
};
AnalyticsManager.Instance.LogCompleteMission(missionId, finalPlayTime, extraData);
```

---

### 3.4. Thất Bại Màn Chơi (Fail Mission / Lose)
- **Vị trí gọi**: Khi người chơi chết, rơi xuống hố, hết thời gian đếm ngược.
- **Cú pháp**: `AnalyticsManager.Instance.LogFailMission(string missionId, float playTime, Dictionary<string, object> extraData = null);`
- **Code mẫu**:
```csharp
public void OnPlayerLose(string reason)
{
    string missionId = $"level_{currentLevel:D2}";

    var extraData = new Dictionary<string, object>
    {
        { "fail_reason", reason }, // ví dụ: "trap", "timeout", "enemy_hit"
        { "progress_percent", 75 } // tiến độ % đạt được trước khi thua
    };

    AnalyticsManager.Instance.LogFailMission(missionId, _playTime, extraData);
}
```

---

### 3.5. Chơi Lại Màn Chơi (Retry Mission)
- **Vị trí gọi**: Gắn vào sự kiện `OnClick` của nút "Chơi lại" (Restart / Try Again) trên popup Lose hoặc Pause Menu.
- **Code mẫu**:
```csharp
public void OnClickRestartButton()
{
    string missionId = $"level_{currentLevel:D2}";

    AnalyticsManager.Instance.LogRetryMission(missionId);

    // Sau đó gọi hàm load lại màn chơi
    StartLevel(currentLevel);
}
```

---

### 3.6. Thoát Màn Chơi Giữa Chừng (Quit Mission)
- **Vị trí gọi**: Khi người chơi đang chơi dở nhưng bấm nút "Home", "Exit", "Thoát ra sảnh".
- **Code mẫu**:
```csharp
public void OnClickQuitToHome()
{
    string missionId = $"level_{currentLevel:D2}";

    AnalyticsManager.Instance.LogQuitMission(missionId, _playTime);

    // Chuyển về Home Scene
    SceneManager.LoadScene("HomeScene");
}
```

---

### 3.7. Bắn Sự Kiện Tùy Chỉnh (Custom Events — Shop, Ads, Gacha...)
- **Vị trí gọi**: Khi có bất kỳ hành vi tương tác kinh tế hoặc tính năng nào bạn muốn theo dõi.
- **Cú pháp**: `AnalyticsManager.Instance.LogCustomEvent(string eventName, string missionId = "global", Dictionary<string, object> customData = null);`
- **Code mẫu — Xem quảng cáo nhận thưởng**:
```csharp
AnalyticsManager.Instance.LogCustomEvent("watch_rewarded_ads", "shop", new Dictionary<string, object>
{
    { "placement", "revive_popup" },
    { "reward_type", "coins" },
    { "reward_amount", 500 }
});
```
- **Code mẫu — Mua đồ trong Shop**:
```csharp
AnalyticsManager.Instance.LogCustomEvent("buy_item", "shop", new Dictionary<string, object>
{
    { "item_id", "skin_ninja_01" },
    { "price", 1000 },
    { "currency", "gold" }
});
```

---

### 3.8. Lấy Thống Kê Người Chơi (Hiển thị WinRate & Kỷ Lục lên UI)
- **Vị trí gọi**: Khi mở bảng chọn màn hoặc bảng thông tin level để hiển thị kỷ lục tốt nhất của chính người chơi đó.
- **Lấy từ Firebase (Bất đồng bộ)**:
```csharp
AnalyticsManager.Instance.GetPlayerMissionStats("level_01", stats =>
{
    if (stats != null)
    {
        winRateText.text = $"Tỉ lệ thắng: {stats.WinRate:F1}%";
        bestTimeText.text = $"Kỷ lục: {stats.bestTime:F1}s";
        attemptsText.text = $"Số lần thử: {stats.TotalAttempts}";
    }
});
```
- **Lấy từ bộ nhớ Cache cục bộ (Tức thì, không tốn request mạng)**:
```csharp
PlayerMissionStats cached = AnalyticsManager.Instance.GetCachedMissionStats("level_01");
if (cached != null)
{
    Debug.Log($"WinRate: {cached.WinRate:F1}%");
}
```

---

### 3.9. Quản Lý Danh Tính Người Chơi (Player ID & Session)
- **Lấy ID máy hiện tại**:
  ```csharp
  string myPlayerId = AnalyticsManager.Instance.GetPlayerId();
  ```
- **Gán ID người chơi riêng** (nếu game có tính năng đăng nhập tài khoản / Facebook / Google Play Games):
  ```csharp
  AnalyticsManager.Instance.SetPlayerId("user_fb_982348234");
  ```
- **Lấy Session ID**:
  ```csharp
  string sessionId = AnalyticsManager.Instance.GetSessionId();
  ```

---

### 3.10. Tên Người Chơi (Player Name)
- **Cú pháp**:
  - `void GetPlayerName(Action<string> onComplete)` — đọc cache PlayerPrefs trước, sau đó Firestore. Trả `""` = chưa đặt tên, `null` = lỗi mạng.
  - `void SetPlayerName(string playerName)` — lưu PlayerPrefs + Firestore.
  - `string PlayerName` — đọc nhanh từ cache (không gọi mạng).
- **Vị trí gọi**: `Start()` của scene đầu tiên. Chỉ bắt đầu màn (`LogStartMission`) **sau khi** đã có tên. Dùng prefab `NamePanel` ở mục 4.

### 3.11. Đánh Giá Sao Màn Chơi (Rate Mission)
- **Cú pháp**: `void LogRateMission(string missionId, int stars)` — `stars` bị kẹp về 1..5.
- **Vị trí gọi**: Sau `LogCompleteMission`, khi người chơi bấm Gửi trên `RatingPanel`. Bỏ qua thì không gọi.
- Ghi 1 custom event `rate_mission` và lưu đánh giá mới nhất vào doc mission của người chơi (Dashboard đọc được).

---

## 🧩 4. UI Có Sẵn: NamePanel & RatingPanel

2 prefab uGUI tự chứa (Canvas Overlay riêng, sortingOrder 100, có nền tối chặn click phía sau), load bằng `Resources.Load`:

| Prefab | Component | API | Callback |
| :--- | :--- | :--- | :--- |
| `Resources/UI/NamePanel` | `GamePlay.NamePanel` | — | `Action<string> OnConfirm` (tên đã Trim, không rỗng, tối đa 20 ký tự) |
| `Resources/UI/RatingPanel` | `GamePlay.RatingPanel` | `Show(string missionId)` | `Action<int> OnDone` (1..5, hoặc **0 = bỏ qua**) |

**Yêu cầu**: scene phải có `EventSystem` (Input System → `InputSystemUIInputModule`, Input cũ → `StandaloneInputModule`). Prefab KHÔNG tự xóa — code gọi phải `Destroy` sau callback.

**Code tích hợp vào game của bạn:**
```csharp
using UnityEngine;
using GameHub.Analytics;
using GamePlay;

public class MyGameFlow : MonoBehaviour
{
    private void Start()
    {
        AnalyticsManager.Instance.GetPlayerName(name =>
        {
            // game không hỏi tên (askPlayerName / isEnabled tắt) hoặc đã có tên -> vào thẳng
            if (!AnalyticsManager.Instance.AskPlayerName || !string.IsNullOrEmpty(name)) { StartLevel(); return; }

            var panel = Instantiate(Resources.Load<NamePanel>("UI/NamePanel"));
            panel.OnConfirm = n =>
            {
                AnalyticsManager.Instance.SetPlayerName(n);
                Destroy(panel.gameObject);
                StartLevel(); // trong đó gọi LogStartMission
            };
        });
    }

    // Gọi sau LogCompleteMission khi người chơi thắng
    private void AskRating(string missionId)
    {
        if (!AnalyticsManager.Instance.AskStarRating) { /* hiện popup Win luôn */ return; }

        var panel = Instantiate(Resources.Load<RatingPanel>("UI/RatingPanel"));
        panel.Show(missionId);
        panel.OnDone = stars =>
        {
            if (stars > 0) AnalyticsManager.Instance.LogRateMission(missionId, stars);
            Destroy(panel.gameObject);
            // rồi hiện popup Win của game
        };
    }

    private void StartLevel() { /* ... */ }
}
```
- Đổi giao diện: mở prefab bằng Prefab Mode và chỉnh trực tiếp. Menu `GameHub > 🎨 Build UI Panel Prefabs` dựng lại prefab từ đầu (**ghi đè** chỉnh sửa tay) từ các file trong `UI/Art/`.
- Game mẫu: menu `GameHub > 🎮 Setup GamePlay In Current Scene` tạo `GamePlayController` (LevelManager + GamePlayUI) dùng đúng luồng trên, bấm Play để xem.

---

## ⚙️ 5. Cờ Trong AnalyticsConfig Ảnh Hưởng Việc Gửi Dữ Liệu

**Quy tắc cố định:** Firebase được dùng trên **mọi bản build** (Android, iOS, PC, WebGL...), **không bao giờ** dùng khi chạy trong Unity Editor. Không có cờ nào để bật gửi từ Editor — muốn xem dữ liệu thật thì build ra máy.

| Cờ / API | Mặc định | Tác dụng |
| :--- | :--- | :--- |
| `isEnabled` (config) | `true` | `false` = không gửi/đọc **bất cứ gì** với Firebase (event, thống kê, tên, profile, rating) **và không hiện** `NamePanel` / `RatingPanel` |
| `askPlayerName` (config) | `true` | Có hiện `NamePanel` hỏi tên lần đầu không. Tắt = game không cần tên |
| `askStarRating` (config) | `true` | Có hiện `RatingPanel` sau khi thắng không. Tắt = game không cần đánh giá sao |
| `enableDebugLog` (config) | `true` | In log chi tiết ra Console, kể cả log `Skipped '...'` khi bị chặn. Không ảnh hưởng việc gửi |
| `AnalyticsManager.Instance.SetEnabled(bool)` | — | Bật/tắt lúc chạy (ví dụ người chơi từ chối thu thập dữ liệu). Chỉ trong bộ nhớ, **không** sửa file config |
| `AnalyticsManager.Instance.IsEnabled` | — | `isEnabled` hiện tại |
| `AnalyticsManager.Instance.AskPlayerName` | — | `IsEnabled && askPlayerName` → **dùng cái này** để quyết định hiện `NamePanel` |
| `AnalyticsManager.Instance.AskStarRating` | — | `IsEnabled && askStarRating` → **dùng cái này** để quyết định hiện `RatingPanel` |
| `AnalyticsManager.Instance.CanSend` | — | `IsEnabled && !Application.isEditor` → Firebase có thực sự được dùng không |

Khi bị chặn, các hàm `Log...` / `SetPlayerName` chỉ bỏ qua (không lỗi); `GetPlayerName` trả tên trong cache hoặc `""`; `GetPlayerMissionStats` trả `null`. Tên người chơi vẫn được lưu PlayerPrefs.

---

## 📊 6. Mở Dashboard Quản Trị & Xuất Báo Cáo

Không cần mở trình duyệt web hay truy cập Firebase Console phức tạp, bạn có thể xem trực tiếp ngay trong Unity Editor:
1. Vào menu: **`GameHub > Analytics > 📊 Open Dashboard`**.
2. Bấm nút **`🔄 Tải Dữ Liệu Thật Từ Firebase`**.
3. Các tính năng có sẵn:
   - **Tab Màn Chơi (Missions)**: Tổng hợp Lượt chơi, Thắng, Thua, Tỉ lệ WinRate (Lion Studios standard), Tỉ lệ Hoàn thành %, Thời gian trung bình.
   - **Tab Người Chơi (Players)**: Chia danh sách theo từng `Player ID` riêng biệt với thẻ tóm tắt và nút thu gọn/mở rộng.
   - **Nút `📁 Xuất Excel (.xlsx)`**: Tự động tạo file Excel chuẩn OpenXML, trong đó **mỗi `Player ID` nằm trên 1 trang tính (Sheet/Tab) riêng biệt**, tương thích 100% khi mở bằng Microsoft Excel hoặc Google Sheets.
   - **Nút `🌐 Copy Cho Google Sheets`**: Copy dữ liệu dạng bảng vào Clipboard để Paste 1-click vào Google Sheets.
