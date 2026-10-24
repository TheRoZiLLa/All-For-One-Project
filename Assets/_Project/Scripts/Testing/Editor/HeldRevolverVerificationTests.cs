using System;
using System.Linq;
using System.Reflection;
using Recorded.ColorSystem;
using Recorded.Weapons;
using Recorded.Weapons.Ammo;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Recorded.Testing.Editor
{
    public static class HeldRevolverVerificationTests
    {
        public static string VerifyCrosshairPresentationInPlayMode()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Run crosshair verification in Play Mode.");
            var fixture = new GameObject("Crosshair presentation verification");
            fixture.SetActive(false);
            var canvasObject = new GameObject("Crosshair_Canvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(fixture.transform, false);
            var reticle = new GameObject("Crosshair_Reticle", typeof(RectTransform));
            reticle.transform.SetParent(canvasObject.transform, false);
            var crosshair = fixture.AddComponent<Recorded.UI.CrosshairUI>();
            var settings = fixture.AddComponent<Recorded.UI.CrosshairSettingsUI>();
            var previousLock = Cursor.lockState;
            bool previousVisible = Cursor.visible;
            try
            {
                fixture.SetActive(true);
                crosshair.EnsureCrosshairHierarchy();
                Equal(1, fixture.GetComponentsInChildren<Canvas>(true).Length, "Crosshair duplicated its existing Canvas.");
                var parts = reticle.GetComponentsInChildren<UnityEngine.UI.Image>();
                Equal(5, parts.Length);
                foreach (var color in new[] { Color.red, Color.green, Color.cyan })
                {
                    crosshair.SetCrosshairColor(color);
                    var after = reticle.GetComponentsInChildren<UnityEngine.UI.Image>();
                    Equal(parts.Length, after.Length, "Recolor rebuilt UI objects.");
                    for (int i = 0; i < parts.Length; i++) { Equal(parts[i], after[i]); Equal(color, after[i].color); }
                }
                typeof(Recorded.UI.CrosshairSettingsUI).GetMethod("BuildIfNeeded", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(settings, null);
                Equal(1, canvasObject.transform.Cast<Transform>().Count(t => t.name == "Crosshair_Settings"));
                var before = crosshair.Style;
                canvasObject.transform.Find("Crosshair_Settings/Style").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                Equal((Recorded.UI.CrosshairUI.ReticleStyle)(((int)before + 1) % 4), crosshair.Style, "Settings reused duplicate callbacks.");
                Require(UnityEngine.EventSystems.EventSystem.current != null, "Settings has no UI input event system.");
                settings.ToggleSettings();
                Require(settings.IsOpen && Cursor.lockState == CursorLockMode.None && Cursor.visible);
                settings.enabled = false;
                Require(!settings.IsOpen);
                Equal(previousLock, Cursor.lockState);
                Equal(previousVisible, Cursor.visible);
                return "PASS: existing Canvas reused, RGB recolor preserves UI objects, settings reuse has one callback, UI input exists and cursor restores on close/disable.";
            }
            finally
            {
                Object.Destroy(fixture);
                Cursor.lockState = previousLock;
                Cursor.visible = previousVisible;
            }
        }

        public static System.Collections.IEnumerator VerifyImpactPresentationInPlayMode(Action<string> completed)
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Run impact verification in Play Mode.");
            var fixture = new GameObject("Impact presentation verification");
            fixture.SetActive(false);
            fixture.transform.localScale = Vector3.one * 25f;
            var muzzle = new GameObject("MuzzlePoint", typeof(Light)).transform;
            muzzle.SetParent(fixture.transform, false);
            muzzle.localScale = Vector3.one / 25f;
            var light = muzzle.GetComponent<Light>();
            light.intensity = 2f;
            var feedback = fixture.AddComponent<WeaponImpactFeedback>();
            feedback.SetMuzzleLight(light);
            SetReference(feedback, "energyMaterial", AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/VFX/SpectrumEnergy.mat"));
            try
            {
                fixture.SetActive(true);
                var flareCollider = muzzle.GetComponentInChildren<Collider>(true);
                Require(flareCollider == null || !flareCollider.enabled, "Flash must never affect player collisions, including its first frame.");
                yield return null;
                var effects = (Transform)typeof(WeaponImpactFeedback).GetField("_worldEffects", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(feedback);
                var impacts = effects.Cast<Transform>().Where(t => t.name.StartsWith("ImpactEffect_Procedural")).ToArray();
                Equal(8, impacts.Length);
                var tracer = effects.Find("WeaponTracer_Line").GetComponent<LineRenderer>();
                Equal((float)UnityEngine.Rendering.BlendMode.One, tracer.sharedMaterial.GetFloat("_DstBlend"));
                Equal(0f, tracer.sharedMaterial.GetFloat("_ZWrite"));
                var materials = (System.Collections.Generic.List<Material>)typeof(WeaponImpactFeedback).GetField("_ownedMaterials", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(feedback);
                int materialCount = materials.Count;
                var point = Vector3.forward * 5f;
                feedback.SetFeedbackColor(SpectrumGunController.ColorRed);
                feedback.PlayHitFeedback(Vector3.zero, new RaycastHit { point = point, normal = Vector3.back });
                var active = impacts.Single(t => t.gameObject.activeSelf);
                Require(active.GetComponent<LineRenderer>().startColor.a > 0.99f, "Impact must start visible even after a slow frame.");
                var sparks = active.Find("ImpactSparks").GetComponent<ParticleSystem>();
                Require(sparks.particleCount > 0 && sparks.main.maxParticles <= 24, "Impact sparks are missing or unbounded.");
                Equal(ParticleSystemSimulationSpace.World, sparks.main.simulationSpace);
                Require(active.Find("ImpactCorona") != null && active.Find("ImpactBurstStar") != null);
                var flare = muzzle.Find("MuzzleFlare_Visual");
                Equal(18, flare.GetComponent<MeshFilter>().sharedMesh.vertexCount);
                Require(flare.lossyScale.magnitude < 3f, "Muzzle flash inherited the hand scale.");
                feedback.SetFeedbackColor(SpectrumGunController.ColorGreen);
                Require(Vector4.Distance(SpectrumGunController.ColorRed, active.GetComponent<LineRenderer>().sharedMaterial.color) < 0.001f, "Cylinder advancement recolored an active impact.");
                Equal(SpectrumGunController.ColorRed, light.color);
                yield return new WaitForSeconds(0.06f);
                Require(active.GetComponent<LineRenderer>().startColor.a < 1f, "Impact ring did not fade.");
                yield return new WaitForSeconds(0.6f);
                Require(impacts.All(t => !t.gameObject.activeSelf), "Impact did not return to the pool.");
                Require(!tracer.enabled && !light.enabled);
                Equal(2f, light.intensity);
                feedback.PlayHitFeedback(Vector3.zero, new RaycastHit { point = point, normal = Vector3.up });
                Equal(8, effects.Cast<Transform>().Count(t => t.name.StartsWith("ImpactEffect_Procedural")));
                Equal(materialCount, materials.Count, "Pooled shots allocated more materials.");
                feedback.enabled = false;
                Require(impacts.All(t => !t.gameObject.activeSelf && !t.GetComponentInChildren<ParticleSystem>(true).isPlaying), "Disable left impact effects alive.");
                Require(!tracer.enabled && !light.enabled && !flare.gameObject.activeSelf);
                completed("PASS: additive crown/dual-ring/sparks, bounded world-space particles, captured shot colors, fading/pool reuse with no new materials, and disable cleanup.");
            }
            finally { Object.Destroy(fixture); }
        }

        public static System.Collections.IEnumerator VerifyReloadHudInPlayMode(Action<string> completed)
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Run HUD verification in Play Mode.");
            var fixture = new GameObject("Reload HUD verification");
            fixture.SetActive(false);
            var player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Recorded/Prefabs/Player.prefab"), fixture.transform);
            player.transform.position = Vector3.one * 2000f;
            foreach (var camera in player.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
            foreach (var listener in player.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
            foreach (var behaviour in player.GetComponents<MonoBehaviour>()) behaviour.enabled = false;
            var gun = player.GetComponentInChildren<SpectrumGunController>();
            var animator = player.transform.Find("CameraPivot/MainCamera/Viewmodel/Arms").GetComponent<Animator>();
            var leftUpper = animator.transform.Find("upper_arm.L");
            var leftForearm = leftUpper.Find("forearm.L");
            var leftHand = leftForearm.Find("hand.L");
            var leftIndex = leftHand.Find("palm.01.L/f_index.01.L/f_index.02.L/f_index.03.L/f_index.03.L_end");
            var leftThumb = leftHand.Find("palm.01.L/thumb.01.L/thumb.02.L/thumb.03.L/thumb.03.L_end");
            var leftRestPosition = leftUpper.localPosition;
            var leftRestRotation = leftHand.localRotation;
            float upperLength = Vector3.Distance(leftUpper.position, leftForearm.position);
            float forearmLength = Vector3.Distance(leftForearm.position, leftHand.position);
            var ammo = fixture.AddComponent<CylinderAmmoController>();
            ammo.enabled = false;
            var rotationObject = new GameObject("Fixture cylinder rotation");
            rotationObject.transform.SetParent(fixture.transform, false);
            var rotation = rotationObject.AddComponent<CylinderRotationController>();
            var hud = fixture.AddComponent<CylinderAmmoHUD>();
            SetReference(gun, "ammoController", ammo);
            SetReference(gun, "rotationController", rotation);
            SetReference(rotation, "ammoController", ammo);
            SetReference(hud, "ammoController", ammo);
            SetReference(hud, "rotationController", rotation);
            SetReference(hud, "weaponController", gun);
            int refills = 0;
            ammo.OnCylinderReloaded += () => refills++;
            try
            {
                fixture.SetActive(true);
                foreach (var canvas in fixture.GetComponentsInChildren<Canvas>()) canvas.enabled = false;
                yield return new WaitForEndOfFrame();
                for (int i = 0; i < CylinderAmmoController.CylinderCapacity; i++) ammo.LoadChamber(i, AmmoType.Red);
                var inner = hud.InnerCylinderRect;
                Require(inner != null);
                var indexedBarrel = gun.transform.Find("Barrel");
                var cylinderCenter = indexedBarrel.GetComponent<MeshFilter>().sharedMesh.bounds.center;
                var authoredBarrel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Recorded/Prefabs/Player.prefab").transform.Find("CameraPivot/MainCamera/Viewmodel/Arms/upper_arm.R/forearm.R/hand.R/REV/Barrel");
                var seatedCenter = authoredBarrel.localPosition + authoredBarrel.localRotation * cylinderCenter;
                var seatedRotation = authoredBarrel.localRotation;
                void CheckModelIndex()
                {
                    var expected = seatedRotation * Quaternion.AngleAxis(rotation.CurrentAngle, Vector3.right);
                    Require(Quaternion.Angle(expected, indexedBarrel.localRotation) < 0.1f, "3D cylinder does not follow the displayed chamber angle: UI=" + rotation.CurrentAngle + ", model=" + indexedBarrel.localEulerAngles + ", rest=" + seatedRotation.eulerAngles);
                    Require(Vector3.Distance(seatedCenter, indexedBarrel.localPosition + indexedBarrel.localRotation * cylinderCenter) < 0.00001f, "Index rotation moved the cylinder off its seat.");
                }
                rotation.RotateChamberStep(2);
                yield return new WaitUntil(() => !rotation.IsRotating);
                yield return new WaitForEndOfFrame();
                CheckModelIndex();
                Require(gun.TriggerReload());
                float openingDeadline = Time.realtimeSinceStartup + 5f;
                while (gun.ReloadProgress < 0.20f && Time.realtimeSinceStartup < openingDeadline) yield return null;
                Require(gun.ReloadProgress >= 0.20f, "Reload failed to reach ejection.");
                animator.speed = 0f;
                yield return new WaitForEndOfFrame();
                Require(inner.anchoredPosition.x < -1f, "HUD cylinder did not swing out with the weapon.");
                for (int i = 0; i < CylinderAmmoController.CylinderCapacity; i++)
                {
                    var ejecting = inner.Find("Chamber_" + i + "/Cartridge_Base");
                    Require(ejecting.GetComponent<CanvasGroup>().alpha < 0.999f, "HUD did not fade ejected rounds at progress " + gun.ReloadProgress);
                }
                var pausedPosition = inner.anchoredPosition;
                var pausedRotation = inner.localRotation;
                float pausedProgress = gun.ReloadProgress;
                yield return new WaitForSeconds(1.2f);
                Equal(0, refills, "Paused animation still refilled ammo on a timer.");
                Require(gun.IsReloading && !gun.ReloadAmmoLoaded);
                Equal(pausedPosition, inner.anchoredPosition);
                Require(Quaternion.Angle(pausedRotation, inner.localRotation) < 0.1f);
                Require(Mathf.Abs(gun.ReloadProgress - pausedProgress) < 0.0001f);
                var viewCamera = player.transform.Find("CameraPivot/MainCamera").GetComponent<Camera>();
                var pickup = viewCamera.transform.Find("Viewmodel/ReloadPickup");
                var grip = leftHand.Find("ReloadGrip.L");
                Vector3 beforePickup = Vector3.zero;
                foreach (float phase in new[] { 0.01f, 0.10f, 0.17f, 0.179f, 0.181f, 0.24f, 0.32f, 0.40f, 0.48f })
                {
                    animator.Play("Reload", 0, phase);
                    animator.Update(0f);
                    yield return new WaitForEndOfFrame();
                    var carried = gun.transform.Find("Barrel/Reload_Cartridges");
                    Require(carried != null && carried.gameObject.activeSelf, "Loader switched on midway through the reach.");
                    var knob = carried.Find("Speedloader/ReleaseKnob");
                    Require(Vector3.Distance(knob.position, phase < 0.18f ? pickup.position : grip.position) < 0.002f, "Loader slipped from its pouch/hand anchor.");
                    if (phase < 0.18f) Require(viewCamera.WorldToViewportPoint(knob.position).y < 0f, "Loader spawned inside the visible frame.");
                    if (phase == 0.179f) beforePickup = knob.position;
                    if (phase == 0.181f) Require(Vector3.Distance(beforePickup, knob.position) < 0.005f, "Pickup transfer teleported the loader.");
                }
                var loader = gun.transform.Find("Barrel/Reload_Cartridges");
                Require(loader != null && loader.gameObject.activeSelf, "Reload did not show physical cartridges.");
                Equal(CylinderAmmoController.CylinderCapacity * 4 + 1, loader.childCount);
                Require(loader.Find("BulletTip_0") != null && loader.Find("Rim_0") != null, "Cartridges lost their bullet tips or case rims.");
                Require(loader.Find("Speedloader/LoaderPlate") != null, "Cartridges have no speedloader carrier.");
                Require(Vector3.Distance((leftIndex.position + leftThumb.position) * 0.5f,
                    loader.Find("Speedloader/ReleaseKnob").position) < 0.008f, "Left hand missed the speedloader grip.");
                Require(Vector3.Distance(leftIndex.position, leftThumb.position) < 0.03f, "Fingers did not pinch the release knob.");
                Require(Mathf.Abs(upperLength - Vector3.Distance(leftUpper.position, leftForearm.position)) < 0.0001f, "Left upper arm stretched.");
                Require(Mathf.Abs(forearmLength - Vector3.Distance(leftForearm.position, leftHand.position)) < 0.0001f, "Left forearm stretched.");
                Require(loader.localPosition.x < 0f, "Cartridges did not approach from behind the cylinder.");
                var cartridge = inner.Find("Chamber_0/Cartridge_Base").GetComponent<RectTransform>();
                var group = cartridge.GetComponent<CanvasGroup>();
                Require(group.alpha < 0.001f, "HUD revealed replacement ammo before loading.");
                Equal(Vector2.zero, cartridge.anchoredPosition, "HUD ammo moved instead of fading.");
                Equal(Vector3.one, cartridge.localScale, "HUD ammo scaled instead of fading.");
                Equal(0, refills, "Animation preview filled ammo before cartridges seated.");
                var insertionPosition = loader.localPosition;
                var pausedHand = leftHand.position;
                var insertionOffset = cartridge.anchoredPosition;
                foreach (var renderer in loader.GetComponentsInChildren<Renderer>()) Equal(gun.gameObject.layer, renderer.gameObject.layer);
                yield return new WaitForSeconds(0.2f);
                yield return new WaitForEndOfFrame();
                Equal(insertionPosition, loader.localPosition);
                Require(Vector3.Distance(pausedHand, leftHand.position) < 0.00001f, "Paused reload still moved the left hand.");
                Equal(insertionOffset, cartridge.anchoredPosition);
                var barrel = gun.transform.Find("Barrel");
                var insertionRotation = barrel.localRotation;
                animator.Play("Reload", 0, 0.659f);
                animator.Update(0f);
                yield return new WaitForEndOfFrame();
                var beforeSeat = loader.Find("Cartridge_0").position;
                Require(Quaternion.Angle(insertionRotation, barrel.localRotation) < 0.05f, "Cylinder spun while cartridges were being inserted.");
                animator.Play("Reload", 0, 0.661f);
                animator.Update(0f);
                yield return new WaitForEndOfFrame();
                Require(Vector3.Distance(beforeSeat, loader.Find("Cartridge_0").position) < 0.005f, "Cartridges teleported when seating.");
                animator.speed = 1f;
                float deadline = Time.time + 5f;
                while (refills == 0 && Time.time < deadline) yield return new WaitForEndOfFrame();
                Equal(1, refills);
                Require(gun.ReloadAmmoLoaded && gun.IsReloading);
                yield return new WaitForEndOfFrame();
                for (int i = 0; i < CylinderAmmoController.CylinderCapacity; i++) Require(inner.Find("Chamber_" + i + "/Cartridge_Base").gameObject.activeSelf, "HUD did not show inserted rounds.");
                while (gun.IsReloading && Time.time < deadline) yield return new WaitForEndOfFrame();
                yield return new WaitForEndOfFrame();
                Require(!gun.IsReloading);
                Require(inner.anchoredPosition.magnitude < 0.001f, "HUD failed to close.");
                Require(Mathf.Abs(Mathf.DeltaAngle(inner.localEulerAngles.z, rotation.CurrentAngle)) < 0.1f, "Reload spin remained on the logical cylinder index.");
                for (int i = 0; i < CylinderAmmoController.CylinderCapacity; i++) Equal(Vector3.one, inner.Find("Chamber_" + i + "/Cartridge_Base").localScale);
                Require(!loader.gameObject.activeSelf, "Cartridges stayed visible after closing.");
                CheckModelIndex();
                Require(Vector3.Distance(leftRestPosition, leftUpper.localPosition) < 0.00001f, "Left shoulder failed to return.");
                Require(Quaternion.Angle(leftRestRotation, leftHand.localRotation) < 0.1f, "Left wrist failed to return.");
                ammo.LoadChamber(0, AmmoType.Red);
                yield return new WaitForSeconds(0.3f);
                var cap = inner.Find("Chamber_0/Cartridge_Base/Color_CoreCap").GetComponent<UnityEngine.UI.Image>();
                var previousColor = cap.color;
                ammo.LoadChamber(0, AmmoType.Green);
                Equal(previousColor, cap.color, "Chamber color snapped instead of blending.");
                Equal(AmmoType.Green, ammo.GetChamberAmmo(0), "Visual transition delayed logical ammo.");
                yield return new WaitForSeconds(0.3f);
                Require(Vector4.Distance(cap.color, hud.GetAmmoColor(AmmoType.Green)) < 0.025f, "Chamber color failed to reach its target.");
                ammo.LoadChamber(0, AmmoType.Empty);
                Require(group.alpha > 0.99f, "Ammo disappeared instantly instead of fading.");
                yield return new WaitForSeconds(0.05f);
                Require(group.alpha > 0f && group.alpha < 0.99f, "Empty ammo skipped its fade.");
                Equal(Vector2.zero, cartridge.anchoredPosition);
                Equal(Vector3.one, cartridge.localScale);
                ammo.LoadChamber(0, AmmoType.Green);
                yield return new WaitForSeconds(0.3f);
                rotation.RotateRight();
                yield return new WaitUntil(() => !rotation.IsRotating);
                yield return new WaitForEndOfFrame();
                CheckModelIndex();
                Require(Mathf.Abs(Mathf.DeltaAngle(inner.localEulerAngles.z, 3f * CylinderRotationController.ChamberAngleStep)) < 0.1f, "Manual rotation desynchronized after reload.");
                rotation.RotateLeft();
                yield return new WaitUntil(() => !rotation.IsRotating);
                yield return new WaitForEndOfFrame();
                CheckModelIndex();
                Equal(2, ammo.ActiveChamberIndex);
                rotation.RotateRight();
                yield return new WaitUntil(() => !rotation.IsRotating);
                rotation.RotateRight();
                rotation.gameObject.SetActive(false);
                Require(!rotation.IsRotating, "Deactivating the cylinder left firing locked.");
                Require(Mathf.Abs(Mathf.DeltaAngle(rotation.CurrentAngle, 4f * CylinderRotationController.ChamberAngleStep)) < 0.1f, "Disabled rotation failed to finish the selected index.");
                rotation.gameObject.SetActive(true);
                rotation.RotateLeft();
                yield return new WaitUntil(() => !rotation.IsRotating);
                yield return new WaitForEndOfFrame();
                CheckModelIndex();
                SetCooldown(gun, 0f);
                Require(gun.AttemptFire(), "Loaded round failed to fire after the rotation completed.");
                yield return new WaitUntil(() => !rotation.IsRotating);
                yield return new WaitForEndOfFrame();
                CheckModelIndex();
                Equal(AmmoType.Empty, ammo.GetChamberAmmo(3));
                Require(Mathf.Abs(Mathf.DeltaAngle(inner.localEulerAngles.z, 4f * CylinderRotationController.ChamberAngleStep)) < 0.1f, "Shot auto-advance desynchronized after reload.");
                Require(gun.TriggerReload());
                yield return new WaitForSeconds(0.85f);
                gun.enabled = false;
                yield return new WaitForEndOfFrame();
                Require(!gun.IsReloading && inner.anchoredPosition.magnitude < 0.001f, "Cancellation left the HUD open.");
                Require(!loader.gameObject.activeSelf, "Cancellation left physical cartridges floating.");
                Require(Vector3.Distance(leftRestPosition, leftUpper.localPosition) < 0.00001f, "Cancelled reload left the shoulder displaced.");
                Require(Quaternion.Angle(leftRestRotation, leftHand.localRotation) < 0.1f, "Cancelled reload left the wrist posed.");
                completed("PASS: 3D cylinder follows both rotation directions and firing without moving its center; indexed reload keeps hand contact and seating, HUD ammo only fades without sliding/scaling, pauses and cancellation recover.");
            }
            finally
            {
                animator.speed = 1f;
                Object.Destroy(fixture);
            }
        }

        public static System.Collections.IEnumerator VerifyReloadInputInPlayMode(Action<string> completed)
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Run reload input verification in Play Mode.");
            var previousKeyboard = UnityEngine.InputSystem.Keyboard.current;
            var settings = UnityEngine.InputSystem.InputSystem.settings;
            var previousBackground = settings.backgroundBehavior;
            var previousEditorInput = settings.editorInputBehaviorInPlayMode;
            settings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
            var fixture = new GameObject("R reload input verification");
            fixture.SetActive(false);
            var player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Recorded/Prefabs/Player.prefab"), fixture.transform);
            player.transform.position = Vector3.one * 2000f;
            foreach (var camera in player.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
            foreach (var listener in player.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
            foreach (var behaviour in player.GetComponents<MonoBehaviour>()) behaviour.enabled = false;
            var gun = player.GetComponentInChildren<SpectrumGunController>();
            var animator = player.transform.Find("CameraPivot/MainCamera/Viewmodel/Arms").GetComponent<Animator>();
            var ammo = fixture.AddComponent<CylinderAmmoController>();
            ammo.enabled = false;
            var rotation = fixture.AddComponent<CylinderRotationController>();
            rotation.enabled = false;
            var input = fixture.AddComponent<AmmoTestInput>();
            SetReference(gun, "ammoController", ammo);
            SetReference(gun, "rotationController", rotation);
            SetReference(rotation, "ammoController", ammo);
            SetReference(input, "ammoController", ammo);
            SetReference(input, "rotationController", rotation);
            int refills = 0;
            ammo.OnCylinderReloaded += () => refills++;
            void PressR(bool fallback)
            {
                UnityEngine.InputSystem.InputSystem.EnableDevice(keyboard);
                keyboard.MakeCurrent();
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.R));
                UnityEngine.InputSystem.InputSystem.Update();
                try
                {
                    Require(keyboard.rKey.wasPressedThisFrame, "Synthetic R event was not delivered to the test keyboard.");
                    if (fallback)
                        typeof(SpectrumGunController).GetMethod("CheckColorSwitchInput", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(gun, null);
                    else
                    {
                        typeof(AmmoTestInput).GetField("_cachedGunController", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(input, gun);
                        typeof(AmmoTestInput).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(input, null);
                    }
                }
                finally
                {
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState());
                    UnityEngine.InputSystem.InputSystem.Update();
                }
            }
            try
            {
                fixture.SetActive(true);
                yield return null;
                animator.Rebind();
                animator.Update(0f);
                PressR(false);
                Require(gun.IsReloading && refills == 0 && !ammo.HasActiveRound, "R bypassed the animation and filled ammo immediately.");
                PressR(false);
                Require(!gun.AttemptFire(), "Firing was allowed during reload.");
                yield return new WaitForSeconds(0.2f);
                Require(animator.GetCurrentAnimatorStateInfo(0).IsName("Reload") ||
                    (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName("Reload")),
                    "R did not start the Reload animation.");
                Require(refills == 0 && !ammo.HasActiveRound, "Ammo refilled before the loading phase.");
                float deadline = Time.time + 3f;
                while (refills == 0 && Time.time < deadline) yield return null;
                Equal(1, refills, "Repeated R started duplicate reloads.");
                Require(gun.IsReloading && ammo.HasActiveRound, "Refill did not occur before the closing phase.");
                while (gun.IsReloading && Time.time < deadline) yield return null;
                Require(!gun.IsReloading, "Reload never released its firing lock.");
                for (int i = 0; i < CylinderAmmoController.CylinderCapacity; i++) Require(ammo.GetChamberAmmo(i) != AmmoType.Empty);
                input.enabled = false;
                for (int i = 0; i < CylinderAmmoController.CylinderCapacity; i++) ammo.LoadChamber(i, AmmoType.Empty);
                PressR(true);
                Require(gun.IsReloading && refills == 1 && !ammo.HasActiveRound, "Fallback R bypassed the animation.");
                gun.enabled = false;
                Require(!gun.IsReloading, "Cancelling reload left the weapon locked.");
                yield return new WaitForSeconds(1.1f);
                Equal(1, refills, "Cancelled reload still filled ammo.");
                gun.enabled = true;
                PressR(true);
                Require(gun.IsReloading && !ammo.HasActiveRound, "Reload could not restart after cancellation.");
                fixture.SetActive(false);
                Require(!gun.IsReloading, "Deactivating the player left reload locked.");
                completed("PASS: actual R keyboard events start Reload on both input routes, delay refill, reject repeated R/fire, and support cancellation/retry.");
            }
            finally
            {
                Object.Destroy(fixture);
                UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);
                if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
                settings.backgroundBehavior = previousBackground;
                settings.editorInputBehaviorInPlayMode = previousEditorInput;
            }
        }

        public static string VerifyKioskInputPriorityInPlayMode()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Run kiosk input verification in Play Mode.");
            var previousKeyboard = UnityEngine.InputSystem.Keyboard.current;
            var settings = UnityEngine.InputSystem.InputSystem.settings;
            var previousBackground = settings.backgroundBehavior;
            var previousEditorInput = settings.editorInputBehaviorInPlayMode;
            settings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
            var fixture = new GameObject("Kiosk input priority verification");
            fixture.SetActive(false);
            fixture.transform.position = Vector3.one * 5000f;
            var player = new GameObject("Player", typeof(CharacterController));
            player.transform.SetParent(fixture.transform, false);
            player.tag = "Player";
            var ammo = player.AddComponent<CylinderAmmoController>();
            var rotation = player.AddComponent<CylinderRotationController>();
            rotation.enabled = false;
            var gun = player.AddComponent<SpectrumGunController>();
            gun.enabled = false;
            SetReference(gun, "ammoController", ammo);
            SetReference(gun, "rotationController", rotation);
            SetReference(rotation, "ammoController", ammo);
            var kioskObject = new GameObject("Kiosk", typeof(BoxCollider));
            kioskObject.transform.SetParent(fixture.transform, false);
            kioskObject.GetComponent<BoxCollider>().isTrigger = true;
            var gate = kioskObject.AddComponent<KioskCyanAirlockGate>();
            var kiosk = kioskObject.AddComponent<SecurityKioskEInteraction>();
            SetReference(kiosk, "airlockGate", gate);
            void Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
            void PressE(bool kioskFirst)
            {
                UnityEngine.InputSystem.InputSystem.EnableDevice(keyboard);
                keyboard.MakeCurrent();
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.E));
                UnityEngine.InputSystem.InputSystem.Update();
                try
                {
                    Require(keyboard.eKey.wasPressedThisFrame, "Synthetic E event was not delivered.");
                    if (kioskFirst) Invoke(kiosk, "Update");
                    Invoke(gun, "CheckColorSwitchInput");
                    if (!kioskFirst) Invoke(kiosk, "Update");
                }
                finally
                {
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState());
                    UnityEngine.InputSystem.InputSystem.Update();
                }
            }
            try
            {
                fixture.SetActive(true);
                PressE(false);
                Equal(1, ammo.ActiveChamberIndex);
                Require(!gate.IsKioskUnlocked, "Kiosk unlocked outside its trigger.");
                var collider = player.GetComponent<CharacterController>();
                Invoke(kiosk, "OnTriggerEnter", collider);
                PressE(true);
                Require(gate.IsKioskUnlocked, "Nearby E failed to unlock the kiosk.");
                Equal(1, ammo.ActiveChamberIndex, "Kiosk-first input also rotated the gun.");
                PressE(false);
                Equal(1, ammo.ActiveChamberIndex, "Gun-first input also rotated the gun.");
                gate.OnColorShotHit(AmmoType.Red, ObjectColorState.Red, default, Vector3.forward);
                Require(!gate.IsAirlockOpen, "Non-Cyan shot opened the airlock.");
                gate.OnColorShotHit(AmmoType.Cyan, ObjectColorState.Green | ObjectColorState.Blue, default, Vector3.forward);
                Require(gate.IsAirlockOpen, "Cyan shot failed after the kiosk unlocked.");
                Invoke(kiosk, "OnTriggerExit", collider);
                PressE(false);
                Equal(2, ammo.ActiveChamberIndex, "E stayed reserved after leaving the kiosk.");
                Invoke(kiosk, "OnTriggerEnter", collider);
                kiosk.enabled = false;
                Require(!SecurityKioskEInteraction.HasInteractionFor(player.transform), "Disabled kiosk kept E reserved.");
                PressE(false);
                Equal(3, ammo.ActiveChamberIndex);
                return "PASS: E prioritizes the nearby kiosk in either update order, resumes cylinder selection on exit/disable, and preserves Cyan-only gate opening.";
            }
            finally
            {
                fixture.SetActive(false);
                Object.Destroy(fixture);
                UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);
                if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
                settings.backgroundBehavior = previousBackground;
                settings.editorInputBehaviorInPlayMode = previousEditorInput;
            }
        }

        public static string VerifyReloadBarrelInPlayMode()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Run reload verification in Play Mode.");
            var fixture = new GameObject("Reload barrel verification");
            fixture.SetActive(false);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Recorded/Prefabs/Player.prefab");
            var player = Object.Instantiate(prefab, fixture.transform);
            foreach (var camera in player.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
            foreach (var listener in player.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
            foreach (var behaviour in player.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
            const string path = "CameraPivot/MainCamera/Viewmodel/Arms/upper_arm.R/forearm.R/hand.R/REV/Barrel";
            var barrel = player.transform.Find(path);
            var source = prefab.transform.Find(path);
            var animator = player.transform.Find("CameraPivot/MainCamera/Viewmodel/Arms").GetComponent<Animator>();
            var restPosition = source.localPosition;
            var restRotation = source.localRotation;
            var leftShoulder = animator.transform.Find("upper_arm.L");
            var leftElbow = leftShoulder.Find("forearm.L");
            var leftWrist = leftShoulder.Find("forearm.L/hand.L");
            var shoulderRest = leftShoulder.localPosition;
            var wristRest = leftWrist.localRotation;
            var meshCenter = barrel.GetComponent<MeshFilter>().sharedMesh.bounds.center;
            var restCenter = restPosition + restRotation * meshCenter;
            var hinge = restCenter + Vector3.down * 0.004f;
            float scale = Mathf.Abs(barrel.parent.lossyScale.x);
            try
            {
                fixture.SetActive(true);
                foreach (int fps in new[] { 30, 60, 144 })
                {
                    animator.Rebind();
                    animator.Update(0f);
                    Require(Vector3.Distance(restPosition, barrel.localPosition) * scale < 0.001f, "Ready uses an outdated cylinder seat.");
                    animator.SetTrigger("Reload");
                    var previous = restCenter;
                    var previousWrist = leftWrist.position;
                    var previousElbowRotation = leftElbow.localRotation;
                    var previousWristRotation = leftWrist.localRotation;
                    float maxTravel = 0f;
                    bool enteredReload = false;
                    for (int frame = 0; frame < fps * 3; frame++)
                    {
                        animator.Update(1f / fps);
                        enteredReload |= animator.GetCurrentAnimatorStateInfo(0).IsName("Reload");
                        var center = barrel.localPosition + barrel.localRotation * meshCenter;
                        maxTravel = Mathf.Max(maxTravel, Vector3.Distance(center, restCenter) * scale);
                        Require(Mathf.Abs(Vector3.Distance(center, hinge) - 0.004f) * scale < 0.002f, "Cylinder spin moved its center away from the hinge arc.");
                        Require(Vector3.Distance(center, previous) * scale < 0.035f, "Cylinder jumped during reload at " + fps + " FPS.");
                        previous = center;
                        Require(Vector3.Distance(previousWrist, leftWrist.position) < 0.15f, "Left wrist jumped at " + fps + " FPS.");
                        Require(Quaternion.Angle(previousElbowRotation, leftElbow.localRotation) < 1100f / fps + 2f, "Left forearm twisted abruptly at " + fps + " FPS.");
                        Require(Quaternion.Angle(previousWristRotation, leftWrist.localRotation) < 1500f / fps + 2f, "Left wrist twisted abruptly at " + fps + " FPS.");
                        previousWrist = leftWrist.position;
                        previousElbowRotation = leftElbow.localRotation;
                        previousWristRotation = leftWrist.localRotation;
                    }
                    Require(enteredReload, "Reload trigger did not enter the animation.");
                    Require(maxTravel > 0.04f && maxTravel < 0.12f, "Cylinder traveled too far or never opened: " + maxTravel);
                    Require(animator.GetCurrentAnimatorStateInfo(0).IsName("Ready"));
                    Require(Vector3.Distance(shoulderRest, leftShoulder.localPosition) < 0.00001f, "Ready retained left shoulder movement.");
                    Require(Quaternion.Angle(wristRest, leftWrist.localRotation) < 0.1f, "Ready retained the left wrist pose.");
                    Require(Vector3.Distance(restPosition, barrel.localPosition) * scale < 0.001f);
                    Require(Quaternion.Angle(restRotation, barrel.localRotation) < 0.1f);
                    animator.Play("Reload", 0, 0.4f);
                    animator.Update(0f);
                    animator.Play("Fire", 0, 0f);
                    animator.Update(0f);
                    Require(Vector3.Distance(restPosition, barrel.localPosition) * scale < 0.001f, "Fire retained an open cylinder pose.");
                    Require(Quaternion.Angle(restRotation, barrel.localRotation) < 0.1f);
                }
                return "PASS: cylinder and left wrist move smoothly at 30/60/144 FPS; Ready restores the left shoulder/wrist and cylinder seat, Fire restores the cylinder seat.";
            }
            finally
            {
                Object.Destroy(fixture);
            }
        }

        public static string VerifyGlowColorsInPlayMode()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Run glow verification in Play Mode.");
            var fixture = new GameObject("Glow color verification");
            fixture.SetActive(false);
            var player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Recorded/Prefabs/Player.prefab"), fixture.transform);
            player.transform.position = Vector3.one * 2000f;
            foreach (var camera in player.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
            foreach (var listener in player.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
            foreach (var behaviour in player.GetComponents<MonoBehaviour>()) behaviour.enabled = false;
            var gun = player.GetComponentInChildren<SpectrumGunController>();
            var ammo = fixture.AddComponent<CylinderAmmoController>();
            ammo.enabled = false;
            var rotation = fixture.AddComponent<CylinderRotationController>();
            rotation.enabled = false;
            SetReference(rotation, "ammoController", ammo);
            SetReference(gun, "ammoController", ammo);
            SetReference(gun, "rotationController", rotation);
            var lights = new Light[11];
            lights[0] = gun.transform.Find("ChamberGlow").GetComponent<Light>();
            for (int i = 1; i <= 10; i++) lights[i] = gun.transform.Find("glow" + i).GetComponent<Light>();
            var intensities = Array.ConvertAll(lights, light => light.intensity);
            var ranges = Array.ConvertAll(lights, light => light.range);
            var positions = Array.ConvertAll(lights, light => light.transform.localPosition);
            try
            {
                fixture.SetActive(true);
                foreach (var type in new[] { AmmoType.Red, AmmoType.Green, AmmoType.Cyan, AmmoType.Empty })
                {
                    ammo.LoadChamber(0, type);
                    foreach (var light in lights)
                    {
                        Equal(type != AmmoType.Empty, light.enabled, "Glow did not follow active chamber availability.");
                        Equal(SpectrumGunController.GetAmmoColor(type), light.color, "Glow did not follow active chamber color.");
                    }
                }
                ammo.LoadChamber(0, AmmoType.Red);
                ammo.LoadChamber(1, AmmoType.Green);
                SetCooldown(gun, 0f);
                Require(gun.AttemptFire());
                foreach (var light in lights) Equal(SpectrumGunController.ColorGreen, light.color, "Auto-advance did not update glow.");
                Equal(SpectrumGunController.ColorRed, gun.GetComponent<RaycastWeapon>().MuzzlePoint.GetComponent<Light>().color, "Glow recolored the fired muzzle flash.");
                ammo.TestReloadRandom();
                foreach (var light in lights) Equal(SpectrumGunController.GetAmmoColor(ammo.ActiveAmmoType), light.color, "Reload did not update glow.");
                SetReference(gun, "ammoController", null);
                gun.SetColorMode(SpectrumColorMode.Blue);
                for (int i = 0; i < lights.Length; i++)
                {
                    Require(lights[i].enabled);
                    Equal(SpectrumGunController.ColorBlue, lights[i].color);
                    Equal(intensities[i], lights[i].intensity);
                    Equal(ranges[i], lights[i].range);
                    Equal(positions[i], lights[i].transform.localPosition);
                }
                return "PASS: all ten glow lights + ChamberGlow follow RED/GREEN/CYAN/empty, auto-advance and standalone mode; authored intensity/range/position and fired flash color are preserved.";
            }
            finally
            {
                Object.Destroy(fixture);
            }
        }

        public static string VerifyShotAnimationInPlayMode()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Run animation verification in Play Mode.");
            var fixture = new GameObject("Held REV animation verification");
            fixture.SetActive(false);
            var player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Recorded/Prefabs/Player.prefab"), fixture.transform);
            player.transform.position = Vector3.one * 1000f;
            foreach (var camera in player.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
            foreach (var behaviour in player.GetComponents<MonoBehaviour>()) behaviour.enabled = false;
            var world = player.transform.Find("CameraPivot/MainCamera").GetComponent<Camera>();
            var viewmodel = world.transform.Find("Viewmodel");
            var arms = viewmodel.Find("Arms");
            var animator = arms.GetComponent<Animator>();
            var gun = arms.GetComponentInChildren<SpectrumGunController>();
            var raycast = gun.GetComponent<RaycastWeapon>();
            var ammo = fixture.AddComponent<CylinderAmmoController>();
            ammo.enabled = false;
            var cylinder = fixture.AddComponent<CylinderRotationController>();
            cylinder.enabled = false;
            SetReference(cylinder, "ammoController", ammo);
            SetReference(gun, "ammoController", ammo);
            SetReference(gun, "rotationController", cylinder);
            gun.enabled = false;
            var gripPosition = gun.transform.localPosition;
            var gripRotation = gun.transform.localRotation;
            var viewPosition = viewmodel.localPosition;
            var viewRotation = viewmodel.localRotation;
            var cameraRotation = world.transform.localRotation;
            var lights = gun.GetComponentsInChildren<Light>(true).Where(light => light.transform != raycast.MuzzlePoint).ToArray();
            var intensities = Array.ConvertAll(lights, light => light.intensity);
            var ranges = Array.ConvertAll(lights, light => light.range);
            try
            {
                Require(animator != null && animator.runtimeAnimatorController != null, "Missing shot Animator.");
                Equal(1, arms.GetComponentsInChildren<Animator>(true).Length, "A nested Animator would compete for the arm bones.");
                fixture.SetActive(true);
                animator.Rebind();
                animator.Update(0f);
                var muzzlePosition = raycast.MuzzlePoint.position;
                var muzzleRotation = raycast.MuzzlePoint.rotation;
                foreach (int fps in new[] { 30, 60, 144 })
                {
                    animator.Rebind();
                    animator.Update(0f);
                    ammo.SetActiveChamber(0);
                    for (int i = 0; i < CylinderAmmoController.CylinderCapacity; i++) ammo.LoadChamber(i, AmmoType.Red);
                    float step = 1f / fps;
                    int shots = 0;
                    float peakAngle = 0f;
                    var previousRotation = raycast.MuzzlePoint.rotation;
                    var previousPosition = raycast.MuzzlePoint.position;
                    for (int frame = 0; frame <= fps * 2; frame++)
                    {
                        if (shots < CylinderAmmoController.CylinderCapacity && frame * step + 0.0001f >= shots * 0.2f)
                        {
                            SetCooldown(gun, 0f);
                            Require(gun.AttemptFire(), "A valid round did not trigger its shot.");
                            shots++;
                        }
                        animator.Update(step);
                        var currentRotation = raycast.MuzzlePoint.rotation;
                        var currentPosition = raycast.MuzzlePoint.position;
                        peakAngle = Mathf.Max(peakAngle, Quaternion.Angle(muzzleRotation, currentRotation));
                        Require(Quaternion.Angle(previousRotation, currentRotation) < 11f, "Re-trigger caused an angular snap at " + fps + " FPS.");
                        Require(Vector3.Distance(previousPosition, currentPosition) < 0.2f, "Re-trigger caused a position snap at " + fps + " FPS.");
                        previousRotation = currentRotation;
                        previousPosition = currentPosition;
                    }
                    Equal(CylinderAmmoController.CylinderCapacity, shots);
                    Require(peakAngle > 5f && peakAngle < 12f, "Missing or excessive recoil at " + fps + " FPS: " + peakAngle);
                    Require(Vector3.Distance(muzzlePosition, raycast.MuzzlePoint.position) < 0.001f, "Recovery did not return to the authored grip.");
                    Require(Quaternion.Angle(muzzleRotation, raycast.MuzzlePoint.rotation) < 0.1f);
                    Require(animator.GetCurrentAnimatorStateInfo(0).IsName("Ready"), "Animator did not return to Ready.");
                    SetCooldown(gun, 0f);
                    Require(!gun.AttemptFire(), "An empty chamber fired.");
                    animator.Update(0.1f);
                    Require(animator.GetCurrentAnimatorStateInfo(0).IsName("Ready") && !animator.IsInTransition(0), "Dry fire triggered the live-shot animation.");
                }
                Equal(gripPosition, gun.transform.localPosition);
                Equal(gripRotation, gun.transform.localRotation);
                Equal(viewPosition, viewmodel.localPosition);
                Equal(viewRotation, viewmodel.localRotation);
                Equal(cameraRotation, world.transform.localRotation);
                for (int i = 0; i < lights.Length; i++)
                {
                    Equal(intensities[i], lights[i].intensity, "Animation changed authored glow intensity.");
                    Equal(ranges[i], lights[i].range, "Animation changed authored glow range.");
                }
                viewmodel.localPosition = viewPosition + Vector3.back * 0.08f;
                viewmodel.localRotation = Quaternion.Euler(10f, 0f, 0f) * viewRotation;
                animator.SetTrigger("Fire");
                animator.Update(0.06f);
                Equal(viewPosition + Vector3.back * 0.08f, viewmodel.localPosition, "Animator overwrote wall retraction.");
                Equal(Quaternion.Euler(10f, 0f, 0f) * viewRotation, viewmodel.localRotation);
                animator.enabled = false;
                animator.enabled = true;
                animator.Rebind();
                animator.Update(0f);
                Require(animator.GetCurrentAnimatorStateInfo(0).IsName("Ready"));
                return "PASS: eight-shot re-trigger/recovery at 30/60/144 FPS, live-shot-only animation, preserved grip/camera/glow, independent wall retraction and Animator reset.";
            }
            finally
            {
                Object.Destroy(fixture);
            }
        }

        public static string VerifyInPlayMode()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Run this verification in Play Mode.");
            var fixture = new GameObject("Held REV verification");
            fixture.SetActive(false);
            var player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Recorded/Prefabs/Player.prefab"), fixture.transform);
            player.transform.position = new Vector3(1000f, 0f, 1000f);
            foreach (var camera in player.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
            foreach (var behaviour in player.GetComponents<MonoBehaviour>()) behaviour.enabled = false;
            var world = player.transform.Find("CameraPivot/MainCamera").GetComponent<Camera>();
            var rev = world.transform.Find("Viewmodel/Arms/upper_arm.R/forearm.R/hand.R/REV");
            var position = rev.localPosition;
            var rotation = rev.localRotation;
            var scale = rev.localScale;
            var authoredGlow = rev.Find("ChamberGlow").GetComponent<Light>();
            float glowIntensity = authoredGlow.intensity, glowRange = authoredGlow.range;
            var glowPosition = authoredGlow.transform.localPosition;
            var ammoObject = new GameObject("Fixture ammo");
            ammoObject.transform.SetParent(fixture.transform, false);
            var ammo = ammoObject.AddComponent<CylinderAmmoController>();
            ammo.enabled = false;
            var cylinder = ammoObject.AddComponent<CylinderRotationController>();
            cylinder.enabled = false;
            SetReference(cylinder, "ammoController", ammo);
            var targetObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            targetObject.transform.position = world.transform.position + world.transform.forward * 10f;
            var target = targetObject.AddComponent<RGBColorTarget>();
            GameObject wall = null;
            try
            {
                Require(RevolverWeaponBuilder.TryConfigureHeldRevolver(world));
                Require(RevolverWeaponBuilder.TryConfigureHeldRevolver(world));
                Equal(glowIntensity, authoredGlow.intensity);
                Equal(glowRange, authoredGlow.range);
                Equal(glowPosition, authoredGlow.transform.localPosition);
                var gun = rev.GetComponent<SpectrumGunController>();
                gun.enabled = false;
                SetReference(gun, "ammoController", ammo);
                SetReference(gun, "rotationController", cylinder);
                fixture.SetActive(true);
                Physics.SyncTransforms();
                var raycast = rev.GetComponent<RaycastWeapon>();
                var feedback = rev.GetComponent<WeaponImpactFeedback>();
                var muzzle = raycast.MuzzlePoint;
                var flash = muzzle.GetComponent<Light>();
                Equal(1, rev.GetComponents<SpectrumGunController>().Length);
                Equal(world, raycast.PlayerCamera);
                Require(Vector3.Dot(muzzle.forward, world.transform.forward) > 0.99f);
                Require(Vector3.Distance(muzzle.lossyScale, Vector3.one) < 0.001f);
                Require(!gun.AttemptFire(), "An empty cylinder must not fire.");
                Require(!flash.enabled);

                var types = new[] { AmmoType.Red, AmmoType.Green, AmmoType.Cyan, AmmoType.Red, AmmoType.Green, AmmoType.Cyan, AmmoType.Red, AmmoType.Green };
                for (int i = 0; i < types.Length; i++) ammo.LoadChamber(i, types[i]);
                int hits = 0;
                raycast.OnTargetHit += hit => { Equal(targetObject.GetComponent<Collider>(), hit.collider); hits++; };
                for (int i = 0; i < types.Length; i++)
                {
                    SetCooldown(gun, 0f);
                    target.ResetColors();
                    Require(gun.AttemptFire());
                    Equal(AmmoType.Empty, ammo.GetChamberAmmo(i));
                    Equal((i + 1) % CylinderAmmoController.CylinderCapacity, ammo.ActiveChamberIndex);
                    var expected = types[i] == AmmoType.Red ? ObjectColorState.Red : types[i] == AmmoType.Green ? ObjectColorState.Green : ObjectColorState.Blue;
                    Equal(expected, target.CurrentColorState);
                    gun.SyncWithActiveChamber();
                    Require(flash.enabled);
                    Equal(SpectrumGunController.GetAmmoColor(types[i]), flash.color, "A fired flash keeps its color after the cylinder advances.");
                    Require(!gun.AttemptFire(), "Cooldown must prevent a second shot.");
                }
                Equal(CylinderAmmoController.CylinderCapacity, hits);
                SetCooldown(gun, 0f);
                Require(!gun.AttemptFire());
                Equal(CylinderAmmoController.CylinderCapacity, hits);
                Equal(position, rev.localPosition);
                Equal(rotation, rev.localRotation);
                Equal(scale, rev.localScale);

                var effects = (Transform)typeof(WeaponImpactFeedback).GetField("_worldEffects", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(feedback);
                Require(Vector3.Distance(effects.lossyScale, Vector3.one) < 0.001f);
                var tracer = effects.GetComponentInChildren<LineRenderer>(true);
                Require(tracer.useWorldSpace);
                Equal(0, tracer.gameObject.layer);
                var sheath = effects.Find("WeaponTracer_HelixSheath").GetComponent<LineRenderer>();
                Require(Vector3.Distance(sheath.transform.lossyScale, Vector3.one) < 0.001f, "Energy sheath inherited hand scale.");
                Require(Vector4.Distance(SpectrumGunController.GetAmmoColor(types[types.Length - 1]), sheath.sharedMaterial.color) < 0.001f, "Cylinder advance recolored the energy sheath.");
                var point = tracer.GetPosition(1);
                var effectPosition = effects.position;
                player.transform.position += Vector3.right * 3f;
                Equal(point, tracer.GetPosition(1));
                Equal(effectPosition, effects.position);
                player.transform.position -= Vector3.right * 3f;
                feedback.enabled = false;
                Require(!sheath.enabled, "Disabling the weapon left the energy sheath visible.");

                wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.position = Vector3.Lerp(world.transform.position, muzzle.position, 0.5f);
                wall.transform.localScale = new Vector3(0.12f, 2f, 0.1f);
                Physics.SyncTransforms();
                // Check without firing events: the muzzle is beyond side cover that the center aim ray misses.
                var isolated = rev.gameObject.AddComponent<RaycastWeapon>();
                isolated.SetCamera(world);
                isolated.SetMuzzlePoint(muzzle);
                Require(isolated.FireRaycast(out var obstruction, out _));
                Equal(wall.GetComponent<Collider>(), obstruction.collider);
                feedback.enabled = false;
                Require(!flash.enabled);
                Require(!tracer.enabled);

                Object.DestroyImmediate(wall);
                wall = null;
                var body = targetObject.AddComponent<Rigidbody>();
                var skills = targetObject.AddComponent<SpectrumSkillResponder>();
                targetObject.AddComponent<ColorMixPhysicsReactor>();
                target.ResetColors();
                Physics.SyncTransforms();
                void FireColor(AmmoType type)
                {
                    ammo.LoadChamber(ammo.ActiveChamberIndex, type);
                    SetCooldown(gun, 0f);
                    Require(gun.AttemptFire(), "Merged weapon failed to fire a color round.");
                }
                FireColor(AmmoType.Green);
                Require(skills.IsGreenBound && !skills.IsTethered, "Lone Green should wait for a partner.");
                FireColor(AmmoType.Cyan);
                Equal(ObjectColorState.Green | ObjectColorState.Blue, target.CurrentColorState);
                Require(!body.isKinematic && !body.useGravity && !skills.IsBlueStasis, "Green + Blue must enter the new Cyan suspension.");
                FireColor(AmmoType.Cyan);
                Equal(ObjectColorState.Blue, target.CurrentColorState);
                Require(body.isKinematic && skills.IsBlueStasis, "Another Blue shot must stop the suspended object.");
                target.ResetColors();
                Require(!body.isKinematic && body.useGravity, "Color reset failed to restore physics.");
                FireColor(AmmoType.Red);
                FireColor(AmmoType.Cyan);
                Equal(ObjectColorState.Red | ObjectColorState.Blue, target.CurrentColorState);
                FireColor(AmmoType.Green);
                Equal(ObjectColorState.None, target.CurrentColorState);
                Require(!body.isKinematic && body.useGravity && body.linearVelocity.sqrMagnitude < 0.0001f, "White mix failed to restore the body.");
                return "PASS: held grip/idempotent setup, eight RGB rounds, cooldown/dry fire, shot colors, effects/cover checks, and actual weapon shots through origin Cyan suspension, Blue stop, Magenta and White reset.";
            }
            finally
            {
                Object.Destroy(fixture);
                Object.Destroy(targetObject);
                if (wall != null) Object.Destroy(wall);
            }
        }

        private static void SetReference(Object owner, string name, Object value)
        {
            var serialized = new SerializedObject(owner);
            serialized.FindProperty(name).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetCooldown(SpectrumGunController gun, float value) => typeof(SpectrumGunController)
            .GetField("_nextFireTime", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(gun, value);

        // These checks also run when UNITY_ASSERTIONS is absent from the calling environment.
        private static void Require(bool condition, string message = "Held REV verification failed.")
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Equal<T>(T expected, T actual, string message = "Held REV values differ.") =>
            Require(System.Collections.Generic.EqualityComparer<T>.Default.Equals(expected, actual),
                message + " Expected " + expected + ", got " + actual);
    }
}
