using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class DifficultyWaveProfile
{
    [Tooltip("Zorluk seviyesi")]
    public Difficulty difficulty = Difficulty.Easy;

    [Header("Dalga Başına Düşman Sayıları (1-10. Dalga)")]
    public int[] waveEnemyCounts = new int[] { 1, 2, 3, 2, 3, 4, 5, 2, 3, 4 };

    [Header("Dalga Başına Oyuncu & Düşman İstatistikleri (1-10. Dalga)")]
    public WaveStats[] waveStats = new WaveStats[10];

    [Header("Grup Koordinatörü (Saldırı Sırası & Nefes Alma Süreleri)")]
    [Tooltip("Düşmanların saldırıları arasındaki minimum bekleme süresi (saniye)")]
    public float minAttackInterval = 1.2f;
    [Tooltip("Düşmanların saldırıları arasındaki maksimum bekleme süresi (saniye)")]
    public float maxAttackInterval = 1.7f;
    [Tooltip("Oyuncu bir düşmana vurduğunda diğer düşmanların bekleyeceği nefes alma süresi (saniye)")]
    public float playerHitGracePeriod = 1.0f;

    [Header("Düşman Blok & Savunma Ayarları")]
    [Tooltip("Düşmanın garda geçmesi için arka arkaya yemesi gereken darbe sayısı")]
    public int hitsToTriggerBlock = 2;
    [Tooltip("Gereken darbe sayısına ulaşıldığında blok yapma olasılığı (0..1)")]
    [Range(0.1f, 1f)]
    public float blockChance = 1.0f;
    [Tooltip("Düşmanın gardını havada tutacağı süre (saniye)")]
    public float blockDuration = 1.2f;
    [Tooltip("Blok bittikten sonra tekrar blok yapabilmesi için bekleme süresi (saniye)")]
    public float blockCooldown = 0.5f;
}

[CreateAssetMenu(fileName = "GameBalanceConfig", menuName = "3D Fighter/Game Balance & Wave Config", order = 2)]
public class GameBalanceSO : ScriptableObject
{
    [Header("Genel Dalga Ayarları")]
    public int totalWaves = 10;
    [Tooltip("Önceki dalgadaki tüm düşmanlar yok olduktan sonra yeni dalganın başlama gecikmesi (saniye)")]
    public float respawnDelay = 1.0f;
    [Tooltip("Birden fazla düşman doğduğunda aralarındaki yatay mesafe")]
    public float spawnSpacing = 1.4f;
    [Tooltip("Bir dalgada aynı anda doğabilecek maksimum düşman sayısı")]
    public int maxEnemiesPerWave = 8;

    [Header("Zorluk Profilleri (Easy, Medium, Hard)")]
    public DifficultyWaveProfile easyProfile = new DifficultyWaveProfile();
    public DifficultyWaveProfile mediumProfile = new DifficultyWaveProfile();
    public DifficultyWaveProfile hardProfile = new DifficultyWaveProfile();

    [Header("Arena Işık Kademeleri (Dalga 1-3, 4-7, 8-10)")]
    public float colorTransitionDuration = 1.5f;
    public List<ArenaLightTier> arenaLightTiers = new List<ArenaLightTier>();

    private void OnEnable()
    {
        EnsureInitialized();
    }

    public void EnsureInitialized()
    {
        if (easyProfile == null || easyProfile.waveStats == null || easyProfile.waveStats.Length == 0 || easyProfile.waveStats[0] == null || easyProfile.waveStats[0].playerHealth <= 0f)
        {
            ResetToDefaults();
        }
    }

    public DifficultyWaveProfile GetProfile(Difficulty difficulty)
    {
        EnsureInitialized();
        switch (difficulty)
        {
            case Difficulty.Easy:
                return easyProfile;
            case Difficulty.Medium:
                return mediumProfile;
            case Difficulty.Hard:
                return hardProfile;
            default:
                return easyProfile;
        }
    }

    public WaveStats GetWaveStats(Difficulty difficulty, int waveNumber)
    {
        DifficultyWaveProfile profile = GetProfile(difficulty);
        if (profile == null || profile.waveStats == null || profile.waveStats.Length == 0)
        {
            return new WaveStats(100f, 10f, 20f, 30f, 30f, 5f, 10f);
        }

        int index = Mathf.Clamp(waveNumber - 1, 0, profile.waveStats.Length - 1);
        return profile.waveStats[index];
    }

    public int GetEnemyCountForWave(Difficulty difficulty, int waveNumber)
    {
        DifficultyWaveProfile profile = GetProfile(difficulty);
        int[] counts = profile != null ? profile.waveEnemyCounts : null;

        if (counts != null && waveNumber - 1 >= 0 && waveNumber - 1 < counts.Length)
        {
            return counts[waveNumber - 1];
        }

        int baseCount = (counts != null && counts.Length > 0) ? counts[counts.Length - 1] : 3;
        int extra = waveNumber - (counts != null ? counts.Length : 3);
        return Mathf.Clamp(baseCount + extra, 1, maxEnemiesPerWave);
    }

    public ArenaLightTier GetLightTierForWave(int waveNumber)
    {
        EnsureInitialized();
        if (arenaLightTiers == null || arenaLightTiers.Count == 0)
        {
            return new ArenaLightTier("Beyaz", Color.white, 10f);
        }

        int tierIndex = waveNumber <= 3 ? 0 : (waveNumber <= 7 ? 1 : 2);
        tierIndex = Mathf.Clamp(tierIndex, 0, arenaLightTiers.Count - 1);
        return arenaLightTiers[tierIndex];
    }

