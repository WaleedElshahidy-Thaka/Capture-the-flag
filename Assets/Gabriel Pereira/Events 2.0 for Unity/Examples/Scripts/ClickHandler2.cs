using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 
/// </summary>
public class ClickHandler2 : MonoBehaviour, IPointerClickHandler
{
	[Serializable]
	public class PointerEvents2 : UnityEvent2<PointerEventData> { }

	/// <summary>
	/// 
	/// </summary>
	[SerializeField]
	private Text m_LogText = null;

	/// <summary>
	/// 
	/// </summary>
	[SerializeField]
	private GameObject m_SomeGameObject;

	/// <summary>
	/// 
	/// </summary>
	[SerializeField]
	[Tooltip("Regular events (This tooltip will appear on Inspector)")]
	private UnityEvent2 unityEvents2 = null;

	/// <summary>
	/// 
	/// </summary>
	[SerializeField]
	[Tooltip("PointerEventData dynamic events (This tooltip will appear on Inspector)")]
	private PointerEvents2 pointerEvents2 = null;

	/// <summary>
	/// 
	/// </summary>
	private static Text m_StaticLogText = null;

	void Awake()
	{
		m_StaticLogText = m_LogText;
	}

	/// <summary>
	/// 
	/// </summary>
	/// <param name="message"></param>
	/// <param name="args"></param>
	private void Log(string message, params object[] args)
	{
		Debug.LogFormat(this, message, args);
		m_LogText.text += string.Format(message, args);
		m_LogText.text += Environment.NewLine;
		m_LogText.text += Environment.NewLine;
	}

	/// <summary>
	/// 
	/// </summary>
	/// <param name="message"></param>
	/// <param name="args"></param>
	private static void StaticLog(string message, params object[] args)
	{
		Debug.LogFormat(message, args);
		m_StaticLogText.text += string.Format(message, args);
		m_StaticLogText.text += Environment.NewLine;
		m_StaticLogText.text += Environment.NewLine;
	}

	/// <summary>
	/// 
	/// </summary>
	/// <param name="eventData"></param>
	public void OnPointerClick(PointerEventData eventData)
	{
		unityEvents2.Invoke();
		Log("------------------------------------------------------------------------------------------------");
		pointerEvents2.Invoke(eventData);
	}

	#region Normal Events

	public void Test()
	{
		Log("Test method. No params");
	}

	public void Test(ClickHandler2 clickHandler2)
	{
		Log("Test method. ClickHandler2:{0}", clickHandler2);
	}

	public void Test(float f1, float f2)
	{
		Log("Test method. float param1:{0} - float param2:{1}", f1, f2);
	}

	public void Test(int int1, int int2, int int3)
	{
		Log("Test method. int param1:{0} - int param2:{1} - int param3:{2}", int1, int2, int3);
	}

	public void Test(string string1, string string2, string string3)
	{
		Log("Test method. string param1:{0} - string param2:{1} - string param3:{2}", string1, string2, string3);
	}

	public void Test(EnumExample enumExample1, EnumExample enumExample2, EnumExample enumExample3)
	{
		Log("Test method. enum param1:{0} - enum param2:{1} - enum param3:{2}", enumExample1, enumExample2, enumExample3);
	}

	public void Test(GameObject gameObject1, GameObject gameObject2)
	{
		Log("Test method. GameObject param1:{0} - GameObject param2:{1}", gameObject1, gameObject2);
	}

	public void Test(int intParam, string stringParam, GameObject gameObject, EnumExample enumExample)
	{
		Log("Test method. int:{0} - string:{1} - GameObject:{2} - Enum:{3}", intParam, stringParam, gameObject, enumExample);
	}

	public void Test(Vector2 vector2)
	{
		Log("Test method. Vector2 param:{0}", vector2);
	}

	public void Test(Vector2Int vector2Int)
	{
		Log("Test method. Vector2Int param:{0}", vector2Int);
	}

	public void Test(Vector3 vector3)
	{
		Log("Test method. Vector3 param:{0}", vector3);
	}

	public void Test(Vector3Int vector3Int)
	{
		Log("Test method. Vector3Int param:{0}", vector3Int);
	}

