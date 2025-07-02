using System.Collections.Generic;
using UnityEngine;

namespace Rules
{
    public class AlignmentRule : MonoBehaviour
    {
        [Header("Настройки симуляции")]
        [SerializeField] private int objectCount = 10;
        [SerializeField] private GameObject objectPrefab;
        [SerializeField] private GameObject boundaryPrefab;
        [SerializeField] private Color defaultObjectColor = Color.green;
    
        [Header("Настройки движения")]
        [SerializeField] private float minSpeed = 1f;
        [SerializeField] private float maxSpeed = 5f;
        [SerializeField] private float rotationSpeed = 180f;
        [SerializeField] private float wanderStrength = 30f;
        [SerializeField] private float noiseScale = 1f;
        
        [Header("Настройки целевого объекта")]
        [SerializeField] private float circleRadius = 3f;
        [SerializeField] private Color circleColor = new Color(1f, 0f, 0f, 0.3f);
        [SerializeField] private Color targetObjectColor = Color.red; // Цвет целевого объекта
        [SerializeField] private GameObject circlePrefab; // Префаб для визуализации круга
        
        [Header("Настройки выравнивания")]
        [SerializeField] [Range(0f, 1f)] private float alignment = 0f;
        [SerializeField] private float alignmentSpeed = 2f; // Скорость выравнивания направления
        
        [Header("Настройки линий направления")]
        [SerializeField] private float lineLength = 1.5f;
        [SerializeField] private float lineWidth = 0.1f;
        [SerializeField] private Color alignedLineColor = Color.cyan;
        [SerializeField] private float alignmentThreshold = 0.8f; // Порог схожести направлений для отображения линии
    
        private GameObject boundary;
        private Bounds boundaryBounds;
        private List<MovingObject> movingObjects = new List<MovingObject>();
        private MovingObject targetObject;
        private GameObject targetCircle;
        private Dictionary<MovingObject, LineRenderer> directionLines = new Dictionary<MovingObject, LineRenderer>();
    
        private class MovingObject
        {
            public GameObject gameObject;
            public Vector2 direction;
            public float speed;
            public float noiseOffset;
            public bool isTarget;
            public bool isAligned; // Находится ли объект в состоянии выравнивания
        }
    
        void Start()
        {
            SetupBoundary();
            SpawnObjects();
            SelectTargetObject();
        }
        
        void SelectTargetObject()
        {
            if (movingObjects.Count == 0) return;
            
            // Выбираем случайный объект как целевой
            int randomIndex = Random.Range(0, movingObjects.Count);
            targetObject = movingObjects[randomIndex];
            targetObject.isTarget = true;
            
            // Изменяем цвет целевого объекта
            Renderer targetRenderer = targetObject.gameObject.GetComponent<Renderer>();
            if (targetRenderer != null)
            {
                // Создаем новый материал, чтобы не влиять на другие объекты
                Material newMaterial = new Material(targetRenderer.material);
                newMaterial.color = targetObjectColor;
                targetRenderer.material = newMaterial;
            }
            else
            {
                // Если нет Renderer, пробуем SpriteRenderer
                SpriteRenderer targetSprite = targetObject.gameObject.GetComponent<SpriteRenderer>();
                if (targetSprite != null)
                {
                    targetSprite.color = targetObjectColor;
                }
            }
            
            // Создаем визуальный круг вокруг целевого объекта
            CreateTargetCircle();
        }
        
