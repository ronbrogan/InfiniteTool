using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace InfiniteTool.WPF
{
    public static class Helpers
    {
        /// <summary>
        /// Finds a children of a given item in the visual tree. 
        /// </summary>
        public static IEnumerable<T> FindChildren<T>(this DependencyObject parent, Func<T, bool> match)
           where T : DependencyObject
        {
            // Confirm parent and childName are valid. 
            if (parent == null) yield break;

            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                var childObj = VisualTreeHelper.GetChild(parent, i);

                // If the child is not of the request child type child
                T? child = childObj as T;

                if (child == null)
                {
                    // recursively drill down the tree
                    foreach(var nested in childObj.FindChildren<T>(match))
                        yield return nested;
                }
                else
                {
                    if (match(child))
                        yield return child;
                }
            }
        }
    }
}
