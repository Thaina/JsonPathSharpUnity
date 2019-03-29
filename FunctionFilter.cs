using System;
using System.Linq;
using System.Globalization;

using System.Collections;
using System.Collections.Generic;

using UnityEngine;

namespace JsonPath
{
	public delegate object PathFilterFunction(object root,object obj,params object[] args);
	public class FunctionFilter : PathFilter
	{
		public static readonly Dictionary<string,PathFilterFunction> Functions  = new Dictionary<string,PathFilterFunction>();

		public string Name { get; set; }
		public PathFilter[] filters { get; set; }
		public override IEnumerable<object> ExecuteFilter(object root,IEnumerable<object> current,bool errorWhenNoMatch)
		{
			if(!Functions.TryGetValue(Name,out var func) || func == null)
			{
				switch(Name)
				{
					case "keys":
						func	= (rootObj,obj,args) => {
							if(!(obj is IDictionary dict))
								return Enumerable.Empty<object>();

							return dict.OfType<DictionaryEntry>().Select((entry) => entry.Key);
						};
						break;
					case "values":
						func	= (rootObj,obj,args) => {
							if(!(obj is IDictionary dict))
								return Enumerable.Empty<object>();

							return dict.OfType<DictionaryEntry>().Select((entry) => entry.Value);
						};
						break;
					case "entries":
						func	= (rootObj,obj,args) => {
							if(!(obj is IDictionary dict))
								return Enumerable.Empty<object>();

							return dict.OfType<DictionaryEntry>().Select((entry) => new Dictionary<object,object>(){ ["key"] = entry.Key,["value"] = entry.Value  });
						};
						break;
					case "count":
						func	= (rootObj,obj,args) => {
							if(obj is ICollection collection)
								return collection.Count;

							if(obj is IEnumerable list)
								return list.Cast<object>().Count();

							return 0;
						};
						break;
					case "inverse":
						func	= (rootObj,obj,args) => {
							if(obj is double d)
								return 1 / d;

							if(obj is float f)
								return 1 / f;

							if(obj is decimal m)
								return 1 / m;

							if(obj is IConvertible c && !(c is string))
								return 1 / c.ToDouble(null);

							return null;
						};
						break;
					default:
						if(errorWhenNoMatch)
							throw new Exception("No function available for : " + Name);

						yield break;
				}
			}

			foreach(var obj in current)
				yield return filters == null ? func(root,obj) : func(root,obj,filters.Select((filter) => filter.ExecuteFilter(root,new[] { obj },errorWhenNoMatch)).ToArray());
		}
	}
}