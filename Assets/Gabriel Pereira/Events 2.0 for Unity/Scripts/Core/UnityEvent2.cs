using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.Serialization;

namespace UnityEngine.Events
{
	[Serializable]
	public abstract class UnityEventBase2 : ISerializationCallbackReceiver
	{
		private bool m_CallsDirty = true;
		private InvokableCallList2 m_Calls;

		[SerializeField]
		[FormerlySerializedAs("m_PersistentListeners")]
		private PersistentCallGroup2 m_PersistentCalls = null;

		protected UnityEventBase2()
		{
			m_PersistentCalls = new PersistentCallGroup2();
		}

		void ISerializationCallbackReceiver.OnBeforeSerialize()
		{
		}

		void ISerializationCallbackReceiver.OnAfterDeserialize()
		{
			DirtyPersistentCalls();
		}

		protected abstract MethodInfo FindMethod_Impl(object targetObj, string name, bool isStatic);

		internal abstract BaseInvokableCall2 GetDelegate(object target, MethodInfo theFunction);

		internal MethodInfo FindMethod(PersistentCall2 call)
		{
			Type[] argumentTypes = new Type[call.arguments.Count];

			for (int i = 0; i < argumentTypes.Length; i++)
				argumentTypes[i] = Type.GetType(call.arguments[i].unityObjectArgumentAssemblyTypeName, false) ?? typeof(Object);

			if (call.isStatic)
				return GetValidMethodInfo(call.assemblyName, call.typeName, call.methodName, argumentTypes);

			return GetValidMethodInfo(call.target, call.methodName, argumentTypes, call.isStaticForInstance);
		}

		internal MethodInfo FindMethod(object listener, string name, PersistentListenerMode2 mode, Type argumentType, bool isStatic)
		{
			switch (mode)
			{
				case PersistentListenerMode2.EventDefined:
					return FindMethod_Impl(listener, name, isStatic);
				case PersistentListenerMode2.Void:
					return GetValidMethodInfo(listener, name, new Type[0], isStatic);
				case PersistentListenerMode2.Object:
					return GetValidMethodInfo(listener, name, new Type[1] { argumentType ?? typeof(Object) }, isStatic);
				case PersistentListenerMode2.Char:
					return GetValidMethodInfo(listener, name, new Type[1] { typeof(char) }, isStatic);
				case PersistentListenerMode2.Byte:
					return GetValidMethodInfo(listener, name, new Type[1] { typeof(byte) }, isStatic);
				case PersistentListenerMode2.SByte:
					return GetValidMethodInfo(listener, name, new Type[1] { typeof(sbyte) }, isStatic);
				case PersistentListenerMode2.Short:
					return GetValidMethodInfo(listener, name, new Type[1] { typeof(short) }, isStatic);
				case PersistentListenerMode2.UShort:
					return GetValidMethodInfo(listener, name, new Type[1] { typeof(ushort) }, isStatic);
				case PersistentListenerMode2.Int:
					return GetValidMethodInfo(listener, name, new Type[1] { typeof(int) }, isStatic);
				case PersistentListenerMode2.UInt:
					return GetValidMethodInfo(listener, name, new Type[1] { typeof(uint) }, isStatic);
				case PersistentListenerMode2.Long:
					return GetValidMethodInfo(listener, name, new Type[1] { typeof(long) }, isStatic);
				case PersistentListenerMode2.Float:
					return GetValidMethodInfo(listener, name, new Type[1] { typeof(float) }, isStatic);
				case PersistentListenerMode2.Double:
					return GetValidMethodInfo(listener, name, new Type[1] { typeof(double) }, isStatic);
				case PersistentListenerMode2.String:
					return GetValidMethodInfo(listener, name, new Type[1] { typeof(string) }, isStatic);
				case PersistentListenerMode2.Bool:
					return GetValidMethodInfo(listener, name, new Type[1] { typeof(bool) }, isStatic);
				case PersistentListenerMode2.Enum:
					return GetValidMethodInfo(listener, name, new Type[1] { typeof(Enum) }, isStatic);
				default:
					return null;
			}
		}

