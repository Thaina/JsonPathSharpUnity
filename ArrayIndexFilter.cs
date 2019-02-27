using System;
using System.Globalization;

using System.Collections;
using System.Collections.Generic;

namespace JsonPath
{
	public class ArrayIndexFilter : PathFilter
	{
		public int? Index { get; set; }

		public override IEnumerable<object> ExecuteFilter(object root,IEnumerable<object> current,bool errorWhenNoMatch)
		{
			foreach(var t in current)
			{
				if(Index != null)
				{
					var v = GetTokenIndex(t, errorWhenNoMatch, Index.GetValueOrDefault());
					if(v != null)
						yield return v;
				}
				else if(!(t is IEnumerable<char>) && t is IEnumerable e)
				{
					foreach(var v in e)
						yield return v;
				}
				else if(errorWhenNoMatch)
					throw new Exception(string.Format(CultureInfo.InvariantCulture,"Index * not valid on {0}.",t.GetType().Name));
			}
		}
	}
}