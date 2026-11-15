using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class CharacterEntry
{
    [Tooltip("Karakterin benzersiz kimliği (Örn: Elvis, Ortiz, Grand Mom, Adem, Emine, Husnu, Kezban)")]
    public string characterId;

    [Tooltip("CharacterChooseUI altındaki Character Name Text'te görünecek isim")]
    public string displayName;

    [Tooltip("Assets/Character Texture klasöründeki RenderTexture / Texture")]
    public Texture previewTexture;

    [Tooltip("Oyuncu bu karakteri seçtiğinde sahnede doğacak Player Prefab")]
    public GameObject playerPrefab;

    [Tooltip("Oyun esnasında rakip/düşman olarak doğacak Enemy Prefab")]
    public GameObject enemyPrefab;
}

[CreateAssetMenu(fileName = "CharacterDatabase", menuName = "3D Fighter/Character Database", order = 1)]
public class CharacterDatabaseSO : ScriptableObject
{
    [Header("7 Karakterin Veri Listesi")]
    [SerializeField] private List<CharacterEntry> characters = new List<CharacterEntry>();

    public IReadOnlyList<CharacterEntry> Characters => characters;
    public int Count => characters != null ? characters.Count : 0;

    /// <summary>
    /// Verilen indeksi döngüsel (circular) olarak 0..Count-1 aralığına sarar.
    /// İlk karakterdeyken Previous (-1) -> Son karakter (Count - 1)
    /// Son karakterdeyken Next (Count) -> 1. karakter (0)
    /// </summary>
    public int WrapIndex(int index)
    {
        int count = Count;
        if (count <= 0) return 0;
        return ((index % count) + count) % count;
    }

    /// <summary>
    /// İndekse göre karakter verisini döner (Döngüsel indeks korumalı).
    /// </summary>
    public CharacterEntry GetCharacter(int index)
    {
        if (Count == 0) return null;
        int safeIndex = WrapIndex(index);
        return characters[safeIndex];
    }

    /// <summary>
    /// İsim veya ID'ye göre karakterin listedeki indeksini bulur. Bulamazsa -1 döner.
    /// </summary>
    public int FindCharacterIndex(string nameOrId)
    {
        if (string.IsNullOrEmpty(nameOrId) || Count == 0) return -1;
        string cleaned = nameOrId.Replace("-Player", "").Trim();

        for (int i = 0; i < characters.Count; i++)
        {
            CharacterEntry entry = characters[i];
            if (entry == null) continue;

            if (!string.IsNullOrEmpty(entry.characterId) &&
                (entry.characterId.Equals(cleaned, StringComparison.OrdinalIgnoreCase) ||
                 cleaned.IndexOf(entry.characterId, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return i;
            }

            if (!string.IsNullOrEmpty(entry.displayName) &&
                (entry.displayName.Equals(cleaned, StringComparison.OrdinalIgnoreCase) ||
                 cleaned.IndexOf(entry.displayName, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return i;
            }

            if (entry.playerPrefab != null &&
                entry.playerPrefab.name.IndexOf(cleaned, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Editor veya çalışma zamanında listeyi güvenli şekilde ayarlamak için kullanılır.
    /// </summary>
    public void SetCharacters(List<CharacterEntry> newEntries)
    {
        characters = newEntries ?? new List<CharacterEntry>();
    }
}