	public void Test(Vector4 vector4)
	{
		Log("Test method. Vector4 param:{0}", vector4);
	}

	public void TestLayer([Layer] int layer)
	{
		Log("TestLayer method. layer index:{0} - name:{1}", layer, LayerMask.LayerToName(layer));
	}

	[Layer("Ignore Raycast")]
	public void TestLayer(int layer1, [Layer(0)] int layer2)
	{
		Log("TestLayer method. layer1 index:{0} - name:{1} - layer2 index:{2} - name:{3}", layer1, LayerMask.LayerToName(layer1), layer2, LayerMask.LayerToName(layer2));
	}

	[Tag]
	public void TestTag(string tag)
	{
		Log("TestTag method. tag:{0}", tag);
	}

	[Tag("Player")]
	public void TestTag(string tag1, [Tag("GameController")] string tag2)
	{
		Log("TestTag method. tag1:{0}, tag2:{1}", tag1, tag2);
	}

	public void TestTag([Tag("Player")] string tag1, string tag2, [Tag("GameController")] string tag3)
	{
		Log("TestTag method. tag1:{0}, tag2:{1}, tag3:{2}", tag1, tag2, tag3);
	}

	public void TestColor(Color color)
	{
		Log("TestColor method. color param:{0}", color);
	}

	public void TestLayerMask(LayerMask layerMask)
	{
		List<string> layersList = new List<string>();
		for (int i = 0; i < 32; i++)
		{
			if ((layerMask.value & 1 << i) > 0 && !string.IsNullOrEmpty(LayerMask.LayerToName(i)))
				layersList.Add(LayerMask.LayerToName(i));
		}
		string layers = string.Join(",", layersList.ToArray());
		Log("TestLayerMask method. Selected layers from layerMask:{0}", layers);
	}

	public void TestQuaternion(Quaternion quaternion)
	{
		Log("Test method. Quaternion param:{0}", quaternion);
	}

	[Slider(0f, 10f)]
	public void TestSlider(float float1)
	{
		Log("TestSlider method. float param:{0}", float1);
	}

	[Slider(0f, 10f)]
	public void TestSlider(float float1, float float2)
	{
		Log("TestSlider method. float param1:{0} - float param2:{1}", float1, float2);
	}

	public void TestSlider([Slider(0f, 5f)] float float1, float float2, [Slider(0f, 10f)] float float3)
	{
		Log("TestSlider method. float param1:{0} - float param2:{1} - float param2:{2}", float1, float2, float3);
	}

	[IntSlider(0, 10)]
	public void TestIntSlider(int int1)
	{
		Log("TestIntSlider method. int param:{0}", int1);
	}

	[IntSlider(0, 10, 5)]
	public void TestIntSlider(int int1, int int2)
	{
		Log("TestIntSlider method. int param1:{0} - int param2:{1}", int1, int2);
	}

	public void TestIntSlider([IntSlider(0, 5, 2)] int int1, int int2, [IntSlider(0, 10, 3)] int int3)
	{
		Log("TestIntSlider method. int param1:{0} - int param2:{1} - int param2:{2}", int1, int2, int3);
	}

	public void TestListInt(List<int> list)
	{
		Log("TestListInt method. list count:{0}, values:{1}", list.Count, string.Join(",", list.Select(x => x.ToString()).ToArray()));
	}

	public void TestListFloat(List<float> list)
	{
		Log("TestListFloat method. list count:{0}, values:{1}", list.Count, string.Join(",", list.Select(x => x.ToString()).ToArray()));
	}

	public void TestListString(List<string> list)
	{
		Log("TestListString method. list count:{0}, values:{1}", list.Count, string.Join(",", list.ToArray()));
	}

	public void TestListBool(List<bool> list)
	{
		Log("TestListBool method. list count:{0}, values:{1}", list.Count, string.Join(",", list.Select(x => x.ToString()).ToArray()));
	}

	public void TestCharList(List<char> list)
	{
		Log("TestCharList method. list count:{0}, values:{1}", list.Count, string.Join(",", list.Select(x => x == char.MinValue ? string.Empty : x.ToString()).ToArray()));
	}

