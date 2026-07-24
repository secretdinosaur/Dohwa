#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(SubclassSelectorAttribute))]
public class SubclassSelectorDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        float lineHeight = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;

        // 첫 줄: 레이블(왼쪽) + 타입 팝업(오른쪽)
        Rect labelRect = new Rect(position.x, position.y, EditorGUIUtility.labelWidth, lineHeight);
        Rect buttonRect = new Rect(position.x + EditorGUIUtility.labelWidth, position.y, position.width - EditorGUIUtility.labelWidth, lineHeight);

        // Draw simple label (foldout 표시를 위해 레이블만 그리기)
        string fullTypeName = property.managedReferenceFullTypename;
        string typeName = string.IsNullOrEmpty(fullTypeName) ? "None" : fullTypeName.Split(' ').Last();

        // Foldout toggle
        property.isExpanded = EditorGUI.Foldout(labelRect, property.isExpanded, label, true);

        if (GUI.Button(buttonRect, typeName, EditorStyles.popup))
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("None"), false, () => SetType(property, null));

            Type baseType = GetBaseType();
            var types = TypeCache.GetTypesDerivedFrom(baseType).Where(t => !t.IsAbstract && !t.IsInterface);

            foreach (Type type in types)
            {
                menu.AddItem(new GUIContent(type.Name), false, () => SetType(property, type));
            }
            menu.ShowAsContext();
        }

        // 두번째 줄 이후: 선택된 타입의 자식 프로퍼티 표시 (managed reference 내부)
        if (property.managedReferenceValue != null && property.isExpanded)
        {
            float y = position.y + lineHeight + spacing;

            SerializedProperty iter = property.Copy();
            SerializedProperty end = iter.GetEndProperty();
            bool enterChildren = iter.NextVisible(true); // move to first child

            while (enterChildren && !SerializedProperty.EqualContents(iter, end))
            {
                float h = EditorGUI.GetPropertyHeight(iter, true);
                Rect childRect = new Rect(position.x, y, position.width, h);
                EditorGUI.PropertyField(childRect, iter, true);
                y += h + spacing;
                enterChildren = iter.NextVisible(false);
            }
        }

        EditorGUI.EndProperty();
    }

    private void SetType(SerializedProperty property, Type type)
    {
        property.managedReferenceValue = type == null ? null : Activator.CreateInstance(type);
        // 선택 시 자동으로 내부 필드를 보이게 하기 위해 확장 상태 true로 설정
        property.isExpanded = (type != null);
        property.serializedObject.ApplyModifiedProperties();
    }

    private Type GetBaseType()
    {
        Type type = fieldInfo.FieldType;
        if (type.IsArray) return type.GetElementType();
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(System.Collections.Generic.List<>))
            return type.GetGenericArguments()[0];
        return type;
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;

        if (property.managedReferenceValue != null && property.isExpanded)
        {
            SerializedProperty iter = property.Copy();
            SerializedProperty end = iter.GetEndProperty();
            bool enterChildren = iter.NextVisible(true);

            while (enterChildren && !SerializedProperty.EqualContents(iter, end))
            {
                height += EditorGUI.GetPropertyHeight(iter, true) + EditorGUIUtility.standardVerticalSpacing;
                enterChildren = iter.NextVisible(false);
            }
        }
        return height;
    }
}
#endif