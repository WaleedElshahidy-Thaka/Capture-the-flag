using System;
using System.Reflection;

namespace UnityEngine.Events
{
	public class InvokableCall2 : BaseInvokableCall2
	{
		private event UnityAction Delegate;

		public InvokableCall2(string typeName, MethodInfo theFunction) : base(typeName, theFunction)
		{
			Delegate += (UnityAction)System.Delegate.CreateDelegate(typeof(UnityAction), theFunction);
		}

		public InvokableCall2(object target, MethodInfo theFunction) : base(target, theFunction)
		{
			Delegate += (UnityAction)System.Delegate.CreateDelegate(typeof(UnityAction), theFunction.IsStatic ? null : target, theFunction);
		}

		public InvokableCall2(UnityAction action)
		{
			Delegate += action;
		}

		public override bool Find(object targetObj, MethodInfo method)
		{
			return Delegate.Target == targetObj && Delegate.Method.Equals(method);
		}

		public override void Invoke(object[] args)
		{
			if (AllowInvoke(Delegate))
				Delegate();
		}

		public void Invoke()
		{
			if (AllowInvoke(Delegate))
				Delegate();
		}
	}

	public class InvokableCall2<T0> : BaseInvokableCall2
	{
		protected event UnityAction<T0> Delegate;

		public InvokableCall2(string typeName, MethodInfo theFunction) : base(typeName, theFunction)
		{
			Delegate += (UnityAction<T0>)System.Delegate.CreateDelegate(typeof(UnityAction<T0>), theFunction);
		}

		public InvokableCall2(object target, MethodInfo theFunction) : base(target, theFunction)
		{
			Delegate += (UnityAction<T0>)System.Delegate.CreateDelegate(typeof(UnityAction<T0>), theFunction.IsStatic ? null : target, theFunction);
		}

		public InvokableCall2(UnityAction<T0> action)
		{
			Delegate += action;
		}

		public override void Invoke(object[] args)
		{
			if (args.Length != 1)
				throw new ArgumentException("Passed argument 'args' is invalid size. Expected size is 1");
			ThrowOnInvalidArg<T0>(args[0], methodName, targetAssembly);
			if (!AllowInvoke(Delegate))
				return;

			Delegate((T0)args[0]);
		}

		public virtual void Invoke(T0 args0)
		{
			if (AllowInvoke(Delegate))
				Delegate(args0);
		}

		public override bool Find(object targetObj, MethodInfo method)
		{
			return Delegate.Target == targetObj && Delegate.Method.Equals(method);
		}
	}

	public class InvokableCall2<T0, T1> : BaseInvokableCall2
	{
		protected event UnityAction<T0, T1> Delegate;

		public InvokableCall2(string typeName, MethodInfo theFunction) : base(typeName, theFunction)
		{
			Delegate += (UnityAction<T0, T1>)System.Delegate.CreateDelegate(typeof(UnityAction<T0, T1>), theFunction);
		}

		public InvokableCall2(object target, MethodInfo theFunction) : base(target, theFunction)
		{
			Delegate += (UnityAction<T0, T1>)System.Delegate.CreateDelegate(typeof(UnityAction<T0, T1>), theFunction.IsStatic ? null : target, theFunction);
		}

		public InvokableCall2(UnityAction<T0, T1> action)
		{
			Delegate += action;
		}

		public override void Invoke(object[] args)
		{
			if (args.Length != 2)
				throw new ArgumentException("Passed argument 'args' is invalid size. Expected size is 2");
			ThrowOnInvalidArg<T0>(args[0], methodName, targetAssembly);
			ThrowOnInvalidArg<T1>(args[1], methodName, targetAssembly);
			if (!AllowInvoke(Delegate))
				return;

			Delegate((T0)args[0], (T1)args[1]);
		}

		public virtual void Invoke(T0 args0, T1 args1)
		{
			if (AllowInvoke(Delegate))
				Delegate(args0, args1);
		}

		public override bool Find(object targetObj, MethodInfo method)
		{
			return Delegate.Target == targetObj && Delegate.Method.Equals(method);
		}
	}

	public class InvokableCall2<T0, T1, T2> : BaseInvokableCall2
	{
		protected event UnityAction<T0, T1, T2> Delegate;

		public InvokableCall2(string typeName, MethodInfo theFunction) : base(typeName, theFunction)
		{
			Delegate += (UnityAction<T0, T1, T2>)System.Delegate.CreateDelegate(typeof(UnityAction<T0, T1, T2>), theFunction);
		}

