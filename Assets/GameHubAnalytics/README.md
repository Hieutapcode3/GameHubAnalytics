# GameHub Analytics

Internal analytics package for Unity — kết nối Firebase Firestore qua REST API.  
Không cần Firebase Unity SDK. Chỉ cần API Key và Project ID.

---

## 🚀 Cài Đặt

### 1. Tạo AnalyticsConfig

Dùng menu: **GameHub > Analytics > ⚙️ Create Analytics Config**

Hoặc thủ công:  
Right-click trong Project Window → **Create > GameHub > Analytics Config**  
→ Đặt tên `AnalyticsConfig` → Đặt vào thư mục `Assets/Resources/`

### 2. Điền thông tin Firebase

Mở file `AnalyticsConfig.asset` trong Inspector:

| Field | Lấy tại |
|-------|---------|
| **API Key** | Firebase Console > Project Settings > General > Web API Key |
| **Project ID** | Firebase Console > Project Settings > General > Project ID |
| **Game ID** | Tên game của bạn (ví dụ: `my-awesome-game`) |

### 3. Test kết nối

Nhấn nút **"🔗 Test Connection"** trong Inspector của AnalyticsConfig.

---

## 📖 Cách Dùng

```csharp
// Khởi tạo (gọi 1 lần, thường trong Start())
AnalyticsManager.Instance.Initialize();

// Log bắt đầu mission
AnalyticsManager.Instance.LogStartMission("level_01");

// Log hoàn thành
AnalyticsManager.Instance.LogCompleteMission("level_01", playTime: 45.5f);

// Log thất bại
AnalyticsManager.Instance.LogFailMission("level_01", playTime: 30.2f);

// Log event tùy chỉnh
AnalyticsManager.Instance.LogCustomEvent("boss_defeated", "level_01", new Dictionary<string, object>
{
    { "boss_name", "Dragon King" },
    { "score", 9500 }
});
```

---

## 🗂️ Cấu Trúc Firestore

```
analytics/
  {gameId}/
    missions/
      {missionId}/
        events/
          {autoId}   ← mỗi event được ghi vào đây
```

---

## 🔧 Menu Items

| Menu | Chức năng |
|------|-----------|
| **GameHub > Analytics > 📊 Open Dashboard** | Mở Dashboard xem stats |
| **GameHub > Analytics > ⚙️ Create Analytics Config** | Tạo file config |
| **GameHub > Analytics > 📦 Export .unitypackage** | Xuất package để share |
| **GameHub > Analytics > ℹ️ About** | Thông tin version |

---

## 📊 Event Types

| Event | Phương thức |
|-------|-------------|
| Bắt đầu mission | `LogStartMission(missionId)` |
| Hoàn thành | `LogCompleteMission(missionId, playTime)` |
| Thất bại | `LogFailMission(missionId, playTime)` |
| Chơi lại | `LogRetryMission(missionId)` |
| Thoát giữa chừng | `LogQuitMission(missionId, playTime)` |
| Tùy chỉnh | `LogCustomEvent(eventName, missionId, data)` |

---

## 🔒 Firestore Security Rules (Test Mode)

```javascript
rules_version = '2';
service cloud.firestore {
  match /databases/{database}/documents {
    match /{document=**} {
      allow read, write: if request.time < timestamp.date(2027, 1, 1);
    }
  }
}
```

---

## 📦 Xuất Package

**GameHub > Analytics > 📦 Export .unitypackage**

File sẽ được đặt tên: `GameHubAnalytics_v1.0.0.unitypackage`

---

**Version:** 1.0.0  
**Compatibility:** Unity 6000.x (URP)  
**Author:** GameHub Team
