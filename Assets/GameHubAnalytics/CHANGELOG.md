# Changelog — GameHub Analytics

All notable changes to this package will be documented in this file.  
Format: [Semantic Versioning](https://semver.org/)

---

## [1.0.0] — 2026-10-03

### Added
- `AnalyticsManager` — Singleton chính với đầy đủ API
- `FirestoreClient` — Kết nối Firebase Firestore qua REST API
- `EventQueue` — Offline queue với PlayerPrefs persistence
- `SessionManager` — Quản lý Session ID
- `PlatformHelper` — Auto-detect platform
- `AnalyticsConfig` — ScriptableObject cấu hình
- `MissionEvent` — Data model với Firestore JSON serialization
- **Editor:** `AnalyticsConfigEditor` — Custom Inspector
- **Editor:** `AnalyticsDashboardWindow` — Stats dashboard
- **Editor:** `PackageExporter` — Export .unitypackage tool
- **Samples:** `SampleUsage.cs` — Demo tích hợp

### Event Types
- `start_mission`
- `complete_mission`
- `fail_mission`
- `retry_mission`
- `quit_mission`
- `custom` (mở rộng tùy ý)

---

## [Unreleased] — Phase 2 (Tuần 2–3)

### Planned
- Firestore fetch để đọc aggregate stats
- Dashboard hiển thị data thật từ Firebase
- Batching: gửi nhiều event cùng một request
- Improved retry strategy với exponential backoff

---

## [Unreleased] — Phase 3 (Tương lai)

### Planned
- Anonymous Authentication
- Export CSV
- Web Dashboard (Next.js)
- A/B Testing support
