# 🎮 GameHub Analytics

> **Internal Unity Package** — Firebase Firestore Analytics cho Gameplay Testing  
> Không cần Firebase Unity SDK. Chỉ cần API Key + Project ID là chạy được.

[![CI](https://github.com/Hieutapcode3/GameHubAnalytics/actions/workflows/ci.yml/badge.svg)](https://github.com/Hieutapcode3/GameHubAnalytics/actions/workflows/ci.yml)
[![Release](https://github.com/Hieutapcode3/GameHubAnalytics/actions/workflows/release.yml/badge.svg)](https://github.com/Hieutapcode3/GameHubAnalytics/releases)
[![Unity 6000.x](https://img.shields.io/badge/Unity-6000.x-black?logo=unity)](https://unity.com)
[![Firebase Firestore](https://img.shields.io/badge/Firebase-Firestore-orange?logo=firebase)](https://firebase.google.com/docs/firestore)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow)](LICENSE)

---

## 🗺️ Gitflow Diagram

```
main          ──────────●──────────────────────────────●── (production)
                        ↑                              ↑
release/v1.0  ──────────●──────────────────────────────
                        ↑
develop       ────●─────●─────●─────────●──────────────── (integration)
                  ↑           ↑         ↑
feature/...   ────●           ●         ●              (features)
fix/...                       ●                        (bugfixes)
hotfix/...                              ●              (emergency)
```

---

## 🌿 Branch Strategy

| Branch | Mục đích | Base | Merge vào |
|--------|----------|------|-----------|
| `main` | Production stable | — | — |
| `develop` | Integration branch | `main` | `main` (via release) |
| `feature/*` | Tính năng mới | `develop` | `develop` |
| `fix/*` | Sửa bug không khẩn cấp | `develop` | `develop` |
| `hotfix/*` | Sửa bug khẩn cấp production | `main` | `main` + `develop` |
| `release/*` | Chuẩn bị release | `develop` | `main` + `develop` |

---

## 🚀 Cài Đặt Package

### Cách 1: Import .unitypackage
1. Tải file từ [Releases](https://github.com/Hieutapcode3/GameHubAnalytics/releases)
2. Double-click file `.zip` để giải nén
3. Kéo thư mục `GameHubAnalytics` vào `Assets/` của project Unity

### Cách 2: Clone repo
```bash
# Clone vào thư mục Assets của Unity project
git clone https://github.com/Hieutapcode3/GameHubAnalytics.git Assets/GameHubAnalytics
```

---

## ⚙️ Setup Firebase

### Bước 1 — Tạo Firebase Project
1. Vào [Firebase Console](https://console.firebase.google.com)
2. Tạo project mới hoặc dùng project có sẵn
3. Enable **Firestore Database** (Start in Test Mode)

### Bước 2 — Lấy Config
1. Project Settings → General → Your apps → **Web App**
2. Copy `apiKey` và `projectId`

### Bước 3 — Tạo Analytics Config trong Unity
Dùng menu: **GameHub > Analytics > ⚙️ Create Analytics Config**

Điền vào Inspector:
```
API Key:     [paste apiKey từ Firebase]
Project ID:  [paste projectId từ Firebase]
Game ID:     my-game-name
```

### Bước 4 — Test kết nối
Nhấn nút **"🔗 Test Connection"** trong Inspector → nên thấy `✅ Kết nối thành công`

---

## 📖 Cách Dùng

```csharp
void Start()
{
    // Khởi tạo (tự load từ Resources/AnalyticsConfig)
    AnalyticsManager.Instance.Initialize();
}

// Ghi events
AnalyticsManager.Instance.LogStartMission("level_01");
AnalyticsManager.Instance.LogCompleteMission("level_01", playTime: 45.5f);
AnalyticsManager.Instance.LogFailMission("level_01", playTime: 30.2f);

// Đọc stats của player hiện tại
AnalyticsManager.Instance.GetPlayerMissionStats("level_01", stats => {
    Debug.Log($"Win Rate: {stats.WinRate:F1}%");
    Debug.Log($"Best Time: {stats.bestTime:F1}s");
});

// Custom event
AnalyticsManager.Instance.LogCustomEvent("boss_defeated", "level_01", new Dictionary<string, object> {
    { "boss_name", "Dragon King" },
    { "score", 9500 }
});
```

---

## 📊 Firestore Data Structure

```
players/
  {devicePlayerId}/                ← Mỗi device = 1 player
    platform: "Android"
    deviceModel: "Samsung Galaxy S21"
    lastSeen: timestamp
    missions/
      {missionId}/                 ← Mỗi mission
        started:       int         ← Atomic increment
        completed:     int         ← Atomic increment
        failed:        int         ← Atomic increment
        bestTime:      float
        totalPlayTime: float
        lastPlayed:    timestamp

analytics/
  {gameId}/
    missions/
      {missionId}/
        events/
          {autoId}/                ← Mỗi event chi tiết
            eventType: string
            timestamp: ISO 8601
            playerId:  string
            platform:  string
```

---

## 🎛️ Editor Tools

| Menu | Mô tả |
|------|-------|
| `GameHub > Analytics > 📊 Open Dashboard` | Dashboard xem stats |
| `GameHub > Analytics > ⚙️ Create Analytics Config` | Tạo file config |
| `GameHub > Analytics > 📦 Export .unitypackage` | Xuất package để share |
| `GameHub > Analytics > ℹ️ About` | Thông tin version |

---

## 🤝 Contributing

### Gitflow Workflow

```bash
# Tạo feature mới
git checkout develop
git pull origin develop
git checkout -b feature/your-feature-name

# Làm việc...
git add .
git commit -m "feat: add your feature description"

# Push và tạo PR vào develop
git push origin feature/your-feature-name
```

### Commit Convention

```
feat:     ✨ Tính năng mới
fix:      🐛 Sửa bug
docs:     📝 Chỉ cập nhật docs
style:    🎨 Format, không thay đổi logic
refactor: ♻️  Cải thiện code
perf:     ⚡ Tối ưu hiệu năng
test:     🧪 Thêm tests
chore:    🔧 Config, build tools
hotfix:   🚑 Sửa bug khẩn cấp production
release:  🚀 Release version mới
```

---

## 📋 Roadmap

- [x] Phase 0 — Firebase Setup Guide
- [x] Phase 1 — Core REST API (write events)
- [x] Phase 1 — Player Data (per-device stats)
- [x] Phase 1 — Editor Inspector + Dashboard
- [ ] Phase 2 — Offline Queue Retry (in progress)
- [ ] Phase 3 — Dashboard fetch real Firestore data
- [ ] Phase 4 — Export .unitypackage via CI
- [ ] Phase 5 — Web Dashboard (Next.js)

---

## 📄 License

MIT License — © 2026 GameHub Team  
See [LICENSE](LICENSE) for details.

---

**Version:** 1.0.0 | **Unity:** 6000.x | **Author:** Hieu-Dev
