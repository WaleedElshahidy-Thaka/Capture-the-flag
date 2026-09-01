using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.Serialization;

namespace UnityEngine.Events
{
	[Serializable]
	public class PersistentCall2
	{
		[SerializeField]
		[FormerlySerializedAs("enabled")]
		[FormerlySerializedAs("m_Enabled")]
		private UnityEventCallState m_CallState = UnityEventCallState.RuntimeOnly;
		[SerializeField]
		private bool m_IsStatic;
		[SerializeField]
		private string m_AssemblyName;
		[SerializeField]
		private string m_TypeName;
		[SerializeField]
		[FormerlySerializedAs("instance")]
		private Object m_Target;
		[SerializeField]
		private bool m_IsStaticForInstance;
		[SerializeField]
		[FormerlySerializedAs("methodName")]
		private string m_MethodName;
		[SerializeField]
		[FormerlySerializedAs("arguments")]
		private List<ArgumentCache2> m_Arguments = new List<ArgumentCache2>();
		[SerializeField]
		[FormerlySerializedAs("modes")]
		private List<PersistentListenerMode2> m_Modes = new List<PersistentListenerMode2>();

		public UnityEventCallState callState
		{
			get { return m_CallState; }
			set { m_CallState = value; }
		}

		public bool isStatic
		{
			get { return m_IsStatic; }
			set { m_IsStatic = value; }
		}

		public string assemblyName
		{
			get { return m_AssemblyName; }
			set { m_AssemblyName = value; }
		}

		public string typeName
		{
			get { return m_TypeName; }
			set { m_TypeName = value; }
		}

		public Object target
		{
			get { return m_Target; }
			set { m_Target = value; }
		}

		public bool isStaticForInstance
		{
			get { return m_IsStaticForInstance; }
			set { m_IsStaticForInstance = value; }
		}

		public string methodName
		{
			get { return m_MethodName; }
			set { m_MethodName = value; }
		}

		public List<ArgumentCache2> arguments
		{
			get { return m_Arguments; }
			set { m_Arguments = value; }
		}

		public List<PersistentListenerMode2> modes
		{
			get { return m_Modes; }
		}

		public bool IsValid()
		{
			if (isStatic)
				return !string.IsNullOrEmpty(typeName) && !string.IsNullOrEmpty(methodName);
			else if (target != null)
				return !string.IsNullOrEmpty(methodName);

			return false;
		}

		public BaseInvokableCall2 GetRuntimeCall(UnityEventBase2 theEvent, object[] parameters)
		{
			if (m_CallState == UnityEventCallState.RuntimeOnly && !Application.isPlaying)
				return null;

			if (m_CallState == UnityEventCallState.Off || theEvent == null)
				return null;

			if (m_Modes.Count == 0)
				return null;

			MethodInfo method = theEvent.FindMethod(this);
			if (method == null)
				return null;

			if (m_Modes.Count == 1 && m_Modes[0] == PersistentListenerMode2.Void)
			{
				if (isStatic)
					return new InvokableCall2(typeName, method);

				return new InvokableCall2(target, method);
			}

			if (isStatic)
				return GetInvokableCall(typeName, method, m_Modes, m_Arguments.ToArray(), parameters);

			return GetInvokableCall(target, method, m_Modes, m_Arguments.ToArray(), parameters);
		}

		private BaseInvokableCall2 GetInvokableCall(string typeName, MethodInfo method, List<PersistentListenerMode2> modes, ArgumentCache2[] arguments, object[] informedParameters)
		{
			return GetInvokableCall(typeof(string), typeName, method, modes, arguments, informedParameters);
		}

		private BaseInvokableCall2 GetInvokableCall(Object target, MethodInfo method, List<PersistentListenerMode2> modes, ArgumentCache2[] arguments, object[] informedParameters)
		{
			return GetInvokableCall(typeof(Object), target, method, modes, arguments, informedParameters);
		}

