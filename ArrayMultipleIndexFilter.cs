using System.Collections.Generic;

namespace JsonPath
{
    public class ArrayMultipleIndexFilter : PathFilter
    {
        public List<int> Indexes { get; set; }

        public override IEnumerable<object> ExecuteFilter(object root, IEnumerable<object> current, bool errorWhenNoMatch)
        {
            foreach (object t in current)
            {
                foreach (int i in Indexes)
                {
                    object v = GetTokenIndex(t, errorWhenNoMatch, i);
                    if (v != null)
                        yield return v;
                }
            }
        }
    }
}