		public InvokableCall2(object target, MethodInfo theFunction) : base(target, theFunction)
		{
			Delegate += (UnityAction<T0, T1, T2>)System.Delegate.CreateDelegate(typeof(UnityAction<T0, T1, T2>), theFunction.IsStatic ? null : target, theFunction);
		}

		public InvokableCall2(UnityAction<T0, T1, T2> action)
		{
			Delegate += action;
		}

		public override void Invoke(object[] args)
		{
			if (args.Length != 3)
				throw new ArgumentException("Passed argument 'args' is invalid size. Expected size is 3");
			ThrowOnInvalidArg<T0>(args[0], methodName, targetAssembly);
			ThrowOnInvalidArg<T1>(args[1], methodName, targetAssembly);
			ThrowOnInvalidArg<T2>(args[2], methodName, targetAssembly);
			if (!AllowInvoke(Delegate))
				return;

			Delegate((T0)args[0], (T1)args[1], (T2)args[2]);
		}

		public virtual void Invoke(T0 args0, T1 args1, T2 args2)
		{
			if (AllowInvoke(Delegate))
				Delegate(args0, args1, args2);
		}

		public override bool Find(object targetObj, MethodInfo method)
		{
			return Delegate.Target == targetObj && Delegate.Method.Equals(method);
		}
	}

	public class InvokableCall2<T0, T1, T2, T3> : BaseInvokableCall2
	{
		protected event UnityAction<T0, T1, T2, T3> Delegate;

		public InvokableCall2(string typeName, MethodInfo theFunction) : base(typeName, theFunction)
		{
			Delegate += (UnityAction<T0, T1, T2, T3>)System.Delegate.CreateDelegate(typeof(UnityAction<T0, T1, T2, T3>), theFunction);
		}

		public InvokableCall2(object target, MethodInfo theFunction) : base(target, theFunction)
		{
			Delegate += (UnityAction<T0, T1, T2, T3>)System.Delegate.CreateDelegate(typeof(UnityAction<T0, T1, T2, T3>), theFunction.IsStatic ? null : target, theFunction);
		}

		public InvokableCall2(UnityAction<T0, T1, T2, T3> action)
		{
			Delegate += action;
		}

		public override void Invoke(object[] args)
		{
			if (args.Length != 4)
				throw new ArgumentException("Passed argument 'args' is invalid size. Expected size is 4");
			ThrowOnInvalidArg<T0>(args[0], methodName, targetAssembly);
			ThrowOnInvalidArg<T1>(args[1], methodName, targetAssembly);
			ThrowOnInvalidArg<T2>(args[2], methodName, targetAssembly);
			ThrowOnInvalidArg<T3>(args[3], methodName, targetAssembly);
			if (!AllowInvoke(Delegate))
				return;
			Delegate((T0)args[0], (T1)args[1], (T2)args[2], (T3)args[3]);
		}

		public virtual void Invoke(T0 args0, T1 args1, T2 args2, T3 args3)
		{
			if (AllowInvoke(Delegate))
				Delegate(args0, args1, args2, args3);
		}

		public override bool Find(object targetObj, MethodInfo method)
		{
			return Delegate.Target == targetObj && Delegate.Method.Equals(method);
		}
	}

	public class InvokableCall2<T0, T1, T2, T3, T4> : BaseInvokableCall2
	{
		protected event UnityAction<T0, T1, T2, T3, T4> Delegate;

		public InvokableCall2(string typeName, MethodInfo theFunction) : base(typeName, theFunction)
		{
			Delegate += (UnityAction<T0, T1, T2, T3, T4>)System.Delegate.CreateDelegate(typeof(UnityAction<T0, T1, T2, T3, T4>), theFunction);
		}

		public InvokableCall2(object target, MethodInfo theFunction) : base(target, theFunction)
		{
			Delegate += (UnityAction<T0, T1, T2, T3, T4>)System.Delegate.CreateDelegate(typeof(UnityAction<T0, T1, T2, T3, T4>), theFunction.IsStatic ? null : target, theFunction);
		}

		public InvokableCall2(UnityAction<T0, T1, T2, T3, T4> action)
		{
			Delegate += action;
		}

