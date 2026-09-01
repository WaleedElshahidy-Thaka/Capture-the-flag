using System;
using System.Collections.Generic;
using UnityEngine.Serialization;

namespace UnityEngine.Events
{
	[Serializable]
	public class DynamicArgumentCache2 : ISerializationCallbackReceiver
	{
		[SerializeField]
		[FormerlySerializedAs("objectArgument")]
		private Object m_ObjectArgument;
		[SerializeField]
		[FormerlySerializedAs("objectArgumentAssemblyTypeName")]
		private string m_ObjectArgumentAssemblyTypeName;
		[SerializeField]
		[FormerlySerializedAs("charArgument")]
		private char m_CharArgument;
		[SerializeField]
		[FormerlySerializedAs("byteArgument")]
		private byte m_ByteArgument;
		[SerializeField]
		[FormerlySerializedAs("sbyteArgument")]
		private sbyte m_SByteArgument;
		[SerializeField]
		[FormerlySerializedAs("shortArgument")]
		private short m_ShortArgument;
		[SerializeField]
		[FormerlySerializedAs("ushortArgument")]
		private ushort m_UShortArgument;
		[SerializeField]
		[FormerlySerializedAs("intArgument")]
		private int m_IntArgument;
		[SerializeField]
		[FormerlySerializedAs("uintArgument")]
		private uint m_UIntArgument;
		[SerializeField]
		[FormerlySerializedAs("longArgument")]
		private long m_LongArgument;
		[SerializeField]
		[FormerlySerializedAs("floatArgument")]
		private float m_FloatArgument;
		[SerializeField]
		[FormerlySerializedAs("doubleArgument")]
		private double m_DoubleArgument;
		[SerializeField]
		[FormerlySerializedAs("stringArgument")]
		private string m_StringArgument;
		[SerializeField]
		[FormerlySerializedAs("boolArgument")]
		private bool m_BoolArgument;
		[SerializeField]
		[FormerlySerializedAs("enumArgument")]
		private Enum m_EnumArgument;
		[SerializeField]
		[FormerlySerializedAs("vector2Argument")]
		private Vector2 m_Vector2Argument;
		[SerializeField]
		[FormerlySerializedAs("vector2IntArgument")]
		private Vector2Int m_Vector2IntArgument;
		[SerializeField]
		[FormerlySerializedAs("vector3Argument")]
		private Vector3 m_Vector3Argument;
		[SerializeField]
		[FormerlySerializedAs("vector3IntArgument")]
		private Vector3Int m_Vector3IntArgument;
		[SerializeField]
		[FormerlySerializedAs("vector4Argument")]
		private Vector4 m_Vector4Argument;
		[SerializeField]
		[FormerlySerializedAs("layerMaskArgument")]
		private LayerMask m_LayerMaskArgument;
		[SerializeField]
		[FormerlySerializedAs("colorArgument")]
		private Color m_ColorArgument;
		[SerializeField]
		[FormerlySerializedAs("quaternionArgument")]
		private Quaternion m_QuaternionArgument;
		[SerializeField]
		[FormerlySerializedAs("objectArrayArgument")]
		private List<Object> m_ObjectArrayArgument;
		[SerializeField]
		[FormerlySerializedAs("charArrayArgument")]
		private List<char> m_CharArrayArgument;
		[SerializeField]
		[FormerlySerializedAs("byteArrayArgument")]
		private List<byte> m_ByteArrayArgument;
		[SerializeField]
		[FormerlySerializedAs("sbyteArrayArgument")]
		private List<sbyte> m_SByteArrayArgument;
		[SerializeField]
		[FormerlySerializedAs("shortArrayArgument")]
		private List<short> m_ShortArrayArgument;
		[SerializeField]
		[FormerlySerializedAs("ushortArrayArgument")]
		private List<ushort> m_UShortArrayArgument;
		[SerializeField]
		[FormerlySerializedAs("intArrayArgument")]
		private List<int> m_IntArrayArgument;
		[SerializeField]
		[FormerlySerializedAs("uintArrayArgument")]
		private List<uint> m_UIntArrayArgument;
		[SerializeField]
		[FormerlySerializedAs("longArrayArgument")]
		private List<long> m_LongArrayArgument;
		[SerializeField]
		[FormerlySerializedAs("floatArrayArgument")]
		private List<float> m_FloatArrayArgument;
		[SerializeField]
		[FormerlySerializedAs("doubleArrayArgument")]
		private List<double> m_DoubleArrayArgument;
		[SerializeField]
		[FormerlySerializedAs("stringArrayArgument")]
		private List<string> m_StringArrayArgument;
		[SerializeField]
		[FormerlySerializedAs("boolArrayArgument")]
		private List<bool> m_BoolArrayArgument;
		[SerializeField]
		[FormerlySerializedAs("enumArrayArgument")]
		private List<Enum> m_EnumArrayArgument;
		[SerializeField]
		[FormerlySerializedAs("vector2ArrayArgument")]
		private List<Vector2> m_Vector2ArrayArgument;
		[SerializeField]
		[FormerlySerializedAs("vector2IntArrayArgument")]
		private List<Vector2Int> m_Vector2IntArrayArgument;
		[SerializeField]
		[FormerlySerializedAs("vector3ArrayArgument")]
		private List<Vector3> m_Vector3ArrayArgument;
		[SerializeField]
		[FormerlySerializedAs("vector3IntArrayArgument")]
		private List<Vector3Int> m_Vector3IntArrayArgument;
		[SerializeField]
		[FormerlySerializedAs("vector4ArrayArgument")]
		private List<Vector4> m_Vector4ArrayArgument;
		[SerializeField]
		[FormerlySerializedAs("layerMaskArrayArgument")]
		private List<LayerMask> m_LayerMaskArrayArgument;
		[SerializeField]
		[FormerlySerializedAs("colorArrayArgument")]
		private List<Color> m_ColorArrayArgument;
		[SerializeField]
		[FormerlySerializedAs("quaternionArrayArgument")]
		private List<Quaternion> m_QuaternionArrayArgument;

