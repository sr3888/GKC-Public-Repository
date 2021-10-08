using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class enemyspawn : MonoBehaviour
{
    //GKC Enemy Spawner v2.b
    [Header("EnemyData")]
    public GameObject[] prefabsToSpawn;
    public int enemiesToSpawn = 5;
    [Header("WorldSpawnLocation")]
    public float x = 0.0f;
    public float y = 0.0f;
    public float z = 0.0f;
    [Header("SpawnSpread")]
    public float minSpread = 1;
    public float maxSpread = 10;
    [Header("CurrentSpawnData")]
    public int currentSpawnCount = 0;
    public int howMany = 0;
    public int howManyBosses = 0;
    [Header("EnemyManager")]
    public GameObject[] enemyManager;
    public GameObject[] bossManager;
    [Header("AutoRespawn")]
    public bool autoRespawn = false;
    public float respawnTimer = 0.0f;
    [Header("WaveSpawns")]
    public bool waveSpawn = false;
    public int increaseEnemySpawn = 0;
    public int howManyWaves = 0;
    public int currentWave = 0;
    [Header("Boss Event")]
    public bool bossActive = false;
    public GameObject[] bossEnemies;
    public int bossLevel = 0;
    public int bossDificulty = 0;
    
    
    //SpawnChecks and ResetData
    private int ememySelectorReset = 0;
    private bool Spawned = false;
    private bool bossSpawned = false;
    private bool inSpawnArea = false;
    private float TimeInterval;
    private int randomEnemyInt;
    private int randomBossInt;
    

    //For use later
    private float spawnSpread;
    

    void Start()
    {
        //For use Later
        spawnSpread = Random.Range(minSpread, maxSpread);
        
    }

    void Update()
    {
        currentSpawnCount = howMany;
        howMany = enemyManager.Length;
        howManyBosses = bossManager.Length;
        
        foreach (GameObject elem in enemyManager)
        {
            if (elem == null)
            {
                howMany -= 1;
            }
        }
        foreach (GameObject elem in bossManager)
        {
            if (elem == null)
            {
                howManyBosses -= 1;
            }
        }
        if (howMany == 0)
        {
            enemyManager = new GameObject[ememySelectorReset];
        }
        if (waveSpawn == false)
        {
            if (autoRespawn == true)
            {
                if (howMany == 0)
                {
                    respawnEnemies();
                }
            }
        }
        if (waveSpawn == true)
        {
            if (howMany == 0)
            {
                if (currentWave <= howManyWaves-1)
                {
                    waveController();
                }  
            }
        }
        
        
    }

    private void respawnEnemies()
    {
        if (inSpawnArea == true)
        {
            TimeInterval += Time.deltaTime;
            if (TimeInterval >= respawnTimer)
            {
                TimeInterval = 0;
                Spawned = true;
                spawnEnemies();
            }
        }       
    }

    public void spawnEnemiesTrue()
    {
        Spawned = true;
        inSpawnArea = true;
    }

    public void spawnEnemies()
    {
        if (Spawned == true)
        {
            if (howMany == 0)
            {
                enemyManager = new GameObject[enemiesToSpawn];
                for (int i = 0; i < enemiesToSpawn; i++)
                {
                    randomEnemyInt = Random.Range(0, prefabsToSpawn.Length);
                    GameObject go = Instantiate(prefabsToSpawn[randomEnemyInt], new Vector3(x + Random.Range(minSpread, maxSpread), y, z + Random.Range(minSpread, maxSpread)), Quaternion.identity) as GameObject;
                    go.transform.localScale = Vector3.one;
                    enemyManager[i] = go;
                    Spawned = false;
                }
            }
        }
    }

    private void waveController()
    {
        //currentSpawnCount += 1;
        if (inSpawnArea == true)
        {
            if (currentWave <= howManyWaves)
            {
                TimeInterval += Time.deltaTime;
                if (TimeInterval >= respawnTimer)
                {
                    TimeInterval = 0;
                    Spawned = true;
                    enemiesToSpawn += increaseEnemySpawn;
                   // currentWave += 1;
                    waveSpawnEnemies();
                }
            }  
        }
    }

    public void waveSpawnEnemies()
    {
        
        currentWave += 1;
        if (Spawned == true)
        {
            if (howMany == 0)
            {
                enemyManager = new GameObject[enemiesToSpawn];
                for (int i = 0; i < enemiesToSpawn; i++)
                {
                    randomEnemyInt = Random.Range(0, prefabsToSpawn.Length);
                    GameObject go = Instantiate(prefabsToSpawn[randomEnemyInt], new Vector3(x + Random.Range(minSpread, maxSpread), y, z + Random.Range(minSpread, maxSpread)), Quaternion.identity) as GameObject;
                    go.transform.localScale = Vector3.one;
                    enemyManager[i] = go;
                    Spawned = false;
                    if (bossLevel == currentWave)
                    {
                        if (bossActive == true)
                        {
                            bossSpawnData();
                        }
                    }
                }
            }
        }
    }

    private void bossSpawnData()
    {

        bossManager = new GameObject[bossDificulty];
        for (int i = 0; i < bossDificulty; i++)
        {
            randomBossInt = Random.Range(0, bossEnemies.Length);
            GameObject go = Instantiate(bossEnemies[randomBossInt], new Vector3(x + Random.Range(minSpread, maxSpread), y, z + Random.Range(minSpread, maxSpread)), Quaternion.identity) as GameObject;
            go.transform.localScale = Vector3.one;
            bossManager[i] = go;
        }
    }

    public void destroyEnemies()
    {
        foreach (GameObject go in enemyManager)
        {
            Destroy(go);
            enemyManager = new GameObject[ememySelectorReset];
            inSpawnArea = false;
        }
    }
}
