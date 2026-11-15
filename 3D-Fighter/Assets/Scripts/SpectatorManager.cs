using UnityEngine;
using System.Collections; // Coroutine kullanmak için bunu ekledik

public class SpectatorManager : MonoBehaviour
{
    [Header("Animasyon İsimleri (Animator'daki isimlerle BİREBİR aynı olmalı)")]
    public string[] animationStates = { "Cheer_1", "Cheer_2", "Clap", "Excited" };

    private void OnEnable()
    {
        StartCoroutine(RandomizeSpectatorsRoutine());
    }

    // Animator'ların yüklenmesi için 1 kare bekleyip rastgele tezahürat animasyonu başlatır
    private IEnumerator RandomizeSpectatorsRoutine()
    {
        // Unity'nin Animator'ları ve modelleri sahneye tam yerleştirmesi için 1 kare (milisaniye) bekler
        yield return null;

        Animator[] allSpectators = GetComponentsInChildren<Animator>(true);

        foreach (Animator anim in allSpectators)
        {
            if (anim == null || !anim.gameObject.activeInHierarchy) continue;

            // Performans ayarı
            anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            if (animationStates != null && animationStates.Length > 0)
            {
                // Rastgele animasyon seç
                string randomAnim = animationStates[Random.Range(0, animationStates.Length)];

                // Animasyonu rastgele bir saniyesinden oynat
                anim.Play(randomAnim, 0, Random.value);
            }
        }
    }
}
