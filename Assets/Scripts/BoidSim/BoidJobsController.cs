using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;
using UnityEngine.Serialization;

namespace BoidSim
{
    public class BoidJobsController : MonoBehaviour
    {
        public BoidCollection boidCollection;

        [Header("Schooling Behavior")] 
        public float cohesionFactor = 1.0f;
        public float alignmentFactor = 1.0f;
        public float separationFactor = 1.0f;
        public float schoolingRadius = 5.0f;
        public float separationRadius = 2.0f;

        [Header("Inter-School Avoidance")] 
        [Tooltip("Сила избегания между разными стаями")]
        public float interSchoolAvoidanceFactor = 2.0f;

        [Tooltip("Радиус избегания между разными стаями")]
        public float interSchoolAvoidanceRadius = 8.0f;

        [Header("Solo Behavior")] 
        public float randomMovementFactor = 1.0f;
        public float avoidanceFactor = 2.0f;
        public float avoidanceRadius = 3.0f;

        [Header("Movement Settings")] 
        public float rotationSpeed = 3.0f;

        [Header("Boundary Settings")] 
        [Tooltip("Префаб с прямоугольным спрайтом для границ")]
        public GameObject boundariesPrefab;

        public float boundaryForce = 5.0f;

        [Tooltip("Расстояние от границы, на котором начинает действовать сила отталкивания")]
        public float boundaryBuffer = 3.0f;

        [Header("Performance")] [Tooltip("Размер батча для Job System")]
        public int batchSize = 32;

        [SerializeField] private bool debugDrawGizmos = true;

        // Границы префаба
        private Vector2 boundarySize;
        private Vector3 boundaryCenter;

        // Native arrays для Jobs
        private NativeArray<float2> positions;
        private NativeArray<float2> velocities;
        private NativeArray<int> boidTypes; // Индекс типа рыбы в fishCollection
        private NativeArray<bool> isSchooling;
        private NativeArray<int> schoolIds;
        private NativeArray<float> moveSpeeds;

        // Вспомогательные массивы
        private NativeArray<float2> newVelocities;
        private NativeArray<Unity.Mathematics.Random> randomGenerators;

        // Обычные списки для GameObject'ов
        private List<Transform> boidTransforms = new List<Transform>();
        private List<BoidData> allBoidData = new List<BoidData>();

        // Словарь для группировки индексов рыб по типам
        private Dictionary<int, List<int>> boidIndicesByType = new Dictionary<int, List<int>>();

        void Start()
        {
            InitializeBoundaries();
            SpawnBoids();
            // Time.timeScale = 0f; 
        }

        void InitializeBoundaries()
        {
            if (boundariesPrefab != null)
            {
                // Получаем размер спрайта из префаба
                SpriteRenderer spriteRenderer = boundariesPrefab.GetComponent<SpriteRenderer>();
                if (spriteRenderer != null)
                {
                    boundarySize = spriteRenderer.bounds.size;
                    boundaryCenter = boundariesPrefab.transform.position;
                }
                else
                {
                    Debug.LogWarning("Boundary prefab doesn't have SpriteRenderer component. Using default size.");
                    boundarySize = new Vector2(20f, 15f);
                    boundaryCenter = Vector3.zero;
                }
            }
            else
            {
                Debug.LogWarning("Boundary prefab is not assigned. Using default size.");
                boundarySize = new Vector2(20f, 15f);
                boundaryCenter = Vector3.zero;
            }

            Debug.Log($"Boundary size: {boundarySize}, Center: {boundaryCenter}");
        }