		private BaseInvokableCall2 GetInvokableCall(Type firstType, object firstValue, MethodInfo method, List<PersistentListenerMode2> modes, ArgumentCache2[] arguments, object[] informedParameters)
		{
			Type[] typeArguments = new Type[arguments.Length];
			Type[] types = new Type[arguments.Length + 4];
			object[] parameters = new object[arguments.Length + 4];
			PersistentCall2[] persistentCalls2 = new PersistentCall2[arguments.Length];

			types[0] = firstType;
			types[1] = typeof(MethodInfo);
			types[2] = typeof(bool);
			types[3] = typeof(PersistentCall2[]);

			parameters[0] = firstValue;
			parameters[1] = method;
			parameters[2] = false;

			for (int i = 0; i < typeArguments.Length; i++)
			{
				typeArguments[i] = Type.GetType(arguments[i].unityObjectArgumentAssemblyTypeName, false) ?? typeof(Object);
				types[i + 4] = typeArguments[i];

				if (modes[i] == PersistentListenerMode2.EventDefined)
				{
					parameters[2] = true;
					parameters[i + 4] = informedParameters[i];
				}
				else
				{
					if (arguments[i].isDynamic)
					{
						parameters[i + 4] = typeArguments[i].IsValueType ? Activator.CreateInstance(typeArguments[i]) : null;

						persistentCalls2[i] = new PersistentCall2()
						{
							isStatic = arguments[i].dynamicIsStatic,
							assemblyName = arguments[i].dynamicAssemblyName,
							typeName = arguments[i].dynamicTypeName,
							target = arguments[i].dynamicTarget,
							isStaticForInstance = arguments[i].dynamicIsStaticForInstance,
							methodName = arguments[i].dynamicMethodName,
							arguments = arguments[i].dynamicArguments.ConvertAll(dynamicArgument => dynamicArgument.ToArgumentCache2())
						};
					}
					else
					{
						parameters[i + 4] = GetValue(typeArguments[i], arguments[i]);
					}
				}
			}

			parameters[3] = persistentCalls2;

			Type invokableType = null;

			if (typeArguments.Length == 1)
				invokableType = typeof(UpdatableInvokableCall<>);
			else if (typeArguments.Length == 2)
				invokableType = typeof(UpdatableInvokableCall<,>);
			else if (typeArguments.Length == 3)
				invokableType = typeof(UpdatableInvokableCall<,,>);
			else if (typeArguments.Length == 4)
				invokableType = typeof(UpdatableInvokableCall<,,,>);
			else if (typeArguments.Length == 5)
				invokableType = typeof(UpdatableInvokableCall<,,,,>);
			else if (typeArguments.Length == 6)
				invokableType = typeof(UpdatableInvokableCall<,,,,,>);
			else if (typeArguments.Length == 7)
				invokableType = typeof(UpdatableInvokableCall<,,,,,,>);
			else if (typeArguments.Length == 8)
				invokableType = typeof(UpdatableInvokableCall<,,,,,,,>);
			else if (typeArguments.Length == 9)
				invokableType = typeof(UpdatableInvokableCall<,,,,,,,,>);
			else if (typeArguments.Length == 10)
				invokableType = typeof(UpdatableInvokableCall<,,,,,,,,,>);
			else
				return null;

			ConstructorInfo constructor = invokableType.MakeGenericType(typeArguments).GetConstructor(types);

			return constructor == null ? null : constructor.Invoke(parameters) as BaseInvokableCall2;
		}

