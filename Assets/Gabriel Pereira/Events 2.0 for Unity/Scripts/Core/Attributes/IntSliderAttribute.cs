using System;

namespace UnityEngine.Events
{
	//// <summary>
	/// Use this attribute to turn an integer parameter into a slider
	/// with min and max values.
	/// </summary>
	[AttributeUsage(AttributeTargets.Method | AttributeTargets.Parameter)]
	public class IntSliderAttribute : PropertyAttribute
	{
		/// <summary>
		/// The min value.
		/// </summary>
		public readonly int minValue = 0;

		/// <summary>
		/// The max value.
		/// </summary>
		public readonly int maxValue = 0;

		/// <summary>
		/// The default value to display.
		/// </summary>
		public readonly int defaultValue = 0;

		/// <summary>
		/// 
		/// </summary>
		/// <param name="min">The min value.</param>
		/// <param name="max">The max value.</param>
		public IntSliderAttribute(int min, int max) : this(min, max, min)
		{ }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="min">The min value.</param>
		/// <param name="max">The max value.</param>
		/// <param name="defaultValue">The default value to display.</param>
		public IntSliderAttribute(int min, int max, int defaultValue)
		{
			if (max < min)
				max = min;

			minValue = min;
			maxValue = max;
			this.defaultValue = Mathf.Clamp(defaultValue, min, max);
		}
	}
}