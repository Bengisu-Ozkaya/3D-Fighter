using UnityEngine;
using System.Collections; // Coroutine kullanmak için bunu ekledik

public class SpectatorManager : MonoBehaviour
{
    [Header("Animasyon İsimleri (Animator'daki isimlerle BİREBİR aynı olmalı)")]
    public string[] animationStates = { "Cheer_1", "Cheer_2", "Clap", "Excited" }; 

    // Start'ı IEnumerator yaptık ki Animator'ların yüklenmesi için 1 kare bekleyebilelim
    IEnumerator Start() 
    {
        // Unity'nin Animator'ları ve modelleri sahneye tam yerleştirmesi için 1 kare (milisaniye) bekler
        yield return null; 

        Animator[] allSpectators = GetComponentsInChildren<Animator>();

        foreach (Animator anim in allSpectators)
        {
            // Performans ayarı
            anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            if (animationStates.Length > 0)
            {
                // Rastgele animasyon seç
                string randomAnim = animationStates[Random.Range(0, animationStates.Length)];
                
                // Animasyonu rastgele bir saniyesinden oynat
                anim.Play(randomAnim, 0, Random.value);
            }
        }
    }
}