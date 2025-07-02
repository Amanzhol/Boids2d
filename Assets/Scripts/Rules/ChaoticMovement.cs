using System.Collections.Generic;
using UnityEngine;

namespace Rules
{
    public class ChaoticMovement : MonoBehaviour
    {
        [Header("Настройки симуляции")]
        [SerializeField] private int objectCount = 10;
        [SerializeField] private GameObject objectPrefab;
        [SerializeField] private GameObject boundaryPrefab;
    
        [Header("Настройки движения")]
        [SerializeField] private float minSpeed = 1f;
        [SerializeField] private float maxSpeed = 5f;
        [SerializeField] private float rotationSpeed = 180f;
        [SerializeField] private float wanderStrength = 30f; // Сила "блуждания" - насколько сильно объекты отклоняются
        [SerializeField] private float noiseScale = 1f; // Масштаб шума для плавности изменений
    
        private GameObject boundary;
        private Bounds boundaryBounds;
        private List<MovingObject> movingObjects = new List<MovingObject>();
    
        private class MovingObject
        {
            public GameObject gameObject;
            public Vector2 direction;
            public float speed;
            public float noiseOffset; // Уникальное смещение для каждого объекта в шуме Перлина
        }
    
        void Start()
        {
            SetupBoundary();
            SpawnObjects();
        }
    
        void SetupBoundary()
        {
            if (boundaryPrefab == null)
            {
                Debug.LogError("Boundary prefab не назначен!");
                return;
            }
        
            // Создаем границы
            boundary = Instantiate(boundaryPrefab, Vector3.zero, Quaternion.identity);
        
            // Получаем размеры границ
            Collider2D collider = boundary.GetComponent<Collider2D>();
            if (collider != null)
            {
                boundaryBounds = collider.bounds;
            }
            else
            {
                // Если нет коллайдера, используем Renderer
                Renderer renderer = boundary.GetComponent<Renderer>();
                if (renderer != null)
                {
                    boundaryBounds = renderer.bounds;
                }
                else
                {
                    Debug.LogError("Boundary prefab должен иметь Collider2D или Renderer!");
                    boundaryBounds = new Bounds(Vector3.zero, new Vector3(10, 10, 0));
                }
            }
        }
    
        void SpawnObjects()
        {
            if (objectPrefab == null)
            {
                Debug.LogError("Object prefab не назначен!");
                return;
            }
        
            for (int i = 0; i < objectCount; i++)
            {
                // Случайная позиция внутри границ
                float x = Random.Range(boundaryBounds.min.x + 0.5f, boundaryBounds.max.x - 0.5f);
                float y = Random.Range(boundaryBounds.min.y + 0.5f, boundaryBounds.max.y - 0.5f);
                Vector3 position = new Vector3(x, y, 0);
            
                // Создаем объект
                GameObject obj = Instantiate(objectPrefab, position, Quaternion.identity);
            
                // Создаем данные для движения
                MovingObject movingObj = new MovingObject
                {
                    gameObject = obj,
                    direction = Random.insideUnitCircle.normalized,
                    speed = Random.Range(minSpeed, maxSpeed),
                    noiseOffset = Random.Range(0f, 1000f) // Уникальное смещение для каждого объекта
                };
            
                // Поворачиваем объект в направлении движения (вершина треугольника вперед)
                float angle = Mathf.Atan2(movingObj.direction.y, movingObj.direction.x) * Mathf.Rad2Deg - 90f;
                obj.transform.rotation = Quaternion.Euler(0, 0, angle);
            
                movingObjects.Add(movingObj);
            }
        }
    
        void Update()
        {
            foreach (var movingObj in movingObjects)
            {
                if (movingObj.gameObject == null) continue;
            
                // Плавное изменение направления с помощью шума Перлина
                UpdateDirection(movingObj);
            
                // Движение
                Vector3 movement = new Vector3(movingObj.direction.x, movingObj.direction.y, 0) * (movingObj.speed * Time.deltaTime);
                movingObj.gameObject.transform.position += movement;
            
                // Плавный поворот в направлении движения
                float targetAngle = Mathf.Atan2(movingObj.direction.y, movingObj.direction.x) * Mathf.Rad2Deg - 90f;
                Quaternion targetRotation = Quaternion.Euler(0, 0, targetAngle);
                movingObj.gameObject.transform.rotation = Quaternion.RotateTowards(
                    movingObj.gameObject.transform.rotation, 
                    targetRotation, 
                    rotationSpeed * Time.deltaTime
                );
            
                // Проверка границ и отражение
                CheckBoundaries(movingObj);
            }
        }
    
