using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    [Header("UI Panelleri")]
    [SerializeField] GameObject startPanel;
    [SerializeField] GameObject victoryPanel;

    [Header("Referanslar")]
    [SerializeField] EnemySpawner enemySpawner;

    void Awake()
    {
        if (enemySpawner == null)
        {
            enemySpawner = FindObjectOfType<EnemySpawner>();
        }
    }

    void Start()
    {
        // Başlangıçta Start paneli açık, Victory paneli kapalı olsun
        if (startPanel != null) startPanel.SetActive(true);
        if (victoryPanel != null) victoryPanel.SetActive(false);
    }

    public void EasyMode()
    {
        if (startPanel != null) startPanel.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (enemySpawner != null)
        {
            enemySpawner.StartEasyMode();
        }
    }

    public void MidMode()
    {
        if (startPanel != null) startPanel.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (enemySpawner != null)
        {
            enemySpawner.StartMidMode();
        }
    }

    public void HardMode()
    {
        if (startPanel != null) startPanel.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (enemySpawner != null)
        {
            enemySpawner.StartHardMode();
        }
    }

    /// <summary>
    /// Tüm dalgalar bittiğinde zafer panelini açar
    /// </summary>
    public void ShowVictoryPanel()
    {
        if (victoryPanel != null)
        {
            victoryPanel.SetActive(true);
        }
    }

    /// <summary>
    /// Yeniden Başla butonu: Aynı zorluk modunda oyunu 1. dalgadan yeniden başlatır
    /// </summary>
    public void RestartGame()
    {
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (startPanel != null) startPanel.SetActive(false);

        // Oyuncuyu sıfırla
        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            player.ResetPlayerState();
        }

        // Spawner'ı aynı modda yeniden başlat
        if (enemySpawner != null)
        {
            enemySpawner.RestartGame();
        }
    }

    /// <summary>
    /// Ana Menü butonu: Start paneline geri döner ve oyunu sıfırlar
    /// </summary>
    public void ReturnToMainMenu()
    {
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (startPanel != null) startPanel.SetActive(true);

        // Oyuncuyu başlangıç durumuna döndür
        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            player.ResetPlayerState();
        }

        // Spawner'ı temizle ve durdur
        if (enemySpawner != null)
        {
            enemySpawner.ResetSpawner();
        }
    }
}
