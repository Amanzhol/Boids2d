using System.Collections.Generic;
using UnityEngine;

namespace BoidSim
{
    [CreateAssetMenu(fileName = "BoidCollection", menuName = "Boid/Boid Collection")]
    public class BoidCollection : ScriptableObject
    {
        public List<BoidData> boids = new List<BoidData>();
    }
}