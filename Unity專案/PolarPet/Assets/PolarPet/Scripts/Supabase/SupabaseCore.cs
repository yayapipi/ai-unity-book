using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

public class SupabaseCore : MonoBehaviour
{
    [Header("Supabase Settings")]
    [Tooltip("Supabase Project URL，例如：https://xxxx.supabase.co")]
    [SerializeField] private string supabaseUrl;

    [Tooltip("Supabase anon public API Key")]
    [SerializeField] private string supabaseAnonKey;

    [Header("Query Settings")]
    [Tooltip("Platform 欄位的值，例如 OPENAI")]
    [SerializeField] private string platform = "OPENAI";

    private void Start()
    {
        FetchApiKey(Debug.Log, Debug.LogError);
    }

    /// <summary>
    /// 對外呼叫：取得 API Key
    /// </summary>
    public void FetchApiKey(
        Action<string> onSuccess,
        Action<string> onError = null)
    {
        StartCoroutine(FetchApiKeyCoroutine(onSuccess, onError));
    }

    private IEnumerator FetchApiKeyCoroutine(
        Action<string> onSuccess,
        Action<string> onError)
    {
        string url =
            $"{supabaseUrl}/rest/v1/APITable" +
            $"?select=API_Key" +
            $"&Platform=eq.{platform}" +
            $"&limit=1";

        using UnityWebRequest request = UnityWebRequest.Get(url);
        request.SetRequestHeader("apikey", supabaseAnonKey);
        request.SetRequestHeader("Authorization", $"Bearer {supabaseAnonKey}");
        request.SetRequestHeader("Accept", "application/json");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke(request.error);
            yield break;
        }

        try
        {
            var json = request.downloadHandler.text;
            List<ApiKeyRow> rows = JsonConvert.DeserializeObject<List<ApiKeyRow>>(json);

            if (rows == null || rows.Count == 0)
            {
                onError?.Invoke("API Key not found");
                yield break;
            }

            onSuccess?.Invoke(rows[0].API_Key);
        }
        catch (Exception e)
        {
            onError?.Invoke(e.Message);
        }
    }

    [Serializable]
    private class ApiKeyRow
    {
        public string API_Key;
    }
}
