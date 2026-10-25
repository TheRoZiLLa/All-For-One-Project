using System;
using System.Reflection;
using Recorded.ColorSystem;
using Recorded.Phase;
using Recorded.Weapons;
using Recorded.Weapons.Ammo;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Recorded.Testing.Editor
{
    public static class PhaseRecorderVerificationTests
    {
        private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Call(object target, string name, params object[] values) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, values);

        public static string VerifyInPlayMode()
        {
            if (!Application.isPlaying) throw new Exception("Play Mode required.");
            Require(!PhaseRecorder.IsWorldPaused, "Another recorder is already active.");
            var fixture = new GameObject("Phase recorder verification");
            fixture.SetActive(false);
            var player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Recorded/Prefabs/Player.prefab"), fixture.transform);
            player.transform.position = Vector3.one * 4000f;
            foreach (var camera in player.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
            foreach (var listener in player.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
            foreach (var behaviour in player.GetComponents<MonoBehaviour>()) behaviour.enabled = false;
            var gun = player.GetComponentInChildren<SpectrumGunController>(true);
            gun.enabled = false;
            var ammo = fixture.AddComponent<CylinderAmmoController>();
            ammo.enabled = false;
            var rotation = fixture.AddComponent<CylinderRotationController>();
            rotation.enabled = false;
            Set(gun, "ammoController", ammo); Set(gun, "rotationController", rotation); Set(rotation, "ammoController", ammo);
            var recorder = player.GetComponent<PhaseRecorder>();
            if (recorder == null) recorder = player.AddComponent<PhaseRecorder>();
            recorder.enabled = true;
            Set(recorder, "hologramPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/PhaseHologram.prefab"));
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/VFX/PhaseWorld.mat");
            Require(material != null, "World hologram material missing.");
            Set(recorder, "worldHologramMaterial", material);
            var targetObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var cameraTransform = player.transform.Find("CameraPivot/MainCamera");
            targetObject.transform.position = cameraTransform.position + cameraTransform.forward * 8f;
            targetObject.transform.localScale = Vector3.one * 2f;
            var target = targetObject.AddComponent<RGBColorTarget>();
            var body = targetObject.AddComponent<Rigidbody>();
            body.useGravity = false;
            var gate = targetObject.AddComponent<KioskCyanAirlockGate>();
            try
            {
                fixture.SetActive(true);
                // The prefab may acquire its own authored ammo later; keep this fixture isolated.
                Set(gun, "ammoController", ammo); Set(gun, "rotationController", rotation);
                SetCooldown(gun);
                for (int i = 0; i < CylinderAmmoController.CylinderCapacity; i++) ammo.LoadChamber(i, AmmoType.Red);
                ammo.SetActiveChamber(0); rotation.SyncToActiveChamber();
                body.linearVelocity = new Vector3(1f, 2f, 3f);
                var originalMaterial = targetObject.GetComponent<Renderer>().sharedMaterial;
                var start = player.transform.position;
                Physics.SyncTransforms();
                Require(recorder.StartRecording(), "Could not enter hologram world.");
                Require(PhaseRecorder.IsWorldPaused && body.isKinematic, "Main-world physics was not held.");
                Require(targetObject.GetComponent<Renderer>().sharedMaterial == material, "World did not become holographic.");
                Require(gun.AttemptFire(), "Recording did not accept a live weapon shot.");
                Require(target.CurrentColorState == ObjectColorState.None, "Recording changed the real target.");
                Require(ammo.GetChamberAmmo(0) == AmmoType.Empty, "Preview shot did not use its temporary chamber.");
                recorder.RecordKiosk(gate);
                Call(recorder, "TickRecording", 0.3f);
                player.transform.position += Vector3.right * 2f;
                recorder.StopRecording();
                Require(recorder.HasRecording && recorder.IsRewinding && !recorder.IsReplaying, "Take did not wait for manual playback.");
                Call(recorder, "TickRewind", 100f);
                Require(!PhaseRecorder.IsWorldPaused && !recorder.IsReplaying, "Returning auto-started playback or left the world paused.");
                Require(Vector3.Distance(player.transform.position, start) < 0.001f, "Player did not return to entry point.");
                Require(!player.GetComponent<FirstPersonMotor>().enabled, "Return enabled an originally disabled motor.");
                Require(!player.GetComponent<FirstPersonLook>().enabled, "Return enabled an originally disabled look.");
                Require(ammo.GetChamberAmmo(0) == AmmoType.Red && ammo.ActiveChamberIndex == 0 && rotation.CurrentAngle == 0f, "Ammo/index did not restore after recording.");
                Require(targetObject.GetComponent<Renderer>().sharedMaterial == originalMaterial && !body.isKinematic && body.linearVelocity == new Vector3(1f, 2f, 3f), "World materials/physics failed to restore.");
                Require(!gate.IsKioskUnlocked, "Recorded kiosk applied before playback.");
                Physics.SyncTransforms();
                Require(recorder.PlayRecording(), "Saved take would not play.");
                Require(!recorder.PlayRecording(), "Duplicate playback was allowed.");
                Call(recorder, "TickReplay", 0.5f);
                Require(gate.IsKioskUnlocked && target.CurrentColorState == ObjectColorState.Red, "Playback failed to deliver the kiosk action and shot.");
                Require(ammo.GetChamberAmmo(0) == AmmoType.Red && ammo.ActiveChamberIndex == 0, "Playback spent the player's ammo.");
                Call(recorder, "TickReplay", 100f);
                Require(!recorder.IsReplaying && !HologramPresence.IsLive, "Completed hologram kept enemy aggro.");
                int savedCount = recorder.Frames.Count;
                Require(recorder.StartRecording(), "Could not record a second take.");
                Call(recorder, "TickRecording", 0.1f);
                recorder.CancelRecording(); Call(recorder, "TickRewind", 100f);
                Require(recorder.Frames.Count == savedCount, "Cancel erased the previous saved take.");
                Require(recorder.StartRecording(), "Could not start disable recovery test.");
                player.transform.position += Vector3.right;
                recorder.enabled = false;
                Require(!PhaseRecorder.IsWorldPaused && Vector3.Distance(player.transform.position, start) < 0.001f
                    && targetObject.GetComponent<Renderer>().sharedMaterial == originalMaterial && !body.isKinematic,
                    "Disabling the recorder leaked the preview world or player pose.");
                recorder.enabled = true;
                Set(recorder, "maxRecordSeconds", 1f);
                Require(recorder.StartRecording(), "Could not start sanity test.");
                Call(recorder, "TickRecording", 2f);
                Require(!recorder.IsRecording && recorder.IsRewinding && recorder.Sanity == 0f, "Sanity exhaustion did not take control.");
                Call(recorder, "TickRewind", 1f);
                Require(recorder.IsRewinding, "Possession released controls too early.");
                Call(recorder, "TickRewind", 2f); Call(recorder, "TickRewind", 100f);
                Require(!PhaseRecorder.IsWorldPaused && recorder.Frames.Count == savedCount && !recorder.StartRecording(), "Overrun corrupted the saved take or allowed immediate re-entry.");
                return "PASS: hologram world/materials and main-world physics isolation; actual gun shots and kiosk replay; manual start; ammo/pose restoration; duplicate playback rejection; cancellation preserves the take; sanity overrun/control loss/re-entry lock.";
            }
            finally
            {
                recorder.enabled = false;
                fixture.SetActive(false);
                Object.Destroy(fixture);
                Object.Destroy(targetObject);
                Require(!PhaseRecorder.IsWorldPaused, "Fixture left global world pause active.");
            }
        }

        private static void SetCooldown(SpectrumGunController gun) => Set(gun, "_nextFireTime", 0f);
    }
}
