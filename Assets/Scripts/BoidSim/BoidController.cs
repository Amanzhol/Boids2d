using System.Collections.Generic;
using UnityEngine;

namespace BoidSim
{
    public class BoidController : MonoBehaviour
    {
        public BoidCollection boidCollection;
        
        [Header("Schooling Behavior")]
        public float cohesionFactor = 1.0f;     // Сила сближения
        public float alignmentFactor = 1.0f;    // Сила выравнивания
        public float separationFactor = 1.0f;   // Сила разделения
        public float schoolingRadius = 5.0f;    // Радиус "видимости"
        public float separationRadius = 2.0f;   // Радиус избегания
        
        [Header("Inter-School Avoidance")]
        // Сила избегания между разными стаями
        public float interSchoolAvoidanceFactor = 2.0f;
        // Радиус избегания между разными стаями
        public float interSchoolAvoidanceRadius = 8.0f;
        
        [Header("Solo Behavior")]
        public float randomMovementFactor = 1.0f;
        public float avoidanceFactor = 2.0f;
        public float avoidanceRadius = 3.0f;
        
        [Header("Movement Settings")]
        public float rotationSpeed = 3.0f;
        
        [Header("Boundary Settings")]
        // Префаб с прямоугольным спрайтом для определения границ
        public GameObject boundariesPrefab;
        // Сила отталкивания
        public float boundaryForce = 5.0f;
        // Расстояние от границы, на котором начинает действовать сила отталкивания
        public float boundaryBuffer = 3.0f;
        
        private List<BoidInfo> allBoid = new List<BoidInfo>();
        private List<BoidInfo> schoolingBoid = new List<BoidInfo>();
        private List<BoidInfo> soloBoid = new List<BoidInfo>();
        
        [SerializeField] private bool debugDrawGizmos = true;
        
        // Границы спрайта
        private Bounds spriteBounds;
        private bool hasBoundaries = false;
        
        // Хранит информацию о каждой рыбе
        private class BoidInfo
        {
            public GameObject boidObject;
            public Transform transform;
            public BoidData data;
            public Vector2 velocity;
            public bool isSchooling;
            public int schoolId; // ID стаи, к которой принадлежит рыба
        }
        
        // Словарь для группировки рыб по типу стаи
        private Dictionary<BoidData, List<BoidInfo>> schoolsByType = new Dictionary<BoidData, List<BoidInfo>>();
        
        void Start()
        {
            InitializeBoundaries();
            SpawnBoids();
        }
        
        void InitializeBoundaries()
        {
            if (boundariesPrefab != null)
            {
                // Получаем компонент SpriteRenderer из префаба
                SpriteRenderer spriteRenderer = boundariesPrefab.GetComponent<SpriteRenderer>();
                if (spriteRenderer != null && spriteRenderer.sprite != null)
                {
                    spriteBounds = spriteRenderer.bounds;
                    hasBoundaries = true;
                    Debug.Log($"Границы установлены: {spriteBounds}");
                }
                else
                {
                    Debug.LogWarning("boundariesPrefab не содержит SpriteRenderer или спрайт!");
                }
            }
            else
            {
                Debug.LogWarning("boundariesPrefab не назначен!");
            }
        }