        void SpawnBoids()
        {
            // Подсчитываем общее количество рыб
            int boidCount = 0;
            foreach (var boid in boidCollection.boids)
            {
                boidCount += boid.boidCount;
            }

            // Инициализируем native arrays
            positions = new NativeArray<float2>(boidCount, Allocator.Persistent);
            velocities = new NativeArray<float2>(boidCount, Allocator.Persistent);
            newVelocities = new NativeArray<float2>(boidCount, Allocator.Persistent);
            boidTypes = new NativeArray<int>(boidCount, Allocator.Persistent);
            isSchooling = new NativeArray<bool>(boidCount, Allocator.Persistent);
            schoolIds = new NativeArray<int>(boidCount, Allocator.Persistent);
            moveSpeeds = new NativeArray<float>(boidCount, Allocator.Persistent);
            randomGenerators = new NativeArray<Unity.Mathematics.Random>(boidCount, Allocator.Persistent);

            // Инициализируем генераторы случайных чисел
            uint seed = (uint)UnityEngine.Random.Range(1, int.MaxValue);
            for (int i = 0; i < boidCount; i++)
            {
                randomGenerators[i] = new Unity.Mathematics.Random(seed + (uint)i);
            }

            int currentIndex = 0;

            // Создаем точки спавна для стайных рыб и направления для каждой стаи
            Dictionary<int, List<Vector2>> schoolSpawnPoints = new Dictionary<int, List<Vector2>>();
            Dictionary<int, List<float>> schoolDirections = new Dictionary<int, List<float>>();

            for (int boidTypeIndex = 0; boidTypeIndex < boidCollection.boids.Count; boidTypeIndex++)
            {
                var boid = boidCollection.boids[boidTypeIndex];
                boidIndicesByType[boidTypeIndex] = new List<int>();

                if (boid.isSchooling)
                {
                    schoolSpawnPoints[boidTypeIndex] = new List<Vector2>();
                    schoolDirections[boidTypeIndex] = new List<float>();
                    int spawnPoints = Mathf.Min(3, Mathf.Max(1, boid.boidCount / 15));

                    for (int i = 0; i < spawnPoints; i++)
                    {
                        Vector2 spawnPoint;

                        if (boid.isRandomSpawn)
                        {
                            // Спавн внутри границ префаба
                            spawnPoint = new Vector2(
                                boundaryCenter.x + UnityEngine.Random.Range(-boundarySize.x * 0.35f, boundarySize.x * 0.35f),
                                boundaryCenter.y + UnityEngine.Random.Range(-boundarySize.y * 0.35f, boundarySize.y * 0.35f)
                            );
                        }
                        else
                        {
                            // Используем точку спавна напрямую без смещения
                            spawnPoint = boid.spawnPoint;
                        }

                        schoolSpawnPoints[boidTypeIndex].Add(spawnPoint);

                        // Случайное направление для каждой отдельной стаи
                        float randomDirection = UnityEngine.Random.Range(0f, 360f);
                        schoolDirections[boidTypeIndex].Add(randomDirection);
                    }
                }
            }

            // Создаем рыб
            for (int boidTypeIndex = 0; boidTypeIndex < boidCollection.boids.Count; boidTypeIndex++)
            {
                var boid = boidCollection.boids[boidTypeIndex];

                for (int i = 0; i < boid.boidCount; i++)
                {
                    Vector2 spawnPos;
                    float rotationAngle = 0f;
                    int schoolId = 0;

                    if (boid.isSchooling && schoolSpawnPoints.ContainsKey(boidTypeIndex))
                    {
                        // Стайные рыбы спавнятся вокруг точек стаи
                        int spawnIndex = i % schoolSpawnPoints[boidTypeIndex].Count;
                        Vector2 schoolCenter = schoolSpawnPoints[boidTypeIndex][spawnIndex];
                        schoolId = spawnIndex;

                        // Уменьшили радиус спавна рыб вокруг центра стаи
                        float radius = UnityEngine.Random.Range(0.3f, 1.5f);
                        float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
                        spawnPos = schoolCenter + new Vector2(
                            Mathf.Cos(angle) * radius,
                            Mathf.Sin(angle) * radius
                        );

                        // Все рыбы в стае смотрят примерно в одном направлении, но у каждой стаи случайное направление
                        float schoolDirection = schoolDirections[boidTypeIndex][spawnIndex];
                        rotationAngle = schoolDirection + UnityEngine.Random.Range(-20f, 20f); // Небольшой разброс ±20°
                    }
                    else
                    {
                        // Одиночные рыбы
                        if (boid.isRandomSpawn)
                        {
                            // Спавн внутри границ префаба
                            spawnPos = new Vector2(
                                boundaryCenter.x + UnityEngine.Random.Range(-boundarySize.x * 0.45f, boundarySize.x * 0.45f),
                                boundaryCenter.y + UnityEngine.Random.Range(-boundarySize.y * 0.45f, boundarySize.y * 0.45f)
                            );
                        }
                        else
                        {
                            // Добавили небольшой случайный разброс для одиночных рыб
                            Vector2 randomOffset = UnityEngine.Random.insideUnitCircle * 0.5f;
                            spawnPos = boid.spawnPoint + randomOffset;
                        }

                        rotationAngle = UnityEngine.Random.Range(0f, 360f);
                    }

                    // Создаем GameObject
                    GameObject boidObj = Instantiate(boid.boidPrefab, spawnPos,
                        Quaternion.Euler(0, 0, rotationAngle));
                    boidTransforms.Add(boidObj.transform);
                    allBoidData.Add(boid);

                    // Заполняем native arrays
                    positions[currentIndex] = new float2(spawnPos.x, spawnPos.y);
                    float radians = rotationAngle * Mathf.Deg2Rad;
                    velocities[currentIndex] = new float2(
                        math.cos(radians) * boid.moveSpeed,
                        math.sin(radians) * boid.moveSpeed
                    );
                    boidTypes[currentIndex] = boidTypeIndex;
                    isSchooling[currentIndex] = boid.isSchooling;
                    schoolIds[currentIndex] = schoolId;
                    moveSpeeds[currentIndex] = boid.moveSpeed;

                    boidIndicesByType[boidTypeIndex].Add(currentIndex);
                    currentIndex++;
                }
            }

            Debug.Log($"Создано рыб: {boidCount}");
        }

