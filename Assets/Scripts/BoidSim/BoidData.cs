using UnityEngine;

namespace BoidSim
{
    [System.Serializable]
    public class BoidData
    {
        public GameObject boidPrefab;   // Префаб боида
        public int boidCount;           // Количество в стае
        public bool isSchooling = false;        // Стайная или одиночная?
        public float moveSpeed;         // Скорость движения
        
        [Header("Spawn Settings")]
        // Если isRandomSpawn = true, спавн в произвольных точках
        public bool isRandomSpawn = true;
        // Точка спавна (используется, если isRandomSpawn = false)
        public Vector2 spawnPoint = Vector2.zero;
    }
}