using System;

namespace UnityEngine.Events
{
	/// <summary>
	/// Use this attribute to trigger a custom method to build
	/// the arguments on the inspector.
	/// Note that the method specified by "methodName" must have signature like below:
	/// <code>
	/// For methods:
	/// public void ExampleMethod(SerializedProperty arguments, Rect argNameRect, Rect argRect) { }
	/// </code>
	/// <code>
	/// For parameters:
	/// public void ExampleMethod(SerializedProperty argument, Rect argNameRect, Rect argRect) { }
	/// </code>
	/// </summary>
	[AttributeUsage(AttributeTargets.Method | AttributeTargets.Parameter)]
	public class CustomInspectorAttribute : PropertyAttribute
	{
		public readonly string methodName = null;

		public CustomInspectorAttribute(string methodName)
		{
			this.methodName = methodName;
		}
	}
}