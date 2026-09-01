using System.Reflection;

namespace UnityEngine.Events
{
	public class UpdatableInvokableCall<T0> : InvokableCall2<T0>
	{
		private readonly object[] m_Args = new object[1];
		private readonly PersistentCall2[] m_PersistentCalls = new PersistentCall2[0];

		private bool m_IsEventDefined = false;

		public UpdatableInvokableCall(string typeName, MethodInfo theFunction, bool isEventDefined, PersistentCall2[] persistentCalls, T0 arg) : base(typeName, theFunction)
		{
			m_PersistentCalls = persistentCalls;
			m_Args[0] = arg;
			m_IsEventDefined = isEventDefined;
		}

		public UpdatableInvokableCall(Object target, MethodInfo theFunction, bool isEventDefined, PersistentCall2[] persistentCalls, T0 arg) : base(target, theFunction)
		{
			m_PersistentCalls = persistentCalls;
			m_Args[0] = arg;
			m_IsEventDefined = isEventDefined;
		}

		public UpdatableInvokableCall(Object target, MethodInfo theFunction, bool isEventDefined) : base(target, theFunction)
		{
			m_IsEventDefined = isEventDefined;
		}

		public override void Invoke(object[] args)
		{
			if (m_IsEventDefined)
				for (int i = 0; i < args.Length; i++)
					m_Args[i] = args[i];

			object[] newArgs = HandlePersistentCalls(m_PersistentCalls, m_Args);

			base.Invoke(newArgs);
		}

		public override void Invoke(T0 arg0)
		{
			if (m_IsEventDefined)
				m_Args[0] = arg0;

			base.Invoke((T0)m_Args[0]);
		}
	}

	public class UpdatableInvokableCall<T0, T1> : InvokableCall2<T0, T1>
	{
		private readonly object[] m_Args = new object[2];
		private readonly PersistentCall2[] m_PersistentCalls = new PersistentCall2[0];

		private bool m_IsEventDefined = false;

		public UpdatableInvokableCall(string typeName, MethodInfo theFunction, bool isEventDefined, PersistentCall2[] persistentCalls, T0 arg0, T1 arg1) : base(typeName, theFunction)
		{
			m_PersistentCalls = persistentCalls;
			m_Args[0] = arg0;
			m_Args[1] = arg1;
			m_IsEventDefined = isEventDefined;
		}

		public UpdatableInvokableCall(Object target, MethodInfo theFunction, bool isEventDefined, PersistentCall2[] persistentCalls, T0 arg0, T1 arg1) : base(target, theFunction)
		{
			m_PersistentCalls = persistentCalls;
			m_Args[0] = arg0;
			m_Args[1] = arg1;
			m_IsEventDefined = isEventDefined;
		}

		public UpdatableInvokableCall(Object target, MethodInfo theFunction, bool isEventDefined) : base(target, theFunction)
		{
			m_IsEventDefined = isEventDefined;
		}

		public override void Invoke(object[] args)
		{
			if (m_IsEventDefined)
				for (int i = 0; i < args.Length; i++)
					m_Args[i] = args[i];

			object[] newArgs = HandlePersistentCalls(m_PersistentCalls, m_Args);

			base.Invoke(newArgs);
		}

		public override void Invoke(T0 arg0, T1 arg1)
		{
			if (m_IsEventDefined)
			{
				m_Args[0] = arg0;
				m_Args[1] = arg1;
			}

			base.Invoke((T0)m_Args[0], (T1)m_Args[1]);
		}
	}

	public class UpdatableInvokableCall<T0, T1, T2> : InvokableCall2<T0, T1, T2>
	{
		private readonly object[] m_Args = new object[3];
		private readonly PersistentCall2[] m_PersistentCalls = new PersistentCall2[0];

		private bool m_IsEventDefined = false;

		public UpdatableInvokableCall(string typeName, MethodInfo theFunction, bool isEventDefined, PersistentCall2[] persistentCalls, T0 arg0, T1 arg1, T2 arg2) : base(typeName, theFunction)
		{
			m_PersistentCalls = persistentCalls;
			m_Args[0] = arg0;
			m_Args[1] = arg1;
			m_Args[2] = arg2;
			m_IsEventDefined = isEventDefined;
		}

		public UpdatableInvokableCall(Object target, MethodInfo theFunction, bool isEventDefined, PersistentCall2[] persistentCalls, T0 arg0, T1 arg1, T2 arg2) : base(target, theFunction)
		{
			m_PersistentCalls = persistentCalls;
			m_Args[0] = arg0;
			m_Args[1] = arg1;
			m_Args[2] = arg2;
			m_IsEventDefined = isEventDefined;
		}

