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

        /// <summary>
        /// Method to ascertain whether the data object is of cmdlet format
        /// </summary>
        /// <param name="obj">JSON object to query</param>
        /// <returns></returns>
        public static bool IsCmdlet(JObject obj)
        {
            return obj["Command"] != null && obj["Value"] != null;
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

    public class Database
    {
        /// <summary>
        /// JSON object containing the current system state
        /// </summary>
        public JObject State = new JObject();

        /// <summary>
        /// Method to query the JSON state
        /// </summary>
        /// <param name="path">Path in dot notation format to get the value from</param>
        /// <returns></returns>
        public object Query(string path)
        {
            return State.SelectToken(path);
        }
        /// <summary>
        /// Method to submit data to the state
        /// </summary>
        /// <param name="obj">JSON object to merge with the state</param>
        public void SubmitData(JObject obj)
        {
            Utility.MergeJsonObjects(State, obj);
        }
    }
}
