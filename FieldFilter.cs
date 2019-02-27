using System;
using System.Linq;
using System.Globalization;

using System.Collections;
using System.Collections.Generic;

using UnityEngine;

namespace JsonPath
{
	public class FieldFilter : PathFilter
	{
		public bool Scan;
		public string Name { get; set; }

		public override IEnumerable<object> ExecuteFilter(object root,IEnumerable<object> current,bool errorWhenNoMatch)
		{
			bool any    = false;
			if(Scan)
			{
				foreach(var c in current.SelectMany((item) => RecursiveJsonValue(item)))
				{
					if(Name == null)
					{
						yield return c;
						any = true;
					}
					else if(c is IDictionary dict && dict.Contains(Name))
					{
						yield return dict[Name];
						any = true;
					}
				}
			}
			else
			{
				foreach(var dict in current.OfType<IDictionary>())
				{
					foreach(var pair in Name == null ? dict.OfType<DictionaryEntry>() : dict.OfType<DictionaryEntry>().Where((pair) => pair.Key is string key && key == Name))
					{
						yield return pair.Value;
						any = true;
					}
				}
			}

			if(!any && errorWhenNoMatch)
				throw new Exception(string.Format(CultureInfo.InvariantCulture,"Property '{0}' does not exist",Name));
		}
	}
}