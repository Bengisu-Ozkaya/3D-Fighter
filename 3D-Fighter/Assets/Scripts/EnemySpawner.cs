using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] GameObject enemyPrefab;
    [SerializeField] EnemyController enemyController;
    [SerializeField] float respawnDelay = 0.2f;

    private bool isSpawning = false;

    void Start()
    {
        // Eğer sahnede atanmış bir düşman yoksa hemen bir tane oluştur
        if (enemyController == null)
        {
            SpawnEnemy();
        }
    }

    void Update()
    {
        // Düşman Destroy edilince enemyController null olur.
        // Düşman yoksa ve henüz yenisi doğrulma aşamasında değilse coroutine başlat.
        if (enemyController == null && !isSpawning)
        {
            StartCoroutine(SpawnEnemyRoutine());
        }
    }

    IEnumerator SpawnEnemyRoutine()
    {
        isSpawning = true;
        yield return new WaitForSeconds(respawnDelay);

        SpawnEnemy();
        isSpawning = false;
    }

    void SpawnEnemy()
    {
        if (enemyPrefab != null)
        {
            GameObject newEnemy = Instantiate(enemyPrefab, new Vector3(0,0,2.6f), transform.rotation);
            enemyController = newEnemy.GetComponent<EnemyController>();
        }
        else
        {
            Debug.LogWarning("EnemySpawner: Enemy Prefab atanmamış!");
        }
    }
}