	public void TestListEnumExample(List<EnumExample> list)
	{
		Log("TestListEnumExample method. list count:{0}, values:{1}", list.Count, string.Join(",", list.Select(x => x.ToString()).ToArray()));
	}

	public void TestListVector2(List<Vector2> list)
	{
		Log("TestListVector2 method. list count:{0}, values:{1}", list.Count, string.Join(",", list.Select(x => x.ToString()).ToArray()));
	}

	public void TestListVector3(List<Vector3> list)
	{
		Log("TestListVector3 method. list count:{0}, values:{1}", list.Count, string.Join(",", list.Select(x => x.ToString()).ToArray()));
	}

	public void TestListVector4(List<Vector4> list)
	{
		Log("TestListVector4 method. list count:{0}, values:{1}", list.Count, string.Join(",", list.Select(x => x.ToString()).ToArray()));
	}

	public void TestListLayerMask(List<LayerMask> list)
	{
		Log("TestListLayerMask method. list count:{0}, values:{1}", list.Count, string.Join(",", list.Select(x => x.ToString()).ToArray()));
	}

	public void TestListColor(List<Color> list)
	{
		Log("TestListColor method. list count:{0}, values:{1}", list.Count, string.Join(",", list.Select(x => x.ToString()).ToArray()));
	}

	public void TestListQuaternion(List<Quaternion> list)
	{
		Log("TestListQuaternion method. list count:{0}, values:{1}", list.Count, string.Join(",", list.Select(x => x.ToString()).ToArray()));
	}

	public void TestListGameObject(List<GameObject> list)
	{
		Log("TestListGameObject method. list count:{0}, values:{1}", list.Count, string.Join(",", list.Select(x => x.name).ToArray()));
	}

	public void TestArrayInt(int[] array)
	{
		Log("TestArrayInt method. array length:{0}, values:{1}", array.Length, string.Join(",", array.Select(x => x.ToString()).ToArray()));
	}

	public void TestArrayFloat(float[] array)
	{
		Log("TestArrayFloat method. array length:{0}, values:{1}", array.Length, string.Join(",", array.Select(x => x.ToString()).ToArray()));
	}

	public void TestArrayString(string[] array)
	{
		Log("TestArrayString method. array length:{0}, values:[{1}]", array.Length, string.Join(",", array.Select(x => x.ToString()).ToArray()));
	}

	public void TestArrayString(string[] array, string[] array2)
	{
		Log("TestArrayString method. array length:{0}, array values:[{1}], array2 length:{0}, array2 values:[{1}]", array.Length, string.Join(",", array.Select(x => x.ToString()).ToArray()), array2.Length, string.Join(",", array2.Select(x => x.ToString()).ToArray()));
	}

	public void TestArrayBool(bool[] array)
	{
		Log("TestArrayBool method. array length:{0}, values:{1}", array.Length, string.Join(",", array.Select(x => x.ToString()).ToArray()));
	}

	public void TestCharArray(char[] array)
	{
		Log("TestCharArray method. array length:{0}, values:{1}", array.Length, string.Join(",", array.Select(x => x == char.MinValue ? string.Empty : x.ToString()).ToArray()));
	}

	public void TestArrayEnumExample(EnumExample[] array)
	{
		Log("TestArrayEnumExample method. array length:{0}, values:{1}", array.Length, string.Join(",", array.Select(x => x.ToString()).ToArray()));
	}

	public void TestArrayVector2(Vector2[] array)
	{
		Log("TestArrayVector2 method. array length:{0}, values:{1}", array.Length, string.Join(",", array.Select(x => x.ToString()).ToArray()));
	}

	public void TestArrayVector3(Vector3[] array)
	{
		Log("TestArrayVector3 method. array length:{0}, values:{1}", array.Length, string.Join(",", array.Select(x => x.ToString()).ToArray()));
	}

	public void TestArrayVector4(Vector4[] array)
	{
		Log("TestArrayVector4 method. array length:{0}, values:{1}", array.Length, string.Join(",", array.Select(x => x.ToString()).ToArray()));
	}

