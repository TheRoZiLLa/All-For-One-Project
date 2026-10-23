using System.Text;
using UnityEditor;
using UnityEngine;
using Recorded.ColorSystem;
using Recorded.Weapons;
using Recorded.Weapons.Ammo;

namespace Recorded.Testing.Editor
{
    /// <summary>
    /// Automated Editor verification suite for the RECORDED Spectrum Gun Overhaul:
    /// - 8-state RGB additive color mixing & independent 5-second timers
    /// - 6 o'clock Cylinder Ammo HUD, rotation sync, and dry-fire rules
    /// - Real physical weapon skills (RED Impulse, GREEN Bind SpringJoint & independent timer, CYAN Stasis & independent timer)
    /// - Exact barrel-tip MuzzlePoint resolution and close-range viewmodel placement
    /// - ColorPuzzleCondition integration
    /// </summary>
    public static class RGBColorSystemVerificationTests
    {
        [MenuItem("RECORDED/Run Automated RGB & Cylinder Verification (28 Cases)", false, 10)]
        public static void RunAllTests()
        {
            StringBuilder results = new StringBuilder();
            results.AppendLine("<b>[RECORDED QA] Running Automated RGB Color, Weapon Skills, Muzzle & 6-O'Clock Cylinder Verification Suite (28 Cases):</b>");

            int passedCount = 0;
            int totalCount = 28;

            GameObject testObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            testObj.name = "Test_RGBTarget_Temp";
            RGBColorTarget target = testObj.AddComponent<RGBColorTarget>();

            GameObject secondObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            secondObj.name = "Test_RGBTarget_Second";
            RGBColorTarget secondTarget = secondObj.AddComponent<RGBColorTarget>();

            GameObject plainObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plainObj.name = "Test_PlainObject_NoColor";

            GameObject ammoSysObj = new GameObject("Test_CylinderAmmo_Temp");
            CylinderAmmoController ammoCtrl = ammoSysObj.AddComponent<CylinderAmmoController>();
            CylinderRotationController rotCtrl = ammoSysObj.AddComponent<CylinderRotationController>();
            CylinderAmmoHUD hud = ammoSysObj.AddComponent<CylinderAmmoHUD>();

            GameObject gunTestObj = new GameObject("Test_SpectrumGun_Temp");
            GameObject muzzleChild = new GameObject("MuzzlePoint");
            muzzleChild.transform.SetParent(gunTestObj.transform, false);
            muzzleChild.transform.localPosition = RevolverWeaponBuilder.DefaultMuzzleLocalPosition;
            RaycastWeapon raycastWep = gunTestObj.AddComponent<RaycastWeapon>();

            try
            {
                // Case 1: Fresh target starts grey (#7F7F7F)
                bool pass1 = (target.CurrentColorState == ObjectColorState.None) &&
                             (RGBColorTarget.GetColorForState(target.CurrentColorState) == RGBColorTarget.ColorNeutralGrey);
                LogCase(1, "Fresh target starts Neutral Grey (#7F7F7F)", pass1, ref passedCount, results);

                // Case 2: Red changes it to Red
                target.ResetColors();
                target.ApplyColor(ObjectColorState.Red);
                bool pass2 = (target.CurrentColorState == ObjectColorState.Red) &&
                             (RGBColorTarget.GetColorForState(target.CurrentColorState) == RGBColorTarget.ColorRed);
                LogCase(2, "Red -> Red", pass2, ref passedCount, results);

                // Case 3: Green changes it to Green
                target.CacheRenderers();
                target.ResetColors();
                target.ApplyColor(ObjectColorState.Green);
                bool pass3 = (target.CurrentColorState == ObjectColorState.Green) &&
                             (RGBColorTarget.GetColorForState(target.CurrentColorState) == RGBColorTarget.ColorGreen);
                LogCase(3, "Green -> Green", pass3, ref passedCount, results);

                // Case 4: Blue changes it to Blue
                target.ResetColors();
                target.ApplyColor(ObjectColorState.Blue);
                bool pass4 = (target.CurrentColorState == ObjectColorState.Blue) &&
                             (RGBColorTarget.GetColorForState(target.CurrentColorState) == RGBColorTarget.ColorBlue);
                LogCase(4, "Blue -> Blue", pass4, ref passedCount, results);

                // Case 5: Red + Green -> Yellow
                target.ResetColors();
                target.ApplyColor(ObjectColorState.Red);
                target.ApplyColor(ObjectColorState.Green);
                bool pass5 = (target.CurrentColorState == (ObjectColorState.Red | ObjectColorState.Green)) &&
                             (RGBColorTarget.GetColorForState(target.CurrentColorState) == RGBColorTarget.ColorYellow);
                LogCase(5, "Red + Green -> Yellow", pass5, ref passedCount, results);

                // Case 6: Red + Blue -> Magenta
                target.ResetColors();
                target.ApplyColor(ObjectColorState.Red);
                target.ApplyColor(ObjectColorState.Blue);
                bool pass6 = (target.CurrentColorState == (ObjectColorState.Red | ObjectColorState.Blue)) &&
                             (RGBColorTarget.GetColorForState(target.CurrentColorState) == RGBColorTarget.ColorMagenta);
                LogCase(6, "Red + Blue -> Magenta", pass6, ref passedCount, results);

                // Case 7: Green + Blue -> Cyan
                target.CacheRenderers();
                target.ResetColors();
                target.ApplyColor(ObjectColorState.Green);
                target.ApplyColor(ObjectColorState.Blue);
                bool pass7 = (target.CurrentColorState == (ObjectColorState.Green | ObjectColorState.Blue)) &&
                             (RGBColorTarget.GetColorForState(target.CurrentColorState) == RGBColorTarget.ColorCyan);
                LogCase(7, "Green + Blue -> Cyan", pass7, ref passedCount, results);

                // Case 8: Red + Green + Blue -> White
                target.ResetColors();
                target.ApplyColor(ObjectColorState.Red);
                target.ApplyColor(ObjectColorState.Green);
                target.ApplyColor(ObjectColorState.Blue);
                bool pass8 = (target.CurrentColorState == (ObjectColorState.Red | ObjectColorState.Green | ObjectColorState.Blue)) &&
                             (RGBColorTarget.GetColorForState(target.CurrentColorState) == RGBColorTarget.ColorWhite);
                LogCase(8, "Red + Green + Blue -> White", pass8, ref passedCount, results);

                // Case 9: Duplicate hit does not reset or extend timer
                target.ResetColors();
                target.ApplyColor(ObjectColorState.Red);
                target.SetExpirationsForTesting(105f, -1f, -1f);
                target.ApplyColor(ObjectColorState.Red); // Duplicate hit while Red active
                bool pass9 = Mathf.Approximately(target.GetRawExpireTime(ObjectColorState.Red), 105f);
                LogCase(9, "Duplicate hit preserves original expiration (does not reset/extend)", pass9, ref passedCount, results);

                // Case 10: Second color starts separate timer without changing first color's expiration
                target.ResetColors();
                target.ApplyColor(ObjectColorState.Red);
                target.SetExpirationsForTesting(105f, -1f, -1f);
                target.ApplyColor(ObjectColorState.Green);
                float redExpireAfter = target.GetRawExpireTime(ObjectColorState.Red);
                float greenExpire = target.GetRawExpireTime(ObjectColorState.Green);
                bool pass10 = Mathf.Approximately(redExpireAfter, 105f) && greenExpire > 0f;
                LogCase(10, "Second color starts independent timer without altering first color's timer", pass10, ref passedCount, results);

                // Case 11: When one color expires, remaining color combination is displayed correctly
                target.SetExpirationsForTesting(205f, 207f, -1f);
                target.SimulateExpirationCheck(206f);
                bool pass11 = (target.CurrentColorState == ObjectColorState.Green) &&
                              (RGBColorTarget.GetColorForState(target.CurrentColorState) == RGBColorTarget.ColorGreen);
                LogCase(11, "When one color expires, remaining color (Green) displays accurately", pass11, ref passedCount, results);

                // Case 12: When all colors expire, target returns to Neutral Grey
                target.SimulateExpirationCheck(208f);
                bool pass12 = (target.CurrentColorState == ObjectColorState.None) &&
                              (RGBColorTarget.GetColorForState(target.CurrentColorState) == RGBColorTarget.ColorNeutralGrey);
                LogCase(12, "When all colors expire, target returns to Neutral Grey", pass12, ref passedCount, results);

                // Case 13: Applying Red again after its expiration starts a fresh timer
                target.ApplyColor(ObjectColorState.Red);
                bool pass13 = (target.CurrentColorState == ObjectColorState.Red) &&
                              (RGBColorTarget.GetColorForState(target.CurrentColorState) == RGBColorTarget.ColorRed) &&
                              (target.GetRawExpireTime(ObjectColorState.Red) > 0f);
                LogCase(13, "Applying expired color again starts a new 5-second timer", pass13, ref passedCount, results);

                // Case 14: Order independence (G+R == R+G, B+G+R == R+G+B)
                target.CacheRenderers();
                target.ResetColors();
                target.ApplyColor(ObjectColorState.Green);
                target.ApplyColor(ObjectColorState.Red);
                ObjectColorState grState = target.CurrentColorState;
                secondTarget.ResetColors();
                secondTarget.ApplyColor(ObjectColorState.Red);
                secondTarget.ApplyColor(ObjectColorState.Green);
                ObjectColorState rgState = secondTarget.CurrentColorState;
                bool pass14 = (grState == rgState) && (grState == (ObjectColorState.Red | ObjectColorState.Green));
                LogCase(14, "Order-independent color mixing (G+R equals R+G)", pass14, ref passedCount, results);

                // Case 15: Plain object without IColorReceivable does not throw
                IColorReceivable nonColorLookup = plainObj.GetComponentInParent<IColorReceivable>();
                bool pass15 = (nonColorLookup == null);
                LogCase(15, "Target without color component is safely ignored without NullReference", pass15, ref passedCount, results);

                // Case 16: Multiple targets manage timers independently
                target.ResetColors();
                secondTarget.ResetColors();
                target.ApplyColor(ObjectColorState.Red);
                secondTarget.ApplyColor(ObjectColorState.Blue);
                target.SetExpirationsForTesting(305f, -1f, -1f);
                secondTarget.SetExpirationsForTesting(-1f, -1f, 309f);
                target.SimulateExpirationCheck(306f);
                secondTarget.SimulateExpirationCheck(306f);
                bool pass16 = (target.CurrentColorState == ObjectColorState.None) &&
                              (secondTarget.CurrentColorState == ObjectColorState.Blue);
                LogCase(16, "Multiple targets manage color states and timers independently", pass16, ref passedCount, results);

                // Case 17: Multi-renderer child support via MaterialPropertyBlock
                GameObject childMesh = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                childMesh.transform.SetParent(testObj.transform, false);
                target.CacheRenderers();
                target.ResetColors();
                target.ApplyColor(ObjectColorState.Red | ObjectColorState.Blue);
                var childBlock = new MaterialPropertyBlock();
                childMesh.GetComponent<Renderer>().GetPropertyBlock(childBlock);
                bool pass17 = (childBlock.GetColor("_BaseColor") == RGBColorTarget.ColorMagenta);
                LogCase(17, "Multi-renderer hierarchy updates child renderers via MaterialPropertyBlock", pass17, ref passedCount, results);

                // Case 18: Cylinder 6 o'clock Indicator, Bracket & Chamber 0 bottom alignment
                hud.BuildHUDHierarchy();
                Transform ind6 = ammoSysObj.transform.Find("CylinderAmmo_Canvas/CylinderHUD_Root/Stationary_6oclock_FiringIndicator");
                Transform bracket6 = ammoSysObj.transform.Find("CylinderAmmo_Canvas/CylinderHUD_Root/Stationary_6oclock_ChamberBracket");
                Transform ch0 = ammoSysObj.transform.Find("CylinderAmmo_Canvas/CylinderHUD_Root/Rotating_InnerCylinder/Chamber_0");
                RectTransform ch0Rt = ch0 != null ? ch0.GetComponent<RectTransform>() : null;
                bool pass18 = (ind6 != null) && (bracket6 != null) && (ch0Rt != null) && (ch0Rt.anchoredPosition.y < -10f) && Mathf.Abs(ch0Rt.anchoredPosition.x) < 1f;
                LogCase(18, "Cylinder HUD places stationary firing indicator/bracket and Chamber_0 at 6 o'clock bottom", pass18, ref passedCount, results);

                // Case 19: Cylinder TestReloadRandom fills all 6 chambers with Red/Green/Cyan
                ammoCtrl.TestReloadRandom();
                bool allFilled = true;
                for (int i = 0; i < CylinderAmmoController.CylinderCapacity; i++)
                {
                    if (ammoCtrl.GetChamberAmmo(i) == AmmoType.Empty) allFilled = false;
                }
                LogCase(19, "Test Reload (R) fills all 6 chambers with non-empty R/G/C ammo", allFilled, ref passedCount, results);

                // Case 20: Consuming active round empties chamber; dry fire on empty returns false
                ammoCtrl.SetActiveChamber(0);
                ammoCtrl.LoadChamber(0, AmmoType.Cyan);
                bool consumedOk = ammoCtrl.TryConsumeActiveRound(out AmmoType consumedType);
                bool dryFireBlocked = !ammoCtrl.TryConsumeActiveRound(out _);
                bool pass20 = consumedOk && (consumedType == AmmoType.Cyan) && (ammoCtrl.GetChamberAmmo(0) == AmmoType.Empty) && dryFireBlocked;
                LogCase(20, "Active chamber consumption empties slot and blocks dry-fire consumption", pass20, ref passedCount, results);

                // Case 21: Q / E rotation step direction, immediate active index update & visual angle sync
                ammoCtrl.SetActiveChamber(0);
                rotCtrl.RotateChamberStep(1);  // RotateLeft / AutoAdvance -> immediately updates ActiveChamberIndex to 1 and visual angle to +60
                bool step1Ok = (ammoCtrl.ActiveChamberIndex == 1) && (hud.InnerCylinderRect != null) &&
                               Mathf.Approximately(Mathf.DeltaAngle(0f, hud.InnerCylinderRect.localEulerAngles.z), 60f);
                rotCtrl.RotateChamberStep(-1); // RotateRight -> back to index 0 and visual angle 0
                bool stepBackOk = (ammoCtrl.ActiveChamberIndex == 0) &&
                                  Mathf.Approximately(Mathf.DeltaAngle(0f, hud.InnerCylinderRect.localEulerAngles.z), 0f);
                bool pass21 = step1Ok && stepBackOk;
                LogCase(21, "Cylinder Q/E and AutoAdvance immediately sync ActiveChamberIndex and inner cylinder Z-rotation", pass21, ref passedCount, results);

                // Case 22: ColorPuzzleCondition triggers when watched target matches required color state
                ColorPuzzleCondition puzzleCond = testObj.AddComponent<ColorPuzzleCondition>();
                puzzleCond.SetWatchedTargets(new RGBColorTarget[] { target }, ObjectColorState.Red | ObjectColorState.Green, true);
                target.ResetColors();
                bool beforeMatch = puzzleCond.IsConditionMet;
                target.ApplyColor(ObjectColorState.Red);
                target.ApplyColor(ObjectColorState.Green);
                bool afterMatch = puzzleCond.IsConditionMet;
                target.ResetColors();
                bool afterReset = !puzzleCond.IsConditionMet;
                bool pass22 = (!beforeMatch) && afterMatch && afterReset;
                LogCase(22, "ColorPuzzleCondition activates on required state (Yellow) and deactivates on expire/reset", pass22, ref passedCount, results);

                // Case 23: SpectrumSkillResponder RED Impulse applies velocity to Rigidbody even on duplicate hit
                Rigidbody rb = secondObj.AddComponent<Rigidbody>();
                rb.useGravity = true;
                SpectrumSkillResponder skillResp = secondObj.AddComponent<SpectrumSkillResponder>();
                skillResp.EnsureInitialized();
                secondTarget.ResetColors();
                secondTarget.ApplyColor(ObjectColorState.Red); // Already Red
                RaycastHit dummyHit = new RaycastHit { point = secondObj.transform.position, normal = Vector3.back };
                skillResp.OnColorShotHit(AmmoType.Red, ObjectColorState.Red, dummyHit, Vector3.forward);
                bool pass23 = (skillResp.RedImpulseHitCount >= 1) && (skillResp.LastRedImpulseApplied.sqrMagnitude > 1f);
                LogCase(23, "SpectrumSkillResponder applies RED Impulse on hit while preserving duplicate timer rule", pass23, ref passedCount, results);

                // Case 24: SpectrumSkillResponder GREEN Bind & CYAN (Blue) Stasis state transitions and physics restoration
                rb.linearVelocity = Vector3.forward * 10f;
                secondTarget.ApplyColor(ObjectColorState.Green);
                secondTarget.ApplyColor(ObjectColorState.Blue);
                bool statesActive = skillResp.IsGreenBound && skillResp.IsBlueStasis && !rb.useGravity;
                secondTarget.ResetColors();
                bool statesRestored = !skillResp.IsGreenBound && !skillResp.IsBlueStasis && rb.useGravity;
                bool pass24 = statesActive && statesRestored;
                LogCase(24, "SpectrumSkillResponder activates GREEN Bind & CYAN Stasis and restores physics on reset", pass24, ref passedCount, results);

                // Case 25: GREEN Bind creates real SpringJoint without duplicating joints on repeated hits and cleans up on expire
                skillResp.OnColorShotHit(AmmoType.Green, ObjectColorState.Green, dummyHit, Vector3.forward);
                skillResp.OnColorShotHit(AmmoType.Green, ObjectColorState.Green, dummyHit, Vector3.forward);
                SpringJoint[] jointsDuringBind = secondObj.GetComponents<SpringJoint>();
                bool singleJointCreated = (jointsDuringBind.Length == 1) && (skillResp.ActiveBindJoint != null);
                skillResp.TickSkillTimers(10.0f); // Advance past independent bindDuration
                SpringJoint[] jointsAfterExpire = secondObj.GetComponents<SpringJoint>();
                bool pass25 = singleJointCreated && (!skillResp.IsGreenBound) && (jointsAfterExpire.Length == 0);
                LogCase(25, "GREEN Bind creates a single SpringJoint tether (no duplicates on re-hit) and cleans up on timer expire", pass25, ref passedCount, results);

                // Case 26: CYAN Stasis runs on its own independent skill timer and cleanly restores gravity/damping when expired
                skillResp.OnColorShotHit(AmmoType.Cyan, ObjectColorState.Blue, dummyHit, Vector3.forward);
                bool stasisStarted = skillResp.IsBlueStasis && (!rb.useGravity);
                skillResp.TickSkillTimers(10.0f);
                bool stasisExpiredCleanly = (!skillResp.IsBlueStasis) && rb.useGravity;
                bool pass26 = stasisStarted && stasisExpiredCleanly;
                LogCase(26, "CYAN Stasis arrests motion with independent timer and restores original Rigidbody gravity/damping on expire", pass26, ref passedCount, results);

                // Case 27: RaycastWeapon auto-resolves MuzzlePoint child so shots never originate from under the gun grip
                Transform resolvedMuzzle = raycastWep.MuzzlePoint;
                bool pass27 = (resolvedMuzzle == muzzleChild.transform) && (resolvedMuzzle.localPosition.y > 0.03f) && (resolvedMuzzle.localPosition.z > 0.05f);
                LogCase(27, "RaycastWeapon resolves MuzzlePoint child at upper barrel tip instead of weapon root", pass27, ref passedCount, results);

                // Case 28: First-Person Viewmodel default placement is ergonomic and close to camera
                Vector3 vmPos = RevolverWeaponBuilder.DefaultViewmodelLocalPosition;
                bool pass28 = (vmPos.z >= 0.18f && vmPos.z <= 0.32f) && (vmPos.x >= 0.10f && vmPos.x <= 0.18f);
                LogCase(28, "First-person viewmodel default placement sits naturally near camera right hand", pass28, ref passedCount, results);
            }
            finally
            {
                Object.DestroyImmediate(testObj);
                Object.DestroyImmediate(secondObj);
                Object.DestroyImmediate(plainObj);
                Object.DestroyImmediate(ammoSysObj);
                Object.DestroyImmediate(gunTestObj);
            }

            results.AppendLine($"\n<b>Total Passed: {passedCount} / {totalCount}</b>");
            if (passedCount == totalCount)
            {
                Debug.Log($"<color=#00FF88>{results}</color>");
            }
            else
            {
                Debug.LogError(results.ToString());
            }
        }

        private static void LogCase(int num, string desc, bool pass, ref int passedCount, StringBuilder sb)
        {
            if (pass)
            {
                passedCount++;
                sb.AppendLine($"[PASS] Case {num}: {desc}");
            }
            else
            {
                sb.AppendLine($"[FAIL] Case {num}: {desc}");
            }
        }
    }
}
