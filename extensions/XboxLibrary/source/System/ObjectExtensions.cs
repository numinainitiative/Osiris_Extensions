using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace System;

public static class ObjectExtensions
{
	public static bool HasMethod(this object obj, string methodName)
	{
		if (obj == null)
		{
			return false;
		}
		try
		{
			return obj.GetType().GetMethod(methodName) != null;
		}
		catch (AmbiguousMatchException)
		{
			return true;
		}
	}

	public static object CrateInstance(this Type type)
	{
		return Activator.CreateInstance(type);
	}

	public static T CrateInstance<T>(this Type type)
	{
		return (T)Activator.CreateInstance(type);
	}

	public static T CrateInstance<T>(this Type type, params object[] parameters)
	{
		return (T)Activator.CreateInstance(type, parameters);
	}

	public static object CreateGenericInstance(Type genericTypeDefinition, Type genericType)
	{
		return Activator.CreateInstance(genericTypeDefinition.MakeGenericType(genericType));
	}

	public static object CreateGenericInstance(Type genericTypeDefinition, Type genericType, params object[] parameters)
	{
		return Activator.CreateInstance(genericTypeDefinition.MakeGenericType(genericType), parameters);
	}

	public static bool HasPropertyAttribute<TAttribute>(this Type type, string propertyName) where TAttribute : Attribute
	{
		PropertyInfo property = type.GetProperty(propertyName);
		if (property == null)
		{
			return false;
		}
		return property.GetCustomAttribute(typeof(TAttribute)) != null;
	}

	public static bool IsGenericList(this Type type, out Type itemType)
	{
		if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
		{
			itemType = type.GenericTypeArguments.First();
			return true;
		}
		itemType = null;
		return false;
	}
}