		public Object unityObjectArgument
		{
			get { return m_ObjectArgument; }
			set
			{
				m_ObjectArgument = value;
				m_ObjectArgumentAssemblyTypeName = value == null ? string.Empty : value.GetType().AssemblyQualifiedName;
			}
		}

		public string unityObjectArgumentAssemblyTypeName
		{
			get { return m_ObjectArgumentAssemblyTypeName; }
		}

		public char charArgument
		{
			get { return m_CharArgument; }
			set
			{
				m_CharArgument = value;
				m_ObjectArgumentAssemblyTypeName = typeof(char).AssemblyQualifiedName;
			}
		}

		public byte byteArgument
		{
			get { return m_ByteArgument; }
			set
			{
				m_ByteArgument = value;
				m_ObjectArgumentAssemblyTypeName = typeof(byte).AssemblyQualifiedName;
			}
		}

		public sbyte sbyteArgument
		{
			get { return m_SByteArgument; }
			set
			{
				m_SByteArgument = value;
				m_ObjectArgumentAssemblyTypeName = typeof(sbyte).AssemblyQualifiedName;
			}
		}

		public short shortArgument
		{
			get { return m_ShortArgument; }
			set
			{
				m_ShortArgument = value;
				m_ObjectArgumentAssemblyTypeName = typeof(short).AssemblyQualifiedName;
			}
		}

		public ushort ushortArgument
		{
			get { return m_UShortArgument; }
			set
			{
				m_UShortArgument = value;
				m_ObjectArgumentAssemblyTypeName = typeof(ushort).AssemblyQualifiedName;
			}
		}

		public int intArgument
		{
			get { return m_IntArgument; }
			set
			{
				m_IntArgument = value;
				m_ObjectArgumentAssemblyTypeName = typeof(int).AssemblyQualifiedName;
			}
		}

		public uint uintArgument
		{
			get { return m_UIntArgument; }
			set
			{
				m_UIntArgument = value;
				m_ObjectArgumentAssemblyTypeName = typeof(uint).AssemblyQualifiedName;
			}
		}

		public long longArgument
		{
			get { return m_LongArgument; }
			set
			{
				m_LongArgument = value;
				m_ObjectArgumentAssemblyTypeName = typeof(long).AssemblyQualifiedName;
			}
		}

		public float floatArgument
		{
			get { return m_FloatArgument; }
			set
			{
				m_FloatArgument = value;
				m_ObjectArgumentAssemblyTypeName = typeof(float).AssemblyQualifiedName;
			}
		}

		public double doubleArgument
		{
			get { return m_DoubleArgument; }
			set
			{
				m_DoubleArgument = value;
				m_ObjectArgumentAssemblyTypeName = typeof(double).AssemblyQualifiedName;
			}
		}

