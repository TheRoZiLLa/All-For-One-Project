# RECORDED — RGB Spectrum Gun & Cylinder Ammo System Guide

เอกสารคู่มือสำหรับทีมพัฒนาโปรเจกต์ **RECORDED** เพื่อใช้งานระบบปืน **Spectrum Gun**, ระบบสี **RGB Object Color State**, **6-Chamber Cylinder Ammo HUD (6 o'clock Indicator)** และการเชื่อมโยงเข้ากับระบบ **Puzzle / Gameplay Skills**

---

## 1. โครงสร้างระบบและไฟล์สำคัญ (`Assets/_Project/Scripts/`)

| หมวดหมู่ | ไฟล์ | หน้าที่หลัก |
|---|---|---|
| **Color System** | `ColorSystem/ObjectColorState.cs` | Bitwise `[Flags]` enum (`None=0`, `Red=1`, `Green=2`, `Blue=4`) รองรับการผสมสี 8 สถานะ |
| **Color System** | `ColorSystem/IColorReceivable.cs` | Interface สำหรับวัตถุที่รับสถานะสี (`ApplyColor`, `ResetColors`, `CurrentColorState`, `OnColorStateChanged`) |
| **Color System** | `ColorSystem/IColorShotImpact.cs` | Interface แยกสำหรับรับแรงกระแทก/เอฟเฟกต์ทันทีที่ถูกยิง (`OnColorShotHit`) โดยไม่รีเซ็ตเวลาสี 5 วินาที |
| **Color System** | `ColorSystem/RGBColorTarget.cs` | คอมโพเนนต์จัดการสถานะสี 8 สถานะ, ตัวจับเวลา 5 วินาทีแยกแต่ละแม่สี, กฎยิงซ้ำไม่รีเซ็ตเวลา และอัปเดตสีผ่าน `MaterialPropertyBlock` (รองรับทั้ง Single และ Child Renderers) |
| **Color System / Puzzle** | `ColorSystem/ColorPuzzleCondition.cs` | คอมโพเนนต์เชื่อมระบบสีเข้ากับ Puzzle ของเพื่อนผ่าน Inspector (`UnityEvent` และ C# events) โดยไม่ต้องแก้โค้ดปืน |
| **Color System / Skills** | `ColorSystem/SpectrumSkillResponder.cs` | คอมโพเนนต์ Provisional Skills ประจำแม่สี: **RED (Impulse)**, **GREEN (Bind)**, **CYAN/Blue (Stasis)** พร้อมคืนค่าฟิสิกส์อัตโนมัติเมื่อสีหมดเวลา |
| **Weapons** | `Weapons/RaycastWeapon.cs` | ยิง Raycast จากกึ่งกลางกล้องผู้เล่น (`ViewportPointToRay(0.5, 0.5)`) |
| **Weapons** | `Weapons/SpectrumGunController.cs` | ควบคุมการยิงปืน Spectrum Gun, ตรวจสอบกระสุนจากช่องยิง 6 นาฬิกา, แปลง `AmmoType` เป็น `ObjectColorState`, และสั่งหมุนโม่หลังยิง |
| **Weapons** | `Weapons/WeaponImpactFeedback.cs` | แสดงเส้น Tracer, แสงไฟปากกระบอกปืน (Muzzle Light), และเอฟเฟกต์จุดตกกระทบตามสีกระสุน (ไม่มีแสงแฟลชสีขาวทับสีเป้าหมาย) |
| **Ammo & HUD** | `UI/Ammo/AmmoType.cs` | ประเภทกระสุน (`Empty`, `Red`, `Green`, `Cyan`) |
| **Ammo & HUD** | `UI/Ammo/CylinderAmmoController.cs` | จัดการสถานะกระสุนทั้ง 6 ช่อง (`Chamber_0` ถึง `Chamber_5`) ช่องยิงปัจจุบัน การใช้กระสุน และการรีโหลดทดสอบ |
| **Ammo & HUD** | `UI/Ammo/CylinderRotationController.cs` | จัดการการหมุนโม่ (`Q` หมุนซ้าย, `E` หมุนขวา, และหมุนอัตโนมัติหลังยิง) พร้อม Animation แบบ Smooth |
| **Ammo & HUD** | `UI/Ammo/CylinderAmmoHUD.cs` | สร้างและอัปเดต UI ลูกโม่ 6 ช่องที่มุมซ้ายล่าง โดย **Firing Indicator อยู่ที่ตำแหน่ง 6 นาฬิกา (ด้านล่าง)** |
| **Ammo & HUD** | `UI/Ammo/AmmoTestInput.cs` | แยกปุ่มทดสอบ `R` (สุ่มเติมกระสุนเต็ม 6 ช่อง) ไว้สำหรับการทดสอบโดยเฉพาะ |

---

## 2. กฎการผสมสีและเวลา (RGB Color Rules)

1. **สถานะเริ่มต้น:** วัตถุที่มี `RGBColorTarget` เริ่มต้นที่ `ObjectColorState.None` แสดงสี **Neutral Grey (`#7F7F7F`)**
2. **การแมปสีกระสุนเข้ากับแม่สี:**
   - `AmmoType.Red` -> `ObjectColorState.Red`
   - `AmmoType.Green` -> `ObjectColorState.Green`
   - `AmmoType.Cyan` -> `ObjectColorState.Blue` (แม่สีน้ำเงินในระบบผสมสี RGB)
3. **ตารางการผสมสี 8 สถานะ:**
   - `None` -> Neutral Grey
   - `Red` -> Red
   - `Green` -> Green
   - `Blue` -> Blue
   - `Red | Green` -> **Yellow**
   - `Red | Blue` -> **Magenta**
   - `Green | Blue` -> **Cyan**
   - `Red | Green | Blue` -> **White**
4. **ตัวจับเวลาแยกอิสระ 5.0 วินาที & กฎยิงซ้ำ (Duplicate-Hit Rule):**
   - แม่สีแต่ละตัว (`Red`, `Green`, `Blue`) มีตัวจับเวลา 5.0 วินาทีแยกกันเป็นอิสระ
   - **หากยิงสีที่มีอยู่แล้วซ้ำขณะที่สีนั้นยังไม่หมดเวลา ระบบจะไม่รีเซ็ตและไม่ต่อเวลาของสีนั้น**
   - เมื่อสีใดสีหนึ่งหมดเวลา เฉพาะ Flag ของสีนั้นจะถูกลบออก และคำนวณสีที่แสดงผลใหม่จาก Flag ที่เหลือทันที

---

## 3. วิธีตั้งค่าวัตถุเป้าหมาย (Target Setup)

1. เลือก GameObject ที่มี `Collider` และ `MeshRenderer` (รองรับทั้ง Renderer บนตัวแม่และ Child Renderers)
2. เพิ่มคอมโพเนนต์ **`RGBColorTarget`**
3. *(ทางเลือก)* หากต้องการให้วัตถุตอบสนองด้วยฟิสิกส์เมื่อถูกยิงสีต่าง ๆ ให้เพิ่ม `Rigidbody` และคอมโพเนนต์ **`SpectrumSkillResponder`**:
   - **RED — Impulse:** ผลักวัตถุตามทิศทางที่ยิงทันที (ทำงานทุกครั้งที่ยิงโดน แม้สีแดงจะติดอยู่แล้ว)
   - **GREEN — Bind:** ยึด/ดึงวัตถุไว้กับตำแหน่ง Anchor ขณะที่ติดสถานะสีเขียว
   - **CYAN (Blue Flag) — Stasis:** ชะลอความเร็ว ปิดแรงโน้มถ่วงชั่วคราว และหน่วงการเคลื่อนที่ขณะที่ติดสถานะสีน้ำเงิน/ฟ้า และคืนค่าฟิสิกส์เดิมทันทีเมื่อหมดเวลา

---

## 4. วิธีเชื่อมเข้ากับระบบ Puzzle ของทีม (Puzzle Integration)

มี 2 วิธีหลักในการเชื่อมกับระบบประตู/กลไก/พัซเซิล โดย**ไม่ต้องแก้ไขโค้ดปืน**:

### วิธีที่ 1: ใช้คอมโพเนนต์ `ColorPuzzleCondition` (ผ่าน Inspector / UnityEvent)
1. แนบ `ColorPuzzleCondition` ลงบน GameObject ของ Puzzle หรือประตู
2. ลากเป้าหมาย `RGBColorTarget` เข้าใส่รายการ **Watched Targets**
3. เลือกสีที่ต้องการใน **Required State** (เช่น `Red | Green` สำหรับสีเหลือง)
   - ตั้ง `Exact Match = true` หากต้องการให้ตรงสีนั้นเป๊ะ (ห้ามมีสีอื่นปน)
   - ตั้ง `Exact Match = false` หากต้องการแค่มีสีนั้นรวมอยู่ด้วย
4. ผูกฟังก์ชันเปิด/ปิดประตูเข้ากับ **`onConditionMet`** และ **`onConditionLost`** ใน Inspector ได้ทันที

### วิธีที่ 2: Subscribe Event ในโค้ด C# โดยตรง
```csharp
using UnityEngine;
using Recorded.ColorSystem;

public class MyPuzzleDoor : MonoBehaviour
{
    [SerializeField] private RGBColorTarget targetSwitch;

    private void OnEnable()
    {
        if (targetSwitch != null)
            targetSwitch.OnColorStateChanged += OnSwitchColorChanged;
    }

    private void OnDisable()
    {
        if (targetSwitch != null)
            targetSwitch.OnColorStateChanged -= OnSwitchColorChanged;
    }

    private void OnSwitchColorChanged(ObjectColorState currentState)
    {
        // ตัวอย่าง: เปิดประตูเมื่อเป้าหมายผสมเป็นสีเหลือง (Red + Green)
        bool isYellow = currentState == (ObjectColorState.Red | ObjectColorState.Green);
        // สั่งทำงานกลไก...
    }
}
```

---

## 5. ระบบโม่กระสุน 6 นัด และปุ่มควบคุม (Cylinder UI & Controls)

- **มุมมองโม่:** มุมมองจากด้านหลังปืนของผู้เล่น (Rear-view 6-chamber cylinder) แสดงที่มุมซ้ายล่างของหน้าจอ
- **ตำแหน่งช่องยิง (Active Firing Chamber):** **อยู่ที่ตำแหน่ง 6 นาฬิกา (ด้านล่างสุดของโม่)** เสมอ โดยมีเข็มชี้ `Stationary_6oclock_FiringIndicator` ชี้ขึ้นที่ช่องล่างสุด
- **ปุ่มควบคุม:**
  - **`Left Mouse Button`:** ยิงกระสุนจากช่องที่ตำแหน่ง **6 นาฬิกา**
    - หากมีกระสุน: ยิงออกไป ช่องนั้นกลายเป็น `Empty` (สีเทาเข้ม) และโม่หมุนเลื่อนช่องถัดไปมาแทนที่ตำแหน่ง 6 นาฬิกาอัตโนมัติ
    - หากช่องที่ 6 นาฬิกาว่าง (`Empty`): **Dry Fire** (ยิงไม่ออก และโม่ไม่หมุนเลื่อนอัตโนมัติ)
  - **`Q`:** หมุนโม่ไปทางซ้าย 1 ช่อง (`60°`)
  - **`E`:** หมุนโม่ไปทางขวา 1 ช่อง (`60°`)
  - **`R` (Test Input ใน `AmmoTestInput.cs`):** สุ่มบรรจุกระสุน (`Red`, `Green`, `Cyan`) เต็มทั้ง 6 ช่องสำหรับการทดสอบ

---

## 6. การรันชุดทดสอบอัตโนมัติ (Automated Verification Suite)

ใน Unity Editor สามารถรันชุดทดสอบอัตโนมัติ 24 เคส ได้จากเมนูด้านบน:
- **`RECORDED -> Verify RGB Color System (Run All Tests)`**
- หรือกดปุ่มตั้งค่าฉากทดสอบอัตโนมัติ: **`RECORDED -> Setup Dev GameTest Scene`**