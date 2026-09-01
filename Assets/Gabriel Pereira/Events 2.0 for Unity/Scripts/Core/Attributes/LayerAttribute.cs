using System;

namespace UnityEngine.Events
{
	/// <summary>
	/// Use this attribute to turn an integer parameter into a layer enum.
	/// </summary>
	[AttributeUsage(AttributeTargets.Method | AttributeTargets.Parameter)]
	public class LayerAttribute : PropertyAttribute
	{
		/// <summary>
		/// The default value to display.
		/// </summary>
		public readonly int defaultValue = 0;

		/// <summary>
		/// 
		/// </summary>
		/// <param name="layerIndex">The layer index</param>
		public LayerAttribute(int layerIndex = 0)
		{
			if (string.IsNullOrEmpty(LayerMask.LayerToName(layerIndex)))
				layerIndex = 0;

			defaultValue = layerIndex;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="layerName">The layer name</param>
		public LayerAttribute(string layerName)
		{
			int layer = LayerMask.NameToLayer(layerName);
			defaultValue = layer > -1 ? layer : 0;
		}
	}
}