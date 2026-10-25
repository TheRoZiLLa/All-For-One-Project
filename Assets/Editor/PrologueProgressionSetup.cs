using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Recorded.ColorSystem;
using Recorded.Prologue;

namespace Recorded.Editor
{
    public static class PrologueProgressionSetup
    {
        private const string ScenePath = "Assets/Scenes/PROLOGUE.unity";

        [MenuItem("RECORDED/Prologue/Fix Progression & Wire Scene Flow")]
        public static void FixProgressionAndWireSceneFlow()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[PrologueProgressionSetup] Cannot run while in Play Mode.");
                return;
            }

            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.path != ScenePath)
            {
                activeScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            Debug.Log("[PrologueProgressionSetup] Starting progression setup for PROLOGUE.unity...");

            // 1. Locate SectorB_SecurityGate and Puzzle_Blue_AirlockDoors
            var secB = GameObject.Find("SectorB_SecurityGate");
            if (secB == null)
            {
                Debug.LogError("[PrologueProgressionSetup] Could not find SectorB_SecurityGate!");
                return;
            }

            var airlockRoot = GameObject.Find("Puzzle_Blue_AirlockDoors");
            if (airlockRoot == null)
            {
                Debug.LogError("[PrologueProgressionSetup] Could not find Puzzle_Blue_AirlockDoors!");
                return;
            }

            var gate = airlockRoot.GetComponent<KioskCyanAirlockGate>();
            if (gate == null)
                gate = airlockRoot.AddComponent<KioskCyanAirlockGate>();

            var leftDoor = airlockRoot.transform.Find("Puzzle_Blue_AirlockDoors_Left");
            var rightDoor = airlockRoot.transform.Find("Puzzle_Blue_AirlockDoors_Right");

            var gateSo = new SerializedObject(gate);
            gateSo.FindProperty("leftDoor").objectReferenceValue = leftDoor;
            gateSo.FindProperty("rightDoor").objectReferenceValue = rightDoor;
            gateSo.FindProperty("doorOpenDistance").floatValue = 1.8f;
            gateSo.FindProperty("doorOpenSpeed").floatValue = 2.5f;

            if (leftDoor != null)
                gateSo.FindProperty("leftClosedPosition").vector3Value = leftDoor.localPosition;
            if (rightDoor != null)
                gateSo.FindProperty("rightClosedPosition").vector3Value = rightDoor.localPosition;
            gateSo.FindProperty("positionsCached").boolValue = true;
            gateSo.ApplyModifiedProperties();

            // Ensure layer for door colliders
            int interactLayer = LayerMask.NameToLayer("ColorInteractable");
            if (interactLayer < 0) interactLayer = 0;
            if (leftDoor != null) leftDoor.gameObject.layer = interactLayer;
            if (rightDoor != null) rightDoor.gameObject.layer = interactLayer;

            // 2. Setup Security_Kiosk in Sector B
            var kioskObj = GameObject.Find("Security_Kiosk");
            if (kioskObj == null)
            {
                kioskObj = new GameObject("Security_Kiosk");
                kioskObj.transform.SetParent(secB.transform, false);
                Undo.RegisterCreatedObjectUndo(kioskObj, "Create Security_Kiosk");
            }

            kioskObj.transform.position = new Vector3(-1.85f, 0.85f, 36.60f);
            kioskObj.transform.rotation = Quaternion.Euler(0f, 135f, 0f);

            var pedestal = kioskObj.transform.Find("Pedestal");
            if (pedestal == null)
            {
                var pObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pObj.name = "Pedestal";
                pObj.transform.SetParent(kioskObj.transform, false);
                pObj.transform.localPosition = new Vector3(0f, -0.3f, 0f);
                pObj.transform.localScale = new Vector3(0.55f, 1.1f, 0.45f);
                Object.DestroyImmediate(pObj.GetComponent<Collider>());
                pedestal = pObj.transform;

                var pedMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                pedMat.color = new Color(0.12f, 0.15f, 0.18f);
                pObj.GetComponent<Renderer>().sharedMaterial = pedMat;
            }

            var screen = kioskObj.transform.Find("Screen");
            Renderer screenRend = null;
            if (screen == null)
            {
                var sObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                sObj.name = "Screen";
                sObj.transform.SetParent(kioskObj.transform, false);
                sObj.transform.localPosition = new Vector3(0f, 0.38f, -0.06f);
                sObj.transform.localRotation = Quaternion.Euler(25f, 0f, 0f);
                sObj.transform.localScale = new Vector3(0.48f, 0.32f, 0.05f);
                Object.DestroyImmediate(sObj.GetComponent<Collider>());
                screen = sObj.transform;

                var scrMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                scrMat.color = new Color(1f, 0.45f, 0.1f);
                scrMat.EnableKeyword("_EMISSION");
                scrMat.SetColor("_EmissionColor", new Color(1f, 0.45f, 0.1f) * 2f);
                screenRend = sObj.GetComponent<Renderer>();
                screenRend.sharedMaterial = scrMat;
            }
            else
            {
                screenRend = screen.GetComponent<Renderer>();
            }

            var lightTr = kioskObj.transform.Find("KioskLight");
            Light kLight = null;
            if (lightTr == null)
            {
                var lObj = new GameObject("KioskLight");
                lObj.transform.SetParent(kioskObj.transform, false);
                lObj.transform.localPosition = new Vector3(0f, 0.45f, -0.15f);
                kLight = lObj.AddComponent<Light>();
                kLight.type = LightType.Point;
                kLight.color = new Color(1f, 0.45f, 0.1f);
                kLight.range = 3.5f;
                kLight.intensity = 2.0f;
            }
            else
            {
                kLight = lightTr.GetComponent<Light>();
            }

            var kioskCol = kioskObj.GetComponent<BoxCollider>();
            if (kioskCol == null)
                kioskCol = kioskObj.AddComponent<BoxCollider>();
            kioskCol.isTrigger = true;
            kioskCol.size = new Vector3(2.6f, 2.2f, 2.6f);
            kioskCol.center = new Vector3(0f, 0.2f, 0f);

            var kioskInteraction = kioskObj.GetComponent<SecurityKioskEInteraction>();
            if (kioskInteraction == null)
                kioskInteraction = kioskObj.AddComponent<SecurityKioskEInteraction>();

            var kioskSo = new SerializedObject(kioskInteraction);
            kioskSo.FindProperty("airlockGate").objectReferenceValue = gate;
            kioskSo.FindProperty("playerTag").stringValue = "Player";
            kioskSo.FindProperty("screenRenderer").objectReferenceValue = screenRend;
            kioskSo.FindProperty("kioskLight").objectReferenceValue = kLight;
            kioskSo.ApplyModifiedProperties();

            // 3. Setup Guard 4-Second Loop on FrozenEcho_Gate
            var echoGate = GameObject.Find("FrozenEcho_Gate");
            if (echoGate != null)
            {
                var loopComp = echoGate.GetComponent<The4SecondLoop>();
                if (loopComp == null)
                    echoGate.AddComponent<The4SecondLoop>();
            }

            // 4. Setup Scene 04 Syntax Error Encounter Trigger
            var secC = GameObject.Find("SectorC_GrandAtrium");
            var encounterObj = GameObject.Find("Encounter_Scene04_SyntaxError");
            if (encounterObj == null)
            {
                encounterObj = new GameObject("Encounter_Scene04_SyntaxError");
                if (secC != null) encounterObj.transform.SetParent(secC.transform, false);
                Undo.RegisterCreatedObjectUndo(encounterObj, "Create Encounter_Scene04_SyntaxError");
            }

            encounterObj.transform.position = new Vector3(0f, 1.5f, 42.5f);
            var encCol = encounterObj.GetComponent<BoxCollider>();
            if (encCol == null) encCol = encounterObj.AddComponent<BoxCollider>();
            encCol.isTrigger = true;
            encCol.size = new Vector3(8.0f, 4.0f, 4.0f);

            var encComp = encounterObj.GetComponent<Scene04SyntaxErrorEncounter>();
            if (encComp == null)
                encounterObj.AddComponent<Scene04SyntaxErrorEncounter>();

            // 5. Setup Atrium Dual-Lock Puzzle & Hologram Tutorial
            SetupAtriumDualLockPuzzle(secC);

            // 6. Setup Player Health & Dodge System
            var player = GameObject.Find("Player");
            if (player != null)
            {
                player.tag = "Player";
                var health = player.GetComponent<Recorded.Player.PlayerHealth>();
                if (health == null)
                    health = player.AddComponent<Recorded.Player.PlayerHealth>();
                EditorUtility.SetDirty(player);
            }

            // 7. Setup Scene 01 Intro Cutscene
            var dropZone = GameObject.Find("DropZone");
            if (dropZone != null)
            {
                var intro = dropZone.GetComponent<PrologueIntroCutscene>();
                if (intro == null)
                    dropZone.AddComponent<PrologueIntroCutscene>();
            }

            // Ensure Player.prefab also has PlayerHealth
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Recorded/Prefabs/Player.prefab");
            if (playerPrefab != null)
            {
                playerPrefab.tag = "Player";
                if (playerPrefab.GetComponent<Recorded.Player.PlayerHealth>() == null)
                    playerPrefab.AddComponent<Recorded.Player.PlayerHealth>();
                EditorUtility.SetDirty(playerPrefab);
                AssetDatabase.SaveAssets();
            }

            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            Debug.Log("[PrologueProgressionSetup] SUCCESS: Full progression & Atrium Hologram Tutorial wired and saved!");
        }

        private static void SetupAtriumDualLockPuzzle(GameObject secC)
        {
            if (secC == null) return;

            Transform root = secC.transform.Find("Atrium_DualLock_System");
            if (root == null)
            {
                var rootObj = new GameObject("Atrium_DualLock_System");
                rootObj.transform.SetParent(secC.transform, false);
                root = rootObj.transform;
            }

            // Build Terminal Alpha
            var termAlpha = BuildTerminal(root, "Atrium_Terminal_Alpha", "A", new Vector3(-7.5f, 0.85f, 56.0f), Quaternion.Euler(0f, 45f, 0f));

            // Build Terminal Beta
            var termBeta = BuildTerminal(root, "Atrium_Terminal_Beta", "B", new Vector3(7.5f, 0.85f, 66.0f), Quaternion.Euler(0f, -135f, 0f));

            // Build Area 2 Blast Doors at Z = 78.5
            var blastDoorRoot = root.Find("Atrium_BlastDoors");
            if (blastDoorRoot == null)
            {
                var bdObj = new GameObject("Atrium_BlastDoors");
                bdObj.transform.SetParent(root, false);
                bdObj.transform.position = new Vector3(0f, 0f, 78.5f);
                blastDoorRoot = bdObj.transform;
            }

            Transform lDoor = blastDoorRoot.Find("BlastDoor_Left");
            if (lDoor == null)
            {
                var lObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                lObj.name = "BlastDoor_Left";
                lObj.transform.SetParent(blastDoorRoot, false);
                lObj.transform.localPosition = new Vector3(-1.7f, 2.5f, 0f);
                lObj.transform.localScale = new Vector3(3.4f, 5.0f, 0.35f);
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                mat.color = new Color(0.18f, 0.2f, 0.24f);
                lObj.GetComponent<Renderer>().sharedMaterial = mat;
                lDoor = lObj.transform;
            }

            Transform rDoor = blastDoorRoot.Find("BlastDoor_Right");
            if (rDoor == null)
            {
                var rObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rObj.name = "BlastDoor_Right";
                rObj.transform.SetParent(blastDoorRoot, false);
                rObj.transform.localPosition = new Vector3(1.7f, 2.5f, 0f);
                rObj.transform.localScale = new Vector3(3.4f, 5.0f, 0.35f);
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                mat.color = new Color(0.18f, 0.2f, 0.24f);
                rObj.GetComponent<Renderer>().sharedMaterial = mat;
                rDoor = rObj.transform;
            }

            // Top Lintel Frame
            if (blastDoorRoot.Find("BlastDoor_Frame") == null)
            {
                var fObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                fObj.name = "BlastDoor_Frame";
                fObj.transform.SetParent(blastDoorRoot, false);
                fObj.transform.localPosition = new Vector3(0f, 5.15f, 0f);
                fObj.transform.localScale = new Vector3(7.4f, 0.5f, 0.5f);
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                mat.color = new Color(0.1f, 0.12f, 0.14f);
                fObj.GetComponent<Renderer>().sharedMaterial = mat;
            }

            // Puzzle component
            var puzzle = root.GetComponent<AtriumDualLockPuzzle>();
            if (puzzle == null) puzzle = root.gameObject.AddComponent<AtriumDualLockPuzzle>();
            puzzle.SetReferences(termAlpha, termBeta, lDoor, rDoor);

            // Tutorial guide component
            var tut = root.GetComponent<AtriumHologramTutorial>();
            if (tut == null) tut = root.gameObject.AddComponent<AtriumHologramTutorial>();

            var tutSo = new SerializedObject(tut);
            tutSo.FindProperty("puzzle").objectReferenceValue = puzzle;
            tutSo.FindProperty("terminalAlpha").objectReferenceValue = termAlpha.transform;
            tutSo.FindProperty("terminalBeta").objectReferenceValue = termBeta.transform;
            tutSo.FindProperty("exitDoor").objectReferenceValue = blastDoorRoot;
            tutSo.ApplyModifiedProperties();
        }

        private static AtriumDualLockTerminal BuildTerminal(Transform parent, string name, string id, Vector3 pos, Quaternion rot)
        {
            Transform tTr = parent.Find(name);
            GameObject tObj;
            if (tTr == null)
            {
                tObj = new GameObject(name);
                tObj.transform.SetParent(parent, false);
            }
            else
            {
                tObj = tTr.gameObject;
            }

            tObj.transform.position = pos;
            tObj.transform.rotation = rot;

            // Pedestal
            var pedTr = tObj.transform.Find("Pedestal");
            if (pedTr == null)
            {
                var p = GameObject.CreatePrimitive(PrimitiveType.Cube);
                p.name = "Pedestal";
                p.transform.SetParent(tObj.transform, false);
                p.transform.localPosition = new Vector3(0f, -0.3f, 0f);
                p.transform.localScale = new Vector3(0.6f, 1.1f, 0.5f);
                Object.DestroyImmediate(p.GetComponent<Collider>());
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                mat.color = new Color(0.14f, 0.16f, 0.2f);
                p.GetComponent<Renderer>().sharedMaterial = mat;
            }

            // Screen
            Renderer scrRend = null;
            var scrTr = tObj.transform.Find("Screen");
            if (scrTr == null)
            {
                var s = GameObject.CreatePrimitive(PrimitiveType.Cube);
                s.name = "Screen";
                s.transform.SetParent(tObj.transform, false);
                s.transform.localPosition = new Vector3(0f, 0.38f, -0.06f);
                s.transform.localRotation = Quaternion.Euler(25f, 0f, 0f);
                s.transform.localScale = new Vector3(0.5f, 0.32f, 0.05f);
                Object.DestroyImmediate(s.GetComponent<Collider>());
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                mat.color = new Color(1f, 0.45f, 0.1f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(1f, 0.45f, 0.1f) * 2f);
                scrRend = s.GetComponent<Renderer>();
                scrRend.sharedMaterial = mat;
            }
            else
            {
                scrRend = scrTr.GetComponent<Renderer>();
            }

            // Light
            Light kLight = null;
            var lTr = tObj.transform.Find("TerminalLight");
            if (lTr == null)
            {
                var lObj = new GameObject("TerminalLight");
                lObj.transform.SetParent(tObj.transform, false);
                lObj.transform.localPosition = new Vector3(0f, 0.5f, -0.15f);
                kLight = lObj.AddComponent<Light>();
                kLight.type = LightType.Point;
                kLight.color = new Color(1f, 0.45f, 0.1f);
                kLight.range = 3.5f;
                kLight.intensity = 2.0f;
            }
            else
            {
                kLight = lTr.GetComponent<Light>();
            }

            // Terminal component
            var comp = tObj.GetComponent<AtriumDualLockTerminal>();
            if (comp == null) comp = tObj.AddComponent<AtriumDualLockTerminal>();
            comp.SetVisualComponents(scrRend, kLight);

            var so = new SerializedObject(comp);
            so.FindProperty("terminalId").stringValue = id;
            so.FindProperty("interactRadius").floatValue = 2.6f;
            so.FindProperty("activeDuration").floatValue = 5.0f;
            so.ApplyModifiedProperties();

            return comp;
        }
    }
}