		public MethodInfo FindMethod(object listener, string name, PersistentListenerMode2 mode, Type[] argumentTypes = null)
		{
			if (mode == PersistentListenerMode2.EventDefined)
				return FindMethod_Impl(listener, name, false);

			return GetValidMethodInfo(listener, name, argumentTypes);
		}

		/// <summary>
		///   <para>Get the number of registered persistent listeners.</para>
		/// </summary>
		public int GetPersistentEventCount()
		{
			return m_PersistentCalls.Count;
		}

		/// <summary>
		///   <para>Get the target component of the listener at index index.</para>
		/// </summary>
		/// <param name="index">Index of the listener to query.</param>
		public Object GetPersistentTarget(int index)
		{
			PersistentCall2 listener = m_PersistentCalls.GetListener(index);
			if (listener != null)
				return listener.target;
			return null;
		}

		/// <summary>
		///   <para>Get the target method name of the listener at index index.</para>
		/// </summary>
		/// <param name="index">Index of the listener to query.</param>
		public string GetPersistentMethodName(int index)
		{
			PersistentCall2 listener = m_PersistentCalls.GetListener(index);
			if (listener != null)
				return listener.methodName;
			return string.Empty;
		}

		private void DirtyPersistentCalls()
		{
			Calls.ClearPersistent();
			m_CallsDirty = true;
		}

		private void RebuildPersistentCallsIfNeeded(object[] parameters)
		{
			if (!m_CallsDirty) return;

			m_PersistentCalls.Initialize(Calls, this, parameters);
			m_CallsDirty = false;
		}

		/// <summary>
		///   <para>Modify the execution state of a persistent listener.</para>
		/// </summary>
		/// <param name="index">Index of the listener to query.</param>
		/// <param name="state">State to set.</param>
		public void SetPersistentListenerState(int index, UnityEventCallState state)
		{
			PersistentCall2 listener = m_PersistentCalls.GetListener(index);
			if (listener != null)
				listener.callState = state;
			DirtyPersistentCalls();
		}

		protected void AddListener(object targetObj, MethodInfo method)
		{
			Calls.AddListener(GetDelegate(targetObj, method));
		}

		internal void AddCall(BaseInvokableCall2 call)
		{
			Calls.AddListener(call);
		}

		protected void RemoveListener(object targetObj, MethodInfo method)
		{
			Calls.RemoveListener(targetObj, method);
		}

		/// <summary>
		/// Remove all listeners from the event.
		/// </summary>
		public void RemoveAllListeners()
		{
			Calls.Clear();
		}

		protected List<BaseInvokableCall2> PrepareInvoke(params object[] parameters)
		{
			RebuildPersistentCallsIfNeeded(parameters);
			return Calls.PrepareInvoke();
		}

		protected void Invoke(object[] parameters)
		{
			List<BaseInvokableCall2> calls = PrepareInvoke(parameters);

			for (var i = 0; i < calls.Count; i++)
				calls[i].Invoke(parameters);
		}

		public override string ToString()
		{
			return base.ToString() + " " + GetType().FullName;
		}

		/// <summary>
		///   <para>Given a type name, function name, and a list of argument types; find the method that matches.</para>
		/// </summary>
		/// <param name="assemblyName">Assembly name that contains the type.</param>
		/// <param name="typeName">Type name to search for the method.</param>
		/// <param name="functionName">Function name to search for.</param>
		/// <param name="argumentTypes">Argument types for the function.</param>
		public static MethodInfo GetValidMethodInfo(string assemblyName, string typeName, string functionName, Type[] argumentTypes)
		{
			return GetValidMethodInfo(Type.GetType(string.Format("{0}, {1}", typeName, assemblyName), false), functionName, argumentTypes, true);
		}

		/// <summary>
		///   <para>Given an object, function name, and a list of argument types; find the method that matches.</para>
		/// </summary>
		/// <param name="obj">Object to search for the method.</param>
		/// <param name="functionName">Function name to search for.</param>
		/// <param name="argumentTypes">Argument types for the function.</param>
		/// <param name="isStatic">Wheter method is static.</param>
		public static MethodInfo GetValidMethodInfo(object obj, string functionName, Type[] argumentTypes, bool isStatic = false)
		{
			return GetValidMethodInfo(obj.GetType(), functionName, argumentTypes, isStatic);
		}

