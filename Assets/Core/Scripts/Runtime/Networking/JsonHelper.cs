using System;
using UnityEngine;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Helper xử lý JSON array cho Unity JsonUtility (vì JsonUtility không hỗ trợ root array).
    /// </summary>
    public static class JsonHelper
    {
        public static T[] FromJsonArray<T>(string json)
        {
            if (string.IsNullOrEmpty(json)) return Array.Empty<T>();
            string newJson = "{\"items\":" + json + "}";
            Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(newJson);
            return wrapper != null && wrapper.items != null ? wrapper.items : Array.Empty<T>();
        }

        public static string ToJsonArray<T>(T[] array, bool prettyPrint = false)
        {
            Wrapper<T> wrapper = new Wrapper<T> { items = array };
            string json = JsonUtility.ToJson(wrapper, prettyPrint);
            // Cắt bớt wrapper để trả về json array thuần
            int start = json.IndexOf('[');
            int end = json.LastIndexOf(']');
            if (start != -1 && end != -1)
            {
                return json.Substring(start, end - start + 1);
            }
            return "[]";
        }

        [Serializable]
        private class Wrapper<T>
        {
            public T[] items;
        }
    }
}
