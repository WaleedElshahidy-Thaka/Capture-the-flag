using System;
using System.Collections.Generic;
using UnityEngine.Serialization;

namespace UnityEngine.Events
{
	[Serializable]
	class PersistentCallGroup2
	{
		[SerializeField]
		[FormerlySerializedAs("m_Listeners")]
		private List<PersistentCall2> m_Calls;

		public int Count
		{
			get { return m_Calls.Count; }
		}

		public PersistentCallGroup2()
		{
			m_Calls = new List<PersistentCall2>();
		}

		public PersistentCall2 GetListener(int index)
		{
			return m_Calls[index];
		}

		public IEnumerable<PersistentCall2> GetListeners()
		{
			return m_Calls;
		}

		public void AddListener()
		{
			m_Calls.Add(new PersistentCall2());
		}

		public void AddListener(PersistentCall2 call)
		{
			m_Calls.Add(call);
		}

		public void RemoveListener(int index)
		{
			m_Calls.RemoveAt(index);
		}

		public void Clear()
		{
			m_Calls.Clear();
		}

		public void RegisterEventPersistentListener(int index, Object targetObj, string methodName)
		{
			PersistentCall2 listener = GetListener(index);
			listener.RegisterPersistentListener(targetObj, methodName);
			listener.modes.Add(PersistentListenerMode2.EventDefined);
		}

		public void RegisterVoidPersistentListener(int index, Object targetObj, string methodName)
		{
			PersistentCall2 listener = GetListener(index);
			listener.RegisterPersistentListener(targetObj, methodName);
			listener.modes.Add(PersistentListenerMode2.Void);
		}

		public void RegisterObjectPersistentListener(int index, Object targetObj, Object argument, string methodName)
		{
			PersistentCall2 listener = GetListener(index);
			listener.RegisterPersistentListener(targetObj, methodName);
			listener.modes.Add(PersistentListenerMode2.Object);
			listener.arguments.Add(new ArgumentCache2()
			{
				unityObjectArgument = argument,
				unityObjectArgumentAssemblyTypeName = argument == null ? string.Empty : argument.GetType().AssemblyQualifiedName
			});
		}

		public void RegisterCharPersistentListener(int index, Object targetObj, char argument, string methodName)
		{
			PersistentCall2 listener = GetListener(index);
			listener.RegisterPersistentListener(targetObj, methodName);
			listener.modes.Add(PersistentListenerMode2.Char);
			listener.arguments.Add(new ArgumentCache2() { charArgument = argument });
		}

		public void RegisterBytePersistentListener(int index, Object targetObj, byte argument, string methodName)
		{
			PersistentCall2 listener = GetListener(index);
			listener.RegisterPersistentListener(targetObj, methodName);
			listener.modes.Add(PersistentListenerMode2.Byte);
			listener.arguments.Add(new ArgumentCache2() { byteArgument = argument });
		}

		public void RegisterSBytePersistentListener(int index, Object targetObj, sbyte argument, string methodName)
		{
			PersistentCall2 listener = GetListener(index);
			listener.RegisterPersistentListener(targetObj, methodName);
			listener.modes.Add(PersistentListenerMode2.SByte);
			listener.arguments.Add(new ArgumentCache2() { sbyteArgument = argument });
		}

		public void RegisterShortPersistentListener(int index, Object targetObj, short argument, string methodName)
		{
			PersistentCall2 listener = GetListener(index);
			listener.RegisterPersistentListener(targetObj, methodName);
			listener.modes.Add(PersistentListenerMode2.Short);
			listener.arguments.Add(new ArgumentCache2() { shortArgument = argument });
		}

		public void RegisterUShortPersistentListener(int index, Object targetObj, ushort argument, string methodName)
		{
			PersistentCall2 listener = GetListener(index);
			listener.RegisterPersistentListener(targetObj, methodName);
			listener.modes.Add(PersistentListenerMode2.UShort);
			listener.arguments.Add(new ArgumentCache2() { ushortArgument = argument });
		}

		public void RegisterIntPersistentListener(int index, Object targetObj, int argument, string methodName)
		{
			PersistentCall2 listener = GetListener(index);
			listener.RegisterPersistentListener(targetObj, methodName);
			listener.modes.Add(PersistentListenerMode2.Int);
			listener.arguments.Add(new ArgumentCache2() { intArgument = argument });
		}

		public void RegisterUIntPersistentListener(int index, Object targetObj, uint argument, string methodName)
		{
			PersistentCall2 listener = GetListener(index);
			listener.RegisterPersistentListener(targetObj, methodName);
			listener.modes.Add(PersistentListenerMode2.UInt);
			listener.arguments.Add(new ArgumentCache2() { uintArgument = argument });
		}

		public void RegisterLongPersistentListener(int index, Object targetObj, long argument, string methodName)
		{
			PersistentCall2 listener = GetListener(index);
			listener.RegisterPersistentListener(targetObj, methodName);
			listener.modes.Add(PersistentListenerMode2.Long);
			listener.arguments.Add(new ArgumentCache2() { longArgument = argument });
		}

		public void RegisterFloatPersistentListener(int index, Object targetObj, float argument, string methodName)
		{
			PersistentCall2 listener = GetListener(index);
			listener.RegisterPersistentListener(targetObj, methodName);
			listener.modes.Add(PersistentListenerMode2.Float);
			listener.arguments.Add(new ArgumentCache2() { floatArgument = argument });
		}