        void SpawnBoids()
        {
            if (!hasBoundaries)
            {
                Debug.LogError("Границы не установлены! Спавн рыб отменен.");
                return;
            }
            
            // Группируем стайных рыб в нескольких точках для начального формирования стай
            Dictionary<BoidData, List<Vector2>> schoolSpawnPoints = new Dictionary<BoidData, List<Vector2>>();
            Dictionary<BoidData, float>
                schoolDirections = new Dictionary<BoidData, float>(); // Направления для каждого типа стаи

            foreach (var BoidData in boidCollection.boids)
            {
                if (BoidData.isSchooling)
                {
                    // Создаем список точек спавна для этого типа рыб
                    schoolSpawnPoints[BoidData] = new List<Vector2>();

                    if (BoidData.isRandomSpawn)
                    {
                        // Для каждого типа стайных рыб создаем несколько точек спавна в зависимости от количества
                        int spawnPoints = Mathf.Min(3, Mathf.Max(1, BoidData.boidCount / 15));

                        for (int i = 0; i < spawnPoints; i++)
                        {
                            // Случайная точка внутри границ спрайта (с отступом от края)
                            Vector2 spawnPoint = new Vector2(
                                Random.Range(spriteBounds.min.x * 0.7f, spriteBounds.max.x * 0.7f),
                                Random.Range(spriteBounds.min.y * 0.7f, spriteBounds.max.y * 0.7f)
                            );

                            schoolSpawnPoints[BoidData].Add(spawnPoint);
                        }
                    }
                    else
                    {
                        // Используем заданную точку спавна
                        schoolSpawnPoints[BoidData].Add(BoidData.spawnPoint);
                    }

                    // Генерируем случайное направление для этого типа стаи
                    schoolDirections[BoidData] = Random.Range(0f, 360f);

                    // Инициализируем список для этого типа рыб
                    schoolsByType[BoidData] = new List<BoidInfo>();
                }
                else
                {
                    // Для одиночных рыб также генерируем случайное направление
                    schoolDirections[BoidData] = Random.Range(0f, 360f);
                }
            }

            // Теперь создаем рыб
            foreach (var BoidData in boidCollection.boids)
            {
                // Присваиваем уникальный ID для стаи этого типа
                int schoolId = 0;

                for (int i = 0; i < BoidData.boidCount; i++)
                {
                    Vector2 spawnPos;
                    float rotationAngle = 0f;

                    if (BoidData.isSchooling && schoolSpawnPoints.ContainsKey(BoidData) &&
                        schoolSpawnPoints[BoidData].Count > 0)
                    {
                        // Для стайных рыб выбираем точку спавна для этого типа
                        int spawnIndex = i % schoolSpawnPoints[BoidData].Count;
                        Vector2 schoolCenter = schoolSpawnPoints[BoidData][spawnIndex];

                        // Определяем школу по индексу спавна
                        schoolId = spawnIndex;

                        // Спавним рыбу в небольшом радиусе от центра
                        float radius = Random.Range(0.5f, 2.0f);
                        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                        spawnPos = schoolCenter + new Vector2(
                            Mathf.Cos(angle) * radius,
                            Mathf.Sin(angle) * radius
                        );

                        // Устанавливаем начальный угол поворота
                        if (BoidData.isRandomSpawn)
                        {
                            // Для случайного спавна: базовый угол для стаи + небольшой разброс
                            float baseAngle = (spawnIndex * 120f) % 360f; // Разные направления для разных стай
                            rotationAngle = baseAngle + Random.Range(-30f, 30f);
                        }
                        else
                        {
                            // Для фиксированного спавна: все рыбы стаи смотрят в одном направлении с небольшим разбросом
                            rotationAngle = schoolDirections[BoidData] + Random.Range(-20f, 20f);
                        }
                    }
                    else
                    {
                        // Для одиночных рыб
                        if (BoidData.isRandomSpawn)
                        {
                            // Случайная позиция внутри границ спрайта
                            spawnPos = new Vector2(
                                Random.Range(spriteBounds.min.x * 0.9f, spriteBounds.max.x * 0.9f),
                                Random.Range(spriteBounds.min.y * 0.9f, spriteBounds.max.y * 0.9f)
                            );
                        }
                        else
                        {
                            // Спавн в заданной точке с небольшим разбросом
                            float radius = Random.Range(0.2f, 1.0f);
                            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                            spawnPos = BoidData.spawnPoint + new Vector2(
                                Mathf.Cos(angle) * radius,
                                Mathf.Sin(angle) * radius
                            );
                        }

                        // Угол поворота для одиночных рыб
                        if (BoidData.isRandomSpawn)
                        {
                            rotationAngle = Random.Range(0f, 360f);
                        }
                        else
                        {
                            // Для фиксированного спавна используем направление с разбросом
                            rotationAngle = schoolDirections[BoidData] + Random.Range(-45f, 45f);
                        }
                    }

                    // Создаем объект рыбы с нужным поворотом в 2D
                    GameObject boidObj = Instantiate(BoidData.boidPrefab, spawnPos,
                        Quaternion.Euler(0, 0, rotationAngle));

                    // Начальная скорость в направлении взгляда
                    Vector2 initialVelocity = new Vector2(
                        Mathf.Cos(rotationAngle * Mathf.Deg2Rad),
                        Mathf.Sin(rotationAngle * Mathf.Deg2Rad)
                    ).normalized * BoidData.moveSpeed;

                    // Создаем информацию о рыбе
                    BoidInfo boidInfo = new BoidInfo
                    {
                        boidObject = boidObj,
                        transform = boidObj.transform,
                        data = BoidData,
                        isSchooling = BoidData.isSchooling,
                        velocity = initialVelocity,
                        schoolId = schoolId // Сохраняем ID стаи
                    };

                    // Добавляем в соответствующие списки
                    allBoid.Add(boidInfo);

                    if (BoidData.isSchooling)
                    {
                        schoolingBoid.Add(boidInfo);
                        schoolsByType[BoidData].Add(boidInfo);
                    }
                    else
                    {
                        soloBoid.Add(boidInfo);
                    }
                }
            }

            Debug.Log($"Создано рыб: {allBoid.Count} всего, {schoolingBoid.Count} стайных, {soloBoid.Count} одиночных");
        }