		/// <summary>
		///   <para>Given a type, function name, and a list of argument types; find the method that matches.</para>
		/// </summary>
		/// <param name="type">Type to search for the method.</param>
		/// <param name="functionName">Function name to search for.</param>
		/// <param name="argumentTypes">Argument types for the function.</param>
		/// <param name="isStatic">Wheter method is static.</param>
		private static MethodInfo GetValidMethodInfo(Type type, string functionName, Type[] argumentTypes, bool isStatic)
		{
			for (Type t = type; t != typeof(object) && t != null; t = t.BaseType)
			{
				MethodInfo method = null;

				try
				{
					BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

					if (isStatic)
						bindingFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

					method = t.GetMethod(functionName, bindingFlags, null, argumentTypes, null);
				}
				catch (Exception)
				{ }

				if (method != null)
				{
					ParameterInfo[] parameters = method.GetParameters();

					if (parameters.Length != argumentTypes.Length)
						continue;

					bool flag = true;
					int index = 0;
					foreach (ParameterInfo parameterInfo in parameters)
					{
						flag = argumentTypes[index].IsPrimitive == parameterInfo.ParameterType.IsPrimitive;
						if (flag)
							++index;
						else
							break;
					}
					if (flag)
						return method;
				}
			}

			return null;
		}

		protected bool ValidateRegistration(MethodInfo method, object targetObj, PersistentListenerMode2 mode)
		{
			return ValidateRegistration(method, targetObj, mode, typeof(Object));
		}

		protected bool ValidateRegistration(MethodInfo method, object targetObj, PersistentListenerMode2 mode, Type argumentType)
		{
			if (method == null)
				throw new ArgumentNullException("method", string.Format("Can not register null method on {0} for callback!", targetObj));
			Object @object = targetObj as Object;
			if (@object == null || @object.GetInstanceID() == 0)
				throw new ArgumentException(string.Format("Could not register callback {0} on {1}. The class {2} does not derive from UnityEngine.Object", method.Name, targetObj, (targetObj != null ? targetObj.GetType().ToString() : "null")));
			//if (method.IsStatic)
			//	throw new ArgumentException(string.Format("Could not register listener {0} on {1} static functions are not supported.", method, GetType()));
			//if (FindMethod(targetObj, method.Name, mode, argumentType) != null)
			//	return true;
			if (FindMethod(targetObj, method.Name, mode, argumentType, method.IsStatic) != null)
				return true;
			Debug.LogWarning(string.Format("Could not register listener {0}.{1} on {2} the method could not be found.", targetObj, method, GetType()));
			return false;
		}

		internal void AddPersistentListener()
		{
			m_PersistentCalls.AddListener();
		}

		protected void RegisterPersistentListener(int index, object targetObj, MethodInfo method)
		{
			if (!ValidateRegistration(method, targetObj, PersistentListenerMode2.EventDefined))
				return;
			m_PersistentCalls.RegisterEventPersistentListener(index, targetObj as Object, method.Name);
			DirtyPersistentCalls();
		}

		internal void RemovePersistentListener(Object target, MethodInfo method)
		{
			//if (method == null || method.IsStatic || (target == null || target.GetInstanceID() == 0))
			//	return;
			if (method == null || (target == null || target.GetInstanceID() == 0))
				return;
			m_PersistentCalls.RemoveListeners(target, method.Name);
			DirtyPersistentCalls();
		}

		internal void RemovePersistentListener(int index)
		{
			m_PersistentCalls.RemoveListener(index);
			DirtyPersistentCalls();
		}

		internal void UnregisterPersistentListener(int index)
		{
			m_PersistentCalls.UnregisterPersistentListener(index);
			DirtyPersistentCalls();
		}

		internal void AddVoidPersistentListener(UnityAction call)
		{
			int persistentEventCount = GetPersistentEventCount();
			AddPersistentListener();
			RegisterVoidPersistentListener(persistentEventCount, call);
		}

		internal void RegisterVoidPersistentListener(int index, UnityAction call)
		{
			if (call == null)
			{
				Debug.LogWarning("Registering a Listener requires an action");
			}
			else
			{
				if (!ValidateRegistration(call.Method, call.Target, PersistentListenerMode2.Void))
					return;
				m_PersistentCalls.RegisterVoidPersistentListener(index, call.Target as Object, call.Method.Name);
				DirtyPersistentCalls();
			}
		}