		public UpdatableInvokableCall(Object target, MethodInfo theFunction, bool isEventDefined) : base(target, theFunction)
		{
			m_IsEventDefined = isEventDefined;
		}

		public override void Invoke(object[] args)
		{
			if (m_IsEventDefined)
				for (int i = 0; i < args.Length; i++)
					m_Args[i] = args[i];

			object[] newArgs = HandlePersistentCalls(m_PersistentCalls, m_Args);

			base.Invoke(newArgs);
		}

		public override void Invoke(T0 arg0, T1 arg1, T2 arg2)
		{
			if (m_IsEventDefined)
			{
				m_Args[0] = arg0;
				m_Args[1] = arg1;
				m_Args[2] = arg2;
			}

			base.Invoke((T0)m_Args[0], (T1)m_Args[1], (T2)m_Args[2]);
		}
	}

	public class UpdatableInvokableCall<T0, T1, T2, T3> : InvokableCall2<T0, T1, T2, T3>
	{
		private readonly object[] m_Args = new object[4];
		private readonly PersistentCall2[] m_PersistentCalls = new PersistentCall2[0];

		private bool m_IsEventDefined = false;

		public UpdatableInvokableCall(string typeName, MethodInfo theFunction, bool isEventDefined, PersistentCall2[] persistentCalls, T0 arg0, T1 arg1, T2 arg2, T3 arg3) : base(typeName, theFunction)
		{
			m_PersistentCalls = persistentCalls;
			m_Args[0] = arg0;
			m_Args[1] = arg1;
			m_Args[2] = arg2;
			m_Args[3] = arg3;
			m_IsEventDefined = isEventDefined;
		}

		public UpdatableInvokableCall(Object target, MethodInfo theFunction, bool isEventDefined, PersistentCall2[] persistentCalls, T0 arg0, T1 arg1, T2 arg2, T3 arg3) : base(target, theFunction)
		{
			m_PersistentCalls = persistentCalls;
			m_Args[0] = arg0;
			m_Args[1] = arg1;
			m_Args[2] = arg2;
			m_Args[3] = arg3;
			m_IsEventDefined = isEventDefined;
		}

		public UpdatableInvokableCall(Object target, MethodInfo theFunction, bool isEventDefined) : base(target, theFunction)
		{
			m_IsEventDefined = isEventDefined;
		}

		public override void Invoke(object[] args)
		{
			if (m_IsEventDefined)
				for (int i = 0; i < args.Length; i++)
					m_Args[i] = args[i];

			object[] newArgs = HandlePersistentCalls(m_PersistentCalls, m_Args);

			base.Invoke(newArgs);
		}

		public override void Invoke(T0 arg0, T1 arg1, T2 arg2, T3 arg3)
		{
			if (m_IsEventDefined)
			{
				m_Args[0] = arg0;
				m_Args[1] = arg1;
				m_Args[2] = arg2;
				m_Args[3] = arg3;
			}

			base.Invoke((T0)m_Args[0], (T1)m_Args[1], (T2)m_Args[2], (T3)m_Args[3]);
		}
	}

	public class UpdatableInvokableCall<T0, T1, T2, T3, T4> : InvokableCall2<T0, T1, T2, T3, T4>
	{
		private readonly object[] m_Args = new object[5];
		private readonly PersistentCall2[] m_PersistentCalls = new PersistentCall2[0];

		private bool m_IsEventDefined = false;

		public UpdatableInvokableCall(string typeName, MethodInfo theFunction, bool isEventDefined, PersistentCall2[] persistentCalls, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4) : base(typeName, theFunction)
		{
			m_PersistentCalls = persistentCalls;
			m_Args[0] = arg0;
			m_Args[1] = arg1;
			m_Args[2] = arg2;
			m_Args[3] = arg3;
			m_Args[4] = arg4;
			m_IsEventDefined = isEventDefined;
		}

		public UpdatableInvokableCall(Object target, MethodInfo theFunction, bool isEventDefined, PersistentCall2[] persistentCalls, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4) : base(target, theFunction)
		{
			m_PersistentCalls = persistentCalls;
			m_Args[0] = arg0;
			m_Args[1] = arg1;
			m_Args[2] = arg2;
			m_Args[3] = arg3;
			m_Args[4] = arg4;
			m_IsEventDefined = isEventDefined;
		}

		public UpdatableInvokableCall(Object target, MethodInfo theFunction, bool isEventDefined) : base(target, theFunction)
		{
			m_IsEventDefined = isEventDefined;
		}