        void Update()
        {
            // Обновляем стайных рыб - для каждого типа отдельно
            foreach (var schoolGroup in schoolsByType)
            {
                UpdateSchoolingBoidGroup(schoolGroup.Value);
            }
            
            // Обновляем одиночных рыб
            UpdateSoloBoid();
            
            // Применяем движение ко всем рыбам
            ApplyMovement();
        }
        
        void UpdateSchoolingBoidGroup(List<BoidInfo> schoolGroup)
        {
            // Для каждой стайной рыбы в группе
            foreach (var boid in schoolGroup)
            {
                Vector2 cohesion = Vector2.zero;
                Vector2 alignment = Vector2.zero;
                Vector2 separation = Vector2.zero;
                int neighborCount = 0;
                
                // Проверяем только рыб в той же группе (того же типа)
                foreach (var otherBoid in schoolGroup)
                {
                    if (otherBoid == boid) continue;
                    
                    float distance = Vector2.Distance(
                        boid.transform.position, 
                        otherBoid.transform.position
                    );
                    
                    // Если рыба в радиусе стаи
                    if (distance < schoolingRadius)
                    {
                        // Приоритезируем соседей из той же начальной стаи
                        float multiplier = (otherBoid.schoolId == boid.schoolId) ? 1.5f : 1.0f;
                        
                        // Сплочение - движение к центру масс стаи
                        cohesion += (Vector2)otherBoid.transform.position * multiplier;
                        
                        // Выравнивание - движение в одном направлении
                        alignment += otherBoid.velocity * multiplier;
                        
                        neighborCount++;
                        
                        // Разделение - избегание слишком близких соседей
                        if (distance < separationRadius)
                        {
                            Vector2 moveAway = (Vector2)boid.transform.position - (Vector2)otherBoid.transform.position;
                            // Чем ближе, тем сильнее отталкивание (обратно пропорционально расстоянию)
                            float strength = Mathf.Clamp01(1.0f - distance/separationRadius);
                            strength = strength * strength; // Квадратичное усиление
                            separation += moveAway.normalized * strength;
                        }
                    }
                }
                
                // Только если есть соседи
                if (neighborCount > 0)
                {
                    // Вычисляем средние значения
                    cohesion = cohesion / neighborCount - (Vector2)boid.transform.position;
                    alignment = alignment / neighborCount;
                    
                    // Нормализуем векторы для равного влияния
                    if (cohesion.sqrMagnitude > 0.001f) cohesion.Normalize();
                    if (alignment.sqrMagnitude > 0.001f) alignment.Normalize();
                    if (separation.sqrMagnitude > 0.001f) separation.Normalize();
                    
                    // Применяем силы с весами
                    Vector2 totalForce = cohesion * cohesionFactor +
                                         alignment * alignmentFactor +
                                         separation * separationFactor;
                    
                    // Применяем силы к скорости (с усилением для более выраженного стайного поведения)
                    boid.velocity += totalForce * 2.0f * Time.deltaTime;
                }
                else
                {
                    // Если нет соседей, добавляем случайное движение для поиска стаи
                    boid.velocity += (Vector2)Random.insideUnitCircle * randomMovementFactor * 0.8f * Time.deltaTime;
                }
                
                // Дополнительное избегание одиночных рыб
                foreach (var otherBoid in soloBoid)
                {
                    float distance = Vector2.Distance(
                        boid.transform.position, 
                        otherBoid.transform.position
                    );
                    
                    if (distance < separationRadius * 1.5f)
                    {
                        Vector2 moveAway = (Vector2)boid.transform.position - (Vector2)otherBoid.transform.position;
                        float strength = 1.0f - distance/(separationRadius * 1.5f);
                        boid.velocity += moveAway.normalized * strength * separationFactor * 0.5f * Time.deltaTime;
                    }
                }
                
                // Избегание рыб из других стай
                ApplyInterSchoolAvoidance(boid, schoolGroup);
            }
        }
        