		public string stringArgument
		{
			get { return m_StringArgument; }
			set
			{
				m_StringArgument = value;
				m_ObjectArgumentAssemblyTypeName = typeof(string).AssemblyQualifiedName;
			}
		}

		public bool boolArgument
		{
			get { return m_BoolArgument; }
			set
			{
				m_BoolArgument = value;
				m_ObjectArgumentAssemblyTypeName = typeof(bool).AssemblyQualifiedName;
			}
		}

		public Enum enumArgument
		{
			get { return m_EnumArgument; }
			set
			{
				m_EnumArgument = value;
				m_ObjectArgumentAssemblyTypeName = value == null ? string.Empty : value.GetType().AssemblyQualifiedName;
			}
		}

		public Vector2 vector2Argument
		{
			get { return m_Vector2Argument; }
			set
			{
				m_Vector2Argument = value;
				m_ObjectArgumentAssemblyTypeName = typeof(Vector2).AssemblyQualifiedName;
			}
		}

		public Vector2Int vector2IntArgument
		{
			get { return m_Vector2IntArgument; }
			set
			{
				m_Vector2IntArgument = value;
				m_ObjectArgumentAssemblyTypeName = typeof(Vector2Int).AssemblyQualifiedName;
			}
		}

		public Vector3 vector3Argument
		{
			get { return m_Vector3Argument; }
			set
			{
				m_Vector3Argument = value;
				m_ObjectArgumentAssemblyTypeName = typeof(Vector3).AssemblyQualifiedName;
			}
		}

		public Vector3Int vector3IntArgument
		{
			get { return m_Vector3IntArgument; }
			set
			{
				m_Vector3IntArgument = value;
				m_ObjectArgumentAssemblyTypeName = typeof(Vector3Int).AssemblyQualifiedName;
			}
		}

		public Vector4 vector4Argument
		{
			get { return m_Vector4Argument; }
			set
			{
				m_Vector4Argument = value;
				m_ObjectArgumentAssemblyTypeName = typeof(Vector4).AssemblyQualifiedName;
			}
		}

		public LayerMask layerMaskArgument
		{
			get { return m_LayerMaskArgument; }
			set
			{
				m_LayerMaskArgument = value;
				m_ObjectArgumentAssemblyTypeName = typeof(LayerMask).AssemblyQualifiedName;
			}
		}

		public Color colorArgument
		{
			get { return m_ColorArgument; }
			set
			{
				m_ColorArgument = value;
				m_ObjectArgumentAssemblyTypeName = typeof(Color).AssemblyQualifiedName;
			}
		}

		public Quaternion quaternionArgument
		{
			get { return m_QuaternionArgument; }
			set
			{
				m_QuaternionArgument = value;
				m_ObjectArgumentAssemblyTypeName = typeof(Quaternion).AssemblyQualifiedName;
			}
		}

		public List<Object> unityObjectArrayArgument
		{
			get { return m_ObjectArrayArgument; }
			set
			{
				m_ObjectArrayArgument = value;
				m_ObjectArgumentAssemblyTypeName = value == null ? string.Empty : value.GetType().AssemblyQualifiedName;
			}
		}

		public List<char> charArrayArgument
		{
			get { return m_CharArrayArgument; }
			set
			{
				m_CharArrayArgument = value;
				m_ObjectArgumentAssemblyTypeName = typeof(List<char>).AssemblyQualifiedName;
			}
		}

		public List<byte> byteArrayArgument
		{
			get { return m_ByteArrayArgument; }
			set
			{
				m_ByteArrayArgument = value;
				m_ObjectArgumentAssemblyTypeName = typeof(List<byte>).AssemblyQualifiedName;
			}
		}

		public List<sbyte> sbyteArrayArgument
		{
			get { return m_SByteArrayArgument; }
			set
			{
				m_SByteArrayArgument = value;
				m_ObjectArgumentAssemblyTypeName = typeof(List<sbyte>).AssemblyQualifiedName;
			}
		}

		public List<short> shortArrayArgument
		{
			get { return m_ShortArrayArgument; }
			set
			{
				m_ShortArrayArgument = value;
				m_ObjectArgumentAssemblyTypeName = typeof(List<short>).AssemblyQualifiedName;
			}
		}