		public override void Invoke(object[] args)
		{
			if (m_IsEventDefined)
				for (int i = 0; i < args.Length; i++)
					m_Args[i] = args[i];

			object[] newArgs = HandlePersistentCalls(m_PersistentCalls, m_Args);

			base.Invoke(newArgs);
		}

		public override void Invoke(T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
		{
			if (m_IsEventDefined)
			{
				m_Args[0] = arg0;
				m_Args[1] = arg1;
				m_Args[2] = arg2;
				m_Args[3] = arg3;
				m_Args[4] = arg4;
			}

			base.Invoke((T0)m_Args[0], (T1)m_Args[1], (T2)m_Args[2], (T3)m_Args[3], (T4)m_Args[4]);
		}
	}

	public class UpdatableInvokableCall<T0, T1, T2, T3, T4, T5> : InvokableCall2<T0, T1, T2, T3, T4, T5>
	{
		private readonly object[] m_Args = new object[6];
		private readonly PersistentCall2[] m_PersistentCalls = new PersistentCall2[0];

		private bool m_IsEventDefined = false;

		public UpdatableInvokableCall(string typeName, MethodInfo theFunction, bool isEventDefined, PersistentCall2[] persistentCalls, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5) : base(typeName, theFunction)
		{
			m_PersistentCalls = persistentCalls;
			m_Args[0] = arg0;
			m_Args[1] = arg1;
			m_Args[2] = arg2;
			m_Args[3] = arg3;
			m_Args[4] = arg4;
			m_Args[5] = arg5;
			m_IsEventDefined = isEventDefined;
		}

		public UpdatableInvokableCall(Object target, MethodInfo theFunction, bool isEventDefined, PersistentCall2[] persistentCalls, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5) : base(target, theFunction)
		{
			m_PersistentCalls = persistentCalls;
			m_Args[0] = arg0;
			m_Args[1] = arg1;
			m_Args[2] = arg2;
			m_Args[3] = arg3;
			m_Args[4] = arg4;
			m_Args[5] = arg5;
			m_IsEventDefined = isEventDefined;
		}

		public UpdatableInvokableCall(Object target, MethodInfo theFunction, bool isEventDefined) : base(target, theFunction)
		{
			m_IsEventDefined = isEventDefined;
		}

		public override void Invoke(object[] args)
		{
			if (m_IsEventDefined)
				for (int i = 0; i < args.Length; i++)
					m_Args[i] = args[i];

			object[] newArgs = HandlePersistentCalls(m_PersistentCalls, m_Args);

			base.Invoke(newArgs);
		}

		public override void Invoke(T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
		{
			if (m_IsEventDefined)
			{
				m_Args[0] = arg0;
				m_Args[1] = arg1;
				m_Args[2] = arg2;
				m_Args[3] = arg3;
				m_Args[4] = arg4;
				m_Args[5] = arg5;
			}

			base.Invoke((T0)m_Args[0], (T1)m_Args[1], (T2)m_Args[2], (T3)m_Args[3], (T4)m_Args[4], (T5)m_Args[5]);
		}
	}

	public class UpdatableInvokableCall<T0, T1, T2, T3, T4, T5, T6> : InvokableCall2<T0, T1, T2, T3, T4, T5, T6>
	{
		private readonly object[] m_Args = new object[7];
		private readonly PersistentCall2[] m_PersistentCalls = new PersistentCall2[0];

		private bool m_IsEventDefined = false;

		public UpdatableInvokableCall(string typeName, MethodInfo theFunction, bool isEventDefined, PersistentCall2[] persistentCalls, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6) : base(typeName, theFunction)
		{
			m_PersistentCalls = persistentCalls;
			m_Args[0] = arg0;
			m_Args[1] = arg1;
			m_Args[2] = arg2;
			m_Args[3] = arg3;
			m_Args[4] = arg4;
			m_Args[5] = arg5;
			m_Args[6] = arg6;
			m_IsEventDefined = isEventDefined;
		}

		public UpdatableInvokableCall(Object target, MethodInfo theFunction, bool isEventDefined, PersistentCall2[] persistentCalls, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6) : base(target, theFunction)
		{
			m_PersistentCalls = persistentCalls;
			m_Args[0] = arg0;
			m_Args[1] = arg1;
			m_Args[2] = arg2;
			m_Args[3] = arg3;
			m_Args[4] = arg4;
			m_Args[5] = arg5;
			m_Args[6] = arg6;
			m_IsEventDefined = isEventDefined;
		}

		public UpdatableInvokableCall(Object target, MethodInfo theFunction, bool isEventDefined) : base(target, theFunction)
		{
			m_IsEventDefined = isEventDefined;
		}

