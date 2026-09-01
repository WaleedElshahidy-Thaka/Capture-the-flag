using System;
using System.Reflection;

namespace UnityEngine.Events
{
	public abstract class BaseInvokableCall2
	{
		protected string targetAssembly = null;
		protected string methodName = null;

		protected BaseInvokableCall2()
		{
		}

		protected BaseInvokableCall2(string typeName, MethodInfo function)
		{
			if (string.IsNullOrEmpty(typeName))
				throw new ArgumentNullException("typeName");
			if (function == null)
				throw new ArgumentNullException("function");

			targetAssembly = typeName;
			methodName = function.Name;
		}

		protected BaseInvokableCall2(object target, MethodInfo function)
		{
			if (target == null)
				throw new ArgumentNullException("target");
			if (function == null)
				throw new ArgumentNullException("function");

			targetAssembly = target.GetType().AssemblyQualifiedName;
			methodName = function.Name;
		}

		public abstract void Invoke(object[] args);

		protected static void ThrowOnInvalidArg<T>(object arg, string methodName, string targetAssembly)
		{
			if (arg != null && !(arg is T))
				throw new ArgumentException(string.Format("Passed argument 'arg' on method '{0}' for target '{1}' is of the wrong type. Type:{2} Expected:{3}", methodName, targetAssembly, arg.GetType(), typeof(T)));
		}

		protected static bool AllowInvoke(Delegate @delegate)
		{
			object target = @delegate.Target;
			if (target == null)
				return true;
			Object @object = target as Object;
			if (!ReferenceEquals(@object, null))
				return @object != null;
			return true;
		}

		public abstract bool Find(object targetObj, MethodInfo method);

		protected object[] HandlePersistentCalls(PersistentCall2[] persistentCalls, object[] args)
		{
			object[] result = new object[persistentCalls.Length];

			for (int i = 0; i < persistentCalls.Length; i++)
			{
				var persistentCall = persistentCalls[i];

				if (persistentCall != null)
				{
					Type[] argumentTypes = new Type[persistentCall.arguments.Count];
					object[] parameters = new object[persistentCall.arguments.Count];

					for (int j = 0; j < argumentTypes.Length; j++)
					{
						argumentTypes[j] = Type.GetType(persistentCall.arguments[j].unityObjectArgumentAssemblyTypeName, false) ?? typeof(Object);
						parameters[j] = persistentCall.GetValue(argumentTypes[j], persistentCall.arguments[j]);
					}

					MethodInfo methodInfo = null;

					if (persistentCall.isStatic)
						methodInfo = UnityEventBase2.GetValidMethodInfo(persistentCall.assemblyName, persistentCall.typeName, persistentCall.methodName, argumentTypes);
					else
						methodInfo = UnityEventBase2.GetValidMethodInfo(persistentCall.target, persistentCall.methodName, argumentTypes, persistentCall.isStaticForInstance);
					
					if (methodInfo != null)
						result[i] = methodInfo.Invoke(persistentCall.target, parameters);
					else
						result[i] = args[i];
				}
				else
				{
					result[i] = args[i];
				}
			}

			return result;
		}
	}
}