## 📋 Pull Request Summary

### 🔗 Liên kết
- Fixes #<!-- issue number -->
- Related to #<!-- issue number -->

---

### 📌 Loại thay đổi
<!-- Đánh dấu [x] vào ô phù hợp -->

- [ ] 🐛 **Bug Fix** — Sửa lỗi (non-breaking)
- [ ] ✨ **Feature** — Tính năng mới (non-breaking)
- [ ] 💥 **Breaking Change** — Thay đổi phá vỡ backward compatibility
- [ ] 🎨 **Refactor** — Cải thiện code, không thay đổi chức năng
- [ ] 📝 **Documentation** — Chỉ cập nhật docs
- [ ] ⚡ **Performance** — Cải thiện hiệu năng
- [ ] 🔒 **Security** — Vá lỗ hổng bảo mật
- [ ] 🧪 **Test** — Thêm/sửa tests
- [ ] 🔧 **Config** — Thay đổi cấu hình (CI, build, ...)

---

### 📝 Mô tả thay đổi
<!-- Mô tả rõ ràng những gì đã thay đổi và lý do tại sao -->

**Vấn đề:**
<!-- Vấn đề gì đang được giải quyết? -->

**Giải pháp:**
<!-- Giải thích cách tiếp cận và quyết định thiết kế -->

**Thay đổi chính:**
- 
- 
- 

---

### 🧪 Cách test

**Môi trường test:**
- Unity Version: 
- Platform: 
- Device: 

**Các bước test:**
1. 
2. 
3. 

**Kết quả mong đợi:**
<!-- Mô tả kết quả đúng -->

---

### 📸 Screenshots / Videos
<!-- Thêm screenshots hoặc video nếu có thay đổi UI -->

| Before | After |
|--------|-------|
|        |       |

---

### ✅ Checklist

**Code Quality:**
- [ ] Code tuân theo quy ước namespace `GameHub.Analytics`
- [ ] Không có hardcoded API keys, credentials
- [ ] Debug.Log có prefix `[Analytics]` hoặc `[PlayerData]`
- [ ] Có summary/comment cho public methods
- [ ] Không có `Debug.Log` dư thừa

**Compatibility:**
- [ ] Test trên Unity Editor (Windows/Mac)
- [ ] Test trên Android (nếu liên quan mobile)
- [ ] Không break API hiện tại (hoặc đã document breaking change)

**Documentation:**
- [ ] Cập nhật `CHANGELOG.md`
- [ ] Cập nhật `README.md` (nếu cần)
- [ ] Cập nhật `DEVELOPMENT_ROADMAP.md` (nếu hoàn thành task)

**Firebase:**
- [ ] Đã test kết nối Firestore thành công
- [ ] Không có credentials bị expose
- [ ] Security rules phù hợp

---

### 📌 Ghi chú cho Reviewer
<!-- Bất kỳ điều gì reviewer cần chú ý đặc biệt? -->

---

> **Branch naming convention:**
> - Feature: `feature/short-description`
> - Bug fix: `fix/short-description`  
> - Hotfix: `hotfix/short-description`
> - Release: `release/v1.x.x`