    public void ResetToDefaults()
    {
        totalWaves = 10;
        respawnDelay = 1.0f;
        spawnSpacing = 1.4f;
        maxEnemiesPerWave = 8;
        colorTransitionDuration = 1.5f;

        // 1. Kolay Mod (Easy)
        easyProfile = new DifficultyWaveProfile
        {
            difficulty = Difficulty.Easy,
            waveEnemyCounts = new int[] { 1, 2, 3, 2, 3, 4, 5, 2, 3, 4 },
            minAttackInterval = 1.6f,
            maxAttackInterval = 2.2f,
            playerHitGracePeriod = 1.2f,
            hitsToTriggerBlock = 2,
            blockChance = 1.0f,
            blockDuration = 1.0f,
            blockCooldown = 0.5f,
            waveStats = new WaveStats[]
            {
                new WaveStats(100f, 10f, 20f, 30f, 30f, 5f, 10f),
                new WaveStats(100f, 10f, 20f, 30f, 30f, 7f, 15f),
                new WaveStats(100f, 10f, 20f, 30f, 30f, 10f, 20f),
                new WaveStats(110f, 10f, 20f, 30f, 30f, 7f, 15f),
                new WaveStats(110f, 10f, 20f, 30f, 30f, 10f, 20f),
                new WaveStats(110f, 10f, 20f, 30f, 30f, 12f, 25f),
                new WaveStats(110f, 10f, 20f, 30f, 30f, 15f, 25f),
                new WaveStats(120f, 12f, 25f, 30f, 30f, 10f, 20f),
                new WaveStats(120f, 12f, 25f, 30f, 30f, 12f, 25f),
                new WaveStats(120f, 12f, 25f, 30f, 30f, 15f, 25f)
            }
        };

        // 2. Orta Mod (Medium)
        mediumProfile = new DifficultyWaveProfile
        {
            difficulty = Difficulty.Medium,
            waveEnemyCounts = new int[] { 1, 2, 3, 2, 3, 4, 5, 2, 3, 4 },
            minAttackInterval = 1.2f,
            maxAttackInterval = 1.7f,
            playerHitGracePeriod = 1.0f,
            hitsToTriggerBlock = 2,
            blockChance = 1.0f,
            blockDuration = 1.2f,
            blockCooldown = 0.5f,
            waveStats = new WaveStats[]
            {
                new WaveStats(100f, 15f, 25f, 40f, 40f, 8f, 15f),
                new WaveStats(100f, 15f, 25f, 40f, 40f, 10f, 20f),
                new WaveStats(100f, 15f, 25f, 40f, 30f, 12f, 20f),
                new WaveStats(120f, 20f, 30f, 40f, 40f, 10f, 20f),
                new WaveStats(120f, 20f, 30f, 40f, 40f, 12f, 20f),
                new WaveStats(120f, 20f, 30f, 30f, 30f, 12f, 25f),
                new WaveStats(120f, 20f, 30f, 40f, 40f, 17f, 25f),
                new WaveStats(150f, 20f, 30f, 40f, 40f, 12f, 20f),
                new WaveStats(150f, 20f, 30f, 40f, 40f, 15f, 25f),
                new WaveStats(150f, 20f, 30f, 40f, 40f, 17f, 30f)
            }
        };

        // 3. Zor Mod (Hard)
        hardProfile = new DifficultyWaveProfile
        {
            difficulty = Difficulty.Hard,
            waveEnemyCounts = new int[] { 1, 2, 3, 2, 3, 4, 5, 2, 3, 4 },
            minAttackInterval = 0.9f,
            maxAttackInterval = 1.3f,
            playerHitGracePeriod = 0.7f,
            hitsToTriggerBlock = 2,
            blockChance = 1.0f,
            blockDuration = 1.4f,
            blockCooldown = 0.5f,
            waveStats = new WaveStats[]
            {
                new WaveStats(100f, 25f, 30f, 50f, 50f, 10f, 20f),
                new WaveStats(100f, 25f, 30f, 50f, 50f, 12f, 25f),
                new WaveStats(100f, 25f, 30f, 50f, 50f, 15f, 30f),
                new WaveStats(130f, 25f, 30f, 50f, 50f, 10f, 20f),
                new WaveStats(130f, 25f, 30f, 50f, 50f, 12f, 25f),
                new WaveStats(130f, 25f, 30f, 50f, 50f, 15f, 30f),
                new WaveStats(130f, 25f, 30f, 50f, 50f, 18f, 30f),
                new WaveStats(160f, 25f, 35f, 50f, 50f, 15f, 30f),
                new WaveStats(160f, 25f, 35f, 50f, 40f, 17f, 35f),
                new WaveStats(160f, 25f, 35f, 50f, 40f, 20f, 40f)
            }
        };

        arenaLightTiers = new List<ArenaLightTier>
        {
            new ArenaLightTier("Dalga 1-3 (Beyaz / Doğal Işık)", Color.white, 10f),
            new ArenaLightTier("Dalga 4-7 (Yırtıcı Neon Mavi)", new Color(0f, 0.7f, 1f, 1f), 11f),
            new ArenaLightTier("Dalga 8-10 (Kan Kırmızısı / Crimson)", new Color(1f, 0.08f, 0.08f, 1f), 12f)
        };
    }
}
