using System;
using System.Collections;
using System.Globalization;
using System.Xml;
using UnityEngine;
using UnityEngine.Networking;

public class WeatherManager : MonoBehaviour
{
    public enum City
    {
        Orlando,
        Yogyakarta,
        Houston,
        Reykjavik,
        Oslo
    }

    public enum WeatherType
    {
        Sunny,
        Cloudy,
        Rainy
    }

    [Header("Settings")]
    [SerializeField] private City selectedCity = City.Orlando;
    [SerializeField] private string apiKey;

    [Header("Sun")]
    [SerializeField] private Light sunLight;
    [Tooltip("Compass direction the sun travels along (Y rotation of the light).")]
    [SerializeField] private float sunYaw = -30f;

    [Header("Skyboxes (Panoramic materials)")]
    [SerializeField] private Material sunnySkybox;
    [SerializeField] private Material cloudySkybox;
    [SerializeField] private Material rainySkybox;

    private const string BaseUrl = "http://api.openweathermap.org/data/2.5/weather";

    private void Start()
    {
        FetchWeather();
    }

    // right click component header to fetch weather
    [ContextMenu("Fetch Weather")]
    public void FetchWeather()
    {
        StartCoroutine(GetWeatherXML(OnXMLDataLoaded));
    }

    private string GetQuery(City city)
    {
        switch (city)
        {
            case City.Orlando: return "Orlando,us";
            case City.Yogyakarta: return "Yogyakarta,id";
            case City.Houston: return "Houston,us";
            case City.Reykjavik: return "Reykjavik,is";
            case City.Oslo: return "Oslo,no";
            default: return "Orlando,us";
        }
    }

    private string BuildUrl()
    {
        return $"{BaseUrl}?q={GetQuery(selectedCity)}&mode=xml&appid={apiKey}";
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
        return CallAPI(BuildUrl(), callback);
    }

    public void OnXMLDataLoaded(string data)
    {
        Debug.Log($"Weather for {selectedCity}:\n{data}");

        var doc = new XmlDocument();
        try
        {
            doc.LoadXml(data);
        }
        catch (XmlException e)
        {
            Debug.LogError($"Could not parse weather XML: {e.Message}");
            return;
        }

        UpdateSun(doc);
        UpdateSkybox(doc);
    }

    private void UpdateSun(XmlDocument doc)
    {
        if (sunLight == null)
        {
            Debug.LogWarning("WeatherManager: no sun light assigned.");
            return;
        }

        XmlNode tzNode = doc.SelectSingleNode("/current/city/timezone");
        if (tzNode == null || !int.TryParse(tzNode.InnerText, out int offsetSeconds))
        {
            Debug.LogWarning("WeatherManager: timezone not found in response.");
            return;
        }

        DateTime localNow = DateTime.UtcNow.AddSeconds(offsetSeconds);
        float nowHours = (float)localNow.TimeOfDay.TotalHours;

        float sunriseHours = 6f;
        float sunsetHours = 18f;

        XmlNode sunNode = doc.SelectSingleNode("/current/city/sun");
        if (sunNode != null
            && TryParseUtc(sunNode.Attributes["rise"]?.Value, out DateTime riseUtc)
            && TryParseUtc(sunNode.Attributes["set"]?.Value, out DateTime setUtc))
        {
            float rise = (float)riseUtc.AddSeconds(offsetSeconds).TimeOfDay.TotalHours;
            float set = (float)setUtc.AddSeconds(offsetSeconds).TimeOfDay.TotalHours;

            if (set > rise)
            {
                sunriseHours = rise;
                sunsetHours = set;
            }
        }

        float angle = CalculateSunAngle(nowHours, sunriseHours, sunsetHours);

        sunLight.transform.rotation = Quaternion.Euler(angle, sunYaw, 0f);

        Debug.Log($"{selectedCity} local time: {localNow:HH:mm} " +
                  $"(sunrise {FormatHours(sunriseHours)}, sunset {FormatHours(sunsetHours)}) " +
                  $"-> sun angle {angle:F1}°");
    }

    private void UpdateSkybox(XmlDocument doc)
    {
        XmlNode weatherNode = doc.SelectSingleNode("/current/weather");
        string numberText = weatherNode?.Attributes["number"]?.Value;

        if (!int.TryParse(numberText, out int conditionId))
        {
            Debug.LogWarning("WeatherManager: weather condition not found in response.");
            return;
        }

        WeatherType type = ConditionToWeatherType(conditionId);

        Material skybox = null;
        switch (type)
        {
            case WeatherType.Sunny: skybox = sunnySkybox; break;
            case WeatherType.Cloudy: skybox = cloudySkybox; break;
            case WeatherType.Rainy: skybox = rainySkybox; break;
        }

        if (skybox == null)
        {
            Debug.LogWarning($"WeatherManager: no skybox material assigned for {type}.");
            return;
        }

        RenderSettings.skybox = skybox;

        //DynamicGI.UpdateEnvironment();

        Debug.Log($"Weather condition {conditionId} ({weatherNode.Attributes["value"]?.Value}) -> {type} skybox");
    }

    private static WeatherType ConditionToWeatherType(int id)
    {
        if (id >= 200 && id < 600) return WeatherType.Rainy;

        if (id == 800 || id == 801) return WeatherType.Sunny;

        return WeatherType.Cloudy;
    }

    private static float CalculateSunAngle(float now, float sunrise, float sunset)
    {
        if (now >= sunrise && now <= sunset)
        {
            return Mathf.InverseLerp(sunrise, sunset, now) * 180f;
        }

        float nightLength = 24f - (sunset - sunrise);
        float sinceSunset = now >= sunset ? now - sunset : now + (24f - sunset);
        return 180f + (sinceSunset / nightLength) * 180f;
    }

    private static bool TryParseUtc(string value, out DateTime result)
    {
        return DateTime.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out result);
    }

    private static string FormatHours(float hours)
    {
        return TimeSpan.FromHours(hours).ToString(@"hh\:mm");
    }
}