		internal void AddCharPersistentListener(UnityAction<char> call, char argument)
		{
			int persistentEventCount = GetPersistentEventCount();
			AddPersistentListener();
			RegisterCharPersistentListener(persistentEventCount, call, argument);
		}

		internal void RegisterCharPersistentListener(int index, UnityAction<char> call, char argument)
		{
			if (call == null)
			{
				Debug.LogWarning("Registering a Listener requires an action");
			}
			else
			{
				if (!ValidateRegistration(call.Method, call.Target, PersistentListenerMode2.Char))
					return;
				m_PersistentCalls.RegisterCharPersistentListener(index, call.Target as Object, argument, call.Method.Name);
				DirtyPersistentCalls();
			}
		}

		internal void AddBytePersistentListener(UnityAction<byte> call, byte argument)
		{
			int persistentEventCount = GetPersistentEventCount();
			AddPersistentListener();
			RegisterBytePersistentListener(persistentEventCount, call, argument);
		}

		internal void RegisterBytePersistentListener(int index, UnityAction<byte> call, byte argument)
		{
			if (call == null)
			{
				Debug.LogWarning("Registering a Listener requires an action");
			}
			else
			{
				if (!ValidateRegistration(call.Method, call.Target, PersistentListenerMode2.Byte))
					return;
				m_PersistentCalls.RegisterBytePersistentListener(index, call.Target as Object, argument, call.Method.Name);
				DirtyPersistentCalls();
			}
		}

		internal void AddSBytePersistentListener(UnityAction<SByte> call, SByte argument)
		{
			int persistentEventCount = GetPersistentEventCount();
			AddPersistentListener();
			RegisterSBytePersistentListener(persistentEventCount, call, argument);
		}

		internal void RegisterSBytePersistentListener(int index, UnityAction<SByte> call, SByte argument)
		{
			if (call == null)
			{
				Debug.LogWarning("Registering a Listener requires an action");
			}
			else
			{
				if (!ValidateRegistration(call.Method, call.Target, PersistentListenerMode2.Int))
					return;
				m_PersistentCalls.RegisterSBytePersistentListener(index, call.Target as Object, argument, call.Method.Name);
				DirtyPersistentCalls();
			}
		}

		internal void AddShortPersistentListener(UnityAction<short> call, short argument)
		{
			int persistentEventCount = GetPersistentEventCount();
			AddPersistentListener();
			RegisterShortPersistentListener(persistentEventCount, call, argument);
		}

		internal void RegisterShortPersistentListener(int index, UnityAction<short> call, short argument)
		{
			if (call == null)
			{
				Debug.LogWarning("Registering a Listener requires an action");
			}
			else
			{
				if (!ValidateRegistration(call.Method, call.Target, PersistentListenerMode2.Short))
					return;
				m_PersistentCalls.RegisterShortPersistentListener(index, call.Target as Object, argument, call.Method.Name);
				DirtyPersistentCalls();
			}
		}

		internal void AddUShortPersistentListener(UnityAction<ushort> call, ushort argument)
		{
			int persistentEventCount = GetPersistentEventCount();
			AddPersistentListener();
			RegisterUShortPersistentListener(persistentEventCount, call, argument);
		}

		internal void RegisterUShortPersistentListener(int index, UnityAction<ushort> call, ushort argument)
		{
			if (call == null)
			{
				Debug.LogWarning("Registering a Listener requires an action");
			}
			else
			{
				if (!ValidateRegistration(call.Method, call.Target, PersistentListenerMode2.UShort))
					return;
				m_PersistentCalls.RegisterUShortPersistentListener(index, call.Target as Object, argument, call.Method.Name);
				DirtyPersistentCalls();
			}
		}

		internal void AddIntPersistentListener(UnityAction<int> call, int argument)
		{
			int persistentEventCount = GetPersistentEventCount();
			AddPersistentListener();
			RegisterIntPersistentListener(persistentEventCount, call, argument);
		}