		public void RegisterDoublePersistentListener(int index, Object targetObj, double argument, string methodName)
		{
			PersistentCall2 listener = GetListener(index);
			listener.RegisterPersistentListener(targetObj, methodName);
			listener.modes.Add(PersistentListenerMode2.Double);
			listener.arguments.Add(new ArgumentCache2() { doubleArgument = argument });
		}

		public void RegisterStringPersistentListener(int index, Object targetObj, string argument, string methodName)
		{
			PersistentCall2 listener = GetListener(index);
			listener.RegisterPersistentListener(targetObj, methodName);
			listener.modes.Add(PersistentListenerMode2.String);
			listener.arguments.Add(new ArgumentCache2() { stringArgument = argument });
		}

		public void RegisterBoolPersistentListener(int index, Object targetObj, bool argument, string methodName)
		{
			PersistentCall2 listener = GetListener(index);
			listener.RegisterPersistentListener(targetObj, methodName);
			listener.modes.Add(PersistentListenerMode2.Bool);
			listener.arguments.Add(new ArgumentCache2() { boolArgument = argument });
		}

		public void RegisterEnumPersistentListener(int index, Object targetObj, Enum argument, string methodName)
		{
			PersistentCall2 listener = GetListener(index);
			listener.RegisterPersistentListener(targetObj, methodName);
			listener.modes.Add(PersistentListenerMode2.Enum);
			listener.arguments.Add(new ArgumentCache2() { enumArgument = argument });
		}

		public void RegisterVector2PersistentListener(int index, Object targetObj, Vector2 argument, string methodName)
		{
			PersistentCall2 listener = GetListener(index);
			listener.RegisterPersistentListener(targetObj, methodName);
			listener.modes.Add(PersistentListenerMode2.Vector2);
			listener.arguments.Add(new ArgumentCache2() { vector2Argument = argument });
		}

		public void RegisterVector2IntPersistentListener(int index, Object targetObj, Vector2Int argument, string methodName)
		{
			PersistentCall2 listener = GetListener(index);
			listener.RegisterPersistentListener(targetObj, methodName);
			listener.modes.Add(PersistentListenerMode2.Vector2Int);
			listener.arguments.Add(new ArgumentCache2() { vector2IntArgument = argument });
		}

		public void RegisterVector3PersistentListener(int index, Object targetObj, Vector3 argument, string methodName)
		{
			PersistentCall2 listener = GetListener(index);
			listener.RegisterPersistentListener(targetObj, methodName);
			listener.modes.Add(PersistentListenerMode2.Vector3);
			listener.arguments.Add(new ArgumentCache2() { vector3Argument = argument });
		}

		public void RegisterVector3IntPersistentListener(int index, Object targetObj, Vector3Int argument, string methodName)
		{
			PersistentCall2 listener = GetListener(index);
			listener.RegisterPersistentListener(targetObj, methodName);
			listener.modes.Add(PersistentListenerMode2.Vector3Int);
			listener.arguments.Add(new ArgumentCache2() { vector3IntArgument = argument });
		}

		public void RegisterVector4PersistentListener(int index, Object targetObj, Vector4 argument, string methodName)
		{
			PersistentCall2 listener = GetListener(index);
			listener.RegisterPersistentListener(targetObj, methodName);
			listener.modes.Add(PersistentListenerMode2.Vector4);
			listener.arguments.Add(new ArgumentCache2() { vector4Argument = argument });
		}

		public void RegisterLayerMaskPersistentListener(int index, Object targetObj, LayerMask argument, string methodName)
		{
			PersistentCall2 listener = GetListener(index);
			listener.RegisterPersistentListener(targetObj, methodName);
			listener.modes.Add(PersistentListenerMode2.LayerMask);
			listener.arguments.Add(new ArgumentCache2() { layerMaskArgument = argument });
		}

		public void RegisterColorPersistentListener(int index, Object targetObj, Color argument, string methodName)
		{
			PersistentCall2 listener = GetListener(index);
			listener.RegisterPersistentListener(targetObj, methodName);
			listener.modes.Add(PersistentListenerMode2.Color);
			listener.arguments.Add(new ArgumentCache2() { colorArgument = argument });
		}

		public void RegisterQuaternionPersistentListener(int index, Object targetObj, Quaternion argument, string methodName)
		{
			PersistentCall2 listener = GetListener(index);
			listener.RegisterPersistentListener(targetObj, methodName);
			listener.modes.Add(PersistentListenerMode2.Quaternion);
			listener.arguments.Add(new ArgumentCache2() { quaternionArgument = argument });
		}

		public void UnregisterPersistentListener(int index)
		{
			GetListener(index).UnregisterPersistentListener();
		}

		public void RemoveListeners(Object target, string methodName)
		{
			List<PersistentCall2> persistentCallList = new List<PersistentCall2>();
			for (int index = 0; index < m_Calls.Count; ++index)
			{
				if (m_Calls[index].target == target && m_Calls[index].methodName == methodName)
					persistentCallList.Add(m_Calls[index]);
			}
			m_Calls.RemoveAll(new Predicate<PersistentCall2>(persistentCallList.Contains));
		}

		public void Initialize(InvokableCallList2 invokableList, UnityEventBase2 unityEventBase, object[] parameters)
		{
			foreach (var persistentCall in m_Calls)
			{
				if (!persistentCall.IsValid())
					continue;

				var call = persistentCall.GetRuntimeCall(unityEventBase, parameters);
				if (call != null)
					invokableList.AddPersistentInvokableCall(call);
			}
		}
	}
}