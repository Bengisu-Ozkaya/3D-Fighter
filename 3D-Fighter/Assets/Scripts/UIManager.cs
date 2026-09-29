using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("UI Panelleri")]
    [SerializeField] GameObject startPanel;
    [SerializeField] GameObject victoryPanel;
    [SerializeField] GameObject gameOverPanel;

    [Header("Referanslar")]
    [SerializeField] EnemySpawner enemySpawner;
    [SerializeField] PlayerController playerController;

    void Awake()
    {
        if (enemySpawner == null)
        {
            enemySpawner = FindObjectOfType<EnemySpawner>();
        }

        if (playerController == null)
        {
            playerController = FindObjectOfType<PlayerController>();
        }

        if (gameOverPanel == null)
        {
            FindGameOverPanel();
        }

        BindGameOverButtons();
    }

    void Start()
    {
        if (startPanel != null) startPanel.SetActive(true);
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
    }

    void FindGameOverPanel()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas != null)
        {
            foreach (Transform child in canvas.transform)
            {
                if (child.name == "Panel" || child.name.Contains("GameOver") || child.name.Contains("Game Over"))
                {
                    if (child.Find("Relive Button") != null || child.Find("Home Button") != null)
                    {
                        gameOverPanel = child.gameObject;
                        break;
                    }
                }
            }
        }
    }

    void BindGameOverButtons()
    {
        if (gameOverPanel != null)
        {
            Button[] buttons = gameOverPanel.GetComponentsInChildren<Button>(true);
            foreach (var btn in buttons)
            {
                if (btn.name.Contains("Relive"))
                {
                    btn.onClick.RemoveListener(Relive);
                    btn.onClick.AddListener(Relive);
                }
                else if (btn.name.Contains("Home"))
                {
                    btn.onClick.RemoveListener(ReturnToMainMenu);
                    btn.onClick.AddListener(ReturnToMainMenu);
                }
            }
        }
    }

    public void EasyMode()
    {
        if (startPanel != null) startPanel.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (enemySpawner != null)
        {
            enemySpawner.StartEasyMode();
        }
    }

    public void MidMode()
    {
        if (startPanel != null) startPanel.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (enemySpawner != null)
        {
            enemySpawner.StartMidMode();
        }
    }

    public void HardMode()
    {
        if (startPanel != null) startPanel.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
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
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (victoryPanel != null)
        {
            victoryPanel.SetActive(true);
        }
    }

    /// <summary>
    /// Oyuncu öldüğünde Game Over panelini açar
    /// </summary>
    public void ShowGameOverPanel()
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }
    }

    /// <summary>
    /// Game Over panelini kapatır
    /// </summary>
    public void HideGameOverPanel()
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Relive Butonu: Öldüğü dalgayı yeniden başlatır ve karakter Kip Up animasyonuyla ayağa kalkar
    /// </summary>
    public void Relive()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        // 1. Spawner'da oyuncunun öldüğü mevcut dalgayı yeniden başlat
        if (enemySpawner == null) enemySpawner = FindObjectOfType<EnemySpawner>();
        if (enemySpawner != null)
        {
            enemySpawner.RestartCurrentWave();
        }

        // 2. Oyuncuyu Kip Up animasyonu ile ayağa kaldır
        if (playerController == null) playerController = FindObjectOfType<PlayerController>();
        if (playerController != null)
        {
            playerController.RelivePlayer();
        }
    }

    public void OnReliveButtonClicked() => Relive();

    public void Home() => ReturnToMainMenu();
    public void OnHomeButtonClicked() => ReturnToMainMenu();

    /// <summary>
    /// Yeniden Başla butonu: Aynı zorluk modunda oyunu 1. dalgadan yeniden başlatır
    /// </summary>
    public void RestartGame()
    {
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (startPanel != null) startPanel.SetActive(false);

        // Oyuncuyu sıfırla
        if (playerController == null) playerController = FindObjectOfType<PlayerController>();
        if (playerController != null)
        {
            playerController.ResetPlayerState();
        }

        // Spawner'ı aynı modda yeniden başlat
        if (enemySpawner == null) enemySpawner = FindObjectOfType<EnemySpawner>();
        if (enemySpawner != null)
        {
            enemySpawner.RestartGame();
        }
    }

    /// <summary>
    /// Ana Menü / Home butonu: Start paneline geri döner ve oyunu sıfırlar
    /// </summary>
    public void ReturnToMainMenu()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (startPanel != null) startPanel.SetActive(true);

        // Oyuncuyu başlangıç durumuna döndür
        if (playerController == null) playerController = FindObjectOfType<PlayerController>();
        if (playerController != null)
        {
            playerController.ResetPlayerState();
        }

        // Spawner'ı temizle ve durdur
        if (enemySpawner == null) enemySpawner = FindObjectOfType<EnemySpawner>();
        if (enemySpawner != null)
        {
            enemySpawner.ResetSpawner();
        }
    }
}
