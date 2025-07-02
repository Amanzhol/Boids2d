using System.Collections.Generic;
using UnityEngine;

namespace Rules
{
    public class CohesionRule : MonoBehaviour
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
        
        [Header("Настройки притяжения")]
        [SerializeField] [Range(0f, 1f)] private float cohesion = 0f; // 0 - хаотичное движение, 1 - полное притяжение
        [SerializeField] private float cohesionForce = 2f; // Сила притяжения к целевому объекту
        
        [Header("Настройки круга обнаружения")]
        [SerializeField] private float circleRadius = 3f;
        [SerializeField] private Color circleColor = new Color(1f, 1f, 1f, 0.1f);
        [SerializeField] private int circleSegments = 64;
        
        [Header("Настройки цветов объектов")]
        [SerializeField] private Color targetObjectColor = Color.red; // Цвет целевого объекта
        [SerializeField] private Color normalObjectColor = Color.white; // Цвет обычных объектов
    
        private GameObject boundary;
        private Bounds boundaryBounds;
        private List<MovingObject> movingObjects = new List<MovingObject>();
        private MovingObject targetObject; // Целевой объект с кругом
        private GameObject targetCircle; // Визуализация круга вокруг целевого объекта
        
        private GameObject detectionCircle;
        private MeshRenderer circleMeshRenderer;
        private MeshFilter circleMeshFilter;
    
        private class MovingObject
        {
            public GameObject gameObject;
            public Vector2 direction;
            public float speed;
            public float noiseOffset; // Уникальное смещение для каждого объекта в шуме Перлина
            public bool isTarget; // Является ли объект целевым
            public Renderer objectRenderer; // Ссылка на рендерер для изменения цвета
            public SpriteRenderer spriteRenderer; // Ссылка на SpriteRenderer для изменения цвета
        }
    
        void Start()
        {
            SetupBoundary();
            SpawnObjects();
            SelectRandomTarget();
            SetupDetectionCircle();
            UpdateObjectColors();
        }
        
        void SetupDetectionCircle()
        {
            // Создаем объект для круга
            detectionCircle = new GameObject("Detection Circle");
        
            // Добавляем компоненты для отображения меша
            circleMeshFilter = detectionCircle.AddComponent<MeshFilter>();
            circleMeshRenderer = detectionCircle.AddComponent<MeshRenderer>();
        
            // Создаем материал для круга
            Material circleMaterial = new Material(Shader.Find("Sprites/Default"));
            circleMaterial.color = circleColor;
            circleMeshRenderer.material = circleMaterial;
        
            // Создаем меш круга
            CreateCircleMesh();
        
            // Устанавливаем слой сортировки, чтобы круг был позади объектов
            circleMeshRenderer.sortingOrder = -1;
        }
    
        void CreateCircleMesh()
        {
            Mesh mesh = new Mesh();
        
            // Вершины (центр + точки по окружности)
            Vector3[] vertices = new Vector3[circleSegments + 1];
            vertices[0] = Vector3.zero; // Центр круга
        
            for (int i = 0; i < circleSegments; i++)
            {
                float angle = (float)i / circleSegments * 2f * Mathf.PI;
                vertices[i + 1] = new Vector3(
                    Mathf.Cos(angle) * circleRadius,
                    Mathf.Sin(angle) * circleRadius,
                    0
                );
            }
        
            // Треугольники
            int[] triangles = new int[circleSegments * 3];
            for (int i = 0; i < circleSegments; i++)
            {
                triangles[i * 3] = 0; // Центр
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = (i + 1) % circleSegments + 1;
            }
        
            // UV координаты
            Vector2[] uv = new Vector2[vertices.Length];
            uv[0] = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < circleSegments; i++)
            {
                float angle = (float)i / circleSegments * 2f * Mathf.PI;
                uv[i + 1] = new Vector2(
                    0.5f + Mathf.Cos(angle) * 0.5f,
                    0.5f + Mathf.Sin(angle) * 0.5f
                );
            }
        
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uv;
            mesh.RecalculateNormals();
        
            circleMeshFilter.mesh = mesh;
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
            
                // Получаем компоненты для изменения цвета
                Renderer objRenderer = obj.GetComponent<Renderer>();
                SpriteRenderer spriteRenderer = obj.GetComponent<SpriteRenderer>();
            
                // Создаем данные для движения
                MovingObject movingObj = new MovingObject
                {
                    gameObject = obj,
                    direction = Random.insideUnitCircle.normalized,
                    speed = Random.Range(minSpeed, maxSpeed),
                    noiseOffset = Random.Range(0f, 1000f), // Уникальное смещение для каждого объекта
                    isTarget = false,
                    objectRenderer = objRenderer,
                    spriteRenderer = spriteRenderer,
                };
            
