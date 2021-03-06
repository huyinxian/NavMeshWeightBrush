using System.Collections;
using UnityEngine;

namespace NavMeshWeightBrush
{
    public class AttachToGround : MonoBehaviour
    {
        public LayerMask layerMask;
        public float yBias = 0.2f;
        public float raycastStartY = 100f;

        private Mesh _mesh;
        private Mesh _newMesh;

        private IEnumerator CreateCollider()
        {
            yield return 0;
            DestroyImmediate(gameObject.GetComponent<MeshCollider>());
            yield return 0;
            gameObject.AddComponent<MeshCollider>();
        }

        public void Attach()
        {
            if (_mesh == null)
                _mesh = gameObject.GetComponent<MeshFilter>().sharedMesh;

            if (_mesh == null)
            {
                Debug.LogError("GameObject dose not contain MeshFilter!");
                return;
            }

            _newMesh = Instantiate(_mesh);

            Vector3[] vertices = _newMesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 positionWS = transform.TransformPoint(vertices[i]);
                GetRaycastHitPos(positionWS, out vertices[i], raycastStartY, layerMask);
                vertices[i] = transform.InverseTransformPoint(vertices[i] + new Vector3(0f, yBias, 0f));
            }

            _newMesh.vertices = vertices;
            gameObject.GetComponent<MeshFilter>().mesh = _newMesh;
            StartCoroutine(CreateCollider());
        }

        public static bool GetRaycastHitPos(Vector3 pos, out Vector3 result, float raycastStartY, LayerMask layerMask)
        {
            Ray ray = new Ray();
            ray.origin = pos + new Vector3(0, raycastStartY, 0);
            ray.direction = Vector3.down;
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, Mathf.Infinity, layerMask))
            {
                result = hit.point;
                return true;
            }

            result = pos;
            return false;
        }
    }
}