	public void TestArrayLayerMask(LayerMask[] array)
	{
		Log("TestArrayLayerMask method. array length:{0}, values:{1}", array.Length, string.Join(",", array.Select(x => x.ToString()).ToArray()));
	}

	public void TestArrayColor(Color[] array)
	{
		Log("TestArrayColor method. array length:{0}, values:{1}", array.Length, string.Join(",", array.Select(x => x.ToString()).ToArray()));
	}

	public void TestArrayQuaternion(Quaternion[] array)
	{
		Log("TestArrayQuaternion method. array length:{0}, values:{1}", array.Length, string.Join(",", array.Select(x => x.ToString()).ToArray()));
	}

	public void TestArrayGameObject(GameObject[] array)
	{
		Log("TestArrayGameObject method. array length:{0}, values:{1}", array.Length, string.Join(",", array.Select(x => x.name).ToArray()));
	}

	public void TestStringArrayInt(int[] array, string stringParam)
	{
		Log("TestStringArrayInt method. array length:{0}, array values:{1}, stringParam:{2}", array.Length, string.Join(",", array.Select(x => x.ToString()).ToArray()), stringParam);
	}

	public void TestStringArrayInt(string stringParam, int[] array)
	{
		Log("TestStringArrayInt method. stringParam:{0}, array length:{1}, array values:{2}", stringParam, array.Length, string.Join(",", array.Select(x => x.ToString()).ToArray()));
	}

	public void TestStringArrayInt(int[] array, string stringParam, List<string> list, float floatParam)
	{
		Log("TestStringArrayInt method. array length:{0}, array values:{1}, stringParam:{2}, list count:{3}, list values:{4}, floatParam:{5}", array.Length, string.Join(",", array.Select(x => x.ToString()).ToArray()), stringParam, list.Count, string.Join(",", list.ToArray()), floatParam);
	}

	public void TestStringArrayInt(GameObject[] array, string stringParam, List<GameObject> list, float floatParam)
	{
		Log("TestStringArrayInt method. array length:{0}, array values:{1}, stringParam:{2}, list count:{3}, list values:{4}, floatParam:{5}", array.Length, string.Join(",", array.Select(x => x.name).ToArray()), stringParam, list.Count, string.Join(",", list.Select(x => x.name).ToArray()), floatParam);
	}

	public void TestArrayIntListString(int[] array, List<string> list)
	{
		Log("TestArrayIntListString method. array length:{0}, array values:{1}, list count:{2}, list values:{3}", array.Length, string.Join(",", array.Select(x => x.ToString()).ToArray()), list.Count, string.Join(",", list.ToArray()));
	}

	public void TestEnumArrayEnumList(EnumExample[] array, List<EnumExample> list)
	{
		Log("TestEnumArrayEnumList method. array length:{0}, array values:{1}, list count:{2}, list values:{3}", array.Length, string.Join(",", array.Select(x => x.ToString()).ToArray()), list.Count, string.Join(",", list.Select(x => x.ToString()).ToArray()));
	}

	public void TestVector3ArrayVector3List(Vector3[] array, List<Vector3> list)
	{
		Log("TestVector3ArrayVector3List method. array length:{0}, array values:{1}, list count:{2}, list values:{3}", array.Length, string.Join(",", array.Select(x => x.ToString()).ToArray()), list.Count, string.Join(",", list.Select(x => x.ToString()).ToArray()));
	}

	public void TestScriptableObjectExample(ScriptableObjectExample soe1, ScriptableObjectExample soe2)
	{
		Log("TestScriptableObjectExample method. soe1:{0}, soe2:{1}", soe1, soe2);
	}

	public void TestArrayScriptableObjectExample(ScriptableObjectExample[] soes)
	{
		Log("TestArrayScriptableObjectExample method. soes length:{0}, soes values:{1}", soes.Length, string.Join(",", soes.Select(x => string.Format("name:{0}, values:{1}", x.name, x.ToString())).ToArray()));
	}

