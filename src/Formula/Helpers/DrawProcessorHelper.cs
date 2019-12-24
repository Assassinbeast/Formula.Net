using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Formula.Exceptions;
using Microsoft.Extensions.Primitives;

namespace Formula.Helpers
{
	internal static class DrawProcessorHelper
	{
		enum ClassParamBindType { Query, Header, Cookie }
		struct BindedClassParam
		{
			public MemberInfo MemberInfo;
			public ClassParamBindType BindType;
			public string Key;

			public BindedClassParam(MemberInfo memberInfo, ClassParamBindType bindType, string key)
			{
				this.MemberInfo = memberInfo;
				this.BindType = bindType;
				this.Key = key;
			}
		}

		public static void SetViewControllerParameters(FormulaContext formulaContext, ViewController viewCtrl)
		{
			var bindedClassParams = GetBindedClassParams(viewCtrl.GetType());

			foreach (var bindedClassParam in bindedClassParams)
			{
				MemberInfo memberInfo = bindedClassParam.MemberInfo;
				Type classParamType = GetMemberInfoType(memberInfo);
				string bindKey = bindedClassParam.Key;
				try
				{
					if (bindedClassParam.BindType == ClassParamBindType.Query)
					{
						if (formulaContext.HttpContext.Request.Query.TryGetValue(bindKey, out StringValues stringValues))
						{
							if (classParamType.IsArray)
							{
								object[] values = stringValues.Select(x => ConvertStringValueToObjByType(x, classParamType.GetElementType())).ToArray();
								Array destinationArray = Array.CreateInstance(classParamType.GetElementType(), values.Length);
								Array.Copy(values, destinationArray, values.Length);
								SetMemberInfoValue(memberInfo, viewCtrl, destinationArray);
							}
							else
							{
								object value = ConvertStringValueToObjByType(stringValues.First(), classParamType);
								SetMemberInfoValue(memberInfo, viewCtrl, value);
							}
						}
					}
					else if (bindedClassParam.BindType == ClassParamBindType.Header)
					{
						if (formulaContext.HttpContext.Request.Headers.ContainsKey(bindKey))
						{
							object value = ConvertStringValueToObjByType(
								formulaContext.HttpContext.Request.Headers[bindKey], classParamType);
							SetMemberInfoValue(memberInfo, viewCtrl, value);
						}
					}
					else if (bindedClassParam.BindType == ClassParamBindType.Cookie)
					{
						if (formulaContext.HttpContext.Request.Cookies.ContainsKey(bindKey))
						{
							object value = ConvertStringValueToObjByType(
								formulaContext.HttpContext.Request.Cookies[bindKey], classParamType);
							SetMemberInfoValue(memberInfo, viewCtrl, value);
						}
					}
					else
						throw new Exception();
				}
				catch (ProcessLogicTypeConversionErrorException e)
				{
					throw new Exception($"The '{viewCtrl.GetType().FullName}' " +
						$"ViewControllers binded variable named '{memberInfo.Name}' " +
						$"is of type '{e.TypeThatCantBeConvertedFromString.FullName}', " +
						$" and a string cannot be converted to that type");
				}
			}
		}

		static ConcurrentDictionary<Type, IEnumerable<BindedClassParam>> bindedClassParamsCachedDic = new ConcurrentDictionary<Type, IEnumerable<BindedClassParam>>();
		static IEnumerable<BindedClassParam> GetBindedClassParams(Type type)
		{
			IEnumerable<BindedClassParam> bindedClassParams;
			if (bindedClassParamsCachedDic.TryGetValue(type, out bindedClassParams) == false)
			{
				bindedClassParams = type.GetMembers()
					.Where(x =>
						(x is FieldInfo || x is PropertyInfo) &&
						 x.GetCustomAttribute<QueryAttribute>() != null ||
						 x.GetCustomAttribute<HeaderAttribute>() != null ||
						 x.GetCustomAttribute<CookieAttribute>() != null)
					.Select(x2 =>
					{
						ClassParamBindType bindType;
						string key;
						if (x2.GetCustomAttribute<QueryAttribute>() != null)
						{
							bindType = ClassParamBindType.Query;
							var queryAttr = x2.GetCustomAttribute<QueryAttribute>();
							key = !string.IsNullOrWhiteSpace(queryAttr.Key) ? queryAttr.Key : x2.Name;
						}
						else if (x2.GetCustomAttribute<HeaderAttribute>() != null)
						{ 
							bindType = ClassParamBindType.Header;
							var headerAttr = x2.GetCustomAttribute<HeaderAttribute>();
							key = !string.IsNullOrWhiteSpace(headerAttr.Key) ? headerAttr.Key : x2.Name;
						}
						else if (x2.GetCustomAttribute<CookieAttribute>() != null)
						{ 
							bindType = ClassParamBindType.Cookie;
							var cookieAttr = x2.GetCustomAttribute<CookieAttribute>();
							key = !string.IsNullOrWhiteSpace(cookieAttr.Key) ? cookieAttr.Key : x2.Name;
						}
						else
							throw new Exception();
						return new BindedClassParam(x2, bindType, key);
					});
				bindedClassParamsCachedDic.TryAdd(type, bindedClassParams);
			}

			return bindedClassParams;
		}

		static object ConvertStringValueToObjByType(string value, Type type)
		{
			var converter = TypeDescriptor.GetConverter(type);
			if (converter.CanConvertFrom(typeof(string)) == false)
				throw new ProcessLogicTypeConversionErrorException(type);
			try
			{
				return converter.ConvertFromString(value);
			}
			catch
			{
				return GetDefaultValue(type);
			}
		}

		static object GetDefaultValue(Type type)
		{
			if (type.IsValueType)
				return Activator.CreateInstance(type);
			return null;
		}

		static void SetMemberInfoValue(MemberInfo memberInfo, object obj, object value)
		{
			if (memberInfo is FieldInfo fieldInfo)
				fieldInfo.SetValue(obj, value);
			else if (memberInfo is PropertyInfo propertyInfo)
				propertyInfo.SetValue(obj, value);
			else
				throw new Exception();
		}

		static Type GetMemberInfoType(MemberInfo memberInfo)
		{
			if (memberInfo is FieldInfo fieldInfo)
				return fieldInfo.FieldType;
			else if (memberInfo is PropertyInfo propertyInfo)
				return propertyInfo.PropertyType;
			else
				throw new Exception();
		}

		static ConcurrentDictionary<Type, bool> shallFireAsyncProcessLogicCacheDic = new ConcurrentDictionary<Type, bool>();
		public static bool ShallFireAsyncProcessLogic(ViewController viewCtrl)
		{
			bool shallFireAsync;
			if (shallFireAsyncProcessLogicCacheDic.TryGetValue(viewCtrl.GetType(), out shallFireAsync) == false)
			{
				//If ProcessLogicAsync is overriden
				if (viewCtrl.GetType().GetMethod("ProcessLogicAsync").DeclaringType != typeof(ViewController))
					shallFireAsync = true;//Fire the async version
				else
					shallFireAsync = false; //Fire the non async version
				shallFireAsyncProcessLogicCacheDic.TryAdd(viewCtrl.GetType(), shallFireAsync); 
			}
			return shallFireAsync;
		}
	}
}