        void CreateTargetCircle()
        {
            if (circlePrefab != null)
            {
                targetCircle = Instantiate(circlePrefab, targetObject.gameObject.transform.position, Quaternion.identity);
                targetCircle.transform.localScale = Vector3.one * circleRadius * 2f;
                
                // Устанавливаем цвет
                Renderer circleRenderer = targetCircle.GetComponent<Renderer>();
                if (circleRenderer != null)
                {
                    circleRenderer.material.color = circleColor;
                }
            }
            else
            {
                // Создаем простой круг с помощью LineRenderer
                GameObject circleGO = new GameObject("TargetCircle");
                LineRenderer lr = circleGO.AddComponent<LineRenderer>();
                lr.material = new Material(Shader.Find("Sprites/Default"));
                lr.startColor = circleColor;
                lr.endColor = circleColor;
                lr.startWidth = 0.1f;
                lr.endWidth = 0.1f;
                lr.positionCount = 64;
                
                for (int i = 0; i < 64; i++)
                {
                    float angle = i * Mathf.PI * 2f / 64;
                    Vector3 pos = new Vector3(Mathf.Cos(angle) * circleRadius, Mathf.Sin(angle) * circleRadius, -0.1f);
                    lr.SetPosition(i, pos);
                }
                
                targetCircle = circleGO;
            }
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
                
                // Устанавливаем цвет по умолчанию для обычных объектов
                Renderer objRenderer = obj.GetComponent<Renderer>();
                if (objRenderer != null && objectPrefab.GetComponent<Renderer>().sharedMaterial.color == Color.white)
                {
                    objRenderer.material.color = defaultObjectColor;
                }
            
                MovingObject movingObj = new MovingObject
                {
                    gameObject = obj,
                    direction = Random.insideUnitCircle.normalized,
                    speed = Random.Range(minSpeed, maxSpeed),
                    noiseOffset = Random.Range(0f, 1000f),
                    isTarget = false,
                    isAligned = false
                };
            
                float angle = Mathf.Atan2(movingObj.direction.y, movingObj.direction.x) * Mathf.Rad2Deg - 90f;
                obj.transform.rotation = Quaternion.Euler(0, 0, angle);
            
                movingObjects.Add(movingObj);
                
                // Создаем LineRenderer для отображения направления
                CreateDirectionLine(movingObj);
            }
        }
        
        void CreateDirectionLine(MovingObject movingObj)
        {
            GameObject lineGO = new GameObject("DirectionLine");
            lineGO.transform.parent = movingObj.gameObject.transform;
            LineRenderer lr = lineGO.AddComponent<LineRenderer>();
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = alignedLineColor;
            lr.endColor = alignedLineColor;
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth * 0.5f;
            lr.positionCount = 2;
            lr.enabled = false; // Изначально выключен
            
            directionLines[movingObj] = lr;
        }
    
