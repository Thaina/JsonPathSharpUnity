using System;
using System.Linq;
using System.Globalization;

using System.Collections;
using System.Collections.Generic;

namespace JsonPath
{
	public class ArraySliceFilter : PathFilter
	{
		public int? Start { get; set; }
		public int? End { get; set; }
		public int? Step { get; set; }

		public override IEnumerable<object> ExecuteFilter(object root,IEnumerable<object> current,bool errorWhenNoMatch)
		{
			if(Step == 0)
				throw new Exception("Step cannot be zero.");

			foreach(var t in current)
			{
				var a	= t as IList;
				if(a == null && t is IEnumerable enumerable && !(t is IEnumerable<char>))
					a	= enumerable.OfType<object>().ToList();

				if(a != null)
				{
					// set defaults for null arguments
					int stepCount = Step ?? 1;
					int startIndex = Start ?? ((stepCount > 0) ? 0 : a.Count - 1);
					int stopIndex = End ?? ((stepCount > 0) ? a.Count : -1);

					// start from the end of the list if start is negative
					if(Start < 0)
					{
						startIndex = a.Count + startIndex;
					}

					// end from the start of the list if stop is negative
					if(End < 0)
					{
						stopIndex = a.Count + stopIndex;
					}

					// ensure indexes keep within collection bounds
					startIndex = Math.Min(Math.Max(startIndex,(stepCount > 0) ? 0 : int.MinValue),(stepCount > 0) ? a.Count : a.Count - 1);
					stopIndex = Math.Min(Math.Max(stopIndex,-1),a.Count);

					bool positiveStep = (stepCount > 0);

					if(IsValid(startIndex,stopIndex,positiveStep))
					{
						for(int i = startIndex; IsValid(i,stopIndex,positiveStep); i += stepCount)
							yield return a[i];
					}
					else if(errorWhenNoMatch)
						throw new Exception(string.Format(CultureInfo.InvariantCulture,"Array slice of {0} to {1} returned no results.",
							Start != null ? Start.GetValueOrDefault().ToString(CultureInfo.InvariantCulture) : "*",
							End != null ? End.GetValueOrDefault().ToString(CultureInfo.InvariantCulture) : "*"));
				}
				else if(errorWhenNoMatch)
					throw new Exception(string.Format(CultureInfo.InvariantCulture,"Array slice is not valid on {0}.",t.GetType().Name));
			}
		}

		private bool IsValid(int index,int stopIndex,bool positiveStep) => positiveStep ? (index < stopIndex) : (index > stopIndex);
	}
}