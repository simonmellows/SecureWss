using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using C5Debugger;

namespace SecureWss
{
    public static class ReflectionHelper
    {
        public static object GetPropertyValue(object obj, string propertyName)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));

            var property = obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).FirstOrDefault(p => p.Name == propertyName);
            if (property == null)
                throw new ArgumentException($"Property '{propertyName}' not found on type '{obj.GetType().FullName}'.");

            return property.GetValue(obj);
        }
        public static void SetPropertyValue(object obj, string propertyName, object value)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));

            var property = obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).FirstOrDefault(p => p.Name == propertyName);
            if (property == null)
                throw new ArgumentException($"Property '{propertyName}' not found on type '{obj.GetType().FullName}'.");

            property.SetValue(obj, value);
        }

        public static EventInfo GetEvent(object obj, string eventName)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));
            var e = obj.GetType().GetEvent(eventName);
            if (e == null)
                throw new ArgumentException($"Event '{eventName}' not found on type '{obj.GetType().FullName}'.");
            return e;
        }

        public static object InvokeMethod(object obj, string methodName, params object[] parameters)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));

            // Get all methods with the given name
            var methods = obj.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                     .Where(m => m.Name == methodName)
                                     .ToArray();

            if (methods.Length == 0)
                throw new ArgumentException($"Method '{methodName}' not found on type '{obj.GetType().FullName}'.");

            // Nullify a MethodInfo object
            MethodInfo method = null;

            // If there is only 1 method, use this method
            if (methods.Length == 1)
            {
                method = methods[0];
            }
            // If there are multiple methods overloads, find the method with matching parameter types
            else
            {
                foreach (var m in methods)
                {
                    var innerMethodParameters = m.GetParameters();
                    if (innerMethodParameters.Length == parameters.Length)
                    {
                        bool parametersMatch = true;
                        for (int i = 0; i < innerMethodParameters.Length; i++)
                        {
                            if (parameters[i] != null && !innerMethodParameters[i].ParameterType.IsInstanceOfType(parameters[i]))
                            {
                                Debug.Print(DebugLevel.Debug, $"Converting parameter {parameters[i]} of type {parameters[i].GetType()} to {innerMethodParameters[i].ParameterType}.");
                                parametersMatch = false;
                                break;
                            }
                        }
                        if (parametersMatch)
                        {
                            method = m;
                            break;
                        }
                    }
                }
            }

            if (method == null)
                throw new ArgumentException($"No suitable method '{methodName}' found on type '{obj.GetType().FullName}' with matching parameters.");

            // Convert parameters to the required types
            var methodParameters = method.GetParameters();
            var convertedParameters = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                convertedParameters[i] = ConvertToType(parameters[i], methodParameters[i].ParameterType);
            }

            return method.Invoke(obj, convertedParameters);
        }

        private static object ConvertToType(object value, Type type)
        {
            if (type.IsEnum)
            {
                return Enum.Parse(type, value.ToString());
            }
            else if (type == typeof(uint))
            {
                return Convert.ToUInt32(value);
            }
            else if (type == typeof(int))
            {
                return Convert.ToInt32(value);
            }
            else if (type == typeof(double))
            {
                return Convert.ToDouble(value);
            }
            else if (type == typeof(float))
            {
                return Convert.ToSingle(value);
            }
            else if (type == typeof(bool))
            {
                return Convert.ToBoolean(value);
            }
            else if (type == typeof(string))
            {
                return Convert.ToString(value);
            }
            else
            {
                return Convert.ChangeType(value, type);
            }
        }


        public static void SetupEventHandler(
            //object instance,
            object parent,
            //string eventSourcePropertyName,
            object eventSource,
            string eventName,
            Type handlerClassType,
            string handlerMethodName)
        {
            /*Debug.Print(DebugLevel.Debug, $"Setup Event Handler: Setting event source: {eventSourcePropertyName}");
            // Step 1: Get the event source property value
            var eventSource = instance
                .GetType()
                .GetProperties().First(p => p.Name == eventSourcePropertyName)?
                .GetValue(instance);

            if (eventSource == null)
            {
                throw new InvalidOperationException($"Property '{eventSourcePropertyName}' not found or is null.");
            }*/

            Debug.Print(DebugLevel.Debug, $"Setup Event Handler: Invoking 'Use' method");
            // Optional: Invoke a method on the event source if required
            //InvokeMethod(eventSource, "Use");

            // Check if the "Use" method exists
            var useMethod = eventSource.GetType().GetMethod("Use");
            if (useMethod != null)
            {
                // Invoke the method if it exists
                useMethod.Invoke(eventSource, null);
            }

            Debug.Print(DebugLevel.Debug, $"Setup Event Handler: Setting event info: {eventName}");
            // Step 2: Get event info from the event source
            var eventInfo = eventSource
                .GetType()
                .GetEvent(eventName);
            if (eventInfo == null)
            {
                throw new InvalidOperationException($"Event '{eventName}' not found on type '{eventSource.GetType().FullName}'.");
            }

            Debug.Print(DebugLevel.Debug, $"Setup Event Handler: Setting method info: {handlerMethodName}");
            // Step 3: Get method info for the event handler
            var methodInfo = handlerClassType.GetMethod(handlerMethodName);
            if (methodInfo == null)
            {
                throw new InvalidOperationException($"Handler method '{handlerMethodName}' not found in type '{handlerClassType.FullName}'.");
            }

            Debug.Print(DebugLevel.Debug, $"Setup Event Handler: Creating event handler delegate.");
            // Step 4: Create a delegate for the event handler
            var handlerDelegate = Delegate.CreateDelegate(eventInfo.EventHandlerType, parent, methodInfo);

            Debug.Print(DebugLevel.Debug, $"Setup Event Handler: Adding event handler to instance.");
            // Step 5: Attach the event handler to the event
            eventInfo.AddEventHandler(eventSource, handlerDelegate);

            Debug.Print(DebugLevel.Debug, $"Event handler '{handlerMethodName}' successfully attached to '{eventName}' on '{eventSource}'.");
        }

    }
}