        void UpdateDirection(MovingObject movingObj)
        {
            // Используем шум Перлина для плавного изменения направления
            float noiseValue = Mathf.PerlinNoise(
                Time.time * noiseScale + movingObj.noiseOffset, 
                movingObj.noiseOffset
            );
        
            // Преобразуем значение шума (0-1) в угол поворота (-1 к 1)
            float turnAmount = (noiseValue - 0.5f) * 2f;
        
            // Применяем поворот к текущему направлению
            float rotationAngle = turnAmount * wanderStrength * Time.deltaTime;
            Vector2 newDirection = Quaternion.Euler(0, 0, rotationAngle) * movingObj.direction;
            movingObj.direction = newDirection.normalized;
        }
    
        void CheckBoundaries(MovingObject movingObj)
        {
            Vector3 pos = movingObj.gameObject.transform.position;
            bool bounced = false;
        
            // Проверка и отражение от границ
            if (pos.x <= boundaryBounds.min.x + 0.5f || pos.x >= boundaryBounds.max.x - 0.5f)
            {
                movingObj.direction.x = -movingObj.direction.x;
                bounced = true;
            
                // Корректировка позиции
                if (pos.x <= boundaryBounds.min.x + 0.5f)
                    pos.x = boundaryBounds.min.x + 0.5f;
                else
                    pos.x = boundaryBounds.max.x - 0.5f;
            }
        
            if (pos.y <= boundaryBounds.min.y + 0.5f || pos.y >= boundaryBounds.max.y - 0.5f)
            {
                movingObj.direction.y = -movingObj.direction.y;
                bounced = true;
            
                // Корректировка позиции
                if (pos.y <= boundaryBounds.min.y + 0.5f)
                    pos.y = boundaryBounds.min.y + 0.5f;
                else
                    pos.y = boundaryBounds.max.y - 0.5f;
            }
        
            if (bounced)
            {
                movingObj.gameObject.transform.position = pos;
                // При отскоке добавляем небольшое случайное отклонение
                float randomDeviation = Random.Range(-15f, 15f);
                movingObj.direction = Quaternion.Euler(0, 0, randomDeviation) * movingObj.direction;
                movingObj.direction = movingObj.direction.normalized;
            
                // Мгновенно поворачиваем объект в новом направлении
                float angle = Mathf.Atan2(movingObj.direction.y, movingObj.direction.x) * Mathf.Rad2Deg - 90f;
                movingObj.gameObject.transform.rotation = Quaternion.Euler(0, 0, angle);
            }
        }
    
        // Метод для динамического изменения количества объектов
        public void SetObjectCount(int newCount)
        {
            if (newCount < movingObjects.Count)
            {
                // Удаляем лишние объекты
                for (int i = movingObjects.Count - 1; i >= newCount; i--)
                {
                    if (movingObjects[i].gameObject != null)
                        Destroy(movingObjects[i].gameObject);
                    movingObjects.RemoveAt(i);
                }
            }
            else if (newCount > movingObjects.Count)
            {
                // Добавляем новые объекты
                int toAdd = newCount - movingObjects.Count;
                objectCount = newCount;
                for (int i = 0; i < toAdd; i++)
                {
                    SpawnSingleObject();
                }
            }
            objectCount = newCount;
        }
    
        void SpawnSingleObject()
        {
            if (objectPrefab == null) return;
        
            float x = Random.Range(boundaryBounds.min.x + 0.5f, boundaryBounds.max.x - 0.5f);
            float y = Random.Range(boundaryBounds.min.y + 0.5f, boundaryBounds.max.y - 0.5f);
            Vector3 position = new Vector3(x, y, 0);
        
            GameObject obj = Instantiate(objectPrefab, position, Quaternion.identity);
        
            MovingObject movingObj = new MovingObject
            {
                gameObject = obj,
                direction = Random.insideUnitCircle.normalized,
                speed = Random.Range(minSpeed, maxSpeed),
                noiseOffset = Random.Range(0f, 1000f)
            };
        
            float angle = Mathf.Atan2(movingObj.direction.y, movingObj.direction.x) * Mathf.Rad2Deg - 90f;
            obj.transform.rotation = Quaternion.Euler(0, 0, angle);
        
            movingObjects.Add(movingObj);
        }
    }
}