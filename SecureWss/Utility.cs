using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace SecureWss
{
    public static class Utility
    {
        private static readonly object Lock = new object();

        /// <summary>
        /// Merges the properties of the source JSON object into the target JSON object.
        /// </summary>
        /// <param name="target">The target JSON object.</param>
        /// <param name="source">The source JSON object.</param>
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

        /// <summary>
        /// Converts a dot notation string into a nested JSON object with the given value.
        /// </summary>
        /// <param name="dotNotation">The dot notation string.</param>
        /// <param name="value">The value to set.</param>
        /// <returns>The resulting JSON object.</returns>
        public static JObject ConvertDotNotationToObject(string dotNotation, object value)
        {
            var result = new JObject();
            var pathSegments = dotNotation.Split('.');
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

        /// <summary>
        /// Converts a JSON object into a list of Cmdlets.
        /// </summary>
        /// <param name="jsonObject">The JSON object to convert.</param>
        /// <returns>A list of Cmdlets.</returns>
        public static List<Cmdlet> ConvertObjectToCmdlets(JObject jsonObject)
        {
            var result = new List<Cmdlet>();
            TraverseJsonObject(jsonObject, string.Empty, result);
            return result;
        }

        /// <summary>
        /// Traverses a JSON object and populates a list of Cmdlets.
        /// </summary>
        /// <param name="jsonObject">The JSON object to traverse.</param>
        /// <param name="currentPath">The current path in dot notation.</param>
        /// <param name="result">The list to populate with Cmdlets.</param>
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
        /// Determines whether a JSON object is in Cmdlet format.
        /// </summary>
        /// <param name="obj">The JSON object to check.</param>
        /// <returns>True if the object is in Cmdlet format; otherwise, false.</returns>
        public static bool IsCmdlet(JObject obj)
        {
            return obj["Command"] != null && obj["Value"] != null && obj.Properties().Count() == 2;
        }
    }

    /// <summary>
    /// Represents a Cmdlet for transmitting dot-notation:value data throughout the program.
    /// </summary>
    public class Cmdlet
    {
        /// <summary>
        /// Gets or sets the command in dot notation format.
        /// </summary>
        public string Command { get; set; }

        /// <summary>
        /// Gets or sets the value of either boolean, int or string type.
        /// </summary>
        public object Value { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Cmdlet"/> class.
        /// </summary>
        /// <param name="command">The command in dot notation format.</param>
        /// <param name="value">The value associated with the command.</param>
        public Cmdlet(string command, object value)
        {
            Command = command;
            Value = value;
        }
    }

    /// <summary>
    /// Represents the event data for state change events.
    /// </summary>
    public class StateChangeEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets the JSON data associated with the state change.
        /// </summary>
        public JObject Data { get; set; }
    }

    /// <summary>
    /// Represents the method that will handle a state change event.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">A <see cref="StateChangeEventArgs"/> that contains the event data.</param>
    public delegate void StateChangeEventHandler(object sender, StateChangeEventArgs e);

    /// <summary>
    /// Represents a database class for storing the system state as a JSON object.
    /// </summary>
    public class Database
    {
        /// <summary>
        /// Gets the JSON object containing the current system state.
        /// </summary>
        public JObject State { get; private set; } = new JObject();

        /// <summary>
        /// Occurs when the database's state changes.
        /// </summary>
        public event StateChangeEventHandler OnStateChange;

        /// <summary>
        /// Queries the JSON state.
        /// </summary>
        /// <param name="path">The path in dot notation format to get the value from.</param>
        /// <returns>The value of the queried property.</returns>
        public object Query(string path)
        {
            return State.SelectToken(path);
        }

        /// <summary>
        /// Submits data to the state.
        /// </summary>
        /// <param name="obj">The JSON object to merge with the state.</param>
        public void SubmitData(JObject obj)
        {
            Utility.MergeJsonObjects(State, obj);
            OnStateChange?.Invoke(this, new StateChangeEventArgs { Data = obj });
        }

        /// <summary>
        /// Initializes the database state.
        /// </summary>
        public void InitState()
        {
            State = new JObject();
        }
    }
}