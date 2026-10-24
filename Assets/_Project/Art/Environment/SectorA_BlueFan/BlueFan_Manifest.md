# Sector A — Blue Tutorial Industrial Fan

**Author:** Mara Kess | **PROLOGUE** @ z=33  
**Pivot:** Frame origin = bottom-center (place at floor y=0.2). Rotor origin = hub center (parent to rig spin node; **spin axis = local +Z / corridor**, Blender **+Y**).

## Opening (confirmed)

| | Value |
|--|-------|
| Wall span | x ±2.0 m (4.0 m clear) |
| Floor / ceiling | y 0.2 → 3.53 m (3.33 m) |
| Walk portal (stopped only) | x ±0.55 m, y 0.2 → 2.45 m (1.1 m × 2.25 m) |

## Pieces

| FBX | Bounds (X × Y × Z m) | Notes |
|-----|----------------------|--------|
| `BlueFan_Frame_Static.fbx` | 4.00 × 0.42 × 3.33 | Posts, jambs, lintel wings, open grille (not solid disc) |
| `BlueFan_Rotor_3Blade.fbx` | 4.13 × 0.56 × 2.84 | Hub + 3 arc ring segments + 3 blades at **90° / 210° / 330°** in fan plane (rest: lintel / left jamb / right jamb); sweeps portal on **+Z** spin |

**Materials:** `M_BlueFan_Frame`, `M_BlueFan_Rotor` (rusted Principled).  
**Source:** `SectorA_BlueFan.blend` (same folder). No scene placement, no physics.
