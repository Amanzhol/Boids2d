using System.Collections.Generic;
using UnityEngine;

namespace Rules
{
    public class SeparationRuleCircle : MonoBehaviour
    {
        [Header("Настройки симуляции")]
        [SerializeField] private int objectCount = 10;
        [SerializeField] private GameObject objectPrefab;
        [SerializeField] private GameObject boundaryPrefab;
    
        [Header("Настройки движения")]
        [SerializeField] private float minSpeed = 1f;
        [SerializeField] private float maxSpeed = 5f;
        [SerializeField] private float rotationSpeed = 180f;
        [SerializeField] private float wanderStrength = 30f;
        [SerializeField] private float noiseScale = 1f;
    
        [Header("Настройки взаимодействия")]
        [SerializeField] [Range(0f, 1f)] private float separation = 1f;
        [SerializeField] private float detectionRadius = 3f;
        [SerializeField] private float separationForce = 5f;
        [SerializeField] private Color lineColor = Color.red;
        [SerializeField] private float lineWidth = 0.02f;
    
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
        private MovingObject selectedObject;
        private LineRenderer lineRenderer;
        private GameObject detectionCircle;
        private MeshRenderer circleMeshRenderer;
        private MeshFilter circleMeshFilter;
    
        private class MovingObject
        {
            public GameObject gameObject;
            public Vector2 direction;
            public float speed;
            public float noiseOffset;
        }
    
        void Start()
        {
            SetupBoundary();
            SpawnObjects();
            SetupLineRenderer();
            SetupDetectionCircle();
        
            // Выбираем случайный объект для отображения линий
            if (movingObjects.Count > 0)
            {
                selectedObject = movingObjects[Random.Range(0, movingObjects.Count)];
                selectedObject.gameObject.GetComponent<SpriteRenderer>().color = targetObjectColor;
            }
        }
    
        void SetupLineRenderer()
        {
            GameObject lineObj = new GameObject("Connection Lines");
            lineRenderer = lineObj.AddComponent<LineRenderer>();
            lineRenderer.startWidth = lineWidth;
            lineRenderer.endWidth = lineWidth;
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.startColor = lineColor;
            lineRenderer.endColor = lineColor;
            lineRenderer.positionCount = 0;
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
        
            boundary = Instantiate(boundaryPrefab, Vector3.zero, Quaternion.identity);
        
            Collider2D collider = boundary.GetComponent<Collider2D>();
            if (collider != null)
            {
                boundaryBounds = collider.bounds;
            }
            else
            {
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
                
                movingObj.gameObject.GetComponent<SpriteRenderer>().color = normalObjectColor;
            
                float angle = Mathf.Atan2(movingObj.direction.y, movingObj.direction.x) * Mathf.Rad2Deg - 90f;
                obj.transform.rotation = Quaternion.Euler(0, 0, angle);
            
                movingObjects.Add(movingObj);
            }
        }
    
        void Update()
        {
            List<Vector3> linePositions = new List<Vector3>();
        
            foreach (var movingObj in movingObjects)
            {
                if (movingObj.gameObject == null) continue;
            
                // Сначала применяем все силы влияния
                Vector2 desiredDirection = movingObj.direction;
            
                // Плавное изменение направления (блуждание)
                desiredDirection = ApplyWander(movingObj, desiredDirection);
            
                // Применяем разделение если separation > 0
                if (separation > 0)
                {
                    desiredDirection = ApplySeparation(movingObj, desiredDirection);
                }
            
                // Плавно интерполируем к желаемому направлению
                movingObj.direction = Vector2.Lerp(movingObj.direction, desiredDirection.normalized, Time.deltaTime * 3f).normalized;
            
                // Движение
                Vector3 movement = new Vector3(movingObj.direction.x, movingObj.direction.y, 0) * (movingObj.speed * Time.deltaTime);
                movingObj.gameObject.transform.position += movement;
            
                // Поворот объекта в направлении движения
                float targetAngle = Mathf.Atan2(movingObj.direction.y, movingObj.direction.x) * Mathf.Rad2Deg - 90f;
                movingObj.gameObject.transform.rotation = Quaternion.Euler(0, 0, targetAngle);
            
                // Проверка границ
                CheckBoundaries(movingObj);
            
                // Собираем позиции для линий от выбранного объекта
                if (selectedObject != null && movingObj == selectedObject)
                {
                    Vector3 selectedPos = selectedObject.gameObject.transform.position;
                
                    // Обновляем позицию круга
                    if (detectionCircle != null)
                    {
                        detectionCircle.transform.position = new Vector3(selectedPos.x, selectedPos.y, 0.1f);
                    }
                
                    foreach (var otherObj in movingObjects)
                    {
                        if (otherObj == selectedObject || otherObj.gameObject == null) continue;
                    
                        Vector3 otherPos = otherObj.gameObject.transform.position;
                        float distance = Vector2.Distance(selectedPos, otherPos);
                    
                        if (distance <= detectionRadius)
                        {
                            linePositions.Add(selectedPos);
                            linePositions.Add(otherPos);
                        }
                    }
                }
            }
        
            // Обновляем LineRenderer
            UpdateLineRenderer(linePositions);
        }
    
        void UpdateLineRenderer(List<Vector3> positions)
        {
            if (lineRenderer == null) return;
        
            if (positions.Count == 0)
            {
                lineRenderer.positionCount = 0;
                return;
            }
        
            // Создаем массив позиций для всех линий
            lineRenderer.positionCount = positions.Count;
        
            for (int i = 0; i < positions.Count; i++)
            {
                lineRenderer.SetPosition(i, positions[i]);
            }
        }
    
