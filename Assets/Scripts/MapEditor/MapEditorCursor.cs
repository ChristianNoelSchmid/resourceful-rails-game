using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Rails.MapEditor
{
    [ExecuteInEditMode]
    public class MapEditorCursor : MonoBehaviour
    {

        #region Singleton

        private static MapEditorCursor _singleton = null;
        public static MapEditorCursor Singleton
        {
            get
            {
                // return existing
                if (_singleton)
                    return _singleton;

                // find in scene
                _singleton = FindObjectOfType<MapEditorCursor>();
                if (_singleton)
                    return _singleton;

                // create new
                GameObject go = new GameObject("mapeditor cursor");
                _singleton = go.AddComponent<MapEditorCursor>();
                go.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
                return _singleton;
            }
        }

        #endregion

        public float Radius = 1f;
        public bool Visible = true;
        public Color Color = Color.red;

        private bool _canSeeFloor = false;

#if UNITY_EDITOR

        private void Update()
        {
            var singleton = Singleton;
            if (singleton && singleton != this)
            {
                DestroyImmediate(this.gameObject);
                return;
            }
        }

        void UpdateCursor(SceneView scene, Event e)
        {
            // get mouse position
            Vector3 mousePosition = e.mousePosition;

            // convert to screen point
            // from https://github.com/slipster216/VertexPaint/blob/master/Editor/VertexPainterWindow_Painting.cs#L2232
            float ppp = EditorGUIUtility.pixelsPerPoint;
            mousePosition.y = scene.camera.pixelHeight - mousePosition.y * ppp;
            mousePosition.x *= ppp;

            // get world space ray from mouse position
            Ray ray = scene.camera.ScreenPointToRay(mousePosition);

            // create an infinite plane at the origin
            // cast ray to plane and, if it hits, set this transform to the hit position
            Plane hPlane = new Plane(Vector3.up, Vector3.zero);
            if (_canSeeFloor = hPlane.Raycast(ray, out var distance))
            {
                this.transform.position = ray.origin + ray.direction * distance;
            }
        }

        private void OnEnable()
        {
            SceneView.beforeSceneGui -= OnSceneGUI;
            SceneView.beforeSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.beforeSceneGui -= OnSceneGUI;
        }

        private void OnDestroy()
        {
            SceneView.beforeSceneGui -= OnSceneGUI;
        }

        void OnSceneGUI(SceneView sceneView)
        {
            Event e = Event.current;

            //Debug.Log($"{DateTime.Now.Ticks}: {e.type} {e.mousePosition}");
            if (   (e.type == EventType.MouseMove 
                || e.type == EventType.MouseDown
                || e.type == EventType.MouseUp
                || e.type == EventType.MouseDrag)
                && e.button == 0)
            {
                UpdateCursor(sceneView, e);
                e.Use();
                sceneView.Repaint();
            }
        }

        private void OnDrawGizmos()
        {
            // only draw if visible
            if (!Visible || !_canSeeFloor)
                return;

            // backup starting matrix
            var m = Gizmos.matrix;

            // draw flat sphere at cursor position
            Gizmos.matrix = Matrix4x4.TRS(
                this.transform.position,
                this.transform.rotation,
                new Vector3(this.transform.localScale.x, 0, this.transform.localScale.z)
                );


            // draw cursor primary sphere
            Gizmos.color = Color;
            Gizmos.DrawSphere(Vector3.zero, Radius);
            
            // draw cursor sphere frame
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(Vector3.zero, Radius);

            // return matrix back to original
            Gizmos.matrix = m;
        }
#endif
    }
}
