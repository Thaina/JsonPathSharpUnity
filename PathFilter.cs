using System;
using System.Linq;
using System.Globalization;

using System.Collections;
using System.Collections.Generic;

namespace JsonPath
{
	public abstract class PathFilter
	{
		public abstract IEnumerable<object> ExecuteFilter(object root,IEnumerable<object> current,bool errorWhenNoMatch);

		public static object GetTokenIndex(object t,bool errorWhenNoMatch,int index)
		{
			if(t is IList l)
				return errorWhenNoMatch ? l[index] : (index < l.Count ? l[index] : null);

			if(!(t is string) && t is IEnumerable e)
				return errorWhenNoMatch ? e.OfType<object>().ElementAt(index) : e.OfType<object>().ElementAtOrDefault(index);

			if(errorWhenNoMatch)
				throw new Exception(string.Format(CultureInfo.InvariantCulture,"Index {0} not valid on {1}.",index,t.GetType().Name));

			return null;
		}

		public static IEnumerable<object> RecursiveJsonValue(object obj)
		{
			yield return obj;
			if(obj is IEnumerable<char>)
				yield break;

			var items	= (obj as IDictionary)?.Values ?? (obj as IEnumerable);
			if(items == null)
				yield break;

			foreach(var item in items.OfType<object>().SelectMany((item) => RecursiveJsonValue(item)))
				yield return item;
		}

		public static IEnumerable<DictionaryEntry> GetScanValues(IEnumerable container)
		{
			if(container is IEnumerable<char>)
				yield break;

			if(container is IDictionary dict)
			{
				foreach(var entry in dict.OfType<DictionaryEntry>())
				{
					yield return entry;
					if(entry.Value is IEnumerable enumerable)
					{
						foreach(var item in GetScanValues(enumerable))
							yield return item;
					}
				}
			}
			else
			{
				foreach(var enumerable in container.OfType<IEnumerable>())
				{
					foreach(var item in GetScanValues(enumerable))
						yield return item;
				}
			}
		}
	}
}