        void Update()
        {
            int boidCount = positions.Length;
            if (boidCount == 0) return;

            // Копируем текущие скорости в новый массив
            newVelocities.CopyFrom(velocities);

            // Создаем и планируем Jobs
            JobHandle jobHandle = default;

            // Job для стайных рыб (по типам)
            foreach (var kvp in boidIndicesByType)
            {
                int boidType = kvp.Key;
                var indices = kvp.Value;

                if (boidCollection.boids[boidType].isSchooling && indices.Count > 0)
                {
                    var indicesArray = new NativeArray<int>(indices.ToArray(), Allocator.TempJob);

                    var schoolingJob = new SchoolingBehaviorJob
                    {
                        indices = indicesArray,
                        positions = positions,
                        velocities = velocities,
                        newVelocities = newVelocities,
                        schoolIds = schoolIds,
                        boidTypes = boidTypes,
                        isSchooling = isSchooling,
                        currentBoidType = boidType,
                        cohesionFactor = cohesionFactor,
                        alignmentFactor = alignmentFactor,
                        separationFactor = separationFactor,
                        schoolingRadius = schoolingRadius,
                        separationRadius = separationRadius,
                        interSchoolAvoidanceFactor = interSchoolAvoidanceFactor,
                        interSchoolAvoidanceRadius = interSchoolAvoidanceRadius,
                        deltaTime = Time.deltaTime
                    };

                    jobHandle = schoolingJob.Schedule(indices.Count, batchSize, jobHandle);
                    jobHandle = indicesArray.Dispose(jobHandle);
                }
            }

            // Job для одиночных рыб
            var soloJob = new SoloBehaviorJob
            {
                positions = positions,
                velocities = velocities,
                newVelocities = newVelocities,
                isSchooling = isSchooling,
                avoidanceFactor = avoidanceFactor,
                avoidanceRadius = avoidanceRadius,
                randomMovementFactor = randomMovementFactor,
                randomGenerators = randomGenerators,
                deltaTime = Time.deltaTime
            };

            jobHandle = soloJob.Schedule(boidCount, batchSize, jobHandle);

            // Job для границ и финального движения
            var movementJob = new ApplyMovementJob
            {
                positions = positions,
                velocities = newVelocities,
                moveSpeeds = moveSpeeds,
                randomGenerators = randomGenerators,
                boundarySize = new float2(boundarySize.x * 0.5f, boundarySize.y * 0.5f), // Половина размера для работы с центром
                boundaryCenter = new float2(boundaryCenter.x, boundaryCenter.y),
                boundaryForce = boundaryForce,
                boundaryBuffer = boundaryBuffer,
                deltaTime = Time.deltaTime
            };

            jobHandle = movementJob.Schedule(boidCount, batchSize, jobHandle);

            // Ждем завершения всех Jobs
            jobHandle.Complete();

            // Копируем обновленные скорости
            velocities.CopyFrom(newVelocities);

            // Применяем позиции и повороты к GameObject'ам
            for (int i = 0; i < boidCount; i++)
            {
                var pos = positions[i];
                boidTransforms[i].position = new Vector3(pos.x, pos.y, boidTransforms[i].position.z);

                // Поворот
                var vel = velocities[i];
                if (math.lengthsq(vel) > 0.0001f)
                {
                    float angle = math.atan2(vel.y, vel.x) * Mathf.Rad2Deg - 90f;
                    float currentAngle = boidTransforms[i].eulerAngles.z;
                    float newAngle = Mathf.LerpAngle(currentAngle, angle, rotationSpeed * Time.deltaTime);
                    boidTransforms[i].rotation = Quaternion.Euler(0, 0, newAngle);
                }
            }
        }

