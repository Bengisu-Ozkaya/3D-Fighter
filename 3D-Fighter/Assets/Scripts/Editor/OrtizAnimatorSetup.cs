using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;
using System.Linq;

public class OrtizAnimatorSetup
{
    private const string CONTROLLER_PATH = "Assets/Ortiz/Idle.controller";

    [MenuItem("Tools/Ortiz/Tam Animasyon ve Animator Kurulumu (Sıfırdan)")]
    public static void BuildCompleteAnimator()
    {
        Debug.Log("<color=yellow>[OrtizSetup]</color> Ortiz kurulumu başlatılıyor...");

        // 1. FBX İçe Aktarma Ayarlarını Yapılandır (Loop, Bake into Pose, In-Place)
        ConfigureFbxSettings();

        // 2. Animator Controller'ı Sıfırdan Oluştur
        AnimatorController controller = CreateOrResetController();

        // 3. Parametreleri Tanımla
        SetupParameters(controller);

        // 4. Tüm Stateleri ve Klipleri Bağla
        SetupStatesAndTransitions(controller);

        // 5. Sahnedeki Oyuncu Nesnesine Controller'ı Ata
        AssignToPlayer(controller);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("<color=green>[OrtizSetup] BAŞARILI!</color> Tüm Ortiz animasyonları, geçişler, mirror ayarları ve loop optimizasyonları sıfırdan kusursuz bir şekilde tamamlandı!");
    }

    private static void ConfigureFbxSettings()
    {
        // 4 Adım animasyonu için loop ve root motion kilitleme ayarları
        string[] stepFiles = new string[]
        {
            "Assets/Ortiz/Forward Step.fbx",
            "Assets/Ortiz/Backward Step.fbx",
            "Assets/Ortiz/Left Step.fbx",
            "Assets/Ortiz/Right Step.fbx"
        };

        foreach (var path in stepFiles)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) continue;

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;

