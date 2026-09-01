using System;

namespace UnityEngine.Events
{
	/// <summary>
	/// Use this attribute to turn a string parameter into a tag field.
	/// </summary>
	[AttributeUsage(AttributeTargets.Method | AttributeTargets.Parameter)]
	public class TagAttribute : PropertyAttribute
	{
		/// <summary>
		/// The default value to display.
		/// </summary>
		public readonly string defaultValue = null;

		/// <summary>
		/// 
		/// </summary>
		/// <param name="defaultValue">The default value to display.</param>
		public TagAttribute(string defaultValue = "Untagged")
		{
			if (string.IsNullOrEmpty(defaultValue)
				|| string.IsNullOrWhiteSpace(defaultValue))
				defaultValue = "Untagged";

			this.defaultValue = defaultValue;
		}
	}
}