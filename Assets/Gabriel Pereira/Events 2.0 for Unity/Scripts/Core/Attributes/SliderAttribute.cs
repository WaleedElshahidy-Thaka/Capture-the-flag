using System;

namespace UnityEngine.Events
{
	/// <summary>
	/// Use this attribute to turn a float parameter into a slider
	/// with min and max values.
	/// </summary>
	[AttributeUsage(AttributeTargets.Method | AttributeTargets.Parameter)]
	public class SliderAttribute : PropertyAttribute
	{
		/// <summary>
		/// The min value.
		/// </summary>
		public readonly float minValue = 0f;

		/// <summary>
		/// The max value.
		/// </summary>
		public readonly float maxValue = 0f;

		/// <summary>
		/// The default value to display.
		/// </summary>
		public readonly float defaultValue = 0;

		/// <summary>
		/// 
		/// </summary>
		/// <param name="min">The min value</param>
		/// <param name="max">The max value</param>
		public SliderAttribute(float min, float max) : this(min, max, min)
		{ }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="min">The min value.</param>
		/// <param name="max">The max value.</param>
		/// <param name="defaultValue">The default value to display.</param>
		public SliderAttribute(float min, float max, float defaultValue)
		{
			if (max < min)
				max = min;

			minValue = min;
			maxValue = max;
			this.defaultValue = Mathf.Clamp(defaultValue, min, max);
		}
	}
}