		public override void Invoke(object[] args)
		{
			if (args.Length != 5)
				throw new ArgumentException("Passed argument 'args' is invalid size. Expected size is 5");
			ThrowOnInvalidArg<T0>(args[0], methodName, targetAssembly);
			ThrowOnInvalidArg<T1>(args[1], methodName, targetAssembly);
			ThrowOnInvalidArg<T2>(args[2], methodName, targetAssembly);
			ThrowOnInvalidArg<T3>(args[3], methodName, targetAssembly);
			ThrowOnInvalidArg<T4>(args[4], methodName, targetAssembly);
			if (!AllowInvoke(Delegate))
				return;
			Delegate((T0)args[0], (T1)args[1], (T2)args[2], (T3)args[3], (T4)args[4]);
		}

		public virtual void Invoke(T0 args0, T1 args1, T2 args2, T3 args3, T4 args4)
		{
			if (AllowInvoke(Delegate))
				Delegate(args0, args1, args2, args3, args4);
		}

		public override bool Find(object targetObj, MethodInfo method)
		{
			return Delegate.Target == targetObj && Delegate.Method.Equals(method);
		}
	}

	public class InvokableCall2<T0, T1, T2, T3, T4, T5> : BaseInvokableCall2
	{
		protected event UnityAction<T0, T1, T2, T3, T4, T5> Delegate;

		public InvokableCall2(string typeName, MethodInfo theFunction) : base(typeName, theFunction)
		{
			Delegate += (UnityAction<T0, T1, T2, T3, T4, T5>)System.Delegate.CreateDelegate(typeof(UnityAction<T0, T1, T2, T3, T4, T5>), theFunction);
		}

		public InvokableCall2(object target, MethodInfo theFunction) : base(target, theFunction)
		{
			Delegate += (UnityAction<T0, T1, T2, T3, T4, T5>)System.Delegate.CreateDelegate(typeof(UnityAction<T0, T1, T2, T3, T4, T5>), theFunction.IsStatic ? null : target, theFunction);
		}

		public InvokableCall2(UnityAction<T0, T1, T2, T3, T4, T5> action)
		{
			Delegate += action;
		}

		public override void Invoke(object[] args)
		{
			if (args.Length != 6)
				throw new ArgumentException("Passed argument 'args' is invalid size. Expected size is 6");
			ThrowOnInvalidArg<T0>(args[0], methodName, targetAssembly);
			ThrowOnInvalidArg<T1>(args[1], methodName, targetAssembly);
			ThrowOnInvalidArg<T2>(args[2], methodName, targetAssembly);
			ThrowOnInvalidArg<T3>(args[3], methodName, targetAssembly);
			ThrowOnInvalidArg<T4>(args[4], methodName, targetAssembly);
			ThrowOnInvalidArg<T5>(args[5], methodName, targetAssembly);
			if (!AllowInvoke(Delegate))
				return;
			Delegate((T0)args[0], (T1)args[1], (T2)args[2], (T3)args[3], (T4)args[4], (T5)args[5]);
		}

		public virtual void Invoke(T0 args0, T1 args1, T2 args2, T3 args3, T4 args4, T5 args5)
		{
			if (AllowInvoke(Delegate))
				Delegate(args0, args1, args2, args3, args4, args5);
		}

		public override bool Find(object targetObj, MethodInfo method)
		{
			return Delegate.Target == targetObj && Delegate.Method.Equals(method);
		}
	}

	public class InvokableCall2<T0, T1, T2, T3, T4, T5, T6> : BaseInvokableCall2
	{
		protected event UnityAction<T0, T1, T2, T3, T4, T5, T6> Delegate;

		public InvokableCall2(string typeName, MethodInfo theFunction) : base(typeName, theFunction)
		{
			Delegate += (UnityAction<T0, T1, T2, T3, T4, T5, T6>)System.Delegate.CreateDelegate(typeof(UnityAction<T0, T1, T2, T3, T4, T5, T6>), theFunction);
		}

		public InvokableCall2(object target, MethodInfo theFunction) : base(target, theFunction)
		{
			Delegate += (UnityAction<T0, T1, T2, T3, T4, T5, T6>)System.Delegate.CreateDelegate(typeof(UnityAction<T0, T1, T2, T3, T4, T5, T6>), theFunction.IsStatic ? null : target, theFunction);
		}

		public InvokableCall2(UnityAction<T0, T1, T2, T3, T4, T5, T6> action)
		{
			Delegate += action;
		}

