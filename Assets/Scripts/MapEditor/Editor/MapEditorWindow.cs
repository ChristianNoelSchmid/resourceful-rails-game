using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rails.MapEditor.Editor
{
    public class MapEditorWindow : EditorWindow
    {
        public enum MapEditorPaintType
        {
            Node,
            Segment
        }

        // 
        bool paintingActive = false;
        float paintingRadius = 1f;
        int tab = 0;
        private MapEditorCursor _cursor;
        MapEditorPaintType paintType = MapEditorPaintType.Node;
        NodeType paintNodeType = NodeType.Clear;
        NodeSegmentType paintSegmentType = NodeSegmentType.None;
        int paintCityId = 0;

        private MapEditorReorderableList cities;
        private MapEditorReorderableList goods;
        private SerializedObject serializedObject;

        private SerializedProperty nodesProperty;
        private SerializedProperty segmentsProperty;

        // Add menu named "My Window" to the Window menu
        [MenuItem("Window/Map Editor")]
        static void Init()
        {
            // Get existing open window or if none, make a new one:
            MapEditorWindow window = (MapEditorWindow)EditorWindow.GetWindow(typeof(MapEditorWindow));
            window.Show();
        }

        private void OnEnable()
        {
            var manager = Manager.Singleton;
            if (!manager)
                return;

            serializedObject = new SerializedObject(manager.MapData);
            nodesProperty = serializedObject.FindProperty("Nodes");
            segmentsProperty = serializedObject.FindProperty("Segments");

            // cities list
            cities = new MapEditorReorderableList(serializedObject,
                serializedObject.FindProperty("Cities"),
                true, true, true, true)
            {
                Header = "Cities",
                DrawElementCallback = OnDrawCitiesElement
            };

            // goods list
            goods = new MapEditorReorderableList(serializedObject,
                serializedObject.FindProperty("Goods"),
                true, true, true, true)
            {
                Header = "Goods",
                DrawElementCallback = OnDrawGoodsElement
            };
        }

        // 
        void OnGUI()
        {
            var manager = Manager.Singleton;
            if (!manager)
                return;

            // 
            paintingActive = GUILayout.Toggle(paintingActive, paintingActive ? "Disable Painting" : "Enable Painting", "Button");

            // tabs
            tab = GUILayout.Toolbar(tab, new string[] { "Cities", "Goods", "Paint" });
            switch (tab)
            {
                case 0: // cities
                    {
                        cities.DoLayoutList();
                        break;
                    }
                case 1: // goods
                    {
                        goods.DoLayoutList();
                        break;
                    }
                case 2: // painting
                    {
                        // painting specific 
                        EditorGUI.BeginDisabledGroup(!paintingActive);
                        paintType = (MapEditorPaintType)EditorGUILayout.EnumPopup("Paint type", paintType);
                        paintingRadius = EditorGUILayout.Slider("Paint Brush Size", paintingRadius, 0.1f, 30f);

                        if (paintType == MapEditorPaintType.Node)
                        {
                            paintNodeType = (NodeType)EditorGUILayout.EnumPopup("Node type", paintNodeType);

                            if (paintNodeType == NodeType.MajorCity || paintNodeType == NodeType.MediumCity || paintNodeType == NodeType.SmallCity)
                            {
                                paintCityId = EditorGUILayout.Popup("City", paintCityId, manager.MapData.Cities.Select(x => x.Name).ToArray());
                            }
                        }
                        else
                        {
                            paintSegmentType = (NodeSegmentType)EditorGUILayout.EnumPopup("Segment type", paintSegmentType);
                        }

                        EditorGUI.EndDisabledGroup();
                        break;
                    }
            }
        }

        void MoveHeight(ref Rect rect, ref float height, float scale = 1f)
        {
            var amount = EditorGUIUtility.singleLineHeight * scale;
            rect.y += amount + 2;
            height += amount + 2;
        }

        float OnDrawCitiesElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            // get manager
            var manager = Manager.Singleton;
            if (!manager)
                return 0;

            // try and get element at index in cities list
            var element = cities.SerializedProperty.GetArrayElementAtIndex(index);
            if (element == null)
                return 0;

            rect.y += 2;
            var height = EditorGUIUtility.singleLineHeight;

            // draw name text box
            var nameProperty = element.FindPropertyRelative("Name");
            var nameValue = EditorGUI.TextField(
                new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight),
                nameProperty.stringValue);

            // handle name changed
            if (nameValue != nameProperty.stringValue)
            {
                nameProperty.stringValue = nameValue;
                serializedObject.ApplyModifiedProperties();
            }

            // draw pairs of goods and their ids
            MoveHeight(ref rect, ref height);
            var goods = element.FindPropertyRelative("Goods");
            EditorGUI.LabelField(
                new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight),
                "Goods");

            // draw each good and their amount
            MoveHeight(ref rect, ref height);
            for (int i = 0; i < goods.arraySize; ++i)
            {
                var goodSerializedProperty = goods.GetArrayElementAtIndex(i);
                Vector2Int goodVector2 = goodSerializedProperty.vector2IntValue;

                // draw good id dropdown
                goodVector2.x = EditorGUI.Popup(
                    new Rect(rect.x, rect.y, (rect.width / 2) - 24, EditorGUIUtility.singleLineHeight),
                    goodVector2.x,
                    manager.MapData.Goods.Select(x => x.Name).ToArray()
                    );

                // draw good amount box
                goodVector2.y = EditorGUI.IntField(
                    new Rect(rect.x - 18 + (rect.width / 2), rect.y, (rect.width / 2) - 24, EditorGUIUtility.singleLineHeight),
                    goodVector2.y
                    );

                // draw delete button
                if (GUI.Button(new Rect(rect.width, rect.y, 16, EditorGUIUtility.singleLineHeight), "-"))
                {
                    goods.DeleteArrayElementAtIndex(i);
                    serializedObject.ApplyModifiedProperties();
                    break;
                }

                // handle changes
                if (goodSerializedProperty.vector2IntValue != goodVector2)
                {
                    goodSerializedProperty.vector2IntValue = goodVector2;
                    serializedObject.ApplyModifiedProperties();
                }

                MoveHeight(ref rect, ref height);
            }

            // draw add box
            if (GUI.Button(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight), "+"))
            {
                goods.InsertArrayElementAtIndex(goods.arraySize);
                serializedObject.ApplyModifiedProperties();
            }

            MoveHeight(ref rect, ref height);
            return height;
        }

        float OnDrawGoodsElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            // get manager
            var manager = Manager.Singleton;
            if (!manager)
                return 0;

            // try and get element at index in goods list
            var element = goods.SerializedProperty.GetArrayElementAtIndex(index);
            if (element == null)
                return 0;

            var height = EditorGUIUtility.singleLineHeight;
            rect.y += 2;

            // draw name text box
            var nameProperty = element.FindPropertyRelative("Name");
            var nameValue = EditorGUI.TextField(
                new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight),
                nameProperty.stringValue);

            // update on change
            if (nameValue != nameProperty.stringValue)
            {
                nameProperty.stringValue = nameValue;
                serializedObject.ApplyModifiedProperties();
            }

            return height;
        }

        private void Update()
        {
            // get cursor
            var cursor = MapEditorCursor.Singleton;
            if (_cursor != cursor)
            {
                _cursor = cursor;

                // resubscribe in case we're aren't already
                cursor.OnPaint -= OnPaint;
                cursor.OnPaint += OnPaint;
            }

            // set properties
            cursor.Visible = paintingActive;
            cursor.Radius = paintingRadius;
            cursor.Color = paintType == MapEditorPaintType.Node ? Utilities.GetNodeColor(paintNodeType) : Utilities.GetSegmentColor(paintSegmentType);
            cursor.HighlightSelectedNodes = paintType == MapEditorPaintType.Node;
            cursor.HighlightSelectedSegments = paintType == MapEditorPaintType.Segment;
        }

        private void OnPaint(object sender, Vector3 position)
        {
            var manager = Manager.Singleton;
            if (!manager)
                return;

            // record undo
            Undo.RecordObject(manager.MapData, "Paint");

            // get surrounding nodes
            var nodeIds = manager.GetNodeIdsByPosition(position, paintingRadius);

            switch (paintType)
            {
                case MapEditorPaintType.Node:
                    {
                        // update node types
                        foreach (var nodeId in nodeIds)
                        {
                            var nodeSP = nodesProperty.GetArrayElementAtIndex(nodeId.GetSingleId());
                            var nodeTypeSP = nodeSP.FindPropertyRelative("Type");
                            var nodeCityIdSP = nodeSP.FindPropertyRelative("CityId");
                            nodeTypeSP.enumValueIndex = (int)paintNodeType;

                            // set node CityId
                            if (paintNodeType == NodeType.MajorCity || paintNodeType == NodeType.MediumCity || paintNodeType == NodeType.SmallCity)
                            {
                                nodeCityIdSP.intValue = paintCityId;
                            }
                            else
                            {
                                nodeCityIdSP.intValue = 0;
                            }
                        }
                        break;
                    }
                case MapEditorPaintType.Segment:
                    {
                        // update node types
                        foreach (var nodeId in nodeIds)
                        {
                            // iterate segments
                            var segmentIndex = nodeId.GetSingleId() * 6;
                            for (Cardinal c = 0; c < Cardinal.MAX_CARDINAL; ++c)
                            {
                                var segmentSP = segmentsProperty.GetArrayElementAtIndex(segmentIndex + (int)c);
                                if (segmentSP != null)
                                {
                                    var neighborId = Utilities.PointTowards(nodeId, c);
                                    if (neighborId.InBounds)
                                    {
                                        var neighborPos = manager.GetPosition(neighborId);
                                        if (Vector3.Distance(neighborPos, position) < paintingRadius)
                                        {
                                            // set segment
                                            var segmentTypeSP = segmentSP.FindPropertyRelative("Type");
                                            segmentTypeSP.enumValueIndex = (int)paintSegmentType;

                                            // set matching segment
                                            var neighborSegmentIndex = neighborId.GetSingleId() * 6;
                                            var neighborSegmentSP = segmentsProperty.GetArrayElementAtIndex(neighborSegmentIndex + (int)Utilities.ReflectCardinal(c));
                                            if (neighborSegmentSP != null)
                                            {
                                                var neighborSegmentTypeSP = neighborSegmentSP.FindPropertyRelative("Type");
                                                neighborSegmentTypeSP.enumValueIndex = (int)paintSegmentType;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        break;
                    }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
