using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Crestron.SimplSharp;

namespace SecureWss
{
    public static class Utility
    {
        private static readonly object Lock = new object();

        public static void MergeJsonObjects(JObject target, JObject source)
        {
            lock (Lock)
            {
                foreach (var property in source.Properties())
                {
                    if (target[property.Name] is JObject existingObject && property.Value is JObject newObject)
                    {
                        MergeJsonObjects(existingObject, newObject);
                    }
                    else
                    {
                        target[property.Name] = property.Value;
                    }
                }
            }
        }

        public static JObject ConvertDotNotationToObject(string dotNotation, object value)
        {

            JObject result = new JObject();
            string[] pathSegments = dotNotation.Split('.');
            var currentObject = result;

            for (int i = 0; i < pathSegments.Length - 1; i++)
            {
                var segment = pathSegments[i];
                currentObject[segment] = new JObject();
                currentObject = (JObject)currentObject[segment];
            }
            currentObject[pathSegments[pathSegments.Length - 1]] = JToken.FromObject(value);

            return result;
        }

        public static List<Cmdlet> ConvertObjectToCmdlets(JObject jsonObject)
        {
            List<Cmdlet> result = new List<Cmdlet>();

            TraverseJsonObject(jsonObject, "", result);

            return result;
        }

        private static void TraverseJsonObject(JObject jsonObject, string currentPath, List<Cmdlet> result)
        {
            foreach (var property in jsonObject.Properties())
            {
                var newPath = string.IsNullOrEmpty(currentPath) ? property.Name : $"{currentPath}.{property.Name}";

                if (property.Value is JObject nestedObject)
                {
                    TraverseJsonObject(nestedObject, newPath, result);
                }
                else
                {
                    result.Add(new Cmdlet(newPath, property.Value.ToObject<object>()));
                }
            }
        }
    }
    public class Cmdlet
    {
        public string Command { get; set; }
        public object Value { get; set; }
        public Cmdlet(string command, object value)
        {
            Command = command;
            Value = value;
        }
    }

    public enum eCrestronValueType
    {
        Digital,
        Analog,
        Serial,
        None
    }
}
