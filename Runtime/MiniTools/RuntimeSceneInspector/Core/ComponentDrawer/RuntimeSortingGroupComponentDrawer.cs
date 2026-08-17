using UnityEngine.Rendering;

namespace HP.Utilities.RuntimeSceneInspector.Core
{
    internal sealed class RuntimeSortingGroupComponentDrawer : RuntimeComponentDrawer<SortingGroup>
    {
        public RuntimeSortingGroupComponentDrawer(RuntimeValueDrawerRegistry valueDrawers) : base(valueDrawers)
        {
            Add("@unity.sortingLayerName", "Sorting Layer Name", group => group.sortingLayerName, (group, value) => group.sortingLayerName = value);
            Add("@unity.sortingLayerId", "Sorting Layer ID", group => group.sortingLayerID, (group, value) => group.sortingLayerID = value);
            Add("@unity.sortingOrder", "Sorting Order", group => group.sortingOrder, (group, value) => group.sortingOrder = value);
            Add("@unity.sortAtRoot", "Sort At Root", group => group.sortAtRoot, (group, value) => group.sortAtRoot = value);
        }
    }
}
