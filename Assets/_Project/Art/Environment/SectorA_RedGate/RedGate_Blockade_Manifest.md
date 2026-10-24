# Sector A — RED Gate Collapsed Industrial Blockade

**Author:** Mara Kess | **Date:** 25 Oct 2026  
**Unity export:** Y-up, meters, per-piece FBX (not fused). No scene placement, no physics.

## Samir stacking (PROLOGUE drop zone)

- Opening at crate line **z = 3.75**: **5.0 m** wide (±2.5 m from center), floor **~y = 0.2**, header **DropZone_Ceiling_North y = 6**.
- Stack from floor: **2× containers** (~2.31 m tall each) plus **3× I-beams** (4.5–4.6 m span) and **2× twisted girders** (4.2–4.4 m) layered diagonally fills **~5.8–6.0 m** to the header with **no crouch gap** when beams bridge the 5 m mouth.
- **8 pipes** + **4 crushed cardboard** as ground scatter around the pile base.

## FBX paths & bounds (m, local AABB)

| Piece | Path | Size (X × Y × Z) |
|-------|------|------------------|
| IBeam_01 | `RedGate_IBeam_01.fbx` | 4.60 × 0.22 × 0.32 |
| IBeam_02 | `RedGate_IBeam_02.fbx` | 4.59 × 0.78 × 0.32 |
| IBeam_03 | `RedGate_IBeam_03.fbx` | 4.52 × 1.33 × 0.32 |
| Girder_01 | `RedGate_Girder_01.fbx` | 4.40 × 0.66 × 0.45 |
| Girder_02 | `RedGate_Girder_02.fbx` | 4.20 × 0.57 × 0.45 |
| Container_01 | `RedGate_Container_01.fbx` | 1.91 × 2.11 × 2.31 |
| Container_02 | `RedGate_Container_02.fbx` | 1.86 × 2.06 × 2.26 |
| Pipe_01–08 | `RedGate_Pipe_01.fbx` … `08` | 0.76–3.16 (longest axis varies by rest rotation) |
| Cardboard_01–04 | `RedGate_Cardboard_01.fbx` … `04` | ~0.84–0.89 × 0.84–0.88 × 0.16–0.19 |

**Materials (Blender Principled):** `M_RustSteel`, `M_ScrapMetal`, `M_ContainerPaint`, `M_PipeRust`, `M_CrushedCardboard` — embedded in FBX; Jordan/Elias can swap to URP instances later.

**Source:** `SectorA_RedGate_Blockade.blend` (same folder).