        void Update()
        {
            // Обновляем позицию круга целевого объекта
            if (targetCircle != null && targetObject != null)
            {
                targetCircle.transform.position = targetObject.gameObject.transform.position;
            }
            
            foreach (var movingObj in movingObjects)
            {
                if (movingObj.gameObject == null) continue;
                
                // Проверяем, находится ли объект в круге целевого объекта
                if (targetObject != null && !movingObj.isTarget)
                {
                    float distance = Vector2.Distance(
                        movingObj.gameObject.transform.position, 
                        targetObject.gameObject.transform.position
                    );
                    
                    movingObj.isAligned = distance <= circleRadius;
                }
                
                // Обновляем направление в зависимости от alignment
                if (alignment > 0 && movingObj.isAligned && targetObject != null)
                {
                    // Плавное выравнивание направления с целевым объектом
                    Vector2 targetDir = targetObject.direction;
                    movingObj.direction = Vector2.Lerp(
                        movingObj.direction, 
                        targetDir, 
                        alignment * alignmentSpeed * Time.deltaTime
                    ).normalized;
                }
                else
                {
                    // Хаотичное движение
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
                
                // Обновляем линию направления
                UpdateDirectionLine(movingObj);
            }
        }
        
        void UpdateDirectionLine(MovingObject movingObj)
        {
            if (!directionLines.ContainsKey(movingObj)) return;
            
            LineRenderer lr = directionLines[movingObj];
            
            // Показываем линию только у объектов внутри круга целевого объекта
            bool showLine = false;
            
            if (targetObject != null)
            {
                if (movingObj.isTarget)
                {
                    // Целевой объект всегда показывает линию
                    showLine = true;
                }
                else
                {
                    // Проверяем, находится ли объект внутри круга
                    float distance = Vector2.Distance(
                        movingObj.gameObject.transform.position, 
                        targetObject.gameObject.transform.position
                    );
                    
                    // Показываем линию только если объект внутри круга
                    showLine = distance <= circleRadius;
                }
            }
            
            lr.enabled = showLine;
            
            if (showLine)
            {
                Vector3 startPos = movingObj.gameObject.transform.position;
                Vector3 endPos = startPos + new Vector3(movingObj.direction.x, movingObj.direction.y, 0) * lineLength;
                
                lr.SetPosition(0, startPos);
                lr.SetPosition(1, endPos);
                
                // Устанавливаем цвет линии
                if (movingObj.isTarget)
                {
                    lr.startColor = targetObjectColor;
                    lr.endColor = targetObjectColor;
                }
                else
                {
                    lr.startColor = alignedLineColor;
                    lr.endColor = alignedLineColor;
                }
            }
        }
    
        void UpdateDirection(MovingObject movingObj)
        {
            float noiseValue = Mathf.PerlinNoise(
                Time.time * noiseScale + movingObj.noiseOffset, 
                movingObj.noiseOffset
            );
        
            float turnAmount = (noiseValue - 0.5f) * 2f;
        
            float rotationAngle = turnAmount * wanderStrength * Time.deltaTime;
            Vector2 newDirection = Quaternion.Euler(0, 0, rotationAngle) * movingObj.direction;
            movingObj.direction = newDirection.normalized;
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
    
        public void SetObjectCount(int newCount)
        {
            if (newCount < movingObjects.Count)
            {
                for (int i = movingObjects.Count - 1; i >= newCount; i--)
                {
                    if (directionLines.ContainsKey(movingObjects[i]))
                    {
                        Destroy(directionLines[movingObjects[i]].gameObject);
                        directionLines.Remove(movingObjects[i]);
                    }
                    
                    if (movingObjects[i].gameObject != null)
                        Destroy(movingObjects[i].gameObject);
                    movingObjects.RemoveAt(i);
                }
            }
            else if (newCount > movingObjects.Count)
            {
                int toAdd = newCount - movingObjects.Count;
                objectCount = newCount;
                for (int i = 0; i < toAdd; i++)
                {
                    SpawnSingleObject();
                }
            }
            objectCount = newCount;
        }
        
        public void SetAlignment(float value)
        {
            alignment = Mathf.Clamp01(value);
        }
        
        public void SetCircleRadius(float radius)
        {
            circleRadius = radius;
            if (targetCircle != null)
            {
                targetCircle.transform.localScale = Vector3.one * circleRadius * 2f;
            }
        }
        
        public void SetCircleColor(Color color)
        {
            circleColor = color;
            if (targetCircle != null)
            {
                Renderer circleRenderer = targetCircle.GetComponent<Renderer>();
                if (circleRenderer != null)
                {
                    circleRenderer.material.color = circleColor;
                }
                else
                {
                    LineRenderer lr = targetCircle.GetComponent<LineRenderer>();
                    if (lr != null)
                    {
                        lr.startColor = circleColor;
                        lr.endColor = circleColor;
                    }
                }
            }
        }
    
        public void SetTargetObjectColor(Color color)
        {
            targetObjectColor = color;
            
            if (targetObject != null)
            {
                // Обновляем цвет целевого объекта
                Renderer targetRenderer = targetObject.gameObject.GetComponent<Renderer>();
                if (targetRenderer != null)
                {
                    // Создаем новый материал, если еще не создан
                    if (targetRenderer.material.name.Contains("Instance"))
                    {
                        targetRenderer.material.color = targetObjectColor;
                    }
                    else
                    {
                        Material newMaterial = new Material(targetRenderer.material);
                        newMaterial.color = targetObjectColor;
                        targetRenderer.material = newMaterial;
                    }
                }
                else
                {
                    SpriteRenderer targetSprite = targetObject.gameObject.GetComponent<SpriteRenderer>();
                    if (targetSprite != null)
                    {
                        targetSprite.color = targetObjectColor;
                    }
                }
                
                // Обновляем цвет линии целевого объекта
                if (directionLines.ContainsKey(targetObject))
                {
                    LineRenderer lr = directionLines[targetObject];
                    lr.startColor = targetObjectColor;
                    lr.endColor = targetObjectColor;
                }
            }
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
                noiseOffset = Random.Range(0f, 1000f),
                isTarget = false,
                isAligned = false
            };
        
            float angle = Mathf.Atan2(movingObj.direction.y, movingObj.direction.x) * Mathf.Rad2Deg - 90f;
            obj.transform.rotation = Quaternion.Euler(0, 0, angle);
        
            movingObjects.Add(movingObj);
            CreateDirectionLine(movingObj);
        }
        
        void OnDestroy()
        {
            // Очистка ресурсов
            if (targetCircle != null)
                Destroy(targetCircle);
                
            foreach (var lr in directionLines.Values)
            {
                if (lr != null)
                    Destroy(lr.gameObject);
            }
            directionLines.Clear();
        }
    }
}