	public void TestListScriptableObjectExample(List<ScriptableObjectExample> soes)
	{
		Log("TestArrayScriptableObjectExample method. soes count:{0}, soes values:{1}", soes.Count, string.Join(",", soes.Select(x => string.Format("name:{0}, values:{1}", x.name, x.ToString())).ToArray()));
	}

	#endregion

	#region Dynamic Events

	public void Test(PointerEventData eventData)
	{
		Log("Test method. Dynamic PointerEventData param:{0}", eventData);
	}

	public void Test(PointerEventData eventData, string stringParam)
	{
		Log("Test method. string param:{1} - Dynamic PointerEventData param:{0}", eventData, stringParam);
	}

	public void Test(PointerEventData eventData, int intParam)
	{
		Log("Test method. int param:{1} - Dynamic PointerEventData param:{0}", eventData, intParam);
	}

	public void Test(PointerEventData eventData, int[] array)
	{
		Log("Test method. array length:{0}, values:{1}, Dynamic PointerEventData param:{2}", array.Length, string.Join(",", array.Select(x => x.ToString()).ToArray()), eventData);
	}

	public void Test(PointerEventData eventData, int[] array, List<string> list)
	{
		Log("Test method. array length:{0}, values:{1}, list count:{2}, values:{3}, Dynamic PointerEventData param:{4}", array.Length, string.Join(",", array.Select(x => x.ToString()).ToArray()), list.Count, string.Join(",", list.ToArray()), eventData);
	}

	#endregion

	#region Normal Static Events

	public static void TestStatic()
	{
		StaticLog("TestStatic method");
	}

	public static void TestStaticString(string stringParam)
	{
		StaticLog("TestStaticString method. string param:{0}", stringParam);
	}

	public static void TestStaticString(string stringParam1, string stringParam2)
	{
		StaticLog("TestStaticString method. string param1:{0}, string param2:{1}", stringParam1, stringParam2);
	}

	public static void TestStaticEnum(EnumExample enumExample1, EnumExample enumExample2, EnumExample enumExample3)
	{
		StaticLog("TestStaticEnum method. enum param1:{0} - enum param2:{1} - enum param3:{2}", enumExample1, enumExample2, enumExample3);
	}

	public static void TestStaticArrayString(string[] array)
	{
		StaticLog("TestStaticArrayString method. array length:{0}, values:{1}", array.Length, string.Join(",", array.Select(x => x.ToString()).ToArray()));
	}

	public static void TestStaticListString(List<string> list)
	{
		StaticLog("TestStaticListString method. list count:{0}, values:{1}", list.Count, string.Join(",", list.Select(x => x.ToString()).ToArray()));
	}

	#endregion

	#region Dynamic Static Events

	public static void TestStatic(PointerEventData eventData)
	{
		StaticLog("TestStatic method. Dynamic PointerEventData param:{0}", eventData);
	}

	public static void TestStaticString(PointerEventData eventData, string stringParam)
	{
		StaticLog("TestStaticString method. string param:{1}, Dynamic PointerEventData param:{0}", eventData, stringParam);
	}

	public static void TestStaticString(PointerEventData eventData, string stringParam1, string stringParam2)
	{
		StaticLog("TestStaticString method. string param1:{1}, string param2:{2}, Dynamic PointerEventData param:{0}", eventData, stringParam1, stringParam2);
	}

	public static void TestStaticEnum(PointerEventData eventData, EnumExample enumExample1, EnumExample enumExample2, EnumExample enumExample3)
	{
		StaticLog("TestStaticEnum method. enum param1:{1}, enum param2:{2}, enum param3:{3}, Dynamic PointerEventData param:{0}", eventData, enumExample1, enumExample2, enumExample3);
	}

	public static void TestStaticArrayString(PointerEventData eventData, string[] array)
	{
		StaticLog("TestStaticArrayString method. array length:{1}, values:{2}, Dynamic PointerEventData param:{0}", eventData, array.Length, string.Join(",", array.Select(x => x.ToString()).ToArray()));
	}

	public static void TestStaticListString(PointerEventData eventData, List<string> list)
	{
		StaticLog("TestStaticListString method. list count:{1}, values:{2}, Dynamic PointerEventData param:{0}", eventData, list.Count, string.Join(",", list.Select(x => x.ToString()).ToArray()));
	}