            foreach (var clip in clips)
            {
                clip.loopTime = true;
                clip.loopPose = true;

                clip.lockRootRotation = true;
                clip.keepOriginalOrientation = true;

                clip.lockRootHeightY = true;
                clip.keepOriginalPositionY = true;

                // XZ Root hareketini pose içine kilitle (In-place adım döngüsü için)
                clip.lockRootPositionXZ = true;
                clip.keepOriginalPositionXZ = false; // Body Position / Center of mass baz alınır; ileri sıçrama engellenir
            }

            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            Debug.Log($"[OrtizSetup] Adım FBX yapılandırıldı: {path}");
        }

        // Idle ve Block Idle döngü ayarları
        string[] loopFiles = new string[]
        {
            "Assets/Ortiz/Idle.fbx",
            "Assets/Ortiz/Block Idle.fbx"
        };

        foreach (var path in loopFiles)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) continue;

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;

            foreach (var clip in clips)
            {
                clip.loopTime = true;
                clip.loopPose = true;
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
            }

            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        // Tek seferlik (non-looping) aksiyon klipleri
        string[] oneShotFiles = new string[]
        {
            "Assets/Ortiz/Blocking.fbx",
            "Assets/Ortiz/Left Punch.fbx",
            "Assets/Ortiz/Right Punch.fbx",
            "Assets/Ortiz/Uppercut.fbx",
            "Assets/Ortiz/Uppercut Nakavt.fbx",
            "Assets/Ortiz/Right Damage.fbx",
            "Assets/Ortiz/Kip Up.fbx",
            "Assets/Ortiz/Show Pose.fbx",
            "Assets/Ortiz/Ulti.fbx"
        };

        foreach (var path in oneShotFiles)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) continue;

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;

            foreach (var clip in clips)
            {
                clip.loopTime = false; // Tek seferlik aksiyonlar loop yapmaz
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
                clip.keepOriginalPositionXZ = false; // Yerinde sabit vuruş (in-place)
            }

            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }
    }

    private static AnimatorController CreateOrResetController()
    {
        if (File.Exists(CONTROLLER_PATH))
        {
            AssetDatabase.DeleteAsset(CONTROLLER_PATH);
        }

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(CONTROLLER_PATH);
        return controller;
    }

    private static void SetupParameters(AnimatorController controller)
    {
        // Hareket Parametreleri (Bools)
        controller.AddParameter("Forward_Move", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Backward_Move", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Left_Move", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Right_Move", AnimatorControllerParameterType.Bool);

        // Blok Durumu (Bool)
        controller.AddParameter("isBlocking", AnimatorControllerParameterType.Bool);

        // Vuruş ve Aksiyon Tetikleyicileri (Triggers)
        controller.AddParameter("PunchLeft", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("PunchRight", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Uppercut", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Ulti", AnimatorControllerParameterType.Trigger);

        // Hasar ve Nakavt Parametreleri
        controller.AddParameter("GetHit", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("GetHeadHit", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("isDead", AnimatorControllerParameterType.Bool);
        controller.AddParameter("isDeadEnemy", AnimatorControllerParameterType.Bool);
        controller.AddParameter("KipUp", AnimatorControllerParameterType.Trigger);
    }

    private static AnimationClip LoadClip(string fbxPath)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        if (assets == null || assets.Length == 0) return null;

        foreach (var asset in assets)
        {
            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
            {
                return clip;
            }
        }
        return null;
    }

    private static void SetupStatesAndTransitions(AnimatorController controller)
    {
        var sm = controller.layers[0].stateMachine;

        // 1. Idle State (Merkez / Varsayılan)
        AnimationClip idleClip = LoadClip("Assets/Ortiz/Idle.fbx");
        AnimatorState idleState = sm.AddState("Idle", new Vector3(300, 100, 0));
        idleState.motion = idleClip;
        sm.defaultState = idleState;

        // 2. Hareket Stateleri (WASD)
        AnimatorState fwdState = sm.AddState("Forward Step", new Vector3(300, -80, 0));
        fwdState.motion = LoadClip("Assets/Ortiz/Forward Step.fbx");
        BindMovementTransition(idleState, fwdState, "Forward_Move");

        AnimatorState backState = sm.AddState("Backward Step", new Vector3(300, 280, 0));
        backState.motion = LoadClip("Assets/Ortiz/Backward Step.fbx");
        BindMovementTransition(idleState, backState, "Backward_Move");

        AnimatorState leftState = sm.AddState("Left Step", new Vector3(80, 100, 0));
        leftState.motion = LoadClip("Assets/Ortiz/Left Step.fbx");
        BindMovementTransition(idleState, leftState, "Left_Move");

        AnimatorState rightState = sm.AddState("Right Step", new Vector3(520, 100, 0));
        rightState.motion = LoadClip("Assets/Ortiz/Right Step.fbx");
        BindMovementTransition(idleState, rightState, "Right_Move");

        // 3. Blok Sistemi (Blocking -> Block Idle -> Idle)
        AnimatorState blockingState = sm.AddState("Blocking", new Vector3(520, -10, 0));
        blockingState.motion = LoadClip("Assets/Ortiz/Blocking.fbx");

        AnimatorState blockIdleState = sm.AddState("Block Idle", new Vector3(720, -10, 0));
        blockIdleState.motion = LoadClip("Assets/Ortiz/Block Idle.fbx");

        // Idle -> Blocking (isBlocking == true)
        var toBlocking = idleState.AddTransition(blockingState);
        toBlocking.hasExitTime = false;
        toBlocking.duration = 0.08f;
        toBlocking.hasFixedDuration = true;
        toBlocking.AddCondition(AnimatorConditionMode.If, 0, "isBlocking");

        // Blocking -> Block Idle (Animasyon bitince otomatik Block Idle'a geçiş)
        var toBlockIdle = blockingState.AddTransition(blockIdleState);
        toBlockIdle.hasExitTime = true;
        toBlockIdle.exitTime = 0.85f;
        toBlockIdle.duration = 0.10f;
        toBlockIdle.hasFixedDuration = true;
        toBlockIdle.AddCondition(AnimatorConditionMode.If, 0, "isBlocking");

        // Block Idle -> Idle (isBlocking == false)
        var blockIdleToIdle = blockIdleState.AddTransition(idleState);
        blockIdleToIdle.hasExitTime = false;
        blockIdleToIdle.duration = 0.12f;
        blockIdleToIdle.hasFixedDuration = true;
        blockIdleToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "isBlocking");

        // Erken bırakılırsa: Blocking -> Idle (isBlocking == false)
        var blockingToIdle = blockingState.AddTransition(idleState);
        blockingToIdle.hasExitTime = false;
        blockingToIdle.duration = 0.10f;
        blockingToIdle.hasFixedDuration = true;
        blockingToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "isBlocking");

        // 4. Yumruklar (Left Punch, Right Punch)
        AnimatorState leftPunchState = sm.AddState("Left Punch", new Vector3(80, -80, 0));
        leftPunchState.motion = LoadClip("Assets/Ortiz/Left Punch.fbx");
        leftPunchState.speed = 1.35f;
        BindAttackTransition(idleState, leftPunchState, "PunchLeft");

        AnimatorState rightPunchState = sm.AddState("Right Punch", new Vector3(520, -80, 0));
        rightPunchState.motion = LoadClip("Assets/Ortiz/Right Punch.fbx");
        rightPunchState.speed = 1.35f;
        BindAttackTransition(idleState, rightPunchState, "PunchRight");

        // Yumruklar arası kombo geçişi (Sağdan Sola, Soldan Sağa)
        var lpToRp = leftPunchState.AddTransition(rightPunchState);
        lpToRp.hasExitTime = false;
        lpToRp.duration = 0.08f;
        lpToRp.hasFixedDuration = true;
        lpToRp.AddCondition(AnimatorConditionMode.If, 0, "PunchRight");

        var rpToLp = rightPunchState.AddTransition(leftPunchState);
        rpToLp.hasExitTime = false;
        rpToLp.duration = 0.08f;
        rpToLp.hasFixedDuration = true;
        rpToLp.AddCondition(AnimatorConditionMode.If, 0, "PunchLeft");

        // 5. Aparkat (Uppercut - E tuşu)
        AnimatorState uppercutState = sm.AddState("Uppercut", new Vector3(80, 200, 0));
        uppercutState.motion = LoadClip("Assets/Ortiz/Uppercut.fbx");
        uppercutState.speed = 1.25f;
        BindAttackTransition(idleState, uppercutState, "Uppercut");

        // 6. Ulti (Ulti - R tuşu)
        AnimatorState ultiState = sm.AddState("Ulti", new Vector3(520, 200, 0));
        ultiState.motion = LoadClip("Assets/Ortiz/Ulti.fbx");
        ultiState.speed = 1.15f;
        BindAttackTransition(idleState, ultiState, "Ulti");

        // 7. Hasar Alma (Right Damage ve Mirror edilmiş Left Damage)
        AnimationClip rightDamageClip = LoadClip("Assets/Ortiz/Right Damage.fbx");

        AnimatorState rightDamageState = sm.AddState("Right Damage", new Vector3(720, 100, 0));
        rightDamageState.motion = rightDamageClip;
        rightDamageState.mirror = false;
        BindDamageTransition(idleState, rightDamageState);

        AnimatorState leftDamageState = sm.AddState("Left Damage", new Vector3(720, 180, 0));
        leftDamageState.motion = rightDamageClip;
        leftDamageState.mirror = true; // Left Damage = Right Damage'ın mirror'ı
        BindDamageTransition(idleState, leftDamageState);

        // 8. Nakavt (Uppercut Nakavt)
        AnimatorState nakavtState = sm.AddState("Uppercut Nakavt", new Vector3(720, 280, 0));
        nakavtState.motion = LoadClip("Assets/Ortiz/Uppercut Nakavt.fbx");

        // AnyState -> Uppercut Nakavt (isDead == true)
        var anyToDead = sm.AddAnyStateTransition(nakavtState);
        anyToDead.hasExitTime = false;
        anyToDead.duration = 0.10f;
        anyToDead.hasFixedDuration = true;
        anyToDead.AddCondition(AnimatorConditionMode.If, 0, "isDead");

        // 9. Ayağa Kalkma (Kip Up - Yeniden Doğ)
        AnimatorState kipUpState = sm.AddState("Kip Up", new Vector3(300, 380, 0));
        kipUpState.motion = LoadClip("Assets/Ortiz/Kip Up.fbx");
        kipUpState.speed = 1.1f;

        // Nakavt -> Kip Up (KipUp trigger)
        var nakavtToKipUp = nakavtState.AddTransition(kipUpState);
        nakavtToKipUp.hasExitTime = false;
        nakavtToKipUp.duration = 0.15f;
        nakavtToKipUp.hasFixedDuration = true;
        nakavtToKipUp.AddCondition(AnimatorConditionMode.If, 0, "KipUp");

        // Kip Up -> Idle (Animasyon bitince)
        var kipUpToIdle = kipUpState.AddTransition(idleState);
        kipUpToIdle.hasExitTime = true;
        kipUpToIdle.exitTime = 0.92f;
        kipUpToIdle.duration = 0.20f;
        kipUpToIdle.hasFixedDuration = true;

        // 10. Zafer Pozu (Show Pose - 10. Dalga sonu)
        AnimatorState showPoseState = sm.AddState("Show Pose", new Vector3(80, 280, 0));
        showPoseState.motion = LoadClip("Assets/Ortiz/Show Pose.fbx");
    }

    private static void BindMovementTransition(AnimatorState idle, AnimatorState moveState, string paramName)
    {
        // Idle -> Move
        var toMove = idle.AddTransition(moveState);
        toMove.hasExitTime = false;
        toMove.duration = 0.12f;
        toMove.hasFixedDuration = true;
        toMove.AddCondition(AnimatorConditionMode.If, 0, paramName);

        // Move -> Idle
        var toIdle = moveState.AddTransition(idle);
        toIdle.hasExitTime = false;
        toIdle.duration = 0.12f;
        toIdle.hasFixedDuration = true;
        toIdle.AddCondition(AnimatorConditionMode.IfNot, 0, paramName);
    }

    private static void BindAttackTransition(AnimatorState idle, AnimatorState attackState, string triggerName)
    {
        // Idle -> Attack
        var toAttack = idle.AddTransition(attackState);
        toAttack.hasExitTime = false;
        toAttack.duration = 0.06f;
        toAttack.hasFixedDuration = true;
        toAttack.AddCondition(AnimatorConditionMode.If, 0, triggerName);

        // Attack -> Idle
        var toIdle = attackState.AddTransition(idle);
        toIdle.hasExitTime = true;
        toIdle.exitTime = 0.82f;
        toIdle.duration = 0.15f;
        toIdle.hasFixedDuration = true;
    }

    private static void BindDamageTransition(AnimatorState idle, AnimatorState damageState)
    {
        var toIdle = damageState.AddTransition(idle);
        toIdle.hasExitTime = true;
        toIdle.exitTime = 0.85f;
        toIdle.duration = 0.15f;
        toIdle.hasFixedDuration = true;
    }

    private static void AssignToPlayer(AnimatorController controller)
    {
        var pc = Object.FindObjectOfType<PlayerController>();
        Animator anim = null;

        if (pc != null)
        {
            // PlayerController üzerindeki playerAnim referansını veya nesnedeki Animator'ı al
            var field = typeof(PlayerController).GetField("playerAnim", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null) anim = field.GetValue(pc) as Animator;
            if (anim == null) anim = pc.GetComponent<Animator>() ?? pc.GetComponentInChildren<Animator>();
        }

        if (anim == null)
        {
            var player = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
            if (player != null) anim = player.GetComponent<Animator>() ?? player.GetComponentInChildren<Animator>();
        }

        if (anim != null)
        {
            anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false; // Kod tabanlı kararlı hareket için root motion kapalı
            EditorUtility.SetDirty(anim);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(anim.gameObject.scene);
            Debug.Log($"<color=cyan>[OrtizSetup]</color> Oyuncunun Animator bileşenine '{CONTROLLER_PATH}' başarıyla bağlandı!");
        }
        else
        {
            Debug.LogWarning("[OrtizSetup] Sahnedeki Oyuncu/Animator nesnesi bulunamadı. Controller'ı Inspector'dan manuel sürükleyebilirsin.");
        }
    }
}
