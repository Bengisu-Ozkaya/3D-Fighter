using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

public class AnimationFixer
{
    [MenuItem("Tools/3D Fighter/Yumruk ve Head Hit Ayarlarini Guncelle")]
    public static void ApplyAllFixes()
    {
        ConfigureClip("Assets/Fighter Animation/Left.fbx", 18f);
        ConfigureClip("Assets/Fighter Animation/Right.fbx", 10f);
        ConfigureClip("Assets/Fighter Animation/Uppercut.fbx", 0f);
        ConfigureClip("Assets/Fighter Animation/Head Hit.fbx", 0f);
        ConfigureClip("Assets/Fighter Animation/Center Block.fbx", 0f);
        ConfigureClip("Assets/Fighter Animation/Ulti.fbx", 14f);
        ConfigureClip("Assets/Fighter Animation/Left Block.fbx", 0f);
        ConfigureClip("Assets/Fighter Animation/Right Block.fbx", 0f);
        ConfigureClip("Assets/Fighter Animation/Body Hit.fbx", 0f);

        // 2. Animator Controller dosyalarını güncelle
        EnsureHeadHitInController("Assets/Fighter Animation/Idle.controller");
        EnsureHeadHitInController("Assets/Fighter Animation/Enemy.controller");
        EnsureUltiInController("Assets/Fighter Animation/Idle.controller");
        EnsureUppercutInController("Assets/Fighter Animation/Idle.controller");
        EnsureUppercutInController("Assets/Fighter Animation/Enemy.controller");
        FixShowPoseInController("Assets/Fighter Animation/Idle.controller");
        FixShowPoseInController("Assets/Fighter Animation/Enemy.controller");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("<color=green>[3D Fighter]</color> Yumruk doğrultuları, Head Hit ve animasyon geçiş ayarları başarıyla güncellendi!");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // ORTIZ ANİMASYON ENTEGRASYONU
    // ─────────────────────────────────────────────────────────────────────────────
    [MenuItem("Tools/3D Fighter/Ortiz Animasyonlarini Entegre Et (Player Controller)")]
    public static void IntegrateOrtizAnimations()
    {
        const string controllerPath = "Assets/Fighter Animation/Idle.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            Debug.LogError("[Ortiz] Idle.controller bulunamadı: " + controllerPath);
            return;
        }

        // Ortiz FBX yolları ve animasyon clip GUIDleri
        var ortizClips = new System.Collections.Generic.Dictionary<string, string>
        {
            { "Block Idle",      "Assets/Ortiz/Block Idle.fbx" },
            { "Uppercut",        "Assets/Ortiz/Uppercut.fbx" },
            { "Left Pivot",      "Assets/Ortiz/Left Pivot.fbx" },
            { "Right Pivot",     "Assets/Ortiz/Right Pivot.fbx" },
            { "Left Punch",      "Assets/Ortiz/Left Punch.fbx" },
            { "Right Punch",     "Assets/Ortiz/Right Punch.fbx" },
            { "Uppercut Nakavt", "Assets/Ortiz/Uppercut Nakavt.fbx" },
            { "Right Damage",    "Assets/Ortiz/Right Damage.fbx" },
            { "Left Damage",     "Assets/Ortiz/Right Damage.fbx" },  // Mirror edilmiş versiyon
            { "Right Step",      "Assets/Ortiz/Right Step.fbx" },
            { "Left Step",       "Assets/Ortiz/Left Step.fbx" },
            { "Backward Step",   "Assets/Ortiz/Backward Step.fbx" },
            { "Forward Step",    "Assets/Ortiz/Forward Step.fbx" },
            { "Idle",            "Assets/Ortiz/Idle.fbx" },
        };

        var sm = controller.layers[0].stateMachine;

        // Mevcut state'leri topla (isim → state)
        var existingStates = new System.Collections.Generic.Dictionary<string, AnimatorState>();
        foreach (var cs in sm.states)
        {
            if (!existingStates.ContainsKey(cs.state.name))
                existingStates[cs.state.name] = cs.state;
        }

        // Idle state'ini bul veya oluştur (referans point)
        AnimatorState idleState = existingStates.ContainsKey("Idle") ? existingStates["Idle"] : null;

        // Her Ortiz state için: varsa güncelle, yoksa ekle
        var statePositions = new System.Collections.Generic.Dictionary<string, Vector3>
        {
            { "Idle",            new Vector3(300,  30, 0) },
            { "Block Idle",      new Vector3(520, 380, 0) },
            { "Uppercut",        new Vector3(170, 220, 0) },
            { "Left Pivot",      new Vector3(700, 140, 0) },
            { "Right Pivot",     new Vector3(700, 220, 0) },
            { "Left Punch",      new Vector3(170, 300, 0) },
            { "Right Punch",     new Vector3(170, 380, 0) },
            { "Uppercut Nakavt", new Vector3(520, 460, 0) },
            { "Right Damage",    new Vector3(650, 290, 0) },
            { "Left Damage",     new Vector3(650, 380, 0) },
            { "Right Step",      new Vector3(700, 300, 0) },
            { "Left Step",       new Vector3(700, 380, 0) },
            { "Backward Step",   new Vector3(700, 460, 0) },
            { "Forward Step",    new Vector3(700, 540, 0) },
        };

        foreach (var kvp in ortizClips)
        {
            string stateName = kvp.Key;
            string clipPath  = kvp.Value;

            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
            {
                Debug.LogWarning($"[Ortiz] Klip bulunamadı: {clipPath}  (State '{stateName}' atlandı)");
                continue;
            }

            // Left Damage için mirror flag'i uygula
            bool isMirror = (stateName == "Left Damage");

            AnimatorState state;
            if (existingStates.ContainsKey(stateName))
            {
                state = existingStates[stateName];
            }
            else
            {
                Vector3 pos = statePositions.ContainsKey(stateName) ? statePositions[stateName] : new Vector3(800, 300, 0);
                state = sm.AddState(stateName, pos);
                existingStates[stateName] = state;
            }

            state.motion = clip;
            state.mirror = isMirror; // Left Damage = Right Damage klibinin mirror'ı
            state.writeDefaultValues = true;

            // Idle'ı varsayılan state yap
            if (stateName == "Idle")
            {
                idleState = state;
                sm.defaultState = state;
            }
        }

        // FBX import ayarları: Root motion ve orientasyon kilitle
        foreach (var kvp in ortizClips)
        {
            string clipPath = kvp.Value;
            ModelImporter importer = AssetImporter.GetAtPath(clipPath) as ModelImporter;
            if (importer == null) continue;

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
            bool changed = false;
            foreach (var c in clips)
            {
                if (!c.lockRootRotation) { c.lockRootRotation = true; changed = true; }
                if (!c.keepOriginalOrientation) { c.keepOriginalOrientation = true; changed = true; }
                if (!c.lockRootPositionXZ) { c.lockRootPositionXZ = true; changed = true; }
                if (!c.keepOriginalPositionXZ) { c.keepOriginalPositionXZ = true; changed = true; }
                c.lockRootHeightY = true;
            }
            if (changed)
            {
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }
        }

        // Eski state'lerin adlarını güncelle (Center Block → silinir, yenisi Block Idle oldu)
        // Eski state'ler kaldırılamıyor (referanslar var), sadece motion güncellenir
        if (existingStates.ContainsKey("Center Block") && existingStates.ContainsKey("Block Idle"))
        {
            // Center Block state'ini Block Idle klibine yönlendir
            var cb = existingStates["Center Block"];
            var bi = existingStates["Block Idle"];
            cb.motion = bi.motion;
            Debug.Log("[Ortiz] Center Block state'i Block Idle klibine güncellendi.");
        }

        if (existingStates.ContainsKey("Hit") && existingStates.ContainsKey("Right Damage"))
        {
            existingStates["Hit"].motion = existingStates["Right Damage"].motion;
            Debug.Log("[Ortiz] Hit state'i Right Damage klibine güncellendi.");
        }
        if (existingStates.ContainsKey("Head Hit") && existingStates.ContainsKey("Left Damage"))
        {
            existingStates["Head Hit"].motion = existingStates["Left Damage"].motion;
            existingStates["Head Hit"].mirror = true;
            Debug.Log("[Ortiz] Head Hit state'i Left Damage (mirror) klibine güncellendi.");
        }
        if (existingStates.ContainsKey("Knockout") && existingStates.ContainsKey("Uppercut Nakavt"))
        {
            existingStates["Knockout"].motion = existingStates["Uppercut Nakavt"].motion;
            Debug.Log("[Ortiz] Knockout state'i Uppercut Nakavt klibine güncellendi.");
        }
        if (existingStates.ContainsKey("Left Move") && existingStates.ContainsKey("Left Step"))
        {
            existingStates["Left Move"].motion = existingStates["Left Step"].motion;
            Debug.Log("[Ortiz] Left Move state'i Left Step klibine güncellendi.");
        }
        if (existingStates.ContainsKey("Right Move") && existingStates.ContainsKey("Right Step"))
        {
            existingStates["Right Move"].motion = existingStates["Right Step"].motion;
            Debug.Log("[Ortiz] Right Move state'i Right Step klibine güncellendi.");
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("<color=cyan>[Ortiz]</color> Tüm Ortiz animasyonları Idle.controller'a başarıyla entegre edildi! " +
                  "Unity Editor'da Animator Controller'ı açarak geçişleri kontrol et.");
    }

    private static void ConfigureClip(string path, float orientationOffsetY)
    {
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null) return;

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
        {
            clips = importer.defaultClipAnimations;
        }

        foreach (var clip in clips)
        {
            clip.lockRootRotation = true;
            clip.keepOriginalOrientation = true;
            clip.rotationOffset = orientationOffsetY;

            clip.lockRootPositionXZ = true;
            clip.keepOriginalPositionXZ = true;
        }

        importer.clipAnimations = clips;
        importer.SaveAndReimport();
    }

