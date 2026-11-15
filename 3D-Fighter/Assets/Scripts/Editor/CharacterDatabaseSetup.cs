using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CharacterDatabaseSetup
{
    private const string RESOURCES_FOLDER = "Assets/Resources";
    private const string DATABASE_ASSET_PATH = "Assets/Resources/CharacterDatabase.asset";
    private const string BALANCE_ASSET_PATH = "Assets/Resources/GameBalanceConfig.asset";
    private const string COMBAT_ASSET_PATH = "Assets/Resources/FighterCombatConfig.asset";
    private const string ARENA_ASSET_PATH = "Assets/Resources/ArenaDatabase.asset";

    private struct CharacterDef
    {
        public string id;
        public string displayName;
        public string texturePath;
        public string playerPrefabPath;
        public string enemyPrefabPath;

        public CharacterDef(string id, string displayName, string textureName, string playerPrefabName, string enemyPrefabName)
        {
            this.id = id;
            this.displayName = displayName;
            this.texturePath = $"Assets/Character Texture/{textureName}.renderTexture";
            this.playerPrefabPath = $"Assets/Prefab/{playerPrefabName}.prefab";
            this.enemyPrefabPath = $"Assets/Prefab/{enemyPrefabName}.prefab";
        }
    }

    private static readonly CharacterDef[] CharacterDefinitions = new CharacterDef[]
    {
        new CharacterDef("Elvis",     "Elvis",     "Elvis",     "Elvis-Player",     "Elvis"),
        new CharacterDef("Ortiz",     "Ortiz",     "Ortiz",     "Ortiz-Player",     "Ortiz"),
        new CharacterDef("Grand Mom", "Grand Mom", "Grand Mom", "Grand Mom-Player", "Grand Mom"),
        new CharacterDef("Adem",      "Adem",      "Adem",      "Adem-Player",      "Adem"),
        new CharacterDef("Emine",     "Emine",     "Emine",     "Emine-Player",      "Emine"),
        new CharacterDef("Husnu",     "Hüsnü",     "Husnu",     "Husnu-Player",      "Husnu"),
        new CharacterDef("Kezban",    "Kezban",    "Kezban",    "Kezban-Player",     "Kezban")
    };

    static CharacterDatabaseSetup()
    {
        EditorApplication.delayCall += EnsureAllScriptableAssetsExist;
    }

    [MenuItem("Tools/ScriptableObject Kurulumu (Karakter + Denge + Dövüş + Arena)")]
    public static void ForceRebuildAllAssets()
    {
        BuildOrUpdateAllAssets(forceLog: true);
    }

    private static void EnsureAllScriptableAssetsExist()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        BuildOrUpdateAllAssets(forceLog: false);
    }

    private static void BuildOrUpdateAllAssets(bool forceLog)
    {
        if (!AssetDatabase.IsValidFolder(RESOURCES_FOLDER))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        bool changed = false;
        changed |= BuildOrUpdateCharacterDatabase(forceLog);
        changed |= BuildOrUpdateGameBalance(forceLog);
        changed |= BuildOrUpdateFighterCombatConfig(forceLog);
        changed |= BuildOrUpdateArenaDatabase(forceLog);

        if (changed)
        {
            AssetDatabase.SaveAssets();
        }
    }

    private static bool BuildOrUpdateCharacterDatabase(bool forceLog)
    {
        CharacterDatabaseSO db = AssetDatabase.LoadAssetAtPath<CharacterDatabaseSO>(DATABASE_ASSET_PATH);
        bool createdNew = false;

        if (db == null)
        {
            db = ScriptableObject.CreateInstance<CharacterDatabaseSO>();
            AssetDatabase.CreateAsset(db, DATABASE_ASSET_PATH);
            createdNew = true;
        }

        bool needsUpdate = createdNew || db.Count != CharacterDefinitions.Length;
        if (!needsUpdate)
        {
            for (int i = 0; i < CharacterDefinitions.Length; i++)
            {
                CharacterEntry existing = db.GetCharacter(i);
                if (existing == null ||
                    existing.previewTexture == null ||
                    existing.playerPrefab == null ||
                    existing.enemyPrefab == null ||
                    string.IsNullOrEmpty(existing.displayName))
                {
                    needsUpdate = true;
                    break;
                }
            }
        }

        if (!needsUpdate && !forceLog) return false;

        List<CharacterEntry> entries = new List<CharacterEntry>(CharacterDefinitions.Length);
        foreach (var def in CharacterDefinitions)
        {
            Texture tex = AssetDatabase.LoadAssetAtPath<Texture>(def.texturePath);
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(def.playerPrefabPath);
            GameObject enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(def.enemyPrefabPath);

            if (playerPrefab == null)
            {
                playerPrefab = enemyPrefab;
            }

            entries.Add(new CharacterEntry
            {
                characterId = def.id,
                displayName = def.displayName,
                previewTexture = tex,
                playerPrefab = playerPrefab,
                enemyPrefab = enemyPrefab
            });
        }

        db.SetCharacters(entries);
        EditorUtility.SetDirty(db);

        if (forceLog || createdNew)
        {
            Debug.Log($"<color=green>[CharacterDatabaseSO]</color> 7 karakterlik veritabanı hazırlandı: {DATABASE_ASSET_PATH}");
        }
        return true;
    }

    private static bool BuildOrUpdateGameBalance(bool forceLog)
    {
        GameBalanceSO balance = AssetDatabase.LoadAssetAtPath<GameBalanceSO>(BALANCE_ASSET_PATH);
        bool createdNew = false;

        if (balance == null)
        {
            balance = ScriptableObject.CreateInstance<GameBalanceSO>();
            balance.ResetToDefaults();
            AssetDatabase.CreateAsset(balance, BALANCE_ASSET_PATH);
            EditorUtility.SetDirty(balance);
            createdNew = true;
        }
        else if (forceLog)
        {
            balance.EnsureInitialized();
            EditorUtility.SetDirty(balance);
        }

        if (forceLog || createdNew)
        {
            Debug.Log($"<color=green>[GameBalanceSO]</color> 10 dalgalık Kolay/Orta/Zor denge ve ışık veritabanı hazırlandı: {BALANCE_ASSET_PATH}");
        }

        return createdNew || forceLog;
    }

    private static bool BuildOrUpdateFighterCombatConfig(bool forceLog)
    {
        FighterCombatConfigSO combat = AssetDatabase.LoadAssetAtPath<FighterCombatConfigSO>(COMBAT_ASSET_PATH);
        bool createdNew = false;
        bool modified = false;

        if (combat == null)
        {
            combat = ScriptableObject.CreateInstance<FighterCombatConfigSO>();
            AssetDatabase.CreateAsset(combat, COMBAT_ASSET_PATH);
            createdNew = true;
            modified = true;
        }

        if (combat.handsPrefab == null)
        {
            combat.handsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Ortiz-Hands.prefab")
                ?? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Fighter Animation/Hands.fbx");
            modified = true;
        }

        if (combat.ultiSoundClip == null)
        {
            combat.ultiSoundClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/GÖKTE NE VAR.mp3");
            modified = true;
        }

        if (combat.ultiPunchSoundClip == null)
        {
            combat.ultiPunchSoundClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/YUMRUK.mp3");
            modified = true;
        }

        if (modified || forceLog)
        {
            EditorUtility.SetDirty(combat);
        }

        if (forceLog || createdNew)
        {
            Debug.Log($"<color=green>[FighterCombatConfigSO]</color> Ortak dövüş ve Ulti veritabanı hazırlandı: {COMBAT_ASSET_PATH}");
        }

        return modified || forceLog;
    }

    private static bool BuildOrUpdateArenaDatabase(bool forceLog)
    {
        ArenaDatabaseSO arenaDb = AssetDatabase.LoadAssetAtPath<ArenaDatabaseSO>(ARENA_ASSET_PATH);
        bool createdNew = false;
        bool modified = false;

        if (arenaDb == null)
        {
            arenaDb = ScriptableObject.CreateInstance<ArenaDatabaseSO>();
            arenaDb.ResetToDefaults();
            AssetDatabase.CreateAsset(arenaDb, ARENA_ASSET_PATH);
            createdNew = true;
            modified = true;
        }
        else if (arenaDb.Count < 3)
        {
            arenaDb.ResetToDefaults();
            modified = true;
        }

        Texture boxTex = AssetDatabase.LoadAssetAtPath<Texture>("Assets/Arena Texture/Box Arena.renderTexture");
        Texture chineseTex = AssetDatabase.LoadAssetAtPath<Texture>("Assets/Arena Texture/Ancient Chinese Arena.renderTexture");
        Texture vikingTex = AssetDatabase.LoadAssetAtPath<Texture>("Assets/Arena Texture/Viking Arena.renderTexture");

        ArenaEntry a0 = arenaDb.GetArena(0);
        if (a0 != null && a0.previewTexture == null && boxTex != null) { a0.previewTexture = boxTex; modified = true; }

        ArenaEntry a1 = arenaDb.GetArena(1);
        if (a1 != null && a1.previewTexture == null && chineseTex != null) { a1.previewTexture = chineseTex; modified = true; }

        ArenaEntry a2 = arenaDb.GetArena(2);
        if (a2 != null && a2.previewTexture == null && vikingTex != null) { a2.previewTexture = vikingTex; modified = true; }

        if (modified || forceLog)
        {
            EditorUtility.SetDirty(arenaDb);
        }

        if (forceLog || createdNew)
        {
            Debug.Log($"<color=green>[ArenaDatabaseSO]</color> Texture Arena altındaki 3 ortam veritabanı hazırlandı: {ARENA_ASSET_PATH}");
        }

        return modified || forceLog;
    }
}
