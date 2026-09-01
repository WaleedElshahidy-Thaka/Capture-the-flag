using UnityEngine;
using UnityEngine.UI;

namespace UnityEditor.UI
{
	[CustomEditor(typeof(Dropdown2), true)]
	[CanEditMultipleObjects]
	public class Dropdown2Editor : SelectableEditor
	{
		SerializedProperty m_Template;
		SerializedProperty m_CaptionText;
		SerializedProperty m_CaptionImage;
		SerializedProperty m_ItemText;
		SerializedProperty m_ItemImage;
		SerializedProperty m_Value;
		SerializedProperty m_Options;
		SerializedProperty m_CallbackType;
		SerializedProperty m_OnValueChanged;
		SerializedProperty m_OnLabelChanged;
		SerializedProperty m_OnSpriteChanged;
		SerializedProperty m_OnValueLabelChanged;
		SerializedProperty m_OnValueSpriteChanged;
		SerializedProperty m_OnLabelSpriteChanged;
		SerializedProperty m_OnValueLabelSpriteChanged;

		readonly GUIContent m_CallbackTypeGUIContent = EditorGUIUtility.TrTextContent("Callback Type:", "The type of callback when dropdown value changes.");

		readonly GUIContent[] m_CallbackTypeDisplayedOptions = new GUIContent[]
		{
			EditorGUIUtility.TrTextContent("Index", "The index of the new selected option is passed as parameter."),
			EditorGUIUtility.TrTextContent("Label", "The text of the new selected option is passed as parameter."),
			EditorGUIUtility.TrTextContent("Sprite", "The sprite of the new selected option is passed as parameter."),
			EditorGUIUtility.TrTextContent("Index and Label", "The index and text of the new selected option are passed as parameter."),
			EditorGUIUtility.TrTextContent("Index and Sprite", "The index and sprite of the new selected option are passed as parameter."),
			EditorGUIUtility.TrTextContent("Label and Sprite", "The text and sprite of the new selected option are passed as parameter."),
			EditorGUIUtility.TrTextContent("All", "The index, text and sprite of the new selected option are passed as parameter."),
		};

		protected override void OnEnable()
		{
			base.OnEnable();

			m_Template = serializedObject.FindProperty("m_Template");
			m_CaptionText = serializedObject.FindProperty("m_CaptionText");
			m_CaptionImage = serializedObject.FindProperty("m_CaptionImage");
			m_ItemText = serializedObject.FindProperty("m_ItemText");
			m_ItemImage = serializedObject.FindProperty("m_ItemImage");
			m_Value = serializedObject.FindProperty("m_Value");
			m_Options = serializedObject.FindProperty("m_Options");
			m_CallbackType = serializedObject.FindProperty("m_CallbackType");
			m_OnValueChanged = serializedObject.FindProperty("m_OnValueChanged");
			m_OnLabelChanged = serializedObject.FindProperty("m_OnLabelChanged");
			m_OnSpriteChanged = serializedObject.FindProperty("m_OnSpriteChanged");
			m_OnValueLabelChanged = serializedObject.FindProperty("m_OnValueLabelChanged");
			m_OnValueSpriteChanged = serializedObject.FindProperty("m_OnValueSpriteChanged");
			m_OnLabelSpriteChanged = serializedObject.FindProperty("m_OnLabelSpriteChanged");
			m_OnValueLabelSpriteChanged = serializedObject.FindProperty("m_OnValueLabelSpriteChanged");
		}

		public override void OnInspectorGUI()
		{
			base.OnInspectorGUI();

			serializedObject.Update();

			EditorGUILayout.Space();

			EditorGUILayout.PropertyField(m_Template);
			EditorGUILayout.PropertyField(m_CaptionText);
			EditorGUILayout.PropertyField(m_CaptionImage);
			EditorGUILayout.PropertyField(m_ItemText);
			EditorGUILayout.PropertyField(m_ItemImage);
			EditorGUILayout.PropertyField(m_Value);
			EditorGUILayout.PropertyField(m_Options);

			m_CallbackType.enumValueIndex = EditorGUILayout.Popup(m_CallbackTypeGUIContent, m_CallbackType.enumValueIndex, m_CallbackTypeDisplayedOptions, GUILayout.ExpandWidth(true));

			Dropdown2.CallbackType callbackType = (Dropdown2.CallbackType)m_CallbackType.enumValueIndex;

			switch (callbackType)
			{
				case Dropdown2.CallbackType.INDEX:
					EditorGUILayout.PropertyField(m_OnValueChanged);
					break;
				case Dropdown2.CallbackType.LABEL:
					EditorGUILayout.PropertyField(m_OnLabelChanged);
					break;
				case Dropdown2.CallbackType.SPRITE:
					EditorGUILayout.PropertyField(m_OnSpriteChanged);
					break;
				case Dropdown2.CallbackType.INDEX_LABEL:
					EditorGUILayout.PropertyField(m_OnValueLabelChanged);
					break;
				case Dropdown2.CallbackType.INDEX_SPRITE:
					EditorGUILayout.PropertyField(m_OnValueSpriteChanged);
					break;
				case Dropdown2.CallbackType.LABEL_SPRITE:
					EditorGUILayout.PropertyField(m_OnLabelSpriteChanged);
					break;
				case Dropdown2.CallbackType.ALL:
					EditorGUILayout.PropertyField(m_OnValueLabelSpriteChanged);
					break;
			}

			serializedObject.ApplyModifiedProperties();
		}
	}
}