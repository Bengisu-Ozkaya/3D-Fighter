using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

public class AnimationFixer
{
    [MenuItem("Tools/3D Fighter/Yumruk ve Head Hit Ayarlarini Guncelle")]
    public static void ApplyAllFixes()
    {
        // 1. Animasyon FBX import ayarlarını güvenceye al
        ConfigureClip("Assets/Fighter Animation/Left.fbx", 18f);
        ConfigureClip("Assets/Fighter Animation/Right.fbx", 10f);
        ConfigureClip("Assets/Fighter Animation/Uppercut.fbx", 0f);
        ConfigureClip("Assets/Fighter Animation/Head Hit.fbx", 0f);
        ConfigureClip("Assets/Fighter Animation/Center Block.fbx", 0f);

        // 2. Animator Controller dosyalarını güncelle
        EnsureHeadHitInController("Assets/Fighter Animation/Idle.controller");
        EnsureHeadHitInController("Assets/Fighter Animation/Enemy.controller");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("<color=green>[3D Fighter]</color> Yumruk doğrultuları ve Head Hit animasyon ayarları başarıyla güncellendi!");
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