		public object GetValue(Type type, ArgumentCache2 argument)
		{
			if (type == typeof(char))
				return argument.charArgument;
			else if (type == typeof(byte))
				return argument.byteArgument;
			else if (type == typeof(sbyte))
				return argument.sbyteArgument;
			else if (type == typeof(short))
				return argument.shortArgument;
			else if (type == typeof(ushort))
				return argument.ushortArgument;
			else if (type == typeof(int))
				return argument.intArgument;
			else if (type == typeof(uint))
				return argument.uintArgument;
			else if (type.IsEnum)
				return Enum.GetValues(type).GetValue(argument.intArgument);
			else if (type == typeof(long))
				return argument.longArgument;
			else if (type == typeof(float))
				return argument.floatArgument;
			else if (type == typeof(double))
				return argument.doubleArgument;
			else if (type == typeof(string))
				return argument.stringArgument;
			else if (type == typeof(bool))
				return argument.boolArgument;
			else if (type == typeof(Vector2))
				return argument.vector2Argument;
			else if (type == typeof(Vector3))
				return argument.vector3Argument;
			else if (type == typeof(Vector2Int))
				return argument.vector2IntArgument;
			else if (type == typeof(Vector3Int))
				return argument.vector3IntArgument;
			else if (type == typeof(Vector4))
				return argument.vector4Argument;
			else if (type == typeof(LayerMask))
				return argument.layerMaskArgument;
			else if (type == typeof(Color))
				return argument.colorArgument;
			else if (type == typeof(Quaternion))
				return argument.quaternionArgument;
			else if (type.IsArray)
			{
				if (type == typeof(char[]))
					return argument.charArrayArgument.ToArray();
				else if (type == typeof(byte[]))
					return argument.byteArrayArgument.ToArray();
				else if (type == typeof(sbyte[]))
					return argument.sbyteArrayArgument.ToArray();
				else if (type == typeof(short[]))
					return argument.shortArrayArgument.ToArray();
				else if (type == typeof(ushort[]))
					return argument.ushortArrayArgument.ToArray();
				else if (type == typeof(int[]))
					return argument.intArrayArgument.ToArray();
				else if (type == typeof(uint[]))
					return argument.uintArrayArgument.ToArray();
				else if (type == typeof(long[]))
					return argument.longArrayArgument.ToArray();
				else if (type == typeof(float[]))
					return argument.floatArrayArgument.ToArray();
				else if (type == typeof(double[]))
					return argument.doubleArrayArgument.ToArray();
				else if (type == typeof(string[]))
					return argument.stringArrayArgument.ToArray();
				else if (type == typeof(bool[]))
					return argument.boolArrayArgument.ToArray();
				else if (type == typeof(Vector2[]))
					return argument.vector2ArrayArgument.ToArray();
				else if (type == typeof(Vector2Int[]))
					return argument.vector2IntArrayArgument.ToArray();
				else if (type == typeof(Vector3[]))
					return argument.vector3ArrayArgument.ToArray();
				else if (type == typeof(Vector3Int[]))
					return argument.vector3IntArrayArgument.ToArray();
				else if (type == typeof(Vector4[]))
					return argument.vector4ArrayArgument.ToArray();
				else if (type == typeof(LayerMask[]))
					return argument.layerMaskArrayArgument.ToArray();
				else if (type == typeof(Color[]))
					return argument.colorArrayArgument.ToArray();
				else if (type == typeof(Quaternion[]))
					return argument.quaternionArrayArgument.ToArray();
				else if (type.GetElementType().IsEnum)
				{
					Type listType = typeof(List<>).MakeGenericType(type.GetElementType());
					var instance = Activator.CreateInstance(listType);

					for (int i = 0; i < argument.intArrayArgument.Count; i++)
						listType.GetMethod("Add").Invoke(instance, new object[] { argument.intArrayArgument[i] });

					return listType.GetMethod("ToArray").Invoke(instance, null);
				}
				else if (type == typeof(ScriptableObject[]))
					return Array.ConvertAll(argument.unityObjectArrayArgument.ToArray(), x => (ScriptableObject)x);
				else if (type == typeof(GameObject[]))
					return Array.ConvertAll(argument.unityObjectArrayArgument.ToArray(), x => (GameObject)x);
				else
				{
					Type listType = typeof(List<>).MakeGenericType(type.GetElementType());
					var instance = Activator.CreateInstance(listType);

					for (int i = 0; i < argument.unityObjectArrayArgument.Count; i++)
						listType.GetMethod("Add").Invoke(instance, new object[] { Convert.ChangeType(argument.unityObjectArrayArgument[i], type.GetElementType()) });

					return listType.GetMethod("ToArray").Invoke(instance, null);
				}
			}
			else if (IsValidListType(type))
			{
				if (type == typeof(List<char>))
					return argument.charArrayArgument;
				else if (type == typeof(List<byte>))
					return argument.byteArrayArgument;
				else if (type == typeof(List<sbyte>))
					return argument.sbyteArrayArgument;
				else if (type == typeof(List<short>))
					return argument.shortArrayArgument;
				else if (type == typeof(List<ushort>))
					return argument.ushortArrayArgument;
				else if (type == typeof(List<int>))
					return argument.intArrayArgument;
				else if (type == typeof(List<uint>))
					return argument.uintArrayArgument;
				else if (type == typeof(List<long>))
					return argument.longArrayArgument;
				else if (type == typeof(List<float>))
					return argument.floatArrayArgument;
				else if (type == typeof(List<double>))
					return argument.doubleArrayArgument;
				else if (type == typeof(List<string>))
					return argument.stringArrayArgument;
				else if (type == typeof(List<bool>))
					return argument.boolArrayArgument;
				else if (type == typeof(List<Vector2>))
					return argument.vector2ArrayArgument;
				else if (type == typeof(List<Vector2Int>))
					return argument.vector2IntArrayArgument;
				else if (type == typeof(List<Vector3>))
					return argument.vector3ArrayArgument;
				else if (type == typeof(List<Vector3Int>))
					return argument.vector3IntArrayArgument;
				else if (type == typeof(List<Vector4>))
					return argument.vector4ArrayArgument;
				else if (type == typeof(List<LayerMask>))
					return argument.layerMaskArrayArgument;
				else if (type == typeof(List<Color>))
					return argument.colorArrayArgument;
				else if (type == typeof(List<Quaternion>))
					return argument.quaternionArrayArgument;
				else if (type.GetGenericArguments()[0].IsEnum)
				{
					Type listType = typeof(List<>).MakeGenericType(type.GetGenericArguments()[0]);
					var instance = Activator.CreateInstance(listType);

					for (int i = 0; i < argument.intArrayArgument.Count; i++)
						listType.GetMethod("Add").Invoke(instance, new object[] { argument.intArrayArgument[i] });

					return instance;
				}
				else
				{
					Type listType = typeof(List<>).MakeGenericType(type.GetGenericArguments()[0]);
					var instance = Activator.CreateInstance(listType);

					for (int i = 0; i < argument.unityObjectArrayArgument.Count; i++)
						listType.GetMethod("Add").Invoke(instance, new object[] { Convert.ChangeType(argument.unityObjectArrayArgument[i], type.GetGenericArguments()[0]) });

					return instance;
				}
			}

			if (argument.unityObjectArgument == null || !type.IsAssignableFrom(argument.unityObjectArgument.GetType()))
				return null;

			return argument.unityObjectArgument;
		}

		public void RegisterPersistentListener(Object target, string methodName)
		{
			m_Target = target;
			m_MethodName = methodName;
		}

		public void UnregisterPersistentListener()
		{
			m_Target = null;
			m_MethodName = string.Empty;
		}

		private bool IsValidListType(Type type)
		{
			var genericArguments = type.GetGenericArguments();

			return type.IsGenericType
					&& genericArguments.Length == 1
					&& typeof(List<>).MakeGenericType(genericArguments[0]).IsAssignableFrom(type);
		}
	}
}