                // Поворачиваем объект в направлении движения (вершина треугольника вперед)
                float angle = Mathf.Atan2(movingObj.direction.y, movingObj.direction.x) * Mathf.Rad2Deg - 90f;
                obj.transform.rotation = Quaternion.Euler(0, 0, angle);
            
                movingObjects.Add(movingObj);
            }
        }
        
        void SelectRandomTarget()
        {
            if (movingObjects.Count == 0) return;
            
            // Сброс предыдущего целевого объекта
            if (targetObject != null)
            {
                targetObject.isTarget = false;
            }
            
            // Выбор случайного объекта как целевого
            int randomIndex = Random.Range(0, movingObjects.Count);
            targetObject = movingObjects[randomIndex];
            targetObject.isTarget = true;
            
            
            // Установка порядка отрисовки для целевого объекта
            if (targetObject.spriteRenderer != null)
            {
                targetObject.spriteRenderer.sortingOrder = 1;
            }

            // Обновляем цвета после смены целевого объекта
            UpdateObjectColors();
        }
        
        void UpdateObjectColors()
        {
            foreach (var movingObj in movingObjects)
            {
                if (movingObj.gameObject == null) continue;
                
                Color colorToApply = movingObj.isTarget ? targetObjectColor : normalObjectColor;
                
                // Пытаемся применить цвет через SpriteRenderer (для 2D спрайтов)
                if (movingObj.spriteRenderer != null)
                {
                    movingObj.spriteRenderer.color = colorToApply;
                    movingObj.spriteRenderer.sortingOrder++;
                }
                // Если SpriteRenderer нет, пытаемся через обычный Renderer
                else if (movingObj.objectRenderer != null)
                {
                    // Создаем новый материал для каждого объекта, чтобы избежать конфликтов
                    Material newMaterial = new Material(movingObj.objectRenderer.material);
                    newMaterial.color = colorToApply;
                    movingObj.objectRenderer.material = newMaterial;
                }
            }
        }
    
        void Update()
        {
            foreach (var movingObj in movingObjects)
            {
                if (movingObj.gameObject == null) continue;

                // Обновляем позицию круга
                if (targetObject != null && detectionCircle != null)
                {
                    Vector3 selectedPos = targetObject.gameObject.transform.position;
                    detectionCircle.transform.position = new Vector3(selectedPos.x, selectedPos.y, 0.1f);
                }
            
                // Применяем cohesion если объект находится в радиусе целевого объекта
                if (cohesion > 0 && targetObject != null && !movingObj.isTarget)
                {
                    ApplyCohesion(movingObj);
                }
                else
                {
                    // Плавное изменение направления с помощью шума Перлина только если не под влиянием cohesion
                    UpdateDirection(movingObj);
                }
            
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
        
        void ApplyCohesion(MovingObject movingObj)
        {
            if (targetObject == null || targetObject.gameObject == null) return;
            
            // Проверяем расстояние до целевого объекта
            Vector2 currentPos = movingObj.gameObject.transform.position;
            Vector2 targetPos = targetObject.gameObject.transform.position;
            float distance = Vector2.Distance(currentPos, targetPos);
            
            // Если объект в радиусе круга
            if (distance <= circleRadius)
            {
                // Направление к целевому объекту
                Vector2 directionToTarget = (targetPos - currentPos).normalized;
                
                // Рассчитываем силу притяжения в зависимости от расстояния
                // Чем ближе к краю круга, тем слабее притяжение
                float distanceRatio = 1f - (distance / circleRadius);
                float attractionStrength = cohesion * cohesionForce * distanceRatio;
                
                // Смешиваем текущее направление с направлением к цели
                Vector2 newDirection = Vector2.Lerp(movingObj.direction, directionToTarget, attractionStrength * Time.deltaTime);
                
                // Добавляем небольшое случайное отклонение для более естественного движения
                float randomAngle = Random.Range(-5f, 5f) * cohesion;
                newDirection = Quaternion.Euler(0, 0, randomAngle) * newDirection;
                
                movingObj.direction = newDirection.normalized;
                
                // Увеличиваем скорость при приближении к цели для более агрессивного преследования
                float speedBoost = 1f + (cohesion * distanceRatio * 0.5f);
                movingObj.speed = Mathf.Lerp(movingObj.speed, maxSpeed * speedBoost, Time.deltaTime * 2f);
            }
            else
            {
                // Возвращаем скорость к нормальной, если объект вне круга
                movingObj.speed = Mathf.Lerp(movingObj.speed, Random.Range(minSpeed, maxSpeed), Time.deltaTime);
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
        
        void OnDestroy()
        {
            if (targetCircle != null)
            {
                Destroy(targetCircle);
            }
        }
    }
}