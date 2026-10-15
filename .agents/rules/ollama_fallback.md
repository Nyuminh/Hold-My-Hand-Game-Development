---
trigger: manual
---

# Rule: Khi token hết, tự chuyển sang Ollama

## 1️⃣ Thay đổi cài đặt mô hình
/setting set modelProvider custom
/setting set model ollama
/setting set fallbackModel none   # không cần fallback nữa

## 2️⃣ Thông báo người dùng
/notify "✅ Đã chuyển sang Ollama (model: qwen3.5:4b) vì token của Cloud đã hết."