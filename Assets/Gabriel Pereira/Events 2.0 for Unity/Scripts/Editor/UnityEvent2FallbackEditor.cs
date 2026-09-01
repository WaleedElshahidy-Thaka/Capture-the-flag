using UnityEngine;

namespace UnityEditor
{
	// This forces any component with UnityEvent2 fields to use IMGUI inspector
	// which will properly invoke the PropertyDrawer's OnGUI
	[CustomEditor(typeof(MonoBehaviour), true, isFallback = true)]
	[CanEditMultipleObjects]
	public class UnityEvent2FallbackEditor : Editor
	{
		public override void OnInspectorGUI()
		{
			DrawDefaultInspector();
		}
	}
}