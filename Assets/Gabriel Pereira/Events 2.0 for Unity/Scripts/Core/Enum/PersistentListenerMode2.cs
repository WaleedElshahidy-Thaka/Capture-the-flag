namespace UnityEngine.Events
{
	/// <summary>
	/// The mode that a listener is operating in.
	/// </summary>
	public enum PersistentListenerMode2
	{
		/// <summary>
		/// The listener will use the function binding specified by the event.
		/// </summary>
		EventDefined,
		
		/// <summary>
		/// The listener will bind to zero argument functions.
		/// </summary>
		Void,
		
		/// <summary>
		/// The listener will bind to an Object type argument functions.
		/// </summary>
		Object,
		
		/// <summary>
		/// The listener will bind to an char (\0 ~ \uffff) type argument functions.
		/// </summary>
		Char,
		
		/// <summary>
		/// The listener will bind to an byte (0 ~ 255) argument functions.
		/// </summary>
		Byte,
		
		/// <summary>
		/// The listener will bind to an sbyte (-128 ~ 127) argument functions.
		/// </summary>
		SByte,
		
		/// <summary>
		/// The listener will bind to an short (-32,768 ~ 32,767) argument functions.
		/// </summary>
		Short,
		
		/// <summary>
		/// The listener will bind to an ushort (0 ~ 65,535) argument functions.
		/// </summary>
		UShort,
		
		/// <summary>
		/// The listener will bind to an int (-2,147,483,648 ~ 2,147,483,647) argument functions.
		/// </summary>
		Int,
		
		/// <summary>
		/// The listener will bind to an uint (0 ~ 4,294,967,295) argument functions.
		/// </summary>
		UInt,
		
		/// <summary>
		/// The listener will bind to an long (-9,223,372,036,854,775,808 ~ 9,223,372,036,854,775,807) argument functions.
		/// </summary>
		Long,
		
		/// <summary>
		/// The listener will bind to a float (±1.5 x 10e-45 ~ ±3.4 x 10e38) argument functions.
		/// </summary>
		Float,
		
		/// <summary>
		/// The listener will bind to a double (±5.0 × 10e−324 ~ ±1.7 × 10e308) argument functions.
		/// </summary>
		Double,
		
		/// <summary>
		/// The listener will bind to a string argument functions.
		/// </summary>
		String,
		
		/// <summary>
		/// The listener will bind to a bool argument functions.
		/// </summary>
		Bool,
		
		/// <summary>
		/// The listener will bind to an enum argument functions.
		/// </summary>
		Enum,
		
		/// <summary>
		/// The listener will bind to a Vector2 argument functions.
		/// </summary>
		Vector2,
		
		/// <summary>
		/// The listener will bind to a Vector2Int argument functions.
		/// </summary>
		Vector2Int,
		
		/// <summary>
		/// The listener will bind to a Vector3 argument functions.
		/// </summary>
		Vector3,
		
		/// <summary>
		/// The listener will bind to a Vector3Int argument functions.
		/// </summary>
		Vector3Int,
		
		/// <summary>
		/// The listener will bind to a Vector4 argument functions.
		/// </summary>
		Vector4,
		
		/// <summary>
		/// The listener will bind to a LayerMask argument functions.
		/// </summary>
		LayerMask,
		
		/// <summary>
		/// The listener will bind to a Color argument functions.
		/// </summary>
		Color,
		
		/// <summary>
		/// The listener will bind to a Quaternion argument functions.
		/// </summary>
		Quaternion,
		
		/// <summary>
		/// The listener will bind to an Array or Generic List argument functions.
		/// </summary>
		Array
	}
}