        void ApplyInterSchoolAvoidance(BoidInfo boid, List<BoidInfo> currentSchoolGroup)
        {
            Vector2 avoidance = Vector2.zero;
            int avoidanceCount = 0;
            
            // Проверяем все другие стайные группы
            foreach (var otherSchoolGroup in schoolsByType.Values)
            {
                // Пропускаем свою группу
                if (otherSchoolGroup == currentSchoolGroup) continue;
                
                // Проверяем рыб из других стай
                foreach (var otherBoid in otherSchoolGroup)
                {
                    float distance = Vector2.Distance(boid.transform.position, otherBoid.transform.position);
                    
                    if (distance < interSchoolAvoidanceRadius)
                    {
                        Vector2 moveAway = (Vector2)boid.transform.position - (Vector2)otherBoid.transform.position;
                        
                        // Чем ближе другая стая, тем сильнее избегание
                        float strength = Mathf.Clamp01(1.0f - distance / interSchoolAvoidanceRadius);
                        strength = strength * strength; // Квадратичное усиление
                        
                        // Дополнительное усиление для рыб из той же начальной стаи
                        float schoolMultiplier = (otherBoid.schoolId == boid.schoolId) ? 0.5f : 1.0f;
                        
                        avoidance += moveAway.normalized * strength * schoolMultiplier;
                        avoidanceCount++;
                    }
                }
            }
            
            // Если есть что избегать
            if (avoidanceCount > 0)
            {
                avoidance = avoidance / avoidanceCount; // Усредняем
                if (avoidance.sqrMagnitude > 0.001f)
                {
                    avoidance.Normalize();
                    boid.velocity += avoidance * interSchoolAvoidanceFactor * Time.deltaTime;
                }
            }
        }
        
        void UpdateSoloBoid()
        {
            // Для каждой одиночной рыбы
            foreach (var boid in soloBoid)
            {
                Vector2 avoidance = Vector2.zero;
                
                // Избегаем всех других рыб
                foreach (var otherBoid in allBoid)
                {
                    if (otherBoid == boid) continue;
                    
                    float distance = Vector2.Distance(
                        boid.transform.position, 
                        otherBoid.transform.position
                    );
                    
                    // Если рыба слишком близко
                    if (distance < avoidanceRadius)
                    {
                        Vector2 moveAway = (Vector2)boid.transform.position - (Vector2)otherBoid.transform.position;
                        // Чем ближе, тем сильнее отталкивание (обратно пропорционально расстоянию)
                        float strength = Mathf.Clamp01(1.0f - distance/avoidanceRadius);
                        strength = strength * strength; // Квадратичное усиление
                        
                        // Особенно сильно избегаем стайных рыб
                        float multiplier = otherBoid.isSchooling ? 1.5f : 1.0f;
                        
                        avoidance += moveAway.normalized * strength * multiplier;
                    }
                }
                
                // Если есть кого избегать
                if (avoidance.sqrMagnitude > 0.001f)
                {
                    avoidance.Normalize();
                    // Применяем силу избегания
                    boid.velocity += avoidance * avoidanceFactor * Time.deltaTime;
                }
                
                // Добавляем случайное движение для одиночных рыб
                boid.velocity += (Vector2)Random.insideUnitCircle * randomMovementFactor * Time.deltaTime;
            }
        }
        