        void OnDestroy()
        {
            // Освобождаем native arrays
            if (positions.IsCreated) positions.Dispose();
            if (velocities.IsCreated) velocities.Dispose();
            if (newVelocities.IsCreated) newVelocities.Dispose();
            if (boidTypes.IsCreated) boidTypes.Dispose();
            if (isSchooling.IsCreated) isSchooling.Dispose();
            if (schoolIds.IsCreated) schoolIds.Dispose();
            if (moveSpeeds.IsCreated) moveSpeeds.Dispose();
            if (randomGenerators.IsCreated) randomGenerators.Dispose();
        }

        void OnDrawGizmos()
        {
            if (!Application.isPlaying || !debugDrawGizmos) return;

            // Рисуем границу на основе размера префаба
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(boundaryCenter, new Vector3(boundarySize.x, boundarySize.y, 0));
            
            Gizmos.color = Color.yellow;
            Vector2 innerSize = boundarySize - Vector2.one * boundaryBuffer * 2;
            Gizmos.DrawWireCube(boundaryCenter, new Vector3(innerSize.x, innerSize.y, 0));
        }
    }

    // Job для поведения стайных рыб
    [BurstCompile]
    struct SchoolingBehaviorJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int> indices;
        [ReadOnly] public NativeArray<float2> positions;
        [ReadOnly] public NativeArray<float2> velocities;
        [ReadOnly] public NativeArray<int> schoolIds;
        [ReadOnly] public NativeArray<int> boidTypes;
        [ReadOnly] public NativeArray<bool> isSchooling;

        [NativeDisableParallelForRestriction] public NativeArray<float2> newVelocities;

        public int currentBoidType;
        public float cohesionFactor;
        public float alignmentFactor;
        public float separationFactor;
        public float schoolingRadius;
        public float separationRadius;
        public float interSchoolAvoidanceFactor;
        public float interSchoolAvoidanceRadius;
        public float deltaTime;

