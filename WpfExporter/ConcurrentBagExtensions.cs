﻿using System.Collections.Concurrent;

namespace WpfExporter
{
    public static class ConcurrentBagExtensions
    {
        public static void AddRange<T>(this ConcurrentBag<T> bag, IEnumerable<T> items)
        {
            foreach (var item in items)
            {
                bag.Add(item);
            }
        }
    }
}