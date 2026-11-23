using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class WeatherManager : MonoBehaviour
{
    private const string xmlApi = "http://api.openweathermap.org/data/2.5/weather?q=Orlando,us&mode=xml&appid=f3bebff90e76370ed86a294cff434b47";

    private void Start()
    {
        StartCoroutine(GetWeatherXML(OnXMLDataLoaded));
    }

    private IEnumerator CallAPI(string url, Action<string> callback)
    {
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.ConnectionError)
            {
                Debug.LogError($"network problem: {request.error}");
            }
            else if (request.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"response error: {request.responseCode}\n{request.downloadHandler.text}");
            }
            else
            {
                callback(request.downloadHandler.text);
            }
        }
    }

    public IEnumerator GetWeatherXML(Action<string> callback)
    {
        return CallAPI(xmlApi, callback);
    }

    public void OnXMLDataLoaded(string data)
    {
        Debug.Log(data);
    }
}