        Vector2 ApplySeparation(MovingObject movingObj, Vector2 currentDirection)
        {
            Vector2 steerDirection = currentDirection;
            Vector2 totalAvoidance = Vector2.zero;
            int nearbyCount = 0;
        
            foreach (var otherObj in movingObjects)
            {
                if (otherObj == movingObj || otherObj.gameObject == null) continue;
            
                Vector2 offset = (movingObj.gameObject.transform.position - otherObj.gameObject.transform.position);
                float distance = offset.magnitude;
            
                if (distance < detectionRadius && distance > 0)
                {
                    // Нормализуем вектор отталкивания
                    Vector2 pushDirection = offset.normalized;
                
                    // Сила отталкивания обратно пропорциональна расстоянию
                    float strength = 1f - (distance / detectionRadius);
                    strength *= strength; // Квадратичная зависимость для более сильного эффекта вблизи
                
                    // Добавляем к общему вектору избегания
                    totalAvoidance += pushDirection * strength;
                    nearbyCount++;
                }
            }
        
            // Если есть соседи поблизости
            if (nearbyCount > 0)
            {
                // Нормализуем общий вектор избегания
                if (totalAvoidance.magnitude > 0)
                {
                    totalAvoidance = totalAvoidance.normalized;
                
                    // Комбинируем текущее направление с вектором избегания
                    steerDirection = currentDirection + (totalAvoidance * (separationForce * separation));
                    steerDirection = steerDirection.normalized;
                }
            }
        
            return steerDirection;
        }
    
        Vector2 ApplyWander(MovingObject movingObj, Vector2 currentDirection)
        {
            float noiseValue = Mathf.PerlinNoise(
                Time.time * noiseScale + movingObj.noiseOffset, 
                movingObj.noiseOffset
            );
        
            float turnAmount = (noiseValue - 0.5f) * 2f;
            float rotationAngle = turnAmount * wanderStrength * Time.deltaTime;
            Vector2 newDirection = Quaternion.Euler(0, 0, rotationAngle) * currentDirection;
            return newDirection.normalized;
        }
    
        void CheckBoundaries(MovingObject movingObj)
        {
            Vector3 pos = movingObj.gameObject.transform.position;
            bool bounced = false;
        
            if (pos.x <= boundaryBounds.min.x + 0.5f || pos.x >= boundaryBounds.max.x - 0.5f)
            {
                movingObj.direction.x = -movingObj.direction.x;
                bounced = true;
            
                if (pos.x <= boundaryBounds.min.x + 0.5f)
                    pos.x = boundaryBounds.min.x + 0.5f;
                else
                    pos.x = boundaryBounds.max.x - 0.5f;
            }
        
            if (pos.y <= boundaryBounds.min.y + 0.5f || pos.y >= boundaryBounds.max.y - 0.5f)
            {
                movingObj.direction.y = -movingObj.direction.y;
                bounced = true;
            
                if (pos.y <= boundaryBounds.min.y + 0.5f)
                    pos.y = boundaryBounds.min.y + 0.5f;
                else
                    pos.y = boundaryBounds.max.y - 0.5f;
            }
        
            if (bounced)
            {
                movingObj.gameObject.transform.position = pos;
                float randomDeviation = Random.Range(-15f, 15f);
                movingObj.direction = Quaternion.Euler(0, 0, randomDeviation) * movingObj.direction;
                movingObj.direction = movingObj.direction.normalized;
            
                float angle = Mathf.Atan2(movingObj.direction.y, movingObj.direction.x) * Mathf.Rad2Deg - 90f;
                movingObj.gameObject.transform.rotation = Quaternion.Euler(0, 0, angle);
            }
        }
    
        // public void SetObjectCount(int newCount)
        // {
        //     if (newCount < movingObjects.Count)
        //     {
        //         for (int i = movingObjects.Count - 1; i >= newCount; i--)
        //         {
        //             if (movingObjects[i].gameObject != null)
        //                 Destroy(movingObjects[i].gameObject);
        //             movingObjects.RemoveAt(i);
        //         }
        //     }
        //     else if (newCount > movingObjects.Count)
        //     {
        //         int toAdd = newCount - movingObjects.Count;
        //         objectCount = newCount;
        //         for (int i = 0; i < toAdd; i++)
        //         {
        //             SpawnSingleObject();
        //         }
        //     }
        //     objectCount = newCount;
        //
        //     // Переназначаем выбранный объект если нужно
        //     if (selectedObject == null || selectedObject.gameObject == null)
        //     {
        //         if (movingObjects.Count > 0)
        //         {
        //             selectedObject = movingObjects[Random.Range(0, movingObjects.Count)];
        //         }
        //     }
        // }
    
        // void SpawnSingleObject()
        // {
        //     if (objectPrefab == null) return;
        //
        //     float x = Random.Range(boundaryBounds.min.x + 0.5f, boundaryBounds.max.x - 0.5f);
        //     float y = Random.Range(boundaryBounds.min.y + 0.5f, boundaryBounds.max.y - 0.5f);
        //     Vector3 position = new Vector3(x, y, 0);
        //
        //     GameObject obj = Instantiate(objectPrefab, position, Quaternion.identity);
        //
        //     MovingObject movingObj = new MovingObject
        //     {
        //         gameObject = obj,
        //         direction = Random.insideUnitCircle.normalized,
        //         speed = Random.Range(minSpeed, maxSpeed),
        //         noiseOffset = Random.Range(0f, 1000f)
        //     };
        //
        //     float angle = Mathf.Atan2(movingObj.direction.y, movingObj.direction.x) * Mathf.Rad2Deg - 90f;
        //     obj.transform.rotation = Quaternion.Euler(0, 0, angle);
        //
        //     movingObjects.Add(movingObj);
        // }
    }
}