		public override void Invoke(object[] args)
		{
			if (m_IsEventDefined)
				for (int i = 0; i < args.Length; i++)
					m_Args[i] = args[i];

			object[] newArgs = HandlePersistentCalls(m_PersistentCalls, m_Args);

			base.Invoke(newArgs);
		}

		public override void Invoke(T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
		{
			if (m_IsEventDefined)
			{
				m_Args[0] = arg0;
				m_Args[1] = arg1;
				m_Args[2] = arg2;
				m_Args[3] = arg3;
				m_Args[4] = arg4;
				m_Args[5] = arg5;
				m_Args[6] = arg6;
			}

			base.Invoke((T0)m_Args[0], (T1)m_Args[1], (T2)m_Args[2], (T3)m_Args[3], (T4)m_Args[4], (T5)m_Args[5], (T6)m_Args[6]);
		}
	}

	public class UpdatableInvokableCall<T0, T1, T2, T3, T4, T5, T6, T7> : InvokableCall2<T0, T1, T2, T3, T4, T5, T6, T7>
	{
		private readonly object[] m_Args = new object[8];
		private readonly PersistentCall2[] m_PersistentCalls = new PersistentCall2[0];

		private bool m_IsEventDefined = false;

		public UpdatableInvokableCall(string typeName, MethodInfo theFunction, bool isEventDefined, PersistentCall2[] persistentCalls, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7) : base(typeName, theFunction)
		{
			m_PersistentCalls = persistentCalls;
			m_Args[0] = arg0;
			m_Args[1] = arg1;
			m_Args[2] = arg2;
			m_Args[3] = arg3;
			m_Args[4] = arg4;
			m_Args[5] = arg5;
			m_Args[6] = arg6;
			m_Args[7] = arg7;
			m_IsEventDefined = isEventDefined;
		}

		public UpdatableInvokableCall(Object target, MethodInfo theFunction, bool isEventDefined, PersistentCall2[] persistentCalls, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7) : base(target, theFunction)
		{
			m_PersistentCalls = persistentCalls;
			m_Args[0] = arg0;
			m_Args[1] = arg1;
			m_Args[2] = arg2;
			m_Args[3] = arg3;
			m_Args[4] = arg4;
			m_Args[5] = arg5;
			m_Args[6] = arg6;
			m_Args[7] = arg7;
			m_IsEventDefined = isEventDefined;
		}

		public UpdatableInvokableCall(Object target, MethodInfo theFunction, bool isEventDefined) : base(target, theFunction)
		{
			m_IsEventDefined = isEventDefined;
		}

		public override void Invoke(object[] args)
		{
			if (m_IsEventDefined)
				for (int i = 0; i < args.Length; i++)
					m_Args[i] = args[i];

			object[] newArgs = HandlePersistentCalls(m_PersistentCalls, m_Args);

			base.Invoke(newArgs);
		}

		public override void Invoke(T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7)
		{
			if (m_IsEventDefined)
			{
				m_Args[0] = arg0;
				m_Args[1] = arg1;
				m_Args[2] = arg2;
				m_Args[3] = arg3;
				m_Args[4] = arg4;
				m_Args[5] = arg5;
				m_Args[6] = arg6;
				m_Args[7] = arg7;
			}

			base.Invoke((T0)m_Args[0], (T1)m_Args[1], (T2)m_Args[2], (T3)m_Args[3], (T4)m_Args[4], (T5)m_Args[5], (T6)m_Args[6], (T7)m_Args[7]);
		}
	}

	public class UpdatableInvokableCall<T0, T1, T2, T3, T4, T5, T6, T7, T8> : InvokableCall2<T0, T1, T2, T3, T4, T5, T6, T7, T8>
	{
		private readonly object[] m_Args = new object[9];
		private readonly PersistentCall2[] m_PersistentCalls = new PersistentCall2[0];

		private bool m_IsEventDefined = false;

		public UpdatableInvokableCall(string typeName, MethodInfo theFunction, bool isEventDefined, PersistentCall2[] persistentCalls, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8) : base(typeName, theFunction)
		{
			m_PersistentCalls = persistentCalls;
			m_Args[0] = arg0;
			m_Args[1] = arg1;
			m_Args[2] = arg2;
			m_Args[3] = arg3;
			m_Args[4] = arg4;
			m_Args[5] = arg5;
			m_Args[6] = arg6;
			m_Args[7] = arg7;
			m_Args[8] = arg8;
			m_IsEventDefined = isEventDefined;
		}