	#endregion

	// THE METHOD BELOW IS NOT AVAILABLE TO CHOOSE
	public void TestArrayList(ArrayList list) { }

	#region Getter Functions

	public float GetFloat()
	{
		return 1f;
	}

	public float GetFloat(float f)
	{
		return f;
	}

	public float GetFloatSum(float f1, float f2)
	{
		return f1 + f2;
	}

	[Slider(0f, 10f)]
	public float GetSliderFloatSum(float f1, [Slider(0f, 5f)] float f2)
	{
		return f1 + f2;
	}

	public int GetInt()
	{
		return 1;
	}

	public int GetInt(int i)
	{
		return i;
	}

	[IntSlider(0, 10, 6)]
	public int GetIntSum(int i1, [IntSlider(0, 5, 3)] int i2)
	{
		return i1 + i2;
	}

	public EnumExample GetEnumExample()
	{
		return EnumExample.FIRST;
	}

	public EnumExample GetEnumExample(EnumExample enumExample)
	{
		return enumExample;
	}

	public EnumExample GetEnumExample(EnumExample enumExample1, EnumExample enumExample2)
	{
		return enumExample1 == EnumExample.FIRST ? enumExample1 : enumExample2;
	}

	public GameObject GetGameObject()
	{
		return m_SomeGameObject;
	}

	public GameObject GetGameObject(GameObject gameObject)
	{
		return gameObject;
	}

	public ClickHandler2 GetClickHandler2(ClickHandler2 clickHandler2)
	{
		return clickHandler2;
	}

	[Layer("TransparentFX")]
	public int GetLayer(int layer)
	{
		return layer;
	}

	[Tag("Player")]
	public string GetTag(string tag)
	{
		return tag;
	}

	public Quaternion GetQuaternion()
	{
		return Quaternion.identity;
	}

	public Vector2 GetVector2(Vector2 vector2)
	{
		return vector2;
	}

	public Vector2Int GetVector2Int(Vector2Int vector2Int)
	{
		return vector2Int;
	}

	public Vector3 GetVector3(Vector3 vector3)
	{
		return vector3;
	}

	public Vector3Int GetVector3Int(Vector3Int vector3Int)
	{
		return vector3Int;
	}

	public Vector4 GetVector4(Vector4 vector4)
	{
		return vector4;
	}

	public Quaternion GetQuaternion(Quaternion quaternion)
	{
		return quaternion;
	}

	public Color GetColor(Color color)
	{
		return color;
	}

	public LayerMask GetLayerMask(LayerMask layerMask)
	{
		return layerMask;
	}

	public int[] GetIntArray(int[] array)
	{
		return array;
	}

	public string[] GetStringArray(string[] array)
	{
		return array;
	}

	public string[] GetStringArray(string[] array1, string[] array2)
	{
		string[] result = new string[array1.Length + array2.Length];

		Array.Copy(array1, result, array1.Length);
		Array.Copy(array2, 0, result, array1.Length, array2.Length);

		return result;
	}

	public List<string> GetStringList(List<string> array1, List<string> array2)
	{
		List<string> result = new List<string>(array1.Count + array2.Count);

		result.AddRange(array1);
		result.AddRange(array2);

		return result;
	}

	#endregion

	#region Static Getter Functions

	public static int GetStaticInt(int i)
	{
		return i;
	}

	[IntSlider(0, 10, 6)]
	public static int GetStaticIntSlider(int i)
	{
		return i;
	}

	public static int GetStaticIntSum(int i1, int i2)
	{
		return i1 + i2;
	}

	[IntSlider(0, 10, 6)]
	public static int GetStaticIntSliderSum(int i1, [IntSlider(0, 5, 2)] int i2)
	{
		return i1 + i2;
	}

	public static string GetStaticString(string s)
	{
		return s;
	}

	[Tag]
	public static string GetStaticTag(string s)
	{
		return s;
	}

	public static EnumExample GetStaticEnumExample()
	{
		return EnumExample.THIRD;
	}

