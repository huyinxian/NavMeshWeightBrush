using System;
using UnityEngine;

namespace NavMeshWeightBrush
{
    [Serializable]
    public struct NavMeshWeightData
    {
        public GameObject brush;
        public Vector3 position;
        public Vector3 localScale;
    }

    public class NavMeshWeightAssets : ScriptableObject
    {
        public NavMeshWeightData[] datas;
    }
}