		internal void RegisterIntPersistentListener(int index, UnityAction<int> call, int argument)
		{
			if (call == null)
			{
				Debug.LogWarning("Registering a Listener requires an action");
			}
			else
			{
				if (!ValidateRegistration(call.Method, call.Target, PersistentListenerMode2.Int))
					return;
				m_PersistentCalls.RegisterIntPersistentListener(index, call.Target as Object, argument, call.Method.Name);
				DirtyPersistentCalls();
			}
		}

		internal void AddUIntPersistentListener(UnityAction<uint> call, uint argument)
		{
			int persistentEventCount = GetPersistentEventCount();
			AddPersistentListener();
			RegisterUIntPersistentListener(persistentEventCount, call, argument);
		}

		internal void RegisterUIntPersistentListener(int index, UnityAction<uint> call, uint argument)
		{
			if (call == null)
			{
				Debug.LogWarning("Registering a Listener requires an action");
			}
			else
			{
				if (!ValidateRegistration(call.Method, call.Target, PersistentListenerMode2.UInt))
					return;
				m_PersistentCalls.RegisterUIntPersistentListener(index, call.Target as Object, argument, call.Method.Name);
				DirtyPersistentCalls();
			}
		}

		internal void AddLongPersistentListener(UnityAction<long> call, long argument)
		{
			int persistentEventCount = GetPersistentEventCount();
			AddPersistentListener();
			RegisterLongPersistentListener(persistentEventCount, call, argument);
		}

		internal void RegisterLongPersistentListener(int index, UnityAction<long> call, long argument)
		{
			if (call == null)
			{
				Debug.LogWarning("Registering a Listener requires an action");
			}
			else
			{
				if (!ValidateRegistration(call.Method, call.Target, PersistentListenerMode2.Long))
					return;
				m_PersistentCalls.RegisterLongPersistentListener(index, call.Target as Object, argument, call.Method.Name);
				DirtyPersistentCalls();
			}
		}

		internal void AddFloatPersistentListener(UnityAction<float> call, float argument)
		{
			int persistentEventCount = GetPersistentEventCount();
			AddPersistentListener();
			RegisterFloatPersistentListener(persistentEventCount, call, argument);
		}

		internal void RegisterFloatPersistentListener(int index, UnityAction<float> call, float argument)
		{
			if (call == null)
			{
				Debug.LogWarning("Registering a Listener requires an action");
			}
			else
			{
				if (!ValidateRegistration(call.Method, call.Target, PersistentListenerMode2.Float))
					return;
				m_PersistentCalls.RegisterFloatPersistentListener(index, call.Target as Object, argument, call.Method.Name);
				DirtyPersistentCalls();
			}
		}

		internal void AddDoublePersistentListener(UnityAction<double> call, double argument)
		{
			int persistentEventCount = GetPersistentEventCount();
			AddPersistentListener();
			RegisterDoublePersistentListener(persistentEventCount, call, argument);
		}

		internal void RegisterDoublePersistentListener(int index, UnityAction<double> call, double argument)
		{
			if (call == null)
			{
				Debug.LogWarning("Registering a Listener requires an action");
			}
			else
			{
				if (!ValidateRegistration(call.Method, call.Target, PersistentListenerMode2.Double))
					return;
				m_PersistentCalls.RegisterDoublePersistentListener(index, call.Target as Object, argument, call.Method.Name);
				DirtyPersistentCalls();
			}
		}

		internal void AddBoolPersistentListener(UnityAction<bool> call, bool argument)
		{
			int persistentEventCount = GetPersistentEventCount();
			AddPersistentListener();
			RegisterBoolPersistentListener(persistentEventCount, call, argument);
		}

		internal void RegisterBoolPersistentListener(int index, UnityAction<bool> call, bool argument)
		{
			if (call == null)
			{
				Debug.LogWarning("Registering a Listener requires an action");
			}
			else
			{
				if (!ValidateRegistration(call.Method, call.Target, PersistentListenerMode2.Bool))
					return;
				m_PersistentCalls.RegisterBoolPersistentListener(index, call.Target as Object, argument, call.Method.Name);
				DirtyPersistentCalls();
			}
		}

		internal void AddEnumPersistentListener(UnityAction<Enum> call, Enum argument)
		{
			int persistentEventCount = GetPersistentEventCount();
			AddPersistentListener();
			RegisterEnumPersistentListener(persistentEventCount, call, argument);
		}

