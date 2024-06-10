using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecureWss
{
    public static class ReflectionHelper
    {
        public static object GetPropertyValue(object obj, string propertyName)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));

            var property = obj.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property == null)
                throw new ArgumentException($"Property '{propertyName}' not found on type '{obj.GetType().FullName}'.");

            return property.GetValue(obj);
        }
        public static void SetPropertyValue(object obj, string propertyName, object value)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));

            var property = obj.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
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

            var method = obj.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (method == null)
                throw new ArgumentException($"Method '{methodName}' not found on type '{obj.GetType().FullName}'.");

            var methodParameters = method.GetParameters();
            if (parameters.Length != methodParameters.Length)
                throw new ArgumentException("Parameter count mismatch.");

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
    }
}
