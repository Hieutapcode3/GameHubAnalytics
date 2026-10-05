# GameHub Analytics — Sổ Tay Tích Hợp & Hướng Dẫn Sử Dụng (Integration Manual)

Tài liệu này tổng hợp toàn bộ các hàm của **GameHub Analytics** kèm vị trí gọi cụ thể trong kiến trúc game để bạn hoặc bất kỳ developer nào khi import package sang project mới chỉ cần xem qua là tích hợp được ngay.

---

## ⚡ 1. Cài Đặt Nhanh (Setup 1 Lần)

Sau khi import package vào dự án mới:
1. Trên thanh Menu Unity, bấm: **`GameHub > Analytics > ⚙️ Create Analytics Config`**.
2. Một file `AnalyticsConfig.asset` sẽ tự động được tạo tại `Assets/Resources/AnalyticsConfig.asset`.
3. Bấm vào file đó và điền thông tin:
   - **`API Key`**: Web API Key lấy từ *Firebase Console > Project Settings > General*.
   - **`Project ID`**: Project ID Firebase (ví dụ: `my-game-12345`).
   - **`Game ID`**: Tên định danh phân loại của game (ví dụ: `StickmanRunner`, `MyPuzzleGame`).
   - **`Mobile Only For Mission Events`**: `true` (mặc định — chỉ gửi dữ liệu khi chạy trên điện thoại thật hoặc giả lập, tránh làm bẩn data khi test trong Unity Editor).

---

## 🚀 2. Bảng Tra Cứu Nhanh — Gọi Hàm Nào? Ở Đâu?

| Hàm Cần Gọi | Vị Trí Gọi Trong Game | Mục Đích |
| :--- | :--- | :--- |
| `AnalyticsManager.Instance.Initialize()` | `Awake()` hoặc `Start()` của Game Manager / Loading Scene | Khởi động hệ thống Analytics & đồng bộ profile |
| `AnalyticsManager.Instance.LogStartMission(...)` | Khi bấm nút Play, hoặc khi bắt đầu load màn chơi | Ghi nhận lượt bắt đầu chơi (`started++`) |
| `AnalyticsManager.Instance.LogCompleteMission(...)` | Khi người chơi vượt ải thành công (mở popup Win) | Ghi nhận chiến thắng, cập nhật kỷ lục thời gian |
| `AnalyticsManager.Instance.LogFailMission(...)` | Khi người chơi thua cuộc, hết giờ, game over | Ghi nhận thất bại, tính tỉ lệ WinRate |
| `AnalyticsManager.Instance.LogRetryMission(...)` | Khi người chơi bấm nút "Chơi lại" / "Thử lại" | Ghi nhận lượt retry màn chơi |
| `AnalyticsManager.Instance.LogQuitMission(...)` | Khi bấm nút "Thoát ra Menu" khi đang chơi dở | Ghi nhận lượt bỏ ngang màn chơi |
| `AnalyticsManager.Instance.LogCustomEvent(...)` | Xem Ads, mua shop, nâng cấp, claim quà | Ghi nhận các sự kiện kinh tế / hành vi tùy chọn |
| `AnalyticsManager.Instance.GetPlayerMissionStats(...)` | Khi load UI màn chơi, xem profile cá nhân | Lấy WinRate, Best Time của người chơi hiện tại |

---

## 📖 3. Chi Tiết Từng Hàm & Code Mẫu Thực Tế

### 3.1. Khởi Tạo Hệ Thống (Initialize)
- **Vị trí gọi**: Trong script quản lý tổng (ví dụ `GameManager.cs`, `AppManager.cs` hoặc `LoadingSceneController.cs`).
- **Code mẫu**:
```csharp
using UnityEngine;
using GameHub.Analytics;

public class GameManager : MonoBehaviour
{
    private void Start()
    {
        // Tự động tìm config trong Resources và kết nối
        AnalyticsManager.Instance.Initialize();
    }
}
```

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

## 📊 4. Mở Dashboard Quản Trị & Xuất Báo Cáo

Không cần mở trình duyệt web hay truy cập Firebase Console phức tạp, bạn có thể xem trực tiếp ngay trong Unity Editor:
1. Vào menu: **`GameHub > Analytics > 📊 Open Dashboard`**.
2. Bấm nút **`🔄 Tải Dữ Liệu Thật Từ Firebase`**.
3. Các tính năng có sẵn:
   - **Tab Màn Chơi (Missions)**: Tổng hợp Lượt chơi, Thắng, Thua, Tỉ lệ WinRate (Lion Studios standard), Tỉ lệ Hoàn thành %, Thời gian trung bình.
   - **Tab Người Chơi (Players)**: Chia danh sách theo từng `Player ID` riêng biệt với thẻ tóm tắt và nút thu gọn/mở rộng.
   - **Nút `📁 Xuất Excel (.xlsx)`**: Tự động tạo file Excel chuẩn OpenXML, trong đó **mỗi `Player ID` nằm trên 1 trang tính (Sheet/Tab) riêng biệt**, tương thích 100% khi mở bằng Microsoft Excel hoặc Google Sheets.
   - **Nút `🌐 Copy Cho Google Sheets`**: Copy dữ liệu dạng bảng vào Clipboard để Paste 1-click vào Google Sheets.
