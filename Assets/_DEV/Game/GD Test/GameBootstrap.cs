using UnityEngine;
[DefaultExecutionOrder(-1000)]
public class GameBootstrap : MonoBehaviour
{
    //[SerializeField] private HandlingValues handlingValues;

    [SerializeField] private bool useJsonValues = true;

    private void Awake()
    {
        HandlingValuesProvider.UseJsonValues = useJsonValues;

        HandlingValuesProvider.Initialize();
    }
}