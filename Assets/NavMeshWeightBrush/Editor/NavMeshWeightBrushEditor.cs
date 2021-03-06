using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AI;
using UnityEngine;

namespace NavMeshWeightBrush.Editor
{
    public class NavMeshWeightBrushEditor : EditorWindow
    {
        [NonSerialized] public static NavMeshWeightBrushEditor Instance;

        private bool _enableBrush;
        private GameObject _brushGO;
        private Transform _root;
        private LayerMask _layerMask;
        private List<NavMeshWeightInstance> _instanceList = new List<NavMeshWeightInstance>();
        private float _density = 1.0f;
        private float _yBias = 0.2f;
        private float _brushScale = 1.0f;
        private int _areaIndex = 3;

        [MenuItem("Tools/Nav Mesh Weight Brush")]
        private static void OpenWindow()
        {
            var window = GetWindow<NavMeshWeightBrushEditor>();
            window.titleContent = new GUIContent("Nav Mesh Weight Brush");
            window.minSize = new Vector2(500, 300);
        }

        private void OnEnable()
        {
            if (Instance == null)
                Instance = this;

            _enableBrush = true;
            _layerMask = 1 << LayerMask.NameToLayer("Terrain");
            _brushGO = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/NavMeshWeightBrush/Prefabs/brush_plane_512.prefab");
            CreateRoot();

            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDestroy()
        {
            _enableBrush = false;
            DeleteAllObjects();
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void CreateRoot()
        {
            if (_root != null)
                return;
            
            GameObject rootGO = GameObject.Find("NavMeshWeightBrushRoot");
            if (rootGO == null)
                rootGO = new GameObject("NavMeshWeightBrushRoot");
            _root = rootGO.transform;
        }

        private void OnGUI()
        {
            _enableBrush = EditorGUILayout.Toggle("Enable", _enableBrush);
            _brushGO = EditorGUILayout.ObjectField("Brush", _brushGO, typeof(GameObject), true) as GameObject;
            _brushScale = EditorGUILayout.Slider("Brush Scale", _brushScale, 0f, 10f);
            _density = EditorGUILayout.Slider("Brush Density", _density, 0, 100);
            _yBias = EditorGUILayout.Slider("Mesh Y Offset", _yBias, 0, 10);

            EditorGUILayout.Space();

            if (GUILayout.Button("Loading Scene Data"))
            {
                if (_instanceList.Count > 0)
                    EditorUtility.DisplayDialog("Tips", "Please delete all objects before loading.", "OK");
                else
                    Load();
            }

            if (GUILayout.Button("Save"))
            {
                Save();
            }

            if (GUILayout.Button("Bake NavMesh"))
            {
                BakeNavMesh();
            }

            if (GUILayout.Button("Delete Selected Objects"))
            {
                DeleteSelectedObjects();
            }

            if (GUILayout.Button("Delete Previous Object"))
            {
                DeletePreviousObject();
            }

            if (GUILayout.Button("Delete All Objects"))
            {
                DeleteAllObjects();
            }
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!_enableBrush)
                return;
            
            HandleUtility.AddDefaultControl(0);

            Event e = Event.current;
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            RaycastHit hit;

            if (_brushGO != null)
            {
                if (Physics.Raycast(ray, out hit, Mathf.Infinity, _layerMask) && CheckDistance(hit.point))
                {
                    if ((e.type == EventType.MouseDrag || e.type == EventType.MouseDown) && e.button == 0 && !e.alt &&
                        !e.shift && !e.control)
                    {
                        GameObject instance = Instantiate(_brushGO, hit.point, Quaternion.identity, _root);
                        instance.transform.localScale *= _brushScale;
                        var instanceComp = instance.AddComponent<NavMeshWeightInstance>();
                        instanceComp.brushGO = _brushGO;
                        var attachComp = instance.AddComponent<AttachToGround>();
                        attachComp.yBias = _yBias;
                        attachComp.layerMask = _layerMask;
                        attachComp.Attach();
                        _instanceList.Add(instanceComp);
                    }
                }
            }
        }

        private bool CheckDistance(Vector3 point)
        {
            foreach (var instance in _instanceList)
            {
                if (Vector3.Distance(point, instance.transform.position) < _density)
                {
                    return false;
                }
            }

            return true;
        }

        private static readonly string FILE_EXTENTION = ".navmeshweightdata";
        private static readonly string FILE_FULL_NAME = "NavMeshWeightAssets.asset";

        private string GetDataDirectory()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene == null || string.IsNullOrEmpty(scene.path))
                return string.Empty;

            string dataDirectory = Path.GetDirectoryName(scene.path) + Path.DirectorySeparatorChar +
                                   Path.GetFileNameWithoutExtension(scene.path) + FILE_EXTENTION +
                                   Path.DirectorySeparatorChar;
            return dataDirectory;
        }

