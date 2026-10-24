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
            var ammo = fixture.AddComponent<CylinderAmmoController>();
            ammo.enabled = false;
            var rotation = fixture.AddComponent<CylinderRotationController>();
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
                yield return null;
                for (int i = 0; i < 6; i++) ammo.LoadChamber(i, AmmoType.Red);
                var inner = hud.InnerCylinderRect;
                Require(inner != null);
                Require(gun.TriggerReload());
                yield return new WaitForSeconds(0.3f);
                animator.speed = 0f;
                yield return null;
                Require(inner.anchoredPosition.x < -1f, "HUD cylinder did not swing out with the weapon.");
                for (int i = 0; i < 6; i++) Require(!inner.Find("Chamber_" + i + "/Cartridge_Base").gameObject.activeSelf, "HUD did not show ejected rounds.");
                var pausedPosition = inner.anchoredPosition;
                var pausedRotation = inner.localRotation;
                float pausedProgress = gun.ReloadProgress;
                yield return new WaitForSeconds(1.2f);
                Equal(0, refills, "Paused animation still refilled ammo on a timer.");
                Require(gun.IsReloading && !gun.ReloadAmmoLoaded);
                Equal(pausedPosition, inner.anchoredPosition);
                Require(Quaternion.Angle(pausedRotation, inner.localRotation) < 0.1f);
                Require(Mathf.Abs(gun.ReloadProgress - pausedProgress) < 0.0001f);
                animator.speed = 1f;
                float deadline = Time.time + 5f;
                while (refills == 0 && Time.time < deadline) yield return null;
                Equal(1, refills);
                Require(gun.ReloadAmmoLoaded && gun.IsReloading);
                yield return null;
                for (int i = 0; i < 6; i++) Require(inner.Find("Chamber_" + i + "/Cartridge_Base").gameObject.activeSelf, "HUD did not show inserted rounds.");
                while (gun.IsReloading && Time.time < deadline) yield return null;
                yield return null;
                Require(!gun.IsReloading);
                Require(inner.anchoredPosition.magnitude < 0.001f, "HUD failed to close.");
                Require(Mathf.Abs(Mathf.DeltaAngle(inner.localEulerAngles.z, rotation.CurrentAngle)) < 0.1f, "Reload spin remained on the logical cylinder index.");
                for (int i = 0; i < 6; i++) Equal(Vector3.one, inner.Find("Chamber_" + i + "/Cartridge_Base").localScale);
                rotation.RotateChamberStep(1);
                yield return new WaitForSeconds(0.2f);
                Require(Mathf.Abs(Mathf.DeltaAngle(inner.localEulerAngles.z, 60f)) < 0.1f, "Manual rotation desynchronized after reload.");
                SetCooldown(gun, 0f);
                Require(gun.AttemptFire());
                yield return new WaitForSeconds(0.2f);
                Equal(AmmoType.Empty, ammo.GetChamberAmmo(1));
                Require(Mathf.Abs(Mathf.DeltaAngle(inner.localEulerAngles.z, 120f)) < 0.1f, "Shot auto-advance desynchronized after reload.");
                Require(gun.TriggerReload());
                yield return new WaitForSeconds(0.3f);
                gun.enabled = false;
                yield return null;
                Require(!gun.IsReloading && inner.anchoredPosition.magnitude < 0.001f, "Cancellation left the HUD open.");
                completed("PASS: HUD follows physical cylinder opening/spin, hides ejected rounds, loads at Animator phase, freezes with paused animation, closes/cancels cleanly and preserves manual/shot indexing.");
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
                for (int i = 0; i < 6; i++) Require(ammo.GetChamberAmmo(i) != AmmoType.Empty);
                input.enabled = false;
                for (int i = 0; i < 6; i++) ammo.LoadChamber(i, AmmoType.Empty);
                PressR(true);
                Require(gun.IsReloading && refills == 1 && !ammo.HasActiveRound, "Fallback R bypassed the animation.");
                gun.enabled = false;
                Require(!gun.IsReloading, "Cancelling reload left the weapon locked.");
                yield return new WaitForSeconds(1.1f);
                Equal(1, refills, "Cancelled reload still filled ammo.");
                gun.enabled = true;
                PressR(true);
                Require(gun.IsReloading && !ammo.HasActiveRound, "Reload could not restart after cancellation.");
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
                    }
                    Require(enteredReload, "Reload trigger did not enter the animation.");
                    Require(maxTravel > 0.04f && maxTravel < 0.12f, "Cylinder traveled too far or never opened: " + maxTravel);
                    Require(animator.GetCurrentAnimatorStateInfo(0).IsName("Ready"));
                    Require(Vector3.Distance(restPosition, barrel.localPosition) * scale < 0.001f);
                    Require(Quaternion.Angle(restRotation, barrel.localRotation) < 0.1f);
                    animator.Play("Reload", 0, 0.4f);
                    animator.Update(0f);
                    animator.Play("Fire", 0, 0f);
                    animator.Update(0f);
                    Require(Vector3.Distance(restPosition, barrel.localPosition) * scale < 0.001f, "Fire retained an open cylinder pose.");
                    Require(Quaternion.Angle(restRotation, barrel.localRotation) < 0.1f);
                }
                return "PASS: reload hinge/center pivot, bounded travel and smooth steps at 30/60/144 FPS; correct prefab seat restored by Ready and Fire.";
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
            var lights = gun.GetComponentsInChildren<Light>(true);
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
                    for (int i = 0; i < 6; i++) ammo.LoadChamber(i, AmmoType.Red);
                    float step = 1f / fps;
                    int shots = 0;
                    float peakAngle = 0f;
                    var previousRotation = raycast.MuzzlePoint.rotation;
                    var previousPosition = raycast.MuzzlePoint.position;
                    for (int frame = 0; frame <= fps * 2; frame++)
                    {
                        if (shots < 6 && frame * step + 0.0001f >= shots * 0.2f)
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
                    Equal(6, shots);
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
                return "PASS: six-shot re-trigger/recovery at 30/60/144 FPS, live-shot-only animation, preserved grip/camera/glow, independent wall retraction and Animator reset.";
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

                var types = new[] { AmmoType.Red, AmmoType.Green, AmmoType.Cyan, AmmoType.Red, AmmoType.Green, AmmoType.Cyan };
                for (int i = 0; i < types.Length; i++) ammo.LoadChamber(i, types[i]);
                int hits = 0;
                raycast.OnTargetHit += hit => { Equal(targetObject.GetComponent<Collider>(), hit.collider); hits++; };
                for (int i = 0; i < types.Length; i++)
                {
                    SetCooldown(gun, 0f);
                    target.ResetColors();
                    Require(gun.AttemptFire());
                    Equal(AmmoType.Empty, ammo.GetChamberAmmo(i));
                    Equal((i + 1) % 6, ammo.ActiveChamberIndex);
                    var expected = types[i] == AmmoType.Red ? ObjectColorState.Red : types[i] == AmmoType.Green ? ObjectColorState.Green : ObjectColorState.Blue;
                    Equal(expected, target.CurrentColorState);
                    gun.SyncWithActiveChamber();
                    Require(flash.enabled);
                    Equal(SpectrumGunController.GetAmmoColor(types[i]), flash.color, "A fired flash keeps its color after the cylinder advances.");
                    Require(!gun.AttemptFire(), "Cooldown must prevent a second shot.");
                }
                Equal(6, hits);
                SetCooldown(gun, 0f);
                Require(!gun.AttemptFire());
                Equal(6, hits);
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
                Require(Vector4.Distance(SpectrumGunController.GetAmmoColor(AmmoType.Cyan), sheath.sharedMaterial.color) < 0.001f, "Cylinder advance recolored the energy sheath.");
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
                return "PASS: held grip/idempotent setup, six RGB rounds and auto-advance, cooldown/dry fire, shot colors, world-space effects/cleanup, camera-to-muzzle wall obstruction.";
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
