using UnityEngine;

[CreateAssetMenu(fileName = "FighterCombatConfig", menuName = "3D Fighter/Fighter Combat Config", order = 3)]
public class FighterCombatConfigSO : ScriptableObject
{
    [Header("Oyuncu Hareket & Fizik Ayarları")]
    [Tooltip("Oyuncu karakterlerin hareket hızı")]
    public float playerSpeed = 1.2f;
    [Tooltip("Oyuncu karakterlerin dönüş hızı")]
    public float playerRotationSpeed = 14f;
    [Tooltip("Oyuncunun fiziksel gövde yarıçapı (Düşmanların içinden geçmeyi engeller)")]
    public float playerBodyRadius = 0.45f;

    [Header("Oyuncu Yumruk & Vuruş Zamanlamaları")]
    [Range(0.12f, 0.35f)]
    public float punchRadius = 0.20f;
    public float maxPunchRange = 25f;
    public float punchStepDistance = 0f;
    public float punchActiveDuration = 0.40f;
    public float punchDuration = 0.55f;
    public float punchWindupTime = 0.14f;
    public float uppercutWindupTime = 0.18f;
    public float punchCooldown = 0.05f;

    [Header("Oyuncu Ulti & Efekt Ayarları")]
    public float ultiCooldown = 3f;
    public float ultiRange = 2.0f;
    public GameObject handsPrefab;
    public int ultiHandsCount = 20;
    public float ultiHandsSpawnInterval = 0.12f;
    public float ultiHandsSpawnY = 3.33f;
    public float ultiHandsTargetY = 0f;
    public float ultiHandsSpeed = 3.5f;
    public float ultiHandsMinX = -2.5f;
    public float ultiHandsMaxX = -0.33f;
    public float ultiHandsMinZ = 1f;
    public float ultiHandsMaxZ = 4f;
    public Vector3 ultiHandsRotation = new Vector3(0f, 0f, -90f);

    [Header("Oyuncu Ulti Ses & Işık Şovu")]
    public AudioClip ultiSoundClip;
    [Range(0f, 1f)]
    public float ultiSoundVolume = 1f;
    public AudioClip ultiPunchSoundClip;
    [Range(0f, 1f)]
    public float ultiPunchSoundVolume = 1f;
    public float punchSoundDelay = 0f;
    public bool enableUltiLightShow = true;
    public float ultiLightSpeed = 2.0f;
    public float ultiLightIntensityMultiplier = 1.2f;
    public bool spawnUltiHeroLight = true;

    [Header("Düşman Ortak Hareket & Dövüş Ayarları")]
    public float enemyMoveSpeed = 1.2f;
    public float enemyAttackRange = 1.1f;
    public float enemyMaxHitDistance = 1.25f;
    public float enemyAttackCooldown = 1.8f;
    public float enemyBodyRadius = 0.45f;
    public float enemySeparationRadius = 0.40f;
    public float enemySeparationStrength = 0.40f;
    public float comboResetThreshold = 1.8f;

    [Header("Grup Koordinatörü Halka Formasyonu")]
    public int maxConcurrentAttackers = 1;
    public float tokenTimeout = 2.8f;
    public float waitingRingDistance = 2.05f;
    public float circlePacingSpeed = 0.35f;
}