		public List<ushort> ushortArrayArgument
		{
			get { return m_UShortArrayArgument; }
			set
			{
				m_UShortArrayArgument = value;
				m_ObjectArgumentAssemblyTypeName = typeof(List<ushort>).AssemblyQualifiedName;
			}
		}

		public List<int> intArrayArgument
		{
			get { return m_IntArrayArgument; }
			set
			{
				m_IntArrayArgument = value;
				m_ObjectArgumentAssemblyTypeName = typeof(List<int>).AssemblyQualifiedName;
			}
		}

		public List<uint> uintArrayArgument
		{
			get { return m_UIntArrayArgument; }
			set
			{
				m_UIntArrayArgument = value;
				m_ObjectArgumentAssemblyTypeName = typeof(List<uint>).AssemblyQualifiedName;
			}
		}

		public List<long> longArrayArgument
		{
			get { return m_LongArrayArgument; }
			set
			{
				m_LongArrayArgument = value;
				m_ObjectArgumentAssemblyTypeName = typeof(List<long>).AssemblyQualifiedName;
			}
		}

		public List<float> floatArrayArgument
		{
			get { return m_FloatArrayArgument; }
			set
			{
				m_FloatArrayArgument = value;
				m_ObjectArgumentAssemblyTypeName = typeof(List<float>).AssemblyQualifiedName;
			}
		}

		public List<double> doubleArrayArgument
		{
			get { return m_DoubleArrayArgument; }
			set
			{
				m_DoubleArrayArgument = value;
				m_ObjectArgumentAssemblyTypeName = typeof(List<double>).AssemblyQualifiedName;
			}
		}

		public List<string> stringArrayArgument
		{
			get { return m_StringArrayArgument; }
			set
			{
				m_StringArrayArgument = value;
				m_ObjectArgumentAssemblyTypeName = typeof(List<string>).AssemblyQualifiedName;
			}
		}

		public List<bool> boolArrayArgument
		{
			get { return m_BoolArrayArgument; }
			set
			{
				m_BoolArrayArgument = value;
				m_ObjectArgumentAssemblyTypeName = typeof(List<bool>).AssemblyQualifiedName;
			}
		}

		public List<Enum> enumArrayArgument
		{
			get { return m_EnumArrayArgument; }
			set
			{
				m_EnumArrayArgument = value;
				m_ObjectArgumentAssemblyTypeName = value == null ? string.Empty : value.GetType().AssemblyQualifiedName;
			}
		}

		public List<Vector2> vector2ArrayArgument
		{
			get { return m_Vector2ArrayArgument; }
			set
			{
				m_Vector2ArrayArgument = value;
				m_ObjectArgumentAssemblyTypeName = typeof(List<Vector2>).AssemblyQualifiedName;
			}
		}

		public List<Vector2Int> vector2IntArrayArgument
		{
			get { return m_Vector2IntArrayArgument; }
			set
			{
				m_Vector2IntArrayArgument = value;
				m_ObjectArgumentAssemblyTypeName = typeof(List<Vector2Int>).AssemblyQualifiedName;
			}
		}

		public List<Vector3> vector3ArrayArgument
		{
			get { return m_Vector3ArrayArgument; }
			set
			{
				m_Vector3ArrayArgument = value;
				m_ObjectArgumentAssemblyTypeName = typeof(List<Vector3>).AssemblyQualifiedName;
			}
		}

		public List<Vector3Int> vector3IntArrayArgument
		{
			get { return m_Vector3IntArrayArgument; }
			set
			{
				m_Vector3IntArrayArgument = value;
				m_ObjectArgumentAssemblyTypeName = typeof(List<Vector3Int>).AssemblyQualifiedName;
			}
		}

		public List<Vector4> vector4ArrayArgument
		{
			get { return m_Vector4ArrayArgument; }
			set
			{
				m_Vector4ArrayArgument = value;
				m_ObjectArgumentAssemblyTypeName = typeof(List<Vector4>).AssemblyQualifiedName;
			}
		}

		public List<LayerMask> layerMaskArrayArgument
		{
			get { return m_LayerMaskArrayArgument; }
			set
			{
				m_LayerMaskArrayArgument = value;
				m_ObjectArgumentAssemblyTypeName = typeof(List<LayerMask>).AssemblyQualifiedName;
			}
		}