        private void Load()
        {
            CreateRoot();
            _instanceList.Clear();

            string dir = GetDataDirectory();
            if (string.IsNullOrEmpty(dir))
            {
                Debug.Log("Directory not exist!");
                return;
            }

            string filePath = dir + FILE_FULL_NAME;
            if (!File.Exists(filePath))
            {
                Debug.Log("Data not exist!");
                return;
            }

            NavMeshWeightAssets assets = AssetDatabase.LoadAssetAtPath<NavMeshWeightAssets>(filePath);
            foreach (var data in assets.datas)
            {
                GameObject instance = Instantiate(data.brush, data.position, Quaternion.identity, _root);
                instance.transform.localScale = data.localScale;
                var instanceComp = instance.AddComponent<NavMeshWeightInstance>();
                instanceComp.brushGO = data.brush;
                var attachComp = instance.AddComponent<AttachToGround>();
                attachComp.yBias = _yBias;
                attachComp.layerMask = _layerMask;
                attachComp.Attach();
                _instanceList.Add(instanceComp);
            }
        }

        private void Save()
        {
            if (_instanceList.Count <= 0)
                return;

            string dir = GetDataDirectory();
            if (string.IsNullOrEmpty(dir))
            {
                Debug.LogError("Can't save navmesh weight data as the scene is not saved! Please save the scene first!");
                return;
            }

            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            NavMeshWeightAssets assets = ScriptableObject.CreateInstance<NavMeshWeightAssets>();
            assets.datas = new NavMeshWeightData[_instanceList.Count];
            for (int i = 0; i < _instanceList.Count; i++)
            {
                var instance = _instanceList[i];
                assets.datas[i].brush = instance.brushGO;
                assets.datas[i].position = instance.transform.position;
                assets.datas[i].localScale = instance.transform.localScale;
            }

            AssetDatabase.DeleteAsset(dir + FILE_FULL_NAME);
            AssetDatabase.CreateAsset(assets, dir + FILE_FULL_NAME);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private void BakeNavMesh()
        {
            if (_instanceList.Count <= 0)
                return;

            foreach (var instance in _instanceList)
            {
                if (instance != null)
                {
                    var flags = StaticEditorFlags.NavigationStatic;
                    GameObjectUtility.SetStaticEditorFlags(instance.gameObject, flags);
                    GameObjectUtility.SetNavMeshArea(instance.gameObject, _areaIndex);
                }
            }

            // If you want to adjust parameters during navmesh baking, use these codes
            // SerializedObject obj = new SerializedObject(NavMeshBuilder.navMeshSettingsObject);
            // SerializedProperty prop = obj.FindProperty("m_BuildSettings.agentSlope");
            // prop.floatValue = 89.0f;
            // obj.ApplyModifiedProperties();
            // prop = obj.FindProperty("m_BuildSettings.agentClimb");
            // prop.floatValue = 90.0f;
            // obj.ApplyModifiedProperties();
            // prop = obj.FindProperty("m_BuildSettings.agentRadius");
            // prop.floatValue = 0.4f;
            // obj.ApplyModifiedProperties();
            // prop = obj.FindProperty("m_BuildSettings.agentHeight");
            // prop.floatValue = 0.5f;
            // obj.ApplyModifiedProperties();
            // prop = obj.FindProperty("m_BuildSettings.accuratePlacement");
            // prop.boolValue = true;
            // obj.ApplyModifiedProperties();
            
            NavMeshBuilder.ClearAllNavMeshes();
            NavMeshBuilder.BuildNavMesh();
        }
        
        private void DeleteSelectedObjects()
        {
            Transform[] transforms = Selection.GetTransforms(SelectionMode.Editable | SelectionMode.ExcludePrefab);
            if (transforms.Length > 0)
            {
                foreach (var trans in transforms)
                {
                    var comp = trans.GetComponent<NavMeshWeightInstance>();
                    if (comp != null)
                    {
                        _instanceList.Remove(comp);
                        DestroyImmediate(comp.gameObject);
                    }
                }
            }
        }

        private void DeletePreviousObject()
        {
            for (int i = _instanceList.Count - 1; i >= 0; i--)
            {
                var instance = _instanceList[i];
                _instanceList.RemoveAt(i);

                if (instance != null)
                {
                    DestroyImmediate(instance.gameObject);
                    break;
                }
            }
        }

        private void DeleteAllObjects()
        {
            foreach (var instance in _instanceList)
                DestroyImmediate(instance.gameObject);
            
            _instanceList.Clear();
            if (_root != null)
                DestroyImmediate(_root.gameObject);
        }
    }
}