		public override void Invoke(object[] args)
		{
			if (args.Length != 7)
				throw new ArgumentException("Passed argument 'args' is invalid size. Expected size is 7");
			ThrowOnInvalidArg<T0>(args[0], methodName, targetAssembly);
			ThrowOnInvalidArg<T1>(args[1], methodName, targetAssembly);
			ThrowOnInvalidArg<T2>(args[2], methodName, targetAssembly);
			ThrowOnInvalidArg<T3>(args[3], methodName, targetAssembly);
			ThrowOnInvalidArg<T4>(args[4], methodName, targetAssembly);
			ThrowOnInvalidArg<T5>(args[5], methodName, targetAssembly);
			ThrowOnInvalidArg<T6>(args[6], methodName, targetAssembly);
			if (!AllowInvoke(Delegate))
				return;
			Delegate((T0)args[0], (T1)args[1], (T2)args[2], (T3)args[3], (T4)args[4], (T5)args[5], (T6)args[6]);
		}

		public virtual void Invoke(T0 args0, T1 args1, T2 args2, T3 args3, T4 args4, T5 args5, T6 args6)
		{
			if (AllowInvoke(Delegate))
				Delegate(args0, args1, args2, args3, args4, args5, args6);
		}

		public override bool Find(object targetObj, MethodInfo method)
		{
			return Delegate.Target == targetObj && Delegate.Method.Equals(method);
		}
	}

	public class InvokableCall2<T0, T1, T2, T3, T4, T5, T6, T7> : BaseInvokableCall2
	{
		protected event UnityAction<T0, T1, T2, T3, T4, T5, T6, T7> Delegate;

		public InvokableCall2(string typeName, MethodInfo theFunction) : base(typeName, theFunction)
		{
			Delegate += (UnityAction<T0, T1, T2, T3, T4, T5, T6, T7>)System.Delegate.CreateDelegate(typeof(UnityAction<T0, T1, T2, T3, T4, T5, T6, T7>), theFunction);
		}

		public InvokableCall2(object target, MethodInfo theFunction) : base(target, theFunction)
		{
			Delegate += (UnityAction<T0, T1, T2, T3, T4, T5, T6, T7>)System.Delegate.CreateDelegate(typeof(UnityAction<T0, T1, T2, T3, T4, T5, T6, T7>), theFunction.IsStatic ? null : target, theFunction);
		}

		public InvokableCall2(UnityAction<T0, T1, T2, T3, T4, T5, T6, T7> action)
		{
			Delegate += action;
		}

		public override void Invoke(object[] args)
		{
			if (args.Length != 8)
				throw new ArgumentException("Passed argument 'args' is invalid size. Expected size is 8");
			ThrowOnInvalidArg<T0>(args[0], methodName, targetAssembly);
			ThrowOnInvalidArg<T1>(args[1], methodName, targetAssembly);
			ThrowOnInvalidArg<T2>(args[2], methodName, targetAssembly);
			ThrowOnInvalidArg<T3>(args[3], methodName, targetAssembly);
			ThrowOnInvalidArg<T4>(args[4], methodName, targetAssembly);
			ThrowOnInvalidArg<T5>(args[5], methodName, targetAssembly);
			ThrowOnInvalidArg<T6>(args[6], methodName, targetAssembly);
			ThrowOnInvalidArg<T7>(args[7], methodName, targetAssembly);
			if (!AllowInvoke(Delegate))
				return;
			Delegate((T0)args[0], (T1)args[1], (T2)args[2], (T3)args[3], (T4)args[4], (T5)args[5], (T6)args[6], (T7)args[7]);
		}

		public virtual void Invoke(T0 args0, T1 args1, T2 args2, T3 args3, T4 args4, T5 args5, T6 args6, T7 args7)
		{
			if (AllowInvoke(Delegate))
				Delegate(args0, args1, args2, args3, args4, args5, args6, args7);
		}

		public override bool Find(object targetObj, MethodInfo method)
		{
			return Delegate.Target == targetObj && Delegate.Method.Equals(method);
		}
	}

	public class InvokableCall2<T0, T1, T2, T3, T4, T5, T6, T7, T8> : BaseInvokableCall2
	{
		protected event UnityAction<T0, T1, T2, T3, T4, T5, T6, T7, T8> Delegate;

		public InvokableCall2(string typeName, MethodInfo theFunction) : base(typeName, theFunction)
		{
			Delegate += (UnityAction<T0, T1, T2, T3, T4, T5, T6, T7, T8>)System.Delegate.CreateDelegate(typeof(UnityAction<T0, T1, T2, T3, T4, T5, T6, T7, T8>), theFunction);
		}

		public InvokableCall2(object target, MethodInfo theFunction) : base(target, theFunction)
		{
			Delegate += (UnityAction<T0, T1, T2, T3, T4, T5, T6, T7, T8>)System.Delegate.CreateDelegate(typeof(UnityAction<T0, T1, T2, T3, T4, T5, T6, T7, T8>), theFunction.IsStatic ? null : target, theFunction);
		}

