using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;
using System.Text.RegularExpressions;
using C5Debugger;
using System.Collections;


namespace SecureWss
{
    public static class Database
    {
        public static object GetPropertyValueFromDotNotation(string dotNotation, object obj)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));

            string[] parts = dotNotation.Split('.');
            foreach (string part in parts)
            {
                string propertyName = part;
                int arrayIndex = -1;

                // Handle array indexing
                if (part.Contains("["))
                {
                    var indexStart = part.IndexOf('[');
                    var indexEnd = part.IndexOf(']');
                    propertyName = part.Substring(0, indexStart);
                    string indexString = part.Substring(indexStart + 1, indexEnd - indexStart - 1);
                    arrayIndex = int.Parse(indexString);
                }

                PropertyInfo propertyInfo = obj.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                if (propertyInfo == null)
                    throw new ArgumentException($"Property '{propertyName}' not found on type '{obj.GetType().FullName}'.");

                obj = propertyInfo.GetValue(obj, null);

                // If the property is an array or list, get the indexed value
                if (arrayIndex >= 0)
                {
                    if (obj is Array array)
                    {
                        obj = array.GetValue(arrayIndex);
                    }
                    else if (obj is System.Collections.IList list)
                    {
                        obj = list[arrayIndex];
                    }
                    else
                    {
                        throw new ArgumentException($"Property '{propertyName}' is not an array or list.");
                    }
                }
            }

            return obj;
        }

        public static void InvokeByDotNotation(object obj, string dotNotation)
        {
            Debug.Print(DebugLevel.Debug, $"Invoke by dot notation called on : {dotNotation}");
            Debug.Print(DebugLevel.Debug, $"Object to invoke method on: {obj}");

            // Regular expression to parse the dot notation string
            string[] parts = dotNotation.Split('.');
            string pattern = @"(\w+)(\[(\d+)\])?(?:\.(.*))?(\(\))?(\(([^)]+)\))?";
            var regex = new Regex(pattern);

            object currentObject = obj;

            foreach (string part in parts)
            {
                Debug.Print(DebugLevel.Debug, $"Part: {part}");

                // Debugging
                /*var matches = regex.Matches(part);
                Debug.Print(DebugLevel.Debug, $"Matches:");
                foreach (Match match in matches)
                {
                    Debug.Print(DebugLevel.Debug, $"Match: {match}");
                    Debug.Print(DebugLevel.Debug, $"Groups:");
                    foreach (Group matchGroup in match.Groups)
                    {
                        Debug.Print(DebugLevel.Debug, $"Match group value: {matchGroup.Value}");
                    }
                }*/
                
                ProcessParts(part);
            }

            void ProcessParts(string part)
            {
                var matches = regex.Matches(part);
                foreach (Match match in matches)
                {
                    string propertyName = match.Groups[1].Value;
                    bool isArrayItem = match.Groups[2].Success;
                    string propertyIndex = match.Groups[3].Value;
                    bool isMethod = match.Groups[5].Success || match.Groups[6].Success || match.Groups[7].Success;
                    bool isMethodWithParameters = match.Groups[6].Success || match.Groups[7].Success;

                    if (!isMethod)
                    {
                        currentObject = ReflectionHelper.GetPropertyValue(currentObject, propertyName);
                        if(currentObject.GetType().IsGenericType && currentObject.GetType().GetGenericTypeDefinition() == typeof(List<>))
                        {
                            Debug.Print(DebugLevel.Debug, $"Getting list item...");
                            var newList = currentObject as IList;
                            currentObject = newList[Int32.Parse(propertyIndex)];
                            Debug.Print(DebugLevel.Debug, $"Current object: {currentObject}");
                        }
                    }
                    else
                    {
                        if (isMethodWithParameters)
                        {
                            object[] parameters = match.Groups[7].ToString().Split(',');
                            Debug.Print(DebugLevel.Debug, $"Invoking method with parameters: {parameters}...");
                            ReflectionHelper.InvokeMethod(currentObject, propertyName, parameters);
                        }
                        else
                        {
                            Debug.Print(DebugLevel.Debug, $"Invoking method: {propertyName}...");
                            ReflectionHelper.InvokeMethod(currentObject, propertyName);
                        }
                    }
                }


            }

        }
    }
}
