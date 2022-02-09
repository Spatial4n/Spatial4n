using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Spatial4n.Util
{
    internal static class CollectionUtils
    {
        private static class EmptyListHolder<T>
        {
            public static readonly ReadOnlyCollection<T> EMPTY_LIST = new List<T>().AsReadOnly();
        }

        public static ReadOnlyCollection<T> EmptyList<T>()
        {
            return EmptyListHolder<T>.EMPTY_LIST; // LUCENENET NOTE: Enumerable.Empty<T>() fails to cast to IList<T> on .NET Core 3.x, so we just create a new list
        }
    }
}