	public static EnumExample GetStaticEnumExample(EnumExample enumExample)
	{
		return enumExample;
	}

	public static List<string> GetStaticListString(List<string> strings)
	{
		return strings;
	}

	#endregion

	#region Get Properties

	public float FloatProperty
	{
		get => 1f;
	}

	public int IntProperty
	{
		get => 2;
	}

	#endregion

	#region Static Get Properties

	public static EnumExample StaticEnumExample => EnumExample.SECOND;

	#endregion

	#region UpdatableInvokableCalls

	// Properties created for IL2CPP Scripting Backend

	/// <summary>
	/// Created for <see cref="Test(float, float)"/>
	/// </summary>
	private UpdatableInvokableCall<float, float> Unused0 { get; set; }

	/// <summary>
	/// Created for <see cref="Test(int, int, int)"/>
	/// </summary>
	private UpdatableInvokableCall<int, int, int> Unused1 { get; set; }

	/// <summary>
	/// Created for <see cref="Test(string, string, string)"/>
	/// </summary>
	private UpdatableInvokableCall<string, string, string> Unused2 { get; set; }

	/// <summary>
	/// Created for <see cref="Test(EnumExample, EnumExample, EnumExample)"/>
	/// </summary>
	private UpdatableInvokableCall<EnumExample, EnumExample, EnumExample> Unused3 { get; set; }

	/// <summary>
	/// Created for <see cref="Test(GameObject, GameObject)"/>
	/// </summary>
	private UpdatableInvokableCall<GameObject, GameObject> Unused4 { get; set; }

	/// <summary>
	/// Created for <see cref="Test(int, string, GameObject, EnumExample)"/>
	/// </summary>
	private UpdatableInvokableCall<int, string, GameObject, EnumExample> Unused5 { get; set; }

	/// <summary>
	/// Created for <see cref="TestLayer(int, int)"/>
	/// </summary>
	private UpdatableInvokableCall<int, int> Unused6 { get; set; }

	/// <summary>
	/// Created for <see cref="TestSlider(float, float)"/>
	/// </summary>
	private UpdatableInvokableCall<float, float> Unused7 { get; set; }

	/// <summary>
	/// Created for <see cref="TestColor(Color)"/>
	/// </summary>
	private UpdatableInvokableCall<Color> Unused8 { get; set; }

	/// <summary>
	/// Created for <see cref="TestLayerMask(LayerMask)"/>
	/// </summary>
	private UpdatableInvokableCall<LayerMask> Unused9 { get; set; }

	/// <summary>
	/// Created for <see cref="TestLayerMask(Quaternion)"/>
	/// </summary>
	private UpdatableInvokableCall<Quaternion> Unused10 { get; set; }

	/// <summary>
	/// Created for <see cref="Test(PointerEventData, string)"/>
	/// </summary>
	private UpdatableInvokableCall<PointerEventData, string> Unused11 { get; set; }

	/// <summary>
	/// Created for <see cref="Test(PointerEventData, int)"/>
	/// </summary>
	private UpdatableInvokableCall<PointerEventData, int> Unused12 { get; set; }

	/// <summary>
	/// Created for <see cref="Test(PointerEventData, int[])"/>
	/// </summary>
	private UpdatableInvokableCall<PointerEventData, int[]> Unused13 { get; set; }

	/// <summary>
	/// Created for <see cref="Test(PointerEventData, int[], List{string})"/>
	/// </summary>
	private UpdatableInvokableCall<PointerEventData, int[], List<string>> Unused14 { get; set; }

	/// <summary>
	/// Created for <see cref="TestSlider(float, float, float)"/>
	/// </summary>
	private UpdatableInvokableCall<float, float, float> Unused15 { get; set; }

	/// <summary>
	/// Created for <see cref="TestCustom(int, string)"/>
	/// </summary>
	private UpdatableInvokableCall<int, string> Unused16 { get; set; }

	/// <summary>
	/// Created for <see cref="TestCustom(int, string, float)"/>
	/// </summary>
	private UpdatableInvokableCall<int, string, float> Unused17 { get; set; }

	#endregion

}