		internal void RegisterEnumPersistentListener(int index, UnityAction<Enum> call, Enum argument)
		{
			if (call == null)
			{
				Debug.LogWarning("Registering a Listener requires an action");
			}
			else
			{
				if (!ValidateRegistration(call.Method, call.Target, PersistentListenerMode2.Enum))
					return;
				m_PersistentCalls.RegisterEnumPersistentListener(index, call.Target as Object, argument, call.Method.Name);
				DirtyPersistentCalls();
			}
		}

		internal void AddStringPersistentListener(UnityAction<string> call, string argument)
		{
			int persistentEventCount = GetPersistentEventCount();
			AddPersistentListener();
			RegisterStringPersistentListener(persistentEventCount, call, argument);
		}

		internal void RegisterStringPersistentListener(int index, UnityAction<string> call, string argument)
		{
			if (call == null)
			{
				Debug.LogWarning("Registering a Listener requires an action");
			}
			else
			{
				if (!ValidateRegistration(call.Method, call.Target, PersistentListenerMode2.String))
					return;
				m_PersistentCalls.RegisterStringPersistentListener(index, call.Target as Object, argument, call.Method.Name);
				DirtyPersistentCalls();
			}
		}

		internal void AddObjectPersistentListener<T>(UnityAction<T> call, T argument) where T : Object
		{
			int persistentEventCount = GetPersistentEventCount();
			AddPersistentListener();
			RegisterObjectPersistentListener<T>(persistentEventCount, call, argument);
		}

		internal void RegisterObjectPersistentListener<T>(int index, UnityAction<T> call, T argument) where T : Object
		{
			if (call == null)
				throw new ArgumentNullException("call", "Registering a Listener requires a non null call");
			if (!ValidateRegistration(call.Method, call.Target, PersistentListenerMode2.Object, !(argument == null) ? argument.GetType() : typeof(Object)))
				return;
			m_PersistentCalls.RegisterObjectPersistentListener(index, call.Target as Object, argument, call.Method.Name);
			DirtyPersistentCalls();
		}

		internal void AddVector2PersistentListener(UnityAction<Vector2> call, Vector2 argument)
		{
			int persistentEventCount = GetPersistentEventCount();
			AddPersistentListener();
			RegisterVector2PersistentListener(persistentEventCount, call, argument);
		}

		internal void RegisterVector2PersistentListener(int index, UnityAction<Vector2> call, Vector2 argument)
		{
			if (call == null)
			{
				Debug.LogWarning("Registering a Listener requires an action");
			}
			else
			{
				if (!ValidateRegistration(call.Method, call.Target, PersistentListenerMode2.Vector2))
					return;
				m_PersistentCalls.RegisterVector2PersistentListener(index, call.Target as Object, argument, call.Method.Name);
				DirtyPersistentCalls();
			}
		}

		internal void AddVector3PersistentListener(UnityAction<Vector3> call, Vector3 argument)
		{
			int persistentEventCount = GetPersistentEventCount();
			AddPersistentListener();
			RegisterVector3PersistentListener(persistentEventCount, call, argument);
		}

		internal void RegisterVector3PersistentListener(int index, UnityAction<Vector3> call, Vector3 argument)
		{
			if (call == null)
			{
				Debug.LogWarning("Registering a Listener requires an action");
			}
			else
			{
				if (!ValidateRegistration(call.Method, call.Target, PersistentListenerMode2.Vector3))
					return;
				m_PersistentCalls.RegisterVector3PersistentListener(index, call.Target as Object, argument, call.Method.Name);
				DirtyPersistentCalls();
			}
		}

		internal void AddVector2IntPersistentListener(UnityAction<Vector2Int> call, Vector2Int argument)
		{
			int persistentEventCount = GetPersistentEventCount();
			AddPersistentListener();
			RegisterVector2IntPersistentListener(persistentEventCount, call, argument);
		}

		internal void RegisterVector2IntPersistentListener(int index, UnityAction<Vector2Int> call, Vector2Int argument)
		{
			if (call == null)
			{
				Debug.LogWarning("Registering a Listener requires an action");
			}
			else
			{
				if (!ValidateRegistration(call.Method, call.Target, PersistentListenerMode2.Vector2Int))
					return;
				m_PersistentCalls.RegisterVector2PersistentListener(index, call.Target as Object, argument, call.Method.Name);
				DirtyPersistentCalls();
			}
		}