		public List<Color> colorArrayArgument
		{
			get { return m_ColorArrayArgument; }
			set
			{
				m_ColorArrayArgument = value;
				m_ObjectArgumentAssemblyTypeName = typeof(List<Color>).AssemblyQualifiedName;
			}
		}

		public List<Quaternion> quaternionArrayArgument
		{
			get { return m_QuaternionArrayArgument; }
			set
			{
				m_QuaternionArrayArgument = value;
				m_ObjectArgumentAssemblyTypeName = typeof(List<Quaternion>).AssemblyQualifiedName;
			}
		}

		private void TidyAssemblyTypeName()
		{
			if (string.IsNullOrEmpty(m_ObjectArgumentAssemblyTypeName))
				return;

			int num = int.MaxValue;
			int val1_1 = m_ObjectArgumentAssemblyTypeName.IndexOf(", Version=");
			if (val1_1 != -1)
				num = Math.Min(val1_1, num);
			int val1_2 = m_ObjectArgumentAssemblyTypeName.IndexOf(", Culture=");
			if (val1_2 != -1)
				num = Math.Min(val1_2, num);
			int val1_3 = m_ObjectArgumentAssemblyTypeName.IndexOf(", PublicKeyToken=");
			if (val1_3 != -1)
				num = Math.Min(val1_3, num);
			if (num == int.MaxValue)
				return;

			m_ObjectArgumentAssemblyTypeName = m_ObjectArgumentAssemblyTypeName.Substring(0, num);

			int val1_4 = m_ObjectArgumentAssemblyTypeName.IndexOf("[[");
			if (val1_4 != -1)
				m_ObjectArgumentAssemblyTypeName += "]]";
		}

		public void OnBeforeSerialize()
		{
			TidyAssemblyTypeName();
		}

		public void OnAfterDeserialize()
		{
			TidyAssemblyTypeName();
		}

		public ArgumentCache2 ToArgumentCache2()
		{
			return new ArgumentCache2()
			{
				unityObjectArgument = m_ObjectArgument,
				charArgument = m_CharArgument,
				byteArgument = m_ByteArgument,
				sbyteArgument = m_SByteArgument,
				shortArgument = m_ShortArgument,
				ushortArgument = m_UShortArgument,
				intArgument = m_IntArgument,
				uintArgument = m_UIntArgument,
				longArgument = m_LongArgument,
				floatArgument = m_FloatArgument,
				doubleArgument = m_DoubleArgument,
				stringArgument = m_StringArgument,
				boolArgument = m_BoolArgument,
				enumArgument = m_EnumArgument,
				vector2Argument = m_Vector2Argument,
				vector2IntArgument = m_Vector2IntArgument,
				vector3Argument = m_Vector3Argument,
				vector3IntArgument = m_Vector3IntArgument,
				vector4Argument = m_Vector4Argument,
				layerMaskArgument = m_LayerMaskArgument,
				colorArgument = m_ColorArgument,
				quaternionArgument = m_QuaternionArgument,
				unityObjectArrayArgument = m_ObjectArrayArgument,
				charArrayArgument = m_CharArrayArgument,
				byteArrayArgument = m_ByteArrayArgument,
				sbyteArrayArgument = m_SByteArrayArgument,
				shortArrayArgument = m_ShortArrayArgument,
				ushortArrayArgument = m_UShortArrayArgument,
				intArrayArgument = m_IntArrayArgument,
				uintArrayArgument = m_UIntArrayArgument,
				longArrayArgument = m_LongArrayArgument,
				floatArrayArgument = m_FloatArrayArgument,
				doubleArrayArgument = m_DoubleArrayArgument,
				stringArrayArgument = m_StringArrayArgument,
				boolArrayArgument = m_BoolArrayArgument,
				enumArrayArgument = m_EnumArrayArgument,
				vector2ArrayArgument = m_Vector2ArrayArgument,
				vector2IntArrayArgument = m_Vector2IntArrayArgument,
				vector3ArrayArgument = m_Vector3ArrayArgument,
				vector3IntArrayArgument = m_Vector3IntArrayArgument,
				vector4ArrayArgument = m_Vector4ArrayArgument,
				layerMaskArrayArgument = m_LayerMaskArrayArgument,
				colorArrayArgument = m_ColorArrayArgument,
				quaternionArrayArgument = m_QuaternionArrayArgument,
				unityObjectArgumentAssemblyTypeName = m_ObjectArgumentAssemblyTypeName,
			};
		}
	}
}