		public InvokableCall2(UnityAction<T0, T1, T2, T3, T4, T5, T6, T7, T8> action)
		{
			Delegate += action;
		}

		public override void Invoke(object[] args)
		{
			if (args.Length != 9)
				throw new ArgumentException("Passed argument 'args' is invalid size. Expected size is 9");
			ThrowOnInvalidArg<T0>(args[0], methodName, targetAssembly);
			ThrowOnInvalidArg<T1>(args[1], methodName, targetAssembly);
			ThrowOnInvalidArg<T2>(args[2], methodName, targetAssembly);
			ThrowOnInvalidArg<T3>(args[3], methodName, targetAssembly);
			ThrowOnInvalidArg<T4>(args[4], methodName, targetAssembly);
			ThrowOnInvalidArg<T5>(args[5], methodName, targetAssembly);
			ThrowOnInvalidArg<T6>(args[6], methodName, targetAssembly);
			ThrowOnInvalidArg<T7>(args[7], methodName, targetAssembly);
			ThrowOnInvalidArg<T8>(args[8], methodName, targetAssembly);
			if (!AllowInvoke(Delegate))
				return;
			Delegate((T0)args[0], (T1)args[1], (T2)args[2], (T3)args[3], (T4)args[4], (T5)args[5], (T6)args[6], (T7)args[7], (T8)args[8]);
		}

		public virtual void Invoke(T0 args0, T1 args1, T2 args2, T3 args3, T4 args4, T5 args5, T6 args6, T7 args7, T8 args8)
		{
			if (AllowInvoke(Delegate))
				Delegate(args0, args1, args2, args3, args4, args5, args6, args7, args8);
		}

		public override bool Find(object targetObj, MethodInfo method)
		{
			return Delegate.Target == targetObj && Delegate.Method.Equals(method);
		}
	}

	public class InvokableCall2<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9> : BaseInvokableCall2
	{
		protected event UnityAction<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9> Delegate;

		public InvokableCall2(string typeName, MethodInfo theFunction) : base(typeName, theFunction)
		{
			Delegate += (UnityAction<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9>)System.Delegate.CreateDelegate(typeof(UnityAction<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9>), theFunction);
		}

		public InvokableCall2(object target, MethodInfo theFunction) : base(target, theFunction)
		{
			Delegate += (UnityAction<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9>)System.Delegate.CreateDelegate(typeof(UnityAction<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9>), theFunction.IsStatic ? null : target, theFunction);
		}

		public InvokableCall2(UnityAction<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9> action)
		{
			Delegate += action;
		}

		public override void Invoke(object[] args)
		{
			if (args.Length != 10)
				throw new ArgumentException("Passed argument 'args' is invalid size. Expected size is 10");
			ThrowOnInvalidArg<T0>(args[0], methodName, targetAssembly);
			ThrowOnInvalidArg<T1>(args[1], methodName, targetAssembly);
			ThrowOnInvalidArg<T2>(args[2], methodName, targetAssembly);
			ThrowOnInvalidArg<T3>(args[3], methodName, targetAssembly);
			ThrowOnInvalidArg<T4>(args[4], methodName, targetAssembly);
			ThrowOnInvalidArg<T5>(args[5], methodName, targetAssembly);
			ThrowOnInvalidArg<T6>(args[6], methodName, targetAssembly);
			ThrowOnInvalidArg<T7>(args[7], methodName, targetAssembly);
			ThrowOnInvalidArg<T8>(args[8], methodName, targetAssembly);
			ThrowOnInvalidArg<T9>(args[9], methodName, targetAssembly);
			if (!AllowInvoke(Delegate))
				return;
			Delegate((T0)args[0], (T1)args[1], (T2)args[2], (T3)args[3], (T4)args[4], (T5)args[5], (T6)args[6], (T7)args[7], (T8)args[8], (T9)args[9]);
		}

		public virtual void Invoke(T0 args0, T1 args1, T2 args2, T3 args3, T4 args4, T5 args5, T6 args6, T7 args7, T8 args8, T9 args9)
		{
			if (AllowInvoke(Delegate))
				Delegate(args0, args1, args2, args3, args4, args5, args6, args7, args8, args9);
		}

		public override bool Find(object targetObj, MethodInfo method)
		{
			return Delegate.Target == targetObj && Delegate.Method.Equals(method);
		}
	}
}