		internal void AddVector3IntPersistentListener(UnityAction<Vector3Int> call, Vector3Int argument)
		{
			int persistentEventCount = GetPersistentEventCount();
			AddPersistentListener();
			RegisterVector3IntPersistentListener(persistentEventCount, call, argument);
		}

		internal void RegisterVector3IntPersistentListener(int index, UnityAction<Vector3Int> call, Vector3Int argument)
		{
			if (call == null)
			{
				Debug.LogWarning("Registering a Listener requires an action");
			}
			else
			{
				if (!ValidateRegistration(call.Method, call.Target, PersistentListenerMode2.Vector3Int))
					return;
				m_PersistentCalls.RegisterVector3IntPersistentListener(index, call.Target as Object, argument, call.Method.Name);
				DirtyPersistentCalls();
			}
		}

		internal void AddVector4PersistentListener(UnityAction<Vector4> call, Vector4 argument)
		{
			int persistentEventCount = GetPersistentEventCount();
			AddPersistentListener();
			RegisterVector4PersistentListener(persistentEventCount, call, argument);
		}

		internal void RegisterVector4PersistentListener(int index, UnityAction<Vector4> call, Vector4 argument)
		{
			if (call == null)
			{
				Debug.LogWarning("Registering a Listener requires an action");
			}
			else
			{
				if (!ValidateRegistration(call.Method, call.Target, PersistentListenerMode2.Vector4))
					return;
				m_PersistentCalls.RegisterVector4PersistentListener(index, call.Target as Object, argument, call.Method.Name);
				DirtyPersistentCalls();
			}
		}

		internal void AddLayerMaskPersistentListener(UnityAction<LayerMask> call, LayerMask argument)
		{
			int persistentEventCount = GetPersistentEventCount();
			AddPersistentListener();
			RegisterLayerMaskPersistentListener(persistentEventCount, call, argument);
		}

		internal void RegisterLayerMaskPersistentListener(int index, UnityAction<LayerMask> call, LayerMask argument)
		{
			if (call == null)
			{
				Debug.LogWarning("Registering a Listener requires an action");
			}
			else
			{
				if (!ValidateRegistration(call.Method, call.Target, PersistentListenerMode2.LayerMask))
					return;
				m_PersistentCalls.RegisterLayerMaskPersistentListener(index, call.Target as Object, argument, call.Method.Name);
				DirtyPersistentCalls();
			}
		}

		internal void AddColorPersistentListener(UnityAction<Color> call, Color argument)
		{
			int persistentEventCount = GetPersistentEventCount();
			AddPersistentListener();
			RegisterColorPersistentListener(persistentEventCount, call, argument);
		}

		internal void RegisterColorPersistentListener(int index, UnityAction<Color> call, Color argument)
		{
			if (call == null)
			{
				Debug.LogWarning("Registering a Listener requires an action");
			}
			else
			{
				if (!ValidateRegistration(call.Method, call.Target, PersistentListenerMode2.Color))
					return;
				m_PersistentCalls.RegisterColorPersistentListener(index, call.Target as Object, argument, call.Method.Name);
				DirtyPersistentCalls();
			}
		}

		internal void AddQuaternionPersistentListener(UnityAction<Quaternion> call, Quaternion argument)
		{
			int persistentEventCount = GetPersistentEventCount();
			AddPersistentListener();
			RegisterQuaternionPersistentListener(persistentEventCount, call, argument);
		}

		internal void RegisterQuaternionPersistentListener(int index, UnityAction<Quaternion> call, Quaternion argument)
		{
			if (call == null)
			{
				Debug.LogWarning("Registering a Listener requires an action");
			}
			else
			{
				if (!ValidateRegistration(call.Method, call.Target, PersistentListenerMode2.Quaternion))
					return;
				m_PersistentCalls.RegisterQuaternionPersistentListener(index, call.Target as Object, argument, call.Method.Name);
				DirtyPersistentCalls();
			}
		}

		private InvokableCallList2 Calls
		{
			get
			{
				if (m_Calls == null)
					m_Calls = new InvokableCallList2();

				return m_Calls;
			}
		}
	}
}