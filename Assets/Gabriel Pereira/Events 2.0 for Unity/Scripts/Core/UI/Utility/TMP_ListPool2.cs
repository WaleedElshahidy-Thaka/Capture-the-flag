#if TEXTMESHPRO
using System.Collections.Generic;

namespace TMPro
{
	internal static class TMP_ListPool2<T>
	{
		// Object pool to avoid allocations.
		private static readonly TMP_ObjectPool2<List<T>> s_ListPool = new TMP_ObjectPool2<List<T>>(null, l => l.Clear());

		public static List<T> Get()
		{
			return s_ListPool.Get();
		}

		public static void Release(List<T> toRelease)
		{
			s_ListPool.Release(toRelease);
		}
	}
} 
#endif