    private static void EnsureHeadHitInController(string controllerPath)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null) return;

        // Parametre ekle
        bool hasParam = false;
        foreach (var p in controller.parameters)
        {
            if (p.name == "GetHeadHit") { hasParam = true; break; }
        }
        if (!hasParam)
        {
            controller.AddParameter("GetHeadHit", AnimatorControllerParameterType.Trigger);
        }

        // State machine
        var sm = controller.layers[0].stateMachine;
        AnimatorState headHitState = null;
        AnimatorState idleState = null;

        foreach (var cs in sm.states)
        {
            if (cs.state.name == "Head Hit") headHitState = cs.state;
            if (cs.state.name == "Idle") idleState = cs.state;
        }

        if (headHitState == null)
        {
            headHitState = sm.AddState("Head Hit", new Vector3(650, 250, 0));
            headHitState.speed = 1.2f;

            // Head Hit.fbx klibini bağla
            AnimationClip headHitClip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Fighter Animation/Head Hit.fbx");
            if (headHitClip != null)
            {
                headHitState.motion = headHitClip;
            }

            // AnyState -> Head Hit
            var anyTrans = sm.AddAnyStateTransition(headHitState);
            anyTrans.AddCondition(AnimatorConditionMode.If, 0, "GetHeadHit");
            anyTrans.AddCondition(AnimatorConditionMode.IfNot, 0, "isDead");
            anyTrans.duration = 0.05f;
            anyTrans.hasExitTime = false;
            anyTrans.hasFixedDuration = true;
            anyTrans.canTransitionToSelf = true;

            // Head Hit -> Idle
            if (idleState != null)
            {
                var toIdle = headHitState.AddTransition(idleState);
                toIdle.hasExitTime = true;
                toIdle.exitTime = 0.8f;
                toIdle.duration = 0.15f;
                toIdle.hasFixedDuration = true;
                toIdle.canTransitionToSelf = true;
            }
        }

        EditorUtility.SetDirty(controller);
    }

    private static void EnsureUltiInController(string controllerPath)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null) return;

        var sm = controller.layers[0].stateMachine;
        AnimatorState ultiState = null;

        foreach (var cs in sm.states)
        {
            if (cs.state.name == "Ulti")
            {
                ultiState = cs.state;
                break;
            }
        }

        if (ultiState == null)
        {
            ultiState = sm.AddState("Ulti", new Vector3(520, 290, 0));
            ultiState.speed = 1.15f;

            AnimationClip ultiClip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Fighter Animation/Ulti.fbx");
            if (ultiClip != null)
            {
                ultiState.motion = ultiClip;
            }
        }

        EditorUtility.SetDirty(controller);
    }

    private static void EnsureUppercutInController(string controllerPath)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null) return;

        var sm = controller.layers[0].stateMachine;
        AnimatorState uppercutState = null;
        AnimatorState idleState = null;

        foreach (var cs in sm.states)
        {
            if (cs.state.name == "Uppercut") uppercutState = cs.state;
            if (cs.state.name == "Idle") idleState = cs.state;
        }

        if (uppercutState != null && idleState != null)
        {
            bool hasIdleTransition = false;
            foreach (var t in uppercutState.transitions)
            {
                if (t.destinationState == idleState)
                {
                    hasIdleTransition = true;
                    break;
                }
            }

            if (!hasIdleTransition)
            {
                var toIdle = uppercutState.AddTransition(idleState);
                toIdle.hasExitTime = true;
                toIdle.exitTime = 0.85f;
                toIdle.duration = 0.15f;
                toIdle.hasFixedDuration = true;
                toIdle.canTransitionToSelf = true;
            }
        }

        EditorUtility.SetDirty(controller);
    }

    private static void FixShowPoseInController(string controllerPath)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null) return;

        var sm = controller.layers[0].stateMachine;
        AnimatorState showPoseState = null;
        AnimatorState idleState = null;

        foreach (var cs in sm.states)
        {
            if (cs.state.name == "Show Pose") showPoseState = cs.state;
            if (cs.state.name == "Idle") idleState = cs.state;
        }

        if (showPoseState != null && idleState != null)
        {
            foreach (var t in showPoseState.transitions)
            {
                if (t.destinationState == idleState)
                {
                    foreach (var cond in t.conditions)
                    {
                        if (cond.parameter == "isDeadEnemy" && cond.mode == AnimatorConditionMode.IfNot)
                        {
                            t.hasExitTime = false;
                            t.duration = 0.15f;
                            break;
                        }
                    }
                }
            }
        }

        EditorUtility.SetDirty(controller);
    }

    [MenuItem("Tools/3D Fighter/Saha ve Direk Sinirlarini Olustur (RingBoundary)")]
    public static void SetupRingBoundaryInScene()
    {
        RingBoundary existing = Object.FindObjectOfType<RingBoundary>();
        if (existing == null)
        {
            GameObject go = new GameObject("RingBoundary");
            existing = go.AddComponent<RingBoundary>();
            Undo.RegisterCreatedObjectUndo(go, "Create RingBoundary");
        }
        existing.InitializeArena();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        Debug.Log("<color=green>[3D Fighter]</color> RingBoundary (Saha ve Direk Sınır Sistemi) sahneye başarıyla eklendi ve ayarlandı!");
    }

    [MenuItem("Tools/3D Fighter/Saha ve Cevreleri Yapilandir (Environment 1 ve 2)")]
    public static void SetupEnvironmentsInScene()
    {
        RingBoundary existing = Object.FindObjectOfType<RingBoundary>();
        if (existing == null)
        {
            GameObject go = new GameObject("RingBoundary");
            existing = go.AddComponent<RingBoundary>();
            Undo.RegisterCreatedObjectUndo(go, "Create RingBoundary");
        }
        existing.AutoSetupEnvironmentsIfNeeded();
        existing.SwitchEnvironment(0);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        Debug.Log("<color=green>[3D Fighter]</color> Environment 1 ve Environment 2 sahaları, dairesel sınırları ve spawn noktaları başarıyla yapılandırıldı!");
    }
}
