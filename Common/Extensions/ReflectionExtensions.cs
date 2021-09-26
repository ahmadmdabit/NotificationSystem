using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Common.Extensions
{
    public static class ReflectionExtensions
    {
        public static PropertyInfo PropertyFind(this object obj, string propertyName)
        {
            var p = obj.GetType().GetProperties();
            for (int ii = 0; ii < p.Length; ii++)
            {
                if (p[ii].Name == propertyName)
                {
                    return p[ii];
                }
            }
            return null;
        }

        public static PropertyInfo PropertyFind(this object obj, Type propertyType)
        {
            var p = obj.GetType().GetProperties();
            for (int ii = 0; ii < p.Length; ii++)
            {
                if (p[ii].PropertyType == propertyType)
                {
                    return p[ii];
                }
            }
            return null;
        }

        public static List<PropertyInfo> PropertiesFindByAttribute(this object obj, Type attributeType)
        {
            //return obj.GetType().GetProperties().PropertiesFindByAttribute(attributeType); // Just predefined Attributes
            return obj.GetType().GetProperties().Where(prop => Attribute.IsDefined(prop, attributeType))?.ToList(); // Custom Attributes and predefined Attributes
        }

        public static PropertyInfo PropertyFindByAttribute(this object obj, Type attributeType)
        {
            return PropertiesFindByAttribute(obj, attributeType)?[0];
        }

        public static object PropertyFindValue(this object obj, string propertyName)
        {
            return PropertyFind(obj, propertyName)?.GetValue(obj);
        }

        public static object PropertyFindValue(this object obj, Type propertyType)
        {
            return PropertyFind(obj, propertyType)?.GetValue(obj);
        }

        public static object PropertyFindValueByAttribute(this object obj, Type attributeType)
        {
            return PropertyFindByAttribute(obj, attributeType)?.GetValue(obj);
        }

        public static bool PropertyIsExists(this object obj, string propertyName)
        {
            return PropertyFind(obj, propertyName) != null;
        }

        public static bool PropertyIsExists(this object obj, Type propertyType)
        {
            return PropertyFind(obj, propertyType) != null;
        }
    }
}