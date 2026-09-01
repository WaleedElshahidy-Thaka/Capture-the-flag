using System;
using System.Collections.Generic;
using System.Reflection;

namespace UnityEngine.Events
{
	class InvokableCallList2
	{
		private readonly List<BaseInvokableCall2> m_PersistentCalls = new List<BaseInvokableCall2>();
		private readonly List<BaseInvokableCall2> m_RuntimeCalls = new List<BaseInvokableCall2>();

		private readonly List<BaseInvokableCall2> m_ExecutingCalls = new List<BaseInvokableCall2>();

		private bool m_NeedsUpdate = true;

		public int Count
		{
			get { return m_PersistentCalls.Count + m_RuntimeCalls.Count; }
		}

		public void AddPersistentInvokableCall(BaseInvokableCall2 call)
		{
			m_PersistentCalls.Add(call);
			m_NeedsUpdate = true;
		}

		public void AddListener(BaseInvokableCall2 call)
		{
			m_RuntimeCalls.Add(call);
			m_NeedsUpdate = true;
		}

		public void RemoveListener(object targetObj, MethodInfo method)
		{
			List<BaseInvokableCall2> baseInvokableCallList = new List<BaseInvokableCall2>();
			for (int index = 0; index < m_RuntimeCalls.Count; ++index)
			{
				if (m_RuntimeCalls[index].Find(targetObj, method))
					baseInvokableCallList.Add(m_RuntimeCalls[index]);
			}
			m_RuntimeCalls.RemoveAll(new Predicate<BaseInvokableCall2>(baseInvokableCallList.Contains));
			m_NeedsUpdate = true;
		}

		public void Clear()
		{
			m_RuntimeCalls.Clear();
			m_NeedsUpdate = true;
		}

		public void ClearPersistent()
		{
			m_PersistentCalls.Clear();
			m_NeedsUpdate = true;
		}

		public List<BaseInvokableCall2> PrepareInvoke()
		{
			if (m_NeedsUpdate)
			{
				m_ExecutingCalls.Clear();
				m_ExecutingCalls.AddRange(m_PersistentCalls);
				m_ExecutingCalls.AddRange(m_RuntimeCalls);
				m_NeedsUpdate = false;
			}

			return m_ExecutingCalls;
		}

		public void Invoke(object[] parameters)
		{
			if (m_NeedsUpdate)
			{
				m_ExecutingCalls.Clear();
				m_ExecutingCalls.AddRange(m_PersistentCalls);
				m_ExecutingCalls.AddRange(m_RuntimeCalls);
				m_NeedsUpdate = false;
			}
			for (int index = 0; index < m_ExecutingCalls.Count; ++index)
				m_ExecutingCalls[index].Invoke(parameters);
		}
	}
}