        public void Execute(int index)
        {
            int boidIndex = indices[index];
            float2 position = positions[boidIndex];
            float2 velocity = velocities[boidIndex];
            int schoolId = schoolIds[boidIndex];

            float2 cohesion = float2.zero;
            float2 alignment = float2.zero;
            float2 separation = float2.zero;
            float2 interSchoolAvoidance = float2.zero;
            int neighborCount = 0;
            int avoidanceCount = 0;

            // Проверяем всех соседей того же типа
            for (int i = 0; i < indices.Length; i++)
            {
                if (i == index) continue;

                int otherIndex = indices[i];
                float2 otherPos = positions[otherIndex];
                float distance = math.distance(position, otherPos);

                if (distance < schoolingRadius)
                {
                    float multiplier = (schoolIds[otherIndex] == schoolId) ? 1.5f : 1.0f;

                    cohesion += otherPos * multiplier;
                    alignment += velocities[otherIndex] * multiplier;
                    neighborCount++;

                    if (distance < separationRadius)
                    {
                        float2 moveAway = position - otherPos;
                        float strength = math.clamp(1.0f - distance / separationRadius, 0f, 1f);
                        strength = strength * strength;
                        separation += math.normalize(moveAway) * strength;
                    }
                }
            }

            // Проверяем ВСЕХ рыб для межстайного избегания
            for (int i = 0; i < positions.Length; i++)
            {
                if (i == boidIndex) continue;

                // Пропускаем рыб того же типа
                if (boidTypes[i] == currentBoidType) continue;

                // Проверяем только стайных рыб из других стай
                if (!isSchooling[i]) continue;

                float2 otherPos = positions[i];
                float distance = math.distance(position, otherPos);

                if (distance < interSchoolAvoidanceRadius)
                {
                    float2 moveAway = position - otherPos;
                    float strength = math.clamp(1.0f - distance / interSchoolAvoidanceRadius, 0f, 1f);
                    strength = strength * strength;
                    interSchoolAvoidance += math.normalize(moveAway) * strength;
                    avoidanceCount++;
                }
            }

            if (neighborCount > 0)
            {
                cohesion = cohesion / neighborCount - position;
                alignment = alignment / neighborCount;

                if (math.lengthsq(cohesion) > 0.001f) cohesion = math.normalize(cohesion);
                if (math.lengthsq(alignment) > 0.001f) alignment = math.normalize(alignment);
                if (math.lengthsq(separation) > 0.001f) separation = math.normalize(separation);

                float2 totalForce = cohesion * cohesionFactor +
                                    alignment * alignmentFactor +
                                    separation * separationFactor;

                velocity += totalForce * 2.0f * deltaTime;
            }

            if (avoidanceCount > 0)
            {
                interSchoolAvoidance = interSchoolAvoidance / avoidanceCount;
                if (math.lengthsq(interSchoolAvoidance) > 0.001f)
                {
                    interSchoolAvoidance = math.normalize(interSchoolAvoidance);
                    velocity += interSchoolAvoidance * interSchoolAvoidanceFactor * deltaTime;
                }
            }

            newVelocities[boidIndex] = velocity;
        }
    }

    // Job для поведения одиночных рыб
    [BurstCompile]
    struct SoloBehaviorJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float2> positions;
        [ReadOnly] public NativeArray<float2> velocities;
        [ReadOnly] public NativeArray<bool> isSchooling;

        public NativeArray<float2> newVelocities;
        public NativeArray<Unity.Mathematics.Random> randomGenerators;

        public float avoidanceFactor;
        public float avoidanceRadius;
        public float randomMovementFactor;
        public float deltaTime;

        public void Execute(int index)
        {
            if (isSchooling[index]) return;

            float2 position = positions[index];
            float2 velocity = newVelocities[index];
            float2 avoidance = float2.zero;

            for (int i = 0; i < positions.Length; i++)
            {
                if (i == index) continue;

                float distance = math.distance(position, positions[i]);

                if (distance < avoidanceRadius)
                {
                    float2 moveAway = position - positions[i];
                    float strength = math.clamp(1.0f - distance / avoidanceRadius, 0f, 1f);
                    strength = strength * strength;

                    float multiplier = isSchooling[i] ? 1.5f : 1.0f;
                    avoidance += math.normalize(moveAway) * strength * multiplier;
                }
            }

            if (math.lengthsq(avoidance) > 0.001f)
            {
                avoidance = math.normalize(avoidance);
                velocity += avoidance * avoidanceFactor * deltaTime;
            }

            // Случайное движение
            var random = randomGenerators[index];
            float2 randomDir = new float2(
                random.NextFloat(-1f, 1f),
                random.NextFloat(-1f, 1f)
            );
            randomGenerators[index] = random;

            velocity += randomDir * randomMovementFactor * deltaTime;
            newVelocities[index] = velocity;
        }
    }

    // Job для применения движения и границ
    [BurstCompile]
    struct ApplyMovementJob : IJobParallelFor
    {
        public NativeArray<float2> positions;
        public NativeArray<float2> velocities;
        [ReadOnly] public NativeArray<float> moveSpeeds;
        public NativeArray<Unity.Mathematics.Random> randomGenerators;

        public float2 boundarySize; // Половина размера границы (для работы с центром)
        public float2 boundaryCenter;
        public float boundaryForce;
        public float boundaryBuffer;
        public float deltaTime;

        public void Execute(int index)
        {
            float2 position = positions[index];
            float2 velocity = velocities[index];
            float moveSpeed = moveSpeeds[index];

            // Применяем силу границы для прямоугольника
            float2 boundaryForceVec = float2.zero;
            float2 relativePos = position - boundaryCenter;

            // Проверка границ по X
            if (relativePos.x > boundarySize.x - boundaryBuffer)
            {
                float dist = boundarySize.x - relativePos.x;
                float strength = math.clamp(1.0f - dist / boundaryBuffer, 0f, 1f);
                boundaryForceVec.x -= boundaryForce * strength * strength;
            }
            else if (relativePos.x < -boundarySize.x + boundaryBuffer)
            {
                float dist = relativePos.x + boundarySize.x;
                float strength = math.clamp(1.0f - dist / boundaryBuffer, 0f, 1f);
                boundaryForceVec.x += boundaryForce * strength * strength;
            }

            // Проверка границ по Y
            if (relativePos.y > boundarySize.y - boundaryBuffer)
            {
                float dist = boundarySize.y - relativePos.y;
                float strength = math.clamp(1.0f - dist / boundaryBuffer, 0f, 1f);
                boundaryForceVec.y -= boundaryForce * strength * strength;
            }
            else if (relativePos.y < -boundarySize.y + boundaryBuffer)
            {
                float dist = relativePos.y + boundarySize.y;
                float strength = math.clamp(1.0f - dist / boundaryBuffer, 0f, 1f);
                boundaryForceVec.y += boundaryForce * strength * strength;
            }

            velocity += boundaryForceVec * deltaTime;

            // Добавляем случайность при приближении к границе
            if (math.lengthsq(boundaryForceVec) > 0.01f)
            {
                var random = randomGenerators[index];
                float2 randomDir = new float2(
                    random.NextFloat(-1f, 1f),
                    random.NextFloat(-1f, 1f)
                );
                randomGenerators[index] = random;
                velocity += randomDir * 0.3f * deltaTime;
            }

            // Ограничиваем скорость
            if (math.lengthsq(velocity) > moveSpeed * moveSpeed)
            {
                velocity = math.normalize(velocity) * moveSpeed;
            }

            // Минимальная скорость
            if (math.lengthsq(velocity) < 0.01f)
            {
                var random = randomGenerators[index];
                velocity = new float2(
                    random.NextFloat(-1f, 1f),
                    random.NextFloat(-1f, 1f)
                ) * 0.1f;
                randomGenerators[index] = random;
            }

            // Применяем скорость
            position += velocity * deltaTime;

            // Жесткое ограничение границ относительно центра
            float2 newRelativePos = position - boundaryCenter;
            newRelativePos.x = math.clamp(newRelativePos.x, -boundarySize.x, boundarySize.x);
            newRelativePos.y = math.clamp(newRelativePos.y, -boundarySize.y, boundarySize.y);
            position = boundaryCenter + newRelativePos;

            // Отражение при столкновении
            if (math.abs(newRelativePos.x) >= boundarySize.x && math.sign(velocity.x) == math.sign(newRelativePos.x))
            {
                velocity.x = -velocity.x * 0.8f;
                var random = randomGenerators[index];
                velocity += new float2(random.NextFloat(-0.5f, 0.5f), random.NextFloat(-0.5f, 0.5f));
                randomGenerators[index] = random;
            }

            if (math.abs(newRelativePos.y) >= boundarySize.y && math.sign(velocity.y) == math.sign(newRelativePos.y))
            {
                velocity.y = -velocity.y * 0.8f;
                var random = randomGenerators[index];
                velocity += new float2(random.NextFloat(-0.5f, 0.5f), random.NextFloat(-0.5f, 0.5f));
                randomGenerators[index] = random;
            }

            positions[index] = position;
            velocities[index] = velocity;
        }
    }
}