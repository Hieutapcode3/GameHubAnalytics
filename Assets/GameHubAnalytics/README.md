# 🎮 GameHub Analytics

> **Bộ công cụ Unity Analytics & Dữ liệu Gameplay nội bộ kết nối trực tiếp với Firebase Firestore**  
> Giải pháp theo dõi chỉ số game (Start, Win Rate, Completion Rate, Play Time...) siêu nhẹ qua REST API — **Hoàn toàn KHÔNG cần cài đặt Firebase Unity SDK**.

[![Unity Version](https://img.shields.io/badge/Unity-2021.3%20%7C%202022.3%20%7C%206000.x-black?logo=unity)](https://unity.com)
[![Firebase Firestore](https://img.shields.io/badge/Firebase-Firestore%20REST%20API-orange?logo=firebase)](https://firebase.google.com/docs/firestore)
[![Platform](https://img.shields.io/badge/Platform-Android%20%7C%20iOS%20%7C%20PC%20%7C%20WebGL-blue)]()
[![License](https://img.shields.io/badge/License-MIT-green)]()

---

## 💡 Giới Thiệu Dự Án

Khi phát triển và test nội bộ các bản build game (đặc biệt là xuất file APK chạy trên máy thật Android, iOS hay PC), đội ngũ phát triển thường gặp khó khăn:
- **Cài đặt Firebase Unity SDK rất nặng**: Dễ gây xung đột thư viện native, lỗi Gradle / CocoaPods khi build.
- **Khó theo dõi dữ liệu của từng thiết bị test**: Không biết máy nào chơi đến màn nào, tỷ lệ thắng/thua ra sao, màn chơi nào quá khó hoặc quá dễ.
- **Thiếu công cụ xem nhanh**: Phải vào console Firebase tìm kiếm thủ công từng log thô.

**GameHub Analytics** được thiết kế để giải quyết toàn bộ các vấn đề trên. Đây là một package trọn gói, độc lập, kết nối với Firestore thông qua **HTTP/REST API thuần (`UnityWebRequest`)**. Bạn chỉ cần nhập `API Key` và `Project ID` là hệ thống sẵn sàng hoạt động trong 1 phút!

---

## ✨ Dự Án Cung Cấp Gì & Làm Được Gì?

### 1. 📱 Nhận diện người chơi tự động theo thiết bị thật (Device Player ID)
- Tự động lấy định danh phần cứng máy (`SystemInfo.deviceUniqueIdentifier`) làm Player ID duy nhất.
- Mỗi chiếc điện thoại / thiết bị cài APK sẽ đóng vai trò là một người chơi độc lập.
- Tự động lưu thông tin thiết bị (`deviceModel`, `operatingSystem`, `platform`, `lastSeen`) lên Firestore.

### 2. 🔄 Đọc & Ghi dữ liệu 2 chiều (Read & Write Player Stats)
Không chỉ ghi log một chiều, GameHub Analytics cho phép game **đọc lại dữ liệu** của chính người chơi đó:
- **Ghi nhận tiến độ**: Số lần chơi (`started`), số lần thắng (`completed`), số lần thua (`failed`), thời gian hoàn thành kỷ lục (`bestTime`), tổng thời gian chơi (`totalPlayTime`).
- **Tự động tính toán chỉ số**:
  - `Win Rate (%)` = $\frac{\text{completed}}{\text{completed} + \text{failed}} \times 100\%$ (Thước đo độ khó cơ học màn chơi trên các ván có kết quả phân định).
  - `Completion Rate (%)`:
    - **Cấp độ Màn chơi (Level Funnel - chuẩn Lion Studios)**: $\frac{\text{completedPlayers}}{\text{playerCount}} \times 100\%$ (Tỷ lệ người chơi thực sự vượt qua màn).
    - **Cấp độ Lượt chơi (Attempt Conversion)**: $\frac{\text{completed}}{\text{started}} \times 100\%$ (Tỷ lệ hoàn thành từ lúc bấm Bắt đầu).
- **Cơ chế cập nhật nguyên tử (Atomic Increments)**: Dữ liệu được cập nhật an toàn trên Firestore, tránh race condition khi mạng gián đoạn.
- **Bộ nhớ đệm thông minh (Local Cache)**: Dữ liệu sau khi tải sẽ được lưu tạm tại client, giúp truy xuất tức thì mà không tốn request mạng liên tục.

### 3. 🎯 Bộ API theo dõi Gameplay chuẩn hóa
Cung cấp sẵn các hàm tiện ích gọi một dòng code cho toàn bộ vòng lặp gameplay:
- Bắt đầu màn chơi (`LogStartMission`)
- Hoàn thành màn chơi (`LogCompleteMission`) kèm thời gian chơi thực tế
- Thất bại (`LogFailMission`) kèm nguyên nhân hoặc thời gian sống sót
- Chơi lại (`LogRetryMission`), Thoát màn giữa chừng (`LogQuitMission`)
- Ghi nhận sự kiện tùy ý (`LogCustomEvent`): Hỗ trợ truyền Dictionary tham số tùy biến (tiêu diệt Boss, nhặt Item, mở hòm, điểm số...).

### 4. 📴 Hàng đợi ngoại tuyến & Tự động gửi lại (Offline Event Queue)
- Khi máy test mất kết nối Internet, các sự kiện gameplay không bị mất mà được lưu vào hàng đợi nội bộ.
- Tự động đồng bộ và gửi dồn lên Firestore ngay khi có mạng trở lại.

### 5. 🎛️ Bộ công cụ tích hợp sẵn trong Unity Editor
- **Config Inspector Trực Quan**: Cấu hình `API Key`, `Project ID`, `Game ID` trực tiếp trên ScriptableObject, đi kèm nút bấm **"🔗 Test Connection"** để kiểm tra kết nối ngay lập tức.
- **Analytics Dashboard Window**: Cửa sổ thống kê tích hợp sẵn trong Unity (`GameHub > Analytics > 📊 Open Dashboard`), giúp đội ngũ phát triển xem trực tiếp số lượt chơi, tỷ lệ win, danh sách sự kiện gần nhất mà không cần mở trình duyệt web.
- **Package Exporter Tool**: Menu `GameHub > Analytics > 📦 Export .unitypackage` giúp đóng gói package chỉ với 1 click để chia sẻ cho các dự án game khác trong studio.

---

## 🗄️ Cấu Trúc Dữ Liệu Trên Firestore

Dữ liệu được tổ chức khoa học thành 2 nhánh chính:

```
firestore-database/
│
├── 📂 players/
│   └── 📄 {devicePlayerId}/                  ← Mỗi thiết bị test là 1 document
│       ├── platform: "Android"
│       ├── deviceModel: "Samsung Galaxy S23"
│       ├── lastSeen: 2026-10-03T10:30:00Z
│       │
│       └── 📂 missions/
│           └── 📄 {missionId}/               ← Thống kê tích lũy từng màn chơi
│               ├── started: 10               ← Số lần bắt đầu
│               ├── completed: 7              ← Số lần vượt qua
│               ├── failed: 3                 ← Số lần thất bại
│               ├── bestTime: 42.5            ← Thời gian kỷ lục (giây)
│               ├── totalPlayTime: 512.0      ← Tổng thời lượng chơi
│               └── lastPlayed: timestamp
│
└── 📂 analytics/
    └── 📄 {gameId}/
        └── 📂 missions/
            └── 📄 {missionId}/
                └── 📂 events/
                    └── 📄 {autoId}/          ← Dòng thời gian chi tiết từng event
                        ├── eventType: "mission_complete"
                        ├── playerId: "..."
                        ├── timestamp: "..."
                        ├── playTime: 42.5
                        └── metadata: { ... }
```

---

## 🚀 Hướng Dẫn Bắt Đầu Nhanh (3 Bước)

### Bước 1: Chuẩn bị Firebase Firestore
1. Mở [Firebase Console](https://console.firebase.google.com/) và tạo project (hoặc chọn project có sẵn).
2. Vào **Firestore Database** > Chọn **Create Database** (chọn chế độ **Test Mode** để test nội bộ).
3. Vào **Project Settings** > Tab **General** > Copy:
   - **Web API Key**
   - **Project ID**

### Bước 2: Tạo cấu hình trong Unity
1. Mở Unity Editor, chọn menu:  
   **`GameHub > Analytics > ⚙️ Create Analytics Config`**
2. File `AnalyticsConfig.asset` sẽ được tự động tạo trong thư mục `Assets/Resources/`.
3. Điền thông tin vào Inspector:
   - **API Key**: Dán Web API Key từ Firebase.
   - **Project ID**: Dán Project ID từ Firebase.
   - **Game ID**: Đặt định danh cho game (ví dụ: `space-shooter`, `hero-rpg`).
4. Bấm nút **"🔗 Test Connection"** để kiểm tra (kết quả hiển thị ngay trong console).

---

## 💻 Hướng Dẫn Sử Dụng Code (Code Examples)

### 1. Khởi tạo hệ thống
Chỉ cần gọi một lần khi game khởi động (ví dụ trong hàm `Awake` hoặc `Start` của GameManager):

```csharp
using UnityEngine;
using GameHub.Analytics;

public class GameManager : MonoBehaviour
{
    private void Start()
    {
        // Tự động nạp cấu hình từ Resources/AnalyticsConfig và khởi tạo Player ID theo máy
        AnalyticsManager.Instance.Initialize();
        
        Debug.Log($"Thiết bị hiện tại: {AnalyticsManager.Instance.GetPlayerId()}");
    }
}
```

### 2. Ghi nhận các sự kiện Gameplay
```csharp
// Khi người chơi ấn bắt đầu màn
AnalyticsManager.Instance.LogStartMission("level_01");

// Khi người chơi vượt qua màn chơi
float timeTaken = 65.4f; // số giây chơi
AnalyticsManager.Instance.LogCompleteMission("level_01", playTime: timeTaken);

// Khi người chơi thất bại
AnalyticsManager.Instance.LogFailMission("level_01", playTime: 32.0f);

// Khi người chơi nhấn thử lại hoặc bỏ cuộc
AnalyticsManager.Instance.LogRetryMission("level_01");
AnalyticsManager.Instance.LogQuitMission("level_01", playTime: 15.2f);
```
## 🧰 Danh Mục Editor Tools Có Sẵn

| Menu Trong Unity | Chức Năng |
|-------------------|-----------|
| **`GameHub > Analytics > 📊 Open Dashboard`** | Mở bảng điều khiển theo dõi stats trực tiếp trong Editor |
| **`GameHub > Analytics > ⚙️ Create Analytics Config`** | Tạo nhanh file cấu hình ScriptableObject trong thư mục Resources |
| **`GameHub > Analytics > 📦 Export .unitypackage`** | Tự động xuất package thành file `.unitypackage` để chia sẻ cho các dự án khác |
| **`GameHub > Analytics > ℹ️ About`** | Xem thông tin phiên bản, bản quyền và tác giả |

---

## 📱 Khả Năng Tương Thích & Nền Tảng

- **Phiên bản Unity hỗ trợ**: Unity 2021.3 LTS, Unity 2022.3 LTS, Unity 6000.x trở lên.
- **Nền tảng mục tiêu (Build Target)**:
  - 🤖 **Android**: Xuất file APK/AAB chạy mượt mà trên mọi thiết bị Android, tự động lấy Android ID / Hardware ID.
  - 🍎 **iOS**: Hoàn toàn tương thích không lo cấu hình CocoaPods / Podfile.
  - 💻 **Windows / macOS Standalone**: Chạy trực tiếp trên bản build máy tính.
  - 🌐 **WebGL**: Hỗ trợ tốt nhờ nền tảng `UnityWebRequest`.

---

## 📋 Lộ Trình Phát Triển Sản Phẩm (Feature Roadmap)

- [x] **Core REST Client**: Đọc & ghi Firestore trực tiếp qua REST API (không cần Firebase SDK).
- [x] **Per-Device Player Tracking**: Định danh theo ID máy, quản lý hồ sơ và lịch sử chơi riêng biệt.
- [x] **Gameplay Stats & Calculations**: Tự động tính toán Win Rate, Completion Rate, Best Time.
- [x] **Atomic Increments**: Cập nhật chỉ số an toàn, chống sai lệch dữ liệu.
- [x] **Unity Editor Suite**: Config Inspector có nút Test kết nối, Cửa sổ Dashboard xem dữ liệu.
- [x] **Package Exporter**: Xuất `.unitypackage` một chạm.
- [ ] **Offline Storage Persistence**: Lưu hàng đợi sự kiện vào PlayerPrefs / disk khi tắt game đột ngột.
- [ ] **Real-time Editor Charting**: Biểu đồ trực quan hóa số liệu ngay trong Unity Dashboard.
- [ ] **Web Dashboard**: Bảng điều khiển web độc lập cho Game Designer & Tester.

---

## 📄 Bản Quyền

Dự án phát triển nội bộ cho đội ngũ phát triển game GameHub.  
Phát hành theo giấy phép **MIT License**.
