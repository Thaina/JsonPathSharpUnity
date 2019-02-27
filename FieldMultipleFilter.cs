using System;
using System.Linq;
using System.Globalization;

using System.Collections;
using System.Collections.Generic;

namespace JsonPath
{
	public class FieldMultipleFilter : PathFilter
	{
		public bool Scan;
		public List<string> Names { get; set; }

		public override IEnumerable<object> ExecuteFilter(object root,IEnumerable<object> current,bool errorWhenNoMatch)
		{
			bool any    = false;
			if(Names == null || !Names.Any())
				yield break;

			if(Scan)
			{
				foreach(var c in current.SelectMany((item) => RecursiveJsonValue(item)))
				{
					if(!(c is IDictionary dict))
						continue;

					foreach(var item in Names.Join(dict.OfType<DictionaryEntry>(),(name) => name,(entry) => entry.Key,(outer,inner) => inner.Value))
						yield return item;
					any = true;
				}
			}
			else
			{
				foreach(var dict in current.OfType<IDictionary>())
				{
					foreach(var pair in dict.OfType<DictionaryEntry>())
					{
						if(!(pair.Key is string key))
							continue;

						foreach(var name in Names)
						{
							if(name == key)
							{
								yield return pair.Value;
								any = true;
							}
						}
					}
				}
			}

			if(!any && errorWhenNoMatch)
				throw new Exception(string.Format(CultureInfo.InvariantCulture,"Property '{0}' does not exist",string.Join(",",Names)));
		}
	}
}