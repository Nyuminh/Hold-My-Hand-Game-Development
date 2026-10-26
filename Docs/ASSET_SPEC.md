# 🎨 Hold My Hand — Asset Specification & 3D Conventions

## 1. Hệ Đơn Vị & Tọa Độ
- **Phần mềm sử dụng**: Blender 4.x / Unity 6.
- **Tỷ lệ (Scale)**: 1 Unit trong Blender = 1 Meter trong Unity. Tỷ lệ xuất phải luôn là `(1.0, 1.0, 1.0)`.
- **Hệ trục tọa độ (Coordinate Orientation)**:
  - Export từ Blender sang Unity FBX: Forward: `-Z`, Up: `Y`.
  - Kiểm tra `Apply Transform` để đảm bảo Rotation trong Unity luôn là `(0, 0, 0)`.
- **Pivot Point (Gốc tọa độ)**:
  - Nhân vật: Nằm chính giữa 2 bàn chân trên mặt sàn `Y=0`.
  - Đồ vật tương tác (Props/Crates): Nằm tại đáy hoặc trọng tâm vật lý.

---

## 2. Quy Chuẩn Đa Giác (Polycount) & Vật Liệu
- **Nhân vật chính (Dad & Child)**:
  - Dad Player: 8,000 – 12,000 tris.
  - Child Player: 6,000 – 9,000 tris.
- **Vật thể môi trường (Environment Props)**:
  - Small Props: 200 – 800 tris.
  - Interactive Puzzle Blocks: 500 – 1,500 tris.
- **Vật liệu (Materials & Textures)**:
  - Shader: Universal Render Pipeline (URP) Lit hoặc Simple Lit.
  - Texture Maps: BaseColor, Normal (OpenGL), Metallic-Roughness (Packed).
  - Kích thước Texture: Tối đa 2048x2048 cho nhân vật, 1024x1024 cho props.

---

## 3. Quy Chuẩn Armature & Animation
- **Armature Hierarchy**: Chuẩn Humanoid Rig (Root -> Hips -> Spine -> Chest -> Neck -> Head...).
- **Quy tắc đặt tên Animation Clips**:
  - `Dad_Idle`, `Dad_Walk`, `Dad_Run`, `Dad_HoldHand_Lead`, `Dad_Pull`
  - `Child_Idle`, `Child_Walk`, `Child_Run`, `Child_HoldHand_Follow`, `Child_Crawl`
- **Root Motion**: Được tách biệt rõ ràng giữa in-place animation và root-motion data.