        void ApplyMovement()
        {
            foreach (var boid in allBoid)
            {
                // Применяем силу границы ПЕРЕД ограничением скорости
                ApplyBoundaryForce(boid);
                
                // Ограничиваем максимальную скорость
                if (boid.velocity.sqrMagnitude > boid.data.moveSpeed * boid.data.moveSpeed)
                {
                    boid.velocity = boid.velocity.normalized * boid.data.moveSpeed;
                }
                
                // Минимальная скорость для предотвращения остановки
                if (boid.velocity.sqrMagnitude < 0.01f)
                {
                    boid.velocity = Random.insideUnitCircle.normalized * 0.1f;
                }
                
                // Получаем текущую позицию как 2D вектор
                Vector2 position = boid.transform.position;
                
                // Применяем скорость к позиции
                position += boid.velocity * Time.deltaTime;
                
                // Жесткое ограничение позиции - не позволяем рыбе выйти за границы спрайта
                if (hasBoundaries)
                {
                    position.x = Mathf.Clamp(position.x, spriteBounds.min.x, spriteBounds.max.x);
                    position.y = Mathf.Clamp(position.y, spriteBounds.min.y, spriteBounds.max.y);
                    
                    // Если рыба столкнулась с границей, разворачиваем её скорость
                    Vector2 currentPos = boid.transform.position;
                    if ((Mathf.Abs(position.x - spriteBounds.min.x) < 0.01f || Mathf.Abs(position.x - spriteBounds.max.x) < 0.01f) && 
                        ((position.x >= spriteBounds.max.x && boid.velocity.x > 0) || (position.x <= spriteBounds.min.x && boid.velocity.x < 0)))
                    {
                        boid.velocity.x = -boid.velocity.x * 0.8f; // Отражаем и немного ослабляем
                        boid.velocity += (Vector2)Random.insideUnitCircle * 0.5f; // Добавляем случайность
                    }
                    if ((Mathf.Abs(position.y - spriteBounds.min.y) < 0.01f || Mathf.Abs(position.y - spriteBounds.max.y) < 0.01f) && 
                        ((position.y >= spriteBounds.max.y && boid.velocity.y > 0) || (position.y <= spriteBounds.min.y && boid.velocity.y < 0)))
                    {
                        boid.velocity.y = -boid.velocity.y * 0.8f; // Отражаем и немного ослабляем
                        boid.velocity += (Vector2)Random.insideUnitCircle * 0.5f; // Добавляем случайность
                    }
                }
                
                // Обновляем позицию рыбы (сохраняя Z-координату)
                boid.transform.position = new Vector3(position.x, position.y, boid.transform.position.z);
                
                // Поворачиваем рыбу в направлении движения (2D поворот вокруг оси Z)
                if (boid.velocity.sqrMagnitude > 0.0001f)
                {
                    float angle = Mathf.Atan2(boid.velocity.y, boid.velocity.x) * Mathf.Rad2Deg;
                    // Корректируем угол для 2D-спрайта (спрайты обычно смотрят вверх, поэтому -90 градусов)
                    angle -= 90f;
                    
                    // Вычисляем текущий угол рыбы
                    float currentAngle = boid.transform.eulerAngles.z;
                    
                    // Плавный поворот
                    float newAngle = Mathf.LerpAngle(currentAngle, angle, rotationSpeed * Time.deltaTime);
                    boid.transform.rotation = Quaternion.Euler(0, 0, newAngle);
                }
            }
        }
        
        void ApplyBoundaryForce(BoidInfo boid)
        {
            if (!hasBoundaries) return;
            
            Vector2 position = boid.transform.position;
            Vector2 force = Vector2.zero;
            
            float buffer = boundaryBuffer;
            
            // Проверяем каждую границу спрайта отдельно
            if (position.x > spriteBounds.max.x - buffer)
            {
                float dist = spriteBounds.max.x - position.x;
                float strength = Mathf.Clamp01(1.0f - dist / buffer);
                force.x -= boundaryForce * strength * strength; // Квадратичное усиление
            }
            else if (position.x < spriteBounds.min.x + buffer)
            {
                float dist = position.x - spriteBounds.min.x;
                float strength = Mathf.Clamp01(1.0f - dist / buffer);
                force.x += boundaryForce * strength * strength;
            }
            
            if (position.y > spriteBounds.max.y - buffer)
            {
                float dist = spriteBounds.max.y - position.y;
                float strength = Mathf.Clamp01(1.0f - dist / buffer);
                force.y -= boundaryForce * strength * strength;
            }
            else if (position.y < spriteBounds.min.y + buffer)
            {
                float dist = position.y - spriteBounds.min.y;
                float strength = Mathf.Clamp01(1.0f - dist / buffer);
                force.y += boundaryForce * strength * strength;
            }
            
            // Применяем силу границы к скорости (только если сила достаточно большая)
            if (force.sqrMagnitude > 0.01f)
            {
                boid.velocity += force * Time.deltaTime;
                
                // Добавляем небольшую случайную компоненту, чтобы избежать движения строго вдоль границы
                boid.velocity += (Vector2)Random.insideUnitCircle * 0.3f * Time.deltaTime;
            }
        }
        
        void OnDrawGizmos()
        {
            if (!Application.isPlaying || !debugDrawGizmos) return;
            
            if (hasBoundaries)
            {
                // Рисуем границу спрайта
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(spriteBounds.center, spriteBounds.size);
                
                // Рисуем внутреннюю границу (зону действия силы)
                Gizmos.color = Color.yellow;
                Vector3 innerSize = spriteBounds.size - Vector3.one * boundaryBuffer * 2;
                Gizmos.DrawWireCube(spriteBounds.center, innerSize);
            }
            
            // Отображаем радиусы для одной стайной рыбы для отладки
            if (schoolingBoid.Count > 0)
            {
                var boidInfo = schoolingBoid[0];
                
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(boidInfo.transform.position, schoolingRadius);

                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(boidInfo.transform.position, separationRadius);
                
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(boidInfo.transform.position, interSchoolAvoidanceRadius);
            }
            
            // Отображаем радиус для одной одиночной рыбы
            if (soloBoid.Count > 0)
            {
                var boidInfo = soloBoid[0];
                
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(boidInfo.transform.position, avoidanceRadius);
            }
        }
    }
}