		public UpdatableInvokableCall(Object target, MethodInfo theFunction, bool isEventDefined, PersistentCall2[] persistentCalls, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8) : base(target, theFunction)
		{
			m_PersistentCalls = persistentCalls;
			m_Args[0] = arg0;
			m_Args[1] = arg1;
			m_Args[2] = arg2;
			m_Args[3] = arg3;
			m_Args[4] = arg4;
			m_Args[5] = arg5;
			m_Args[6] = arg6;
			m_Args[7] = arg7;
			m_Args[8] = arg8;
			m_IsEventDefined = isEventDefined;
		}

		public UpdatableInvokableCall(Object target, MethodInfo theFunction, bool isEventDefined) : base(target, theFunction)
		{
			m_IsEventDefined = isEventDefined;
		}

		public override void Invoke(object[] args)
		{
			if (m_IsEventDefined)
				for (int i = 0; i < args.Length; i++)
					m_Args[i] = args[i];

			object[] newArgs = HandlePersistentCalls(m_PersistentCalls, m_Args);

			base.Invoke(newArgs);
		}

		public override void Invoke(T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8)
		{
			if (m_IsEventDefined)
			{
				m_Args[0] = arg0;
				m_Args[1] = arg1;
				m_Args[2] = arg2;
				m_Args[3] = arg3;
				m_Args[4] = arg4;
				m_Args[5] = arg5;
				m_Args[6] = arg6;
				m_Args[7] = arg7;
				m_Args[8] = arg8;
			}

			base.Invoke((T0)m_Args[0], (T1)m_Args[1], (T2)m_Args[2], (T3)m_Args[3], (T4)m_Args[4], (T5)m_Args[5], (T6)m_Args[6], (T7)m_Args[7], (T8)m_Args[8]);
		}
	}

	public class UpdatableInvokableCall<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9> : InvokableCall2<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9>
	{
		private readonly object[] m_Args = new object[10];
		private readonly PersistentCall2[] m_PersistentCalls = new PersistentCall2[0];

		private bool m_IsEventDefined = false;

		public UpdatableInvokableCall(string typeName, MethodInfo theFunction, bool isEventDefined, PersistentCall2[] persistentCalls, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9) : base(typeName, theFunction)
		{
			m_PersistentCalls = persistentCalls;
			m_Args[0] = arg0;
			m_Args[1] = arg1;
			m_Args[2] = arg2;
			m_Args[3] = arg3;
			m_Args[4] = arg4;
			m_Args[5] = arg5;
			m_Args[6] = arg6;
			m_Args[7] = arg7;
			m_Args[8] = arg8;
			m_Args[9] = arg9;
			m_IsEventDefined = isEventDefined;
		}

		public UpdatableInvokableCall(Object target, MethodInfo theFunction, bool isEventDefined, PersistentCall2[] persistentCalls, T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9) : base(target, theFunction)
		{
			m_PersistentCalls = persistentCalls;
			m_Args[0] = arg0;
			m_Args[1] = arg1;
			m_Args[2] = arg2;
			m_Args[3] = arg3;
			m_Args[4] = arg4;
			m_Args[5] = arg5;
			m_Args[6] = arg6;
			m_Args[7] = arg7;
			m_Args[8] = arg8;
			m_Args[9] = arg9;
			m_IsEventDefined = isEventDefined;
		}

		public UpdatableInvokableCall(Object target, MethodInfo theFunction, bool isEventDefined) : base(target, theFunction)
		{
			m_IsEventDefined = isEventDefined;
		}

		public override void Invoke(object[] args)
		{
			if (m_IsEventDefined)
				for (int i = 0; i < args.Length; i++)
					m_Args[i] = args[i];

			object[] newArgs = HandlePersistentCalls(m_PersistentCalls, m_Args);

			base.Invoke(newArgs);
		}

		public override void Invoke(T0 arg0, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9)
		{
			if (m_IsEventDefined)
			{
				m_Args[0] = arg0;
				m_Args[1] = arg1;
				m_Args[2] = arg2;
				m_Args[3] = arg3;
				m_Args[4] = arg4;
				m_Args[5] = arg5;
				m_Args[6] = arg6;
				m_Args[7] = arg7;
				m_Args[8] = arg8;
				m_Args[9] = arg9;
			}

			base.Invoke((T0)m_Args[0], (T1)m_Args[1], (T2)m_Args[2], (T3)m_Args[3], (T4)m_Args[4], (T5)m_Args[5], (T6)m_Args[6], (T7)m_Args[7], (T8)m_Args[8], (T